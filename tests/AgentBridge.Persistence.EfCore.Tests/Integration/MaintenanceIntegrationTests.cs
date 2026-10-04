using System.Security.Cryptography;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Maintenance;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Реальные migrations, обслуживание и восстановимость backup этапов 05/11/12.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class MaintenanceIntegrationTests
{
    /// <summary>Missing/initialize/no-pending, настоящий Up/Down/Up и независимость ledger подключающего приложения.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task MigrationsAndHostHistoryAreIsolated(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
            Assert.False((await maintenance.InspectAsync(TimeSpan.FromSeconds(30))).Exists);
            Assert.Equal(MaintenanceError.DatabaseMissing, (await Assert.ThrowsAsync<MaintenanceException>(() =>
                maintenance.UpdateExistingAsync(TimeSpan.FromSeconds(30)))).Code);
        }
        await database.InitializeAsync();
        await database.ExecuteAsync("CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" text NOT NULL PRIMARY KEY, \"ProductVersion\" text NOT NULL)");
        await database.ExecuteAsync("INSERT INTO \"__EFMigrationsHistory\" VALUES ('Host_001', '10.0.11')");
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
            DatabaseInspection inspected = await maintenance.InspectAsync(TimeSpan.FromSeconds(30));
            Assert.True(inspected.Exists);
            Assert.Empty(inspected.PendingMigrations);
            Assert.False(scope.ServiceProvider.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>().Database.HasPendingModelChanges());
            DatabaseMaintenanceResult unchanged = await maintenance.UpdateExistingAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(MaintenanceOutcome.Unchanged, unchanged.Outcome);
            Assert.Null(unchanged.Backup);
            Assert.Equal(MaintenanceError.DatabaseAlreadyExists, (await Assert.ThrowsAsync<MaintenanceException>(() =>
                maintenance.InitializeNewAsync(TimeSpan.FromSeconds(30)))).Code);
            // Maintenance API не предоставляет downgrade: штатный EF migrator доступен через библиотечный facade.
            await scope.ServiceProvider.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>().Database.GetService<IMigrator>().MigrateAsync("0");
        }
        Assert.Equal("Host_001", Assert.Single(await database.QueryAsync("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\""))["MigrationId"]);
        Assert.Empty(await database.QueryAsync("SELECT \"MigrationId\" FROM \"__AgentBridgeMigrationsHistory\""));
        string tables = provider == DatabaseProvider.SQLite
            ? "SELECT name FROM sqlite_schema WHERE type='table' AND name IN ('Dialogs','DialogTurns','ModelSteps','CanonicalItems','DialogContexts')"
            : "SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('Dialogs','DialogTurns','ModelSteps','CanonicalItems','DialogContexts')";
        Assert.Empty(await database.QueryAsync(tables));
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
            Assert.Single((await maintenance.InspectAsync(TimeSpan.FromSeconds(30))).PendingMigrations);
            DatabaseMaintenanceResult reapplied = await maintenance.UpdateExistingAsync(TimeSpan.FromSeconds(45));
            Assert.Equal(MaintenanceOutcome.Migrated, reapplied.Outcome);
            Assert.Single(reapplied.AppliedMigrations);
            Assert.NotNull(reapplied.Backup);
        }
        Assert.Equal(5, (await database.QueryAsync(tables)).Count);
        string expected = provider == DatabaseProvider.SQLite ? "20261003155233_InitialAgentBridgeSchema" : "20261003155235_InitialAgentBridgeSchema";
        Assert.Equal(expected, Assert.Single(await database.QueryAsync("SELECT \"MigrationId\" FROM \"__AgentBridgeMigrationsHistory\""))["MigrationId"]);
        Assert.Equal("Host_001", Assert.Single(await database.QueryAsync("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\""))["MigrationId"]);
    }

    /// <summary>Backup существующей БД содержит именно данные/схему до migration; restore выполняется отдельно.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task ExistingDatabaseIsBackedUpBeforeInitialMigration(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.CreateEmptyAsync();
        await SeedSentinelAsync(database);
        string before = await database.FingerprintAsync();
        LocalBackupArtifact artifact;
        using (IServiceScope scope = database.Root.CreateScope())
        {
            DatabaseMaintenanceResult migrated = await scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>()
                .UpdateExistingAsync(TimeSpan.FromSeconds(45));
            Assert.Equal(MaintenanceOutcome.Migrated, migrated.Outcome);
            Assert.Single(migrated.AppliedMigrations);
            artifact = Assert.IsType<LocalBackupArtifact>(migrated.Backup!.Artifact);
            Assert.True(migrated.Backup.Confirms(migrated.Backup.OperationId, migrated.Backup.TargetIdentity,
                scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>().Capabilities, migrated.Backup.StartedAtUtc));
        }
        await AssertArtifactAsync(artifact);
        string restored = await database.RestoreAsync(artifact);
        Assert.Equal(before, await database.FingerprintAsync(restored));
        Assert.NotEqual(before, await database.FingerprintAsync());
        Assert.Single(await database.QueryAsync("SELECT * FROM \"Sentinel\""));
        Assert.Empty(Directory.EnumerateDirectories(database.BackupDirectory, ".maintenance-*"));
    }

    /// <summary>Native SQLite/pg_dump backup полного payload восстанавливает все таблицы, ограничения и историю.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task FullBackupRestoresSchemaAndDataIntoSeparateDatabase(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        token = await PersistenceIntegrationTests.FillAsync(database, token, Guid.NewGuid(), Guid.NewGuid(), "backup 😀");
        string before = await database.FingerprintAsync();
        LocalBackupArtifact artifact;
        using (IServiceScope scope = database.Root.CreateScope())
        {
            IDatabaseMaintenanceProvider<AgentBridgeContextKey> backup = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>();
            using MaintenanceBudget budget = new(TimeSpan.FromSeconds(45), default);
            DatabaseInspection inspection = await backup.InspectAsync(budget);
            DatabaseBackupReceipt receipt = await backup.BackupAsync(Guid.NewGuid(), inspection.TargetIdentity, budget);
            artifact = Assert.IsType<LocalBackupArtifact>(receipt.Artifact);
        }
        await AssertArtifactAsync(artifact);
        string restored = await database.RestoreAsync(artifact);
        Assert.NotEqual(database.ConnectionString, restored);
        Assert.Equal(before, await database.FingerprintAsync(restored));
        using ServiceProvider restoreRoot = database.BuildRoot(connection: restored);
        using IServiceScope restoreScope = restoreRoot.CreateScope();
        DialogSnapshot snapshot = (await restoreScope.ServiceProvider.GetRequiredService<IDialogReader>()
            .ReadAsync(PersistenceIntegrationTests.Access(token))).Data!;
        Assert.Equal("backup 😀", snapshot.Turns[0].Items[0].Content.GetProperty("text").GetString());
        Assert.Equal(token.Revision, snapshot.Token.Revision);
        using (IServiceScope maintenanceScope = restoreRoot.CreateScope())
        {
            Assert.Empty((await maintenanceScope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>()
                .InspectAsync(TimeSpan.FromSeconds(30))).PendingMigrations);
        }
        Assert.Empty(Directory.EnumerateDirectories(database.BackupDirectory, ".maintenance-*"));
    }

    /// <summary>Отсутствующий backup directory останавливает DDL, не портит gate и не меняет исходную БД.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task BackupFailureStopsMigrationsAndLeavesGateUsable(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.CreateEmptyAsync();
        await SeedSentinelAsync(database);
        string before = await database.FingerprintAsync();
        using ServiceProvider invalid = database.BuildRoot(options => options.BackupDirectory = Path.Combine(database.DirectoryPath, "missing-directory"));
        using IServiceScope scope = invalid.CreateScope();
        IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
        MaintenanceException failure = await Assert.ThrowsAsync<MaintenanceException>(() => maintenance.UpdateExistingAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(MaintenanceError.Configuration, failure.Code);
        Assert.Single((await maintenance.InspectAsync(TimeSpan.FromSeconds(30))).PendingMigrations);
        Assert.Equal(before, await database.FingerprintAsync());
        Assert.Empty(Directory.EnumerateFiles(database.BackupDirectory));
    }

    /// <summary>Настоящий конфликт DDL после backup сохраняет существующие данные и блокирует общий root gate.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task ActualMigrationFailurePoisonsSharedGateAndPreservesBackup(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.CreateEmptyAsync();
        await SeedSentinelAsync(database);
        await database.ExecuteAsync("CREATE TABLE \"Dialogs\" (\"Legacy\" text NOT NULL)");
        await database.ExecuteAsync("INSERT INTO \"Dialogs\" VALUES ('existing')");
        string before = await database.FingerprintAsync();
        using (IServiceScope scope = database.Root.CreateScope())
        {
            MaintenanceException failure = await Assert.ThrowsAsync<MaintenanceException>(() => scope.ServiceProvider
                .GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>().UpdateExistingAsync(TimeSpan.FromSeconds(45)));
            Assert.Equal(MaintenanceError.MigrationFailed, failure.Code);
            Assert.Null(failure.InnerException);
        }
        using (IServiceScope another = database.Root.CreateScope())
        {
            Assert.Equal(MaintenanceError.GatePoisoned, (await Assert.ThrowsAsync<MaintenanceException>(() => another.ServiceProvider
                .GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>().InspectAsync(TimeSpan.FromSeconds(30)))).Code);
        }
        Assert.Equal("existing", Assert.Single(await database.QueryAsync("SELECT * FROM \"Dialogs\""))["Legacy"]);
        string backupFile = Assert.Single(Directory.EnumerateFiles(database.BackupDirectory));
        await using FileStream stream = File.OpenRead(backupFile);
        LocalBackupArtifact artifact = new(backupFile, stream.Length, Convert.ToHexString(await SHA256.HashDataAsync(stream)));
        await stream.DisposeAsync();
        string restored = await database.RestoreAsync(artifact);
        Assert.Equal(before, await database.FingerprintAsync(restored));
        Assert.Empty(Directory.EnumerateDirectories(database.BackupDirectory, ".maintenance-*"));
    }

    /// <summary>Caller cancellation до I/O сохраняет исходный token и не запускает первую установку.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task CancelledInitializationDoesNotCreateDatabase(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        using CancellationTokenSource caller = new();
        caller.Cancel();
        using IServiceScope scope = database.Root.CreateScope();
        IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
        OperationCanceledException failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => maintenance.InitializeNewAsync(TimeSpan.FromSeconds(30), caller.Token));
        Assert.Equal(caller.Token, failure.CancellationToken);
        Assert.False((await maintenance.InspectAsync(TimeSpan.FromSeconds(30))).Exists);
    }

    /// <summary>Authentication и major mismatch не выдаются за Missing и не разрешают CREATE.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task PostgreSqlAuthenticationAndMajorMismatchDoNotInitialize(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        NpgsqlConnectionStringBuilder wrong = new(database.ConnectionString) { Password = "invalid-task-password" };
        using ServiceProvider unauthenticated = database.BuildRoot(connection: wrong.ConnectionString);
        using (IServiceScope scope = unauthenticated.CreateScope())
        {
            IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
            MaintenanceException failure = await Assert.ThrowsAsync<MaintenanceException>(() => maintenance.InitializeNewAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(MaintenanceError.AuthenticationFailed, failure.Code);
            Assert.DoesNotContain(wrong.Password!, failure.ToString());
            Assert.Null(failure.InnerException);
        }
        using ServiceProvider mismatch = database.BuildRoot(options => options.PostgreSqlServerMajorVersion = 17);
        using (IServiceScope scope = mismatch.CreateScope())
        {
            Assert.Equal(MaintenanceError.UnsupportedCapability, (await Assert.ThrowsAsync<MaintenanceException>(() => scope.ServiceProvider
                .GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>().InitializeNewAsync(TimeSpan.FromSeconds(30)))).Code);
        }
        using IServiceScope valid = database.Root.CreateScope();
        Assert.False((await valid.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>().InspectAsync(TimeSpan.FromSeconds(30))).Exists);
    }

    /// <summary>Реальная проверка отсутствующей dump-утилиты останавливает update до запуска процесса и migration, без утечки секретов.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task PostgreSqlMissingExecutableStopsUpdate(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.CreateEmptyAsync();
        await SeedSentinelAsync(database);
        string before = await database.FingerprintAsync();
        using ServiceProvider invalid = database.BuildRoot(options => options.PostgreSqlDumpExecutablePath = Path.Combine(database.DirectoryPath, "absent-pg_dump.exe"));
        using IServiceScope scope = invalid.CreateScope();
        IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => maintenance.UpdateExistingAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal(MaintenanceError.Configuration, error.Code);
        Assert.Null(error.InnerException);
        Assert.Single((await maintenance.InspectAsync(TimeSpan.FromSeconds(30))).PendingMigrations);
        Assert.Equal(before, await database.FingerprintAsync());
        Assert.Empty(Directory.EnumerateFileSystemEntries(database.BackupDirectory));
    }

    /// <summary>Настоящий pg_restore отвергает повреждённый dump; отдельная target-БД остаётся пустой, source не меняется.</summary>
    [DatabaseIntegrationTheory]
    [InlineData(DatabaseProvider.PostgreSql)]
    public async Task PostgreSqlRestoreRejectsCorruptArtifact(DatabaseProvider provider)
    {
        await using IntegrationDatabase database = new(provider);
        await database.InitializeAsync();
        DialogWriteToken token = await PersistenceIntegrationTests.CreateAsync(database);
        await PersistenceIntegrationTests.FillAsync(database, token, Guid.NewGuid(), Guid.NewGuid(), "source preserved");
        string before = await database.FingerprintAsync();
        string corrupt = Path.Combine(database.DirectoryPath, "corrupt.dump");
        await File.WriteAllTextAsync(corrupt, "This is not a PostgreSQL custom dump.");
        byte[] bytes = await File.ReadAllBytesAsync(corrupt);
        LocalBackupArtifact artifact = new(corrupt, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        string destination = await database.RestoreAsync(artifact, expectSuccess: false);
        Assert.Empty(await database.QueryAsync("SELECT table_name FROM information_schema.tables WHERE table_schema='public'", destination));
        Assert.Equal(before, await database.FingerprintAsync());
        Assert.Empty(Directory.EnumerateFileSystemEntries(database.BackupDirectory));
    }

    /// <summary>Создаёт независимые сохранённые данные до появления схемы AgentBridge.</summary>
    private static async Task SeedSentinelAsync(IntegrationDatabase database)
    {
        await database.ExecuteAsync("CREATE TABLE \"Sentinel\" (\"Id\" integer NOT NULL PRIMARY KEY, \"Text\" text NOT NULL)");
        await database.ExecuteAsync("INSERT INTO \"Sentinel\" (\"Id\", \"Text\") VALUES (@id, @text)",
            new Dictionary<string, object?> { ["id"] = 1, ["text"] = "сохранено 😀" });
    }

    /// <summary>Проверяет физический артефакт отдельно от обещания receipt.</summary>
    private static async Task AssertArtifactAsync(LocalBackupArtifact artifact)
    {
        await using FileStream stream = File.OpenRead(artifact.Path);
        Assert.Equal(artifact.Length, stream.Length);
        Assert.Equal(artifact.Sha256, Convert.ToHexString(await SHA256.HashDataAsync(stream)));
    }
}
