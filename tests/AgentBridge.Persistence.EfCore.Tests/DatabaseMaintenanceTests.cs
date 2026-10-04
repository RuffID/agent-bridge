using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Явный API AgentBridge с настоящим библиотечным coordinator и fake provider/migration boundaries.</summary>
public class DatabaseMaintenanceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Inspection возвращает pending без backup, create или migrate.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InspectDoesNotChangeDatabase(bool exists)
    {
        FakeMaintenanceBoundary fake = new() { Exists = exists };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseInspection inspection = await Service(scope).InspectAsync(Timeout);
        Assert.Equal(exists, inspection.Exists);
        Assert.Equal(exists ? new[] { "synthetic-migration" } : [], inspection.PendingMigrations);
        Assert.Equal(exists ? new[] { "inspect", "pin", "inspect", "pending", "unpin" } : ["inspect"], fake.Calls);
        Assert.False(fake.Pinned);
    }

    /// <summary>Перед pending migrations подтверждается backup и повторно проверяется target; session освобождается.</summary>
    [Fact]
    public async Task ExistingDatabaseBacksUpBeforeMigration()
    {
        FakeMaintenanceBoundary fake = new();
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseMaintenanceResult result = await Service(scope).UpdateExistingAsync(Timeout);
        Assert.Equal(MaintenanceOutcome.Migrated, result.Outcome);
        Assert.Equal(["synthetic-migration"], result.AppliedMigrations);
        Assert.NotNull(result.Backup);
        Assert.Equal(fake.Target, result.Backup.TargetIdentity);
        Assert.Equal(fake.Capabilities.Scope, result.Backup.Scope);
        Assert.Equal(["inspect", "pin", "inspect", "pending", "backup", "inspect", "migrate", "pending", "unpin"], fake.Calls);
        Assert.False(fake.Pinned);
    }

    /// <summary>Без изменений результат Unchanged не содержит фиктивный backup.</summary>
    [Fact]
    public async Task NoPendingMeansNoBackup()
    {
        FakeMaintenanceBoundary fake = new() { Pending = [] };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseMaintenanceResult result = await Service(scope).UpdateExistingAsync(Timeout);
        Assert.Equal(MaintenanceOutcome.Unchanged, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Empty(result.AppliedMigrations);
        Assert.Equal(["inspect", "pin", "inspect", "pending", "unpin"], fake.Calls);
    }

    /// <summary>Первая установка выбирается отдельно и не создаёт фиктивную копию отсутствующей БД.</summary>
    [Fact]
    public async Task NewDatabaseIsInitializedExplicitly()
    {
        FakeMaintenanceBoundary fake = new() { Exists = false };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        DatabaseMaintenanceResult result = await Service(scope).InitializeNewAsync(Timeout);
        Assert.Equal(MaintenanceOutcome.Initialized, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Equal(["inspect", "create", "inspect", "pin", "inspect", "pending", "inspect", "migrate", "pending", "unpin"], fake.Calls);
    }

    /// <summary>Оба режима отклоняют неверное существование цели без переключения на другой режим.</summary>
    [Theory]
    [InlineData(false, MaintenanceError.DatabaseMissing)]
    [InlineData(true, MaintenanceError.DatabaseAlreadyExists)]
    public async Task ModesDoNotFallback(bool initialize, MaintenanceError expected)
    {
        FakeMaintenanceBoundary fake = new() { Exists = initialize };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => initialize
            ? Service(scope).InitializeNewAsync(Timeout) : Service(scope).UpdateExistingAsync(Timeout));
        Assert.Equal(expected, error.Code);
        Assert.Equal(["inspect"], fake.Calls);
    }

    /// <summary>Ошибки доступа и подключения сохраняют safe code и не разрешают CREATE.</summary>
    [Theory]
    [InlineData(MaintenanceError.AuthenticationFailed)]
    [InlineData(MaintenanceError.PermissionDenied)]
    [InlineData(MaintenanceError.ConnectionFailed)]
    public async Task ConnectionFailureNeverMeansMissing(MaintenanceError code)
    {
        FakeMaintenanceBoundary fake = new() { InspectionAction = _ => throw new MaintenanceException(code) };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InitializeNewAsync(Timeout));
        Assert.Equal(code, error.Code);
        Assert.Equal(["inspect"], fake.Calls);
    }

    /// <summary>Неподтверждённый receipt любого происхождения запрещает migration.</summary>
    [Theory]
    [InlineData("operation")]
    [InlineData("target")]
    [InlineData("provider")]
    [InlineData("format")]
    [InlineData("scope")]
    [InlineData("artifact")]
    [InlineData("time")]
    public async Task InvalidReceiptStopsBeforeMigration(string field)
    {
        FakeMaintenanceBoundary fake = new()
        {
            ChangeReceipt = receipt => field switch
            {
                "operation" => receipt with { OperationId = Guid.NewGuid() },
                "target" => receipt with { TargetIdentity = "other-target" },
                "provider" => receipt with { Provider = "other-provider" },
                "format" => receipt with { Format = "other-format" },
                "scope" => receipt with { Scope = "other-scope" },
                "artifact" => receipt with { Artifact = new LocalBackupArtifact("relative", 0, "invalid") },
                "time" => receipt with { StartedAtUtc = DateTimeOffset.UnixEpoch },
                _ => throw new InvalidOperationException("Неизвестный test case.")
            }
        };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout));
        Assert.Equal(MaintenanceError.BackupNotConfirmed, error.Code);
        Assert.DoesNotContain("migrate", fake.Calls);
        Assert.False(fake.Pinned);
    }

    /// <summary>Provider backup failure или collision не продолжают migration и не раскрывают raw error.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackupFailureOrNameCollisionStopsMigration(bool collision)
    {
        FakeMaintenanceBoundary fake = new()
        {
            BackupAction = _ => collision ? throw new IOException("synthetic-secret-collision")
                : throw new MaintenanceException(MaintenanceError.BackupFailed)
        };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout));
        Assert.Equal(MaintenanceError.BackupFailed, error.Code);
        Assert.DoesNotContain("synthetic-secret", error.ToString());
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("migrate", fake.Calls);
        Assert.Equal("unpin", fake.Calls.Last());
    }

    /// <summary>Смена target после backup останавливает схему без попытки обновить новую цель.</summary>
    [Fact]
    public async Task ChangedTargetAfterBackupStopsMigration()
    {
        FakeMaintenanceBoundary fake = new();
        fake.ChangeReceipt = receipt => { fake.Target = "changed-target"; return receipt; };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout));
        Assert.Equal(MaintenanceError.TargetMismatch, error.Code);
        Assert.DoesNotContain("migrate", fake.Calls);
    }

    /// <summary>Неизвестный исход migration, verification или cleanup блокирует другие scope того же root.</summary>
    [Theory]
    [InlineData("migration", MaintenanceError.MigrationFailed)]
    [InlineData("verification", MaintenanceError.MigrationFailed)]
    [InlineData("cleanup", MaintenanceError.CleanupUnconfirmed)]
    [InlineData("backup-cleanup", MaintenanceError.CleanupUnconfirmed)]
    public async Task PartialFailurePoisonsSharedGate(string failure, MaintenanceError expected)
    {
        FakeMaintenanceBoundary fake = new();
        if (failure == "migration") fake.MigrationAction = _ => throw new InvalidOperationException("synthetic-secret-driver");
        if (failure == "verification") fake.KeepPending = true;
        if (failure == "cleanup") fake.FailCleanup = true;
        if (failure == "backup-cleanup") fake.BackupAction = _ => throw new MaintenanceException(MaintenanceError.CleanupUnconfirmed, MaintenanceError.BackupFailed);
        using ServiceProvider root = Root(fake);
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(first).UpdateExistingAsync(Timeout));
        Assert.Equal(expected, error.Code);
        Assert.DoesNotContain("synthetic-secret", error.ToString());
        if (failure == "backup-cleanup") Assert.Equal(MaintenanceError.BackupFailed, error.PrimaryError);
        int calls = fake.Calls.Count;
        MaintenanceException blocked = await Assert.ThrowsAsync<MaintenanceException>(() => Service(second).InspectAsync(Timeout));
        Assert.Equal(MaintenanceError.GatePoisoned, blocked.Code);
        Assert.Equal(calls, fake.Calls.Count);
    }

    /// <summary>CREATE collision явно завершается ошибкой без backup/migrate и без retry.</summary>
    [Fact]
    public async Task InitializationCollisionDoesNotContinue()
    {
        FakeMaintenanceBoundary fake = new() { Exists = false, CreationAction = _ => throw new MaintenanceException(MaintenanceError.DatabaseAlreadyExists) };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InitializeNewAsync(Timeout));
        Assert.Equal(MaintenanceError.DatabaseAlreadyExists, error.Code);
        Assert.Equal(["inspect", "create"], fake.Calls);
        MaintenanceException blocked = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InspectAsync(Timeout));
        Assert.Equal(MaintenanceError.GatePoisoned, blocked.Code);
    }

    /// <summary>Отмена до gate или во время backup возвращает исходный caller token без migration.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationIsPreserved(bool duringBackup)
    {
        using CancellationTokenSource caller = new();
        FakeMaintenanceBoundary fake = new();
        if (duringBackup) fake.BackupAction = budget => { caller.Cancel(); budget.Check(); return Task.CompletedTask; };
        else caller.Cancel();
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(scope).UpdateExistingAsync(Timeout, caller.Token));
        Assert.Equal(caller.Token, error.CancellationToken);
        Assert.DoesNotContain("migrate", fake.Calls);
        Assert.False(fake.Pinned);
        if (!duringBackup) Assert.Empty(fake.Calls);
    }

    /// <summary>Typed backup failure не заменяется одновременно сработавшей отменой.</summary>
    [Fact]
    public async Task TypedFailureWinsOverConcurrentCancellation()
    {
        using CancellationTokenSource caller = new();
        FakeMaintenanceBoundary fake = new() { BackupAction = _ => { caller.Cancel(); throw new MaintenanceException(MaintenanceError.BackupFailed); } };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout, caller.Token));
        Assert.Equal(MaintenanceError.BackupFailed, error.Code);
    }

    /// <summary>Собственный deadline отличается от отмены вызывающего кода.</summary>
    [Fact]
    public async Task DeadlineIsReportedWithoutCallerCancellation()
    {
        FakeMaintenanceBoundary fake = new() { InspectionAction = budget => Task.Delay(System.Threading.Timeout.Infinite, budget.Token) };
        using ServiceProvider root = Root(fake);
        using IServiceScope scope = root.CreateScope();
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).InspectAsync(TimeSpan.FromMilliseconds(25)));
        Assert.Equal(MaintenanceError.DeadlineExceeded, error.Code);
        Assert.Equal(["inspect"], fake.Calls);
    }

    /// <summary>Второй scope ждёт общий gate; отменённый waiter не начинает provider I/O.</summary>
    [Fact]
    public async Task GateSerializesScopesAndCancellationStopsWaiter()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeMaintenanceBoundary firstFake = new() { InspectionAction = async budget => { entered.TrySetResult(); await release.Task.WaitAsync(budget.Token); } };
        FakeMaintenanceBoundary secondFake = new();
        ServiceCollection services = DatabaseMaintenanceRegistrationTests.Services(DatabaseProvider.SQLite);
        services.AddScoped<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>(_ => firstFake);
        services.AddScoped<IRelationalMigrationOperations<AgentBridgeContextKey>>(_ => firstFake);
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope first = root.CreateScope();
        using IServiceScope second = root.CreateScope();
        // Отдельная fake session для второго coordinator сохраняет общий root gate.
        DatabaseMaintenance<AgentBridgeContextKey> secondService = new(secondFake, secondFake,
            second.ServiceProvider.GetRequiredService<SingleInitializerGate>(), second.ServiceProvider.GetRequiredService<ILogger<DatabaseMaintenance<AgentBridgeContextKey>>>());
        Task<DatabaseInspection> active = Service(first).InspectAsync(Timeout);
        using CancellationTokenSource waiter = new();
        try
        {
            await entered.Task.WaitAsync(Timeout);
            Task<DatabaseInspection> waiting = secondService.InspectAsync(Timeout, waiter.Token);
            Assert.False(waiting.IsCompleted);
            Assert.Empty(secondFake.Calls);
            waiter.Cancel();
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.Equal(waiter.Token, error.CancellationToken);
            Assert.Empty(secondFake.Calls);
        }
        finally
        {
            release.TrySetResult();
            await active;
        }
        await secondService.InspectAsync(Timeout);
        Assert.Contains("inspect", secondFake.Calls);
    }

    /// <summary>Стадии и safe code доступны через logger приложения без raw exception или секретов.</summary>
    [Fact]
    public async Task DiagnosticsPreserveSafeStagesAndErrors()
    {
        MaintenanceTestLogger logger = new();
        FakeMaintenanceBoundary fake = new() { BackupAction = _ => throw new InvalidOperationException("synthetic-secret-driver") };
        using ServiceProvider root = Root(fake, logger);
        using IServiceScope scope = root.CreateScope();
        await Assert.ThrowsAsync<MaintenanceException>(() => Service(scope).UpdateExistingAsync(Timeout));
        IReadOnlyDictionary<string, object?> failure = Assert.Single(logger.Events);
        Assert.Equal(MaintenanceStage.Backup, failure["Stage"]);
        Assert.Equal(MaintenanceError.BackupFailed, failure["Code"]);
        Assert.IsType<Guid>(failure["OperationId"]);
        Assert.All(logger.Exceptions, Assert.Null);
        Assert.DoesNotContain("synthetic-secret", string.Join(" ", failure.Values));
        fake.BackupAction = null;
        await Service(scope).UpdateExistingAsync(Timeout);
        Assert.Equal(MaintenanceStage.Verification, logger.Events.Last()["Stage"]);
    }

    /// <summary>Создаёт production registrations и заменяет только технические I/O-границы.</summary>
    private static ServiceProvider Root(FakeMaintenanceBoundary fake, MaintenanceTestLogger? logger = null)
    {
        ServiceCollection services = DatabaseMaintenanceRegistrationTests.Services(DatabaseProvider.SQLite);
        services.RemoveAll<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>();
        services.RemoveAll<IRelationalMigrationOperations<AgentBridgeContextKey>>();
        services.AddScoped<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>(_ => fake);
        services.AddScoped<IRelationalMigrationOperations<AgentBridgeContextKey>>(_ => fake);
        if (logger is not null) services.AddLogging(builder => builder.AddProvider(logger));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Разрешает только явно вызываемый библиотечный API выбранного scope.</summary>
    private static IDatabaseMaintenance<AgentBridgeContextKey> Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
}
