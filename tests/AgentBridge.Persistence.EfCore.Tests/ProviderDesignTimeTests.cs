using System.Reflection;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.Migrations.PostgreSql;
using AgentBridge.Persistence.Migrations.Sqlite;
using AgentBridge.Persistence.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверки общей runtime/design-time модели и выбора сборки без БД, SQL или генерации миграций.</summary>
public class ProviderDesignTimeTests
{
    /// <summary>Factory и DI выбирают одну модель и assembly; контекст содержит только собственные таблицы и исходные guards.</summary>
    [Theory]
    [InlineData(DatabaseProvider.SQLite, "Microsoft.EntityFrameworkCore.Sqlite", "BINARY", "INTEGER")]
    [InlineData(DatabaseProvider.PostgreSql, "Npgsql.EntityFrameworkCore.PostgreSQL", "C", "bigint")]
    [InlineData(DatabaseProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer", null, "bigint")]
    public void RuntimeAndFactoryShareProviderSchema(DatabaseProvider selected, string providerName, string? collation, string ticksType)
    {
        IDesignTimeDbContextFactory<AgentBridgeDbContext> factory = CreateFactory(selected);
        using AgentBridgeDbContext design = factory.CreateDbContext([]);
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = selected;
            options.ConnectionString = design.Database.GetConnectionString()!;
        });
        services.AddAgentBridgePersistence();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using IServiceScope scope = provider.CreateScope();
        AgentBridgeDbContext runtime = scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
        Assert.Equal(providerName, design.Database.ProviderName);
        Assert.Equal(providerName, runtime.Database.ProviderName);
        Assert.Equal(factory.GetType().Assembly, design.GetService<IMigrationsAssembly>().Assembly);
        Assert.Equal(factory.GetType().Assembly, runtime.GetService<IMigrationsAssembly>().Assembly);
        foreach (AgentBridgeDbContext configured in new[] { design, runtime })
        {
            RelationalOptionsExtension relational = RelationalOptionsExtension.Extract(configured.GetService<IDbContextOptions>());
            Assert.Equal("__AgentBridgeMigrationsHistory", relational.MigrationsHistoryTableName);
            Assert.Null(relational.MigrationsHistoryTableSchema);
            Assert.Equal(selected switch { DatabaseProvider.SQLite => "SqliteHistoryRepository", DatabaseProvider.PostgreSql => "NpgsqlHistoryRepository", DatabaseProvider.SqlServer => "SqlServerHistoryRepository", _ => throw new ArgumentOutOfRangeException(nameof(selected)) },
                configured.GetService<IHistoryRepository>().GetType().Name);
        }
        Assert.Equal(typeof(AgentBridgeDbContext), design.GetType());
        Assert.False(design.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single().IsSensitiveDataLoggingEnabled);
        IModel designModel = design.GetService<IDesignTimeModel>().Model;
        IModel runtimeModel = runtime.GetService<IDesignTimeModel>().Model;
        Assert.Equal(runtimeModel.ToDebugString(MetadataDebugStringOptions.LongDefault),
            designModel.ToDebugString(MetadataDebugStringOptions.LongDefault));
        Assert.Equal(new[] { "CanonicalItems", "DialogContexts", "DialogSettings", "DialogTurns", "Dialogs", "ModelSteps" },
            designModel.GetEntityTypes().Select(entity => entity.GetTableName()).OrderBy(name => name, StringComparer.Ordinal));
        IEntityType root = designModel.FindEntityType(typeof(DialogRecord))!;
        Assert.Equal(collation, root.FindProperty(nameof(DialogRecord.OwnerId))!.GetCollation());
        Assert.Equal(ticksType, root.FindProperty(nameof(DialogRecord.ExpiresAtUtc))!.GetColumnType());
        Assert.Equal(typeof(long), root.FindProperty(nameof(DialogRecord.ExpiresAtUtc))!.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal(new[] { "ExpiresAtUtc", "Id" }, Assert.Single(root.GetIndexes()).Properties.Select(property => property.Name));
        foreach (string name in new[] { "IncarnationId", "OwnerId", "CreatedAtUtc", "ExpiresAtUtc" })
        {
            Assert.Equal(PropertySaveBehavior.Throw, root.FindProperty(name)!.GetAfterSaveBehavior());
        }
        foreach (string name in new[] { "IncarnationId", "Revision", "OwnerId", "ExpiresAtUtc" })
        {
            Assert.True(root.FindProperty(name)!.IsConcurrencyToken);
        }
        Assert.Equal(5, designModel.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()).Count());
        Assert.All(designModel.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior));
    }

    /// <summary>Forwarded arguments не могут передать реальное подключение или незаметно изменить provider фабрики.</summary>
    [Theory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void FactoryRejectsForwardedArguments(DatabaseProvider selected)
    {
        IDesignTimeDbContextFactory<AgentBridgeDbContext> factory = CreateFactory(selected);
        Assert.Throws<ArgumentNullException>(() => factory.CreateDbContext(null!));
        ArgumentException error = Assert.Throws<ArgumentException>(() => factory.CreateDbContext(["synthetic-secret"]));
        Assert.Equal("args", error.ParamName);
        Assert.DoesNotContain("synthetic-secret", error.Message);
    }

    /// <summary>Штатные snapshot и designer описывают текущую схему без pending model differences; Up/Down и SQL не исполняются.</summary>
    [Theory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void GeneratedSnapshotAndDesignerMatchCurrentSchema(DatabaseProvider selected)
    {
        using AgentBridgeDbContext context = CreateFactory(selected).CreateDbContext([]);
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        Assert.NotNull(assembly.ModelSnapshot);
        Assert.Equal(selected == DatabaseProvider.SqlServer ? 1 : 3, assembly.Migrations.Count);
        KeyValuePair<string, TypeInfo> registered = assembly.Migrations.OrderBy(pair => pair.Key).Last();
        Assert.EndsWith(selected == DatabaseProvider.SqlServer ? "_InitialAgentBridgeSchema" : "_AddDialogSettings", registered.Key);
        Migration migration = assembly.CreateMigration(registered.Value, context.Database.ProviderName!);
        IModelRuntimeInitializer initializer = context.GetService<IModelRuntimeInitializer>();
        IModel snapshotModel = initializer.Initialize(assembly.ModelSnapshot.Model, designTime: true);
        IModel targetModel = initializer.Initialize(migration.TargetModel, designTime: true);
        IModel currentModel = context.GetService<IDesignTimeModel>().Model;
        IMigrationsModelDiffer differ = context.GetService<IMigrationsModelDiffer>();
        Assert.False(differ.HasDifferences(snapshotModel.GetRelationalModel(), currentModel.GetRelationalModel()));
        Assert.False(differ.HasDifferences(targetModel.GetRelationalModel(), snapshotModel.GetRelationalModel()));
    }

    /// <summary>Выбирает настоящую factory соответствующего отдельного проекта.</summary>
    private static IDesignTimeDbContextFactory<AgentBridgeDbContext> CreateFactory(DatabaseProvider selected) => selected switch
    {
        DatabaseProvider.SQLite => new SqliteAgentBridgeDbContextFactory(),
        DatabaseProvider.PostgreSql => new PostgreSqlAgentBridgeDbContextFactory(),
        DatabaseProvider.SqlServer => new SqlServerAgentBridgeDbContextFactory(),
        _ => throw new ArgumentOutOfRangeException(nameof(selected))
    };
}
