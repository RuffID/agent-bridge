using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.Migrations.SqlServer;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using EFCoreLibrary.EfCore;
using EFCoreLibrary.EfCore.Repository.Base;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.SqlServer.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Настоящие SQL Server metadata/DI/параметры без соединения или исполнения SQL.</summary>
public class SqlServerProviderTests
{
    /// <summary>Generated operations описывают ровно шесть таблиц и безопасный порядок Down; SQL generator и БД не вызываются.</summary>
    [Fact]
    public void GeneratedInitialOperationsMatchSixTableGraph()
    {
        using AgentBridgeDbContext context = new SqlServerAgentBridgeDbContextFactory().CreateDbContext([]);
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        Assert.Equal(2, assembly.Migrations.Count);
        Migration migration = assembly.CreateMigration(Assert.Single(assembly.Migrations,
            item => item.Key.EndsWith("_InitialAgentBridgeSchema", StringComparison.Ordinal)).Value, context.Database.ProviderName!);
        CreateTableOperation[] tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(6, tables.Length);
        Assert.Equal(23, tables.Sum(table => table.CheckConstraints.Count));
        Assert.Equal(5, tables.Sum(table => table.ForeignKeys.Count));
        Assert.Equal(3, migration.UpOperations.OfType<CreateIndexOperation>().Count());
        Assert.Equal(9, migration.UpOperations.Count);
        Assert.DoesNotContain(migration.UpOperations, operation => operation is SqlOperation);
        Assert.DoesNotContain(tables, table => table.Name == AgentBridgeMigrationsHistory.TABLE_NAME);
        Assert.All(tables.SelectMany(table => table.ForeignKeys), key => Assert.Equal(ReferentialAction.Cascade, key.OnDelete));
        string[] drops = migration.DownOperations.OfType<DropTableOperation>().Select(table => table.Name).ToArray();
        Assert.Equal(6, drops.Length);
        foreach (CreateTableOperation table in tables)
        {
            foreach (AddForeignKeyOperation key in table.ForeignKeys)
            {
                Assert.True(Array.IndexOf(drops, table.Name) < Array.IndexOf(drops, key.PrincipalTable));
            }
        }
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>Сохраняет numeric совместимость и явный выбор; forwarded аргументы не читают secrets.</summary>
    [Fact]
    public void ProviderAndFactoryRemainExplicit()
    {
        Assert.Equal(0, (int)DatabaseProvider.SQLite);
        Assert.Equal(1, (int)DatabaseProvider.PostgreSql);
        Assert.Equal(2, (int)DatabaseProvider.SqlServer);
        SqlServerAgentBridgeDbContextFactory factory = new();
        Assert.Throws<ArgumentNullException>(() => factory.CreateDbContext(null!));
        ArgumentException error = Assert.Throws<ArgumentException>(() => factory.CreateDbContext(["synthetic-secret"]));
        Assert.DoesNotContain("synthetic-secret", error.Message);
    }

    /// <summary>Все строки payload используют Unicode max, UTC — bigint, owner — binary; constraints не требуют length или quoted identifiers.</summary>
    [Fact]
    public void ModelPreservesPayloadGuardsAndSingleCascadePaths()
    {
        using AgentBridgeDbContext context = new SqlServerAgentBridgeDbContextFactory().CreateDbContext([]);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IRelationalModel relational = model.GetRelationalModel();
        Assert.Equal(10, relational.Tables.Count());
        Assert.All(relational.Tables.SelectMany(table => table.Columns), column =>
        {
            if (column.Name == "OwnerId" && column.Table.Name == "Dialogs") Assert.Equal("varbinary(max)", column.StoreType);
            else if (column.PropertyMappings.Any(mapping => mapping.Property.ClrType == typeof(string)))
            {
                int? length = column.PropertyMappings.First().Property.GetMaxLength();
                Assert.Equal(length is null ? "nvarchar(max)" : $"nvarchar({length})", column.StoreType);
            }
            if (column.Name.EndsWith("AtUtc", StringComparison.Ordinal)) Assert.Equal("bigint", column.StoreType);
        });
        Assert.All(model.GetEntityTypes().SelectMany(entity => entity.GetCheckConstraints()), constraint =>
        {
            Assert.False(Regex.IsMatch(constraint.Sql, @"\blength\(", RegexOptions.IgnoreCase));
            Assert.DoesNotContain('"', constraint.Sql);
        });
        IEntityType root = model.FindEntityType(typeof(DialogRecord))!;
        Assert.Equal("DATALENGTH([OwnerId]) > 0", root.GetCheckConstraints().Single(check => check.Name == "CK_Dialog_Owner").Sql);
        Assert.Contains(model.FindEntityType(typeof(DialogSettingsRecord))!.GetCheckConstraints(), check => check.Sql == "DATALENGTH([Model]) > 0");
        Assert.True(model.FindEntityType(typeof(DialogSettingsRecord))!.FindProperty("Version")!.IsConcurrencyToken);
        Assert.True(model.FindEntityType(typeof(DialogTurnRecord))!.FindProperty("SettingsJson")!.IsNullable);
        Assert.True(model.FindEntityType(typeof(ModelStepRecord))!.FindProperty("ToolAttemptsJson")!.IsNullable);
        Assert.True(model.FindEntityType(typeof(DialogContextRecord))!.FindProperty("SelectedModel")!.IsNullable);
        Assert.Equal(new[] { "DialogId", "Id" }, model.FindEntityType(typeof(DialogTurnRecord))!.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(new[] { "DialogId", "TurnId", "Id" }, model.FindEntityType(typeof(ModelStepRecord))!.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(new[] { "DialogId", "TurnId" }, Assert.Single(model.FindEntityType(typeof(CanonicalItemRecord))!.GetForeignKeys()).Properties.Select(property => property.Name));
        foreach (IEntityType entity in model.GetEntityTypes().Where(entity => entity != root && entity.GetForeignKeys().Any()))
        {
            HashSet<IEntityType> visited = [];
            IEntityType current = entity;
            while (current != root)
            {
                Assert.True(visited.Add(current));
                IForeignKey parent = Assert.Single(current.GetForeignKeys());
                Assert.Equal(DeleteBehavior.Cascade, parent.DeleteBehavior);
                current = parent.PrincipalEntityType;
            }
        }
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>Binary converter сохраняет ordinal различия, включая trailing spaces, NUL и непарные суррогаты; параметры получают varbinary без БД.</summary>
    [Theory]
    [InlineData(" owner ")]
    [InlineData("Владелец😀")]
    [InlineData("a\0b")]
    public void OwnerRoundTripAndParameterMappingPreserveCodeUnits(string owner)
    {
        VerifyOwner(owner);
        VerifyOwner("a" + (char)0xD800 + "b" + (char)0xDC00);
        VerifyOwner(new string('Ж', 5000));
    }

    /// <summary>Сверяет production converter и настоящий EF parameter mapping, сохраняя immutable original concurrency.</summary>
    private static void VerifyOwner(string owner)
    {
        using AgentBridgeDbContext context = new SqlServerAgentBridgeDbContextFactory().CreateDbContext([]);
        IProperty property = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(DialogRecord))!.FindProperty("OwnerId")!;
        ValueConverter converter = property.GetTypeMapping().Converter!;
        byte[] bytes = Assert.IsType<byte[]>(converter.ConvertToProvider(owner));
        Assert.Equal(owner, converter.ConvertFromProvider(bytes));
        Assert.Equal(owner.Length * 2, bytes.Length);
        Assert.NotEqual(bytes, Assert.IsType<byte[]>(converter.ConvertToProvider(owner + " ")));
        Assert.NotEqual(bytes, Assert.IsType<byte[]>(converter.ConvertToProvider(owner + "\0")));
        Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider(new byte[1]));
        using DbCommand command = context.Database.GetDbConnection().CreateCommand();
        DbParameter parameter = property.GetRelationalTypeMapping().CreateParameter(command, "@owner", owner);
        Assert.Equal(DbType.Binary, parameter.DbType);
        Assert.Equal(bytes, Assert.IsType<byte[]>(parameter.Value));
        Assert.Equal(PropertySaveBehavior.Throw, property.GetAfterSaveBehavior());
        Assert.True(property.IsConcurrencyToken);
        string predicate = context.Set<DialogRecord>().Where(record => record.OwnerId == owner).ToQueryString();
        Assert.Contains(owner.Length > 4000 ? "varbinary(max)" : "varbinary(8000)", predicate);
        Assert.Contains("0x" + Convert.ToHexString(bytes), predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[d].[OwnerId] = @", predicate);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    /// <summary>Actual base CRUD/scoped adapter и SQL Server maintenance разрешаются без I/O; server path не зависит от ОС app host.</summary>
    [Theory]
    [InlineData("/var/opt/mssql/backups")]
    [InlineData("C:\\SqlBackups")]
    [InlineData("\\\\server\\backups")]
    public void MaintenanceUsesLibraryAndServerDestination(string destination)
    {
        ServiceCollection services = Services(destination);
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        provider.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope scope = provider.CreateScope();
        AgentBridgeDbContext context = scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
        Assert.IsType<EfDbContextAdapter<AgentBridgeDbContext, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IAppDbContext<AgentBridgeContextKey>>());
        Assert.IsType<CreateItemRepository<DialogRecord, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IContextCreateItemRepository<DialogRecord, AgentBridgeContextKey>>());
        Assert.IsType<SqlServerMaintenanceProvider<AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<AgentBridgeContextKey>>());
        IDatabaseMaintenance<AgentBridgeContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
        Assert.Equal("sqlserver", maintenance.Capabilities.Provider);
        Assert.Same(context.Database.GetDbConnection(), scope.ServiceProvider.GetRequiredService<IRelationalMigrationOperations<AgentBridgeContextKey>>().Connection);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Null(provider.GetRequiredService<IOptions<DatabaseBackupOptions>>().Value.BackupDirectory);
    }

    /// <summary>Backup path/retention проверяются локально без раскрытия значений или fallback на host directory.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("synthetic-secret-relative")]
    [InlineData("/synthetic-secret\n")]
    public void InvalidServerDestinationFailsBeforeContext(string? destination)
    {
        ServiceCollection services = Services(destination);
        services.AddScoped<AgentBridgeDbContext>(_ => throw new InvalidOperationException("Контекст не должен создаваться."));
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>());
        Assert.DoesNotContain("synthetic-secret", error.ToString());
    }

    /// <summary>Явные синтетические settings без файлов, сети или реального server.</summary>
    private static ServiceCollection Services(string? destination)
    {
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer;
            options.ConnectionString = "Server=invalid.example;Database=synthetic;User ID=synthetic;Password=synthetic;Encrypt=True;TrustServerCertificate=False;ConnectRetryCount=0";
        });
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(options =>
        {
            options.SqlServerBackupDirectory = destination;
            options.BackupRetentionPeriod = TimeSpan.FromDays(7);
        }, MaintenanceExecutionMode.SingleInitializer);
        return services;
    }
}
