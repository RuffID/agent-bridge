using AgentBridge.Application.Ports;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using EFCoreLibrary.EfCore;
using EFCoreLibrary.EfCore.Repository.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверки явной регистрации context-key и всех base repositories без приложения и подключения к БД.</summary>
public class PersistenceRegistrationTests
{
    /// <summary>Каждый scope получает один общий контекст через точный актуальный adapter API EFCoreLibrary.</summary>
    [Theory]
    [InlineData(DatabaseProvider.SQLite, "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData(DatabaseProvider.PostgreSql, "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void RepositoriesUseSelectedProviderAndSharedScopedContext(DatabaseProvider selected, string providerName)
    {
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = selected;
            options.ConnectionString = selected == DatabaseProvider.SQLite
                ? "Data Source=metadata-only-never-open.db"
                : "Host=invalid.example;Database=synthetic;Username=synthetic;Password=synthetic";
        });
        services.AddAgentBridgePersistence();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using IServiceScope scope = provider.CreateScope();
        AgentBridgeDbContext context = scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>();
        IAppDbContext<AgentBridgeContextKey> adapter = scope.ServiceProvider.GetRequiredService<IAppDbContext<AgentBridgeContextKey>>();
        Assert.IsType<EfDbContextAdapter<AgentBridgeDbContext, AgentBridgeContextKey>>(adapter);
        Assert.Same(context.Database, adapter.Database);
        Assert.Same(adapter, scope.ServiceProvider.GetRequiredService<IAppDbContext<AgentBridgeContextKey>>());
        Assert.Same(context.Database, scope.ServiceProvider.GetRequiredService<IUnitOfWorkContext<AgentBridgeContextKey>>().Database);
        Assert.Equal(providerName, context.Database.ProviderName);
        Assert.False(context.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single().IsSensitiveDataLoggingEnabled);
        Assert.IsType<CreateItemRepository<DialogRecord, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IContextCreateItemRepository<DialogRecord, AgentBridgeContextKey>>());
        Assert.IsType<UpdateItemRepository<DialogRecord, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IContextUpdateItemRepository<DialogRecord, AgentBridgeContextKey>>());
        Assert.IsType<DeleteItemRepository<DialogRecord, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IContextDeleteItemRepository<DialogRecord, AgentBridgeContextKey>>());
        Assert.IsType<GetItemByIdRepository<DialogRecord, Guid, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IContextGetItemByIdRepository<DialogRecord, Guid, AgentBridgeContextKey>>());
        Assert.IsType<GetItemByPredicateRepository<DialogTurnRecord, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IContextGetItemByPredicateRepository<DialogTurnRecord, AgentBridgeContextKey>>());
        Assert.IsType<QueryRepository<DialogRecord, AgentBridgeContextKey>>(scope.ServiceProvider.GetRequiredService<IContextQueryRepository<DialogRecord, AgentBridgeContextKey>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRepositoryContext<AgentBridgeContextKey>>());
        IDialogReader reader = scope.ServiceProvider.GetRequiredService<IDialogReader>();
        Assert.IsType<DialogReader>(reader);
        Assert.Same(reader, scope.ServiceProvider.GetRequiredService<IDialogReader>());
        Assert.IsType<ExpiredDialogReader>(scope.ServiceProvider.GetRequiredService<IExpiredDialogReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TurnRecordQueries>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ItemRecordQueries>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ModelStepRecordQueries>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContextRecordQueries>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RecordStaging<DialogRecord>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RecordStaging<DialogTurnRecord>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RecordStaging<CanonicalItemRecord>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RecordStaging<ModelStepRecord>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RecordStaging<DialogContextRecord>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDialogCreator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDialogTurnWriter>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDialogContextWriter>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IDialogDeletion>(), scope.ServiceProvider.GetRequiredService<IExpiredDialogDeletion>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<UnitOfWorkScope>(), scope.ServiceProvider.GetRequiredService<UnitOfWorkScope>());
        Assert.IsType<EfUnitOfWorkSession>(scope.ServiceProvider.GetRequiredService<IUnitOfWorkSession>());
        using IServiceScope otherScope = provider.CreateScope();
        Assert.NotSame(context, otherScope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>());
        Assert.NotSame(reader, otherScope.ServiceProvider.GetRequiredService<IDialogReader>());
        Assert.NotSame(scope.ServiceProvider.GetRequiredService<PersistenceOperationGate>(), otherScope.ServiceProvider.GetRequiredService<PersistenceOperationGate>());
    }

    /// <summary>Настоящий base Update сохраняет исходные root guards и не помечает immutable поля изменёнными.</summary>
    [Fact]
    public void BaseUpdatePreservesOriginalConcurrencyAndFixedFields()
    {
        using AgentBridgeDbContext context = new(new DbContextOptionsBuilder<AgentBridgeDbContext>()
            .UseSqlite("Data Source=metadata-only-never-open.db").Options);
        DialogRecord root = new()
        {
            Id = Guid.NewGuid(), IncarnationId = Guid.NewGuid(), OwnerId = " owner ", Revision = 7,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, ExpiresAtUtc = DateTimeOffset.UnixEpoch.AddDays(1),
            LastChangedAtUtc = DateTimeOffset.UnixEpoch
        };
        context.Attach(root);
        EfDbContextAdapter<AgentBridgeDbContext, AgentBridgeContextKey> adapter = new(context);
        UpdateItemRepository<DialogRecord, AgentBridgeContextKey> repository = new(adapter);
        RecordStaging<DialogRecord> staging = new(new CreateItemRepository<DialogRecord, AgentBridgeContextKey>(adapter),
            repository, new DeleteItemRepository<DialogRecord, AgentBridgeContextKey>(adapter));
        root.Revision = 8;
        root.ContentBytes = 42;
        staging.StageUpdate(root);
        Assert.Equal(7, context.Entry(root).Property(row => row.Revision).OriginalValue);
        Assert.Equal(8, context.Entry(root).Property(row => row.Revision).CurrentValue);
        Assert.Equal(root.IncarnationId, context.Entry(root).Property(row => row.IncarnationId).OriginalValue);
        Assert.Equal(" owner ", context.Entry(root).Property(row => row.OwnerId).OriginalValue);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(1), context.Entry(root).Property(row => row.ExpiresAtUtc).OriginalValue);
        Assert.True(context.Entry(root).Property(row => row.Revision).IsModified);
        Assert.False(context.Entry(root).Property(row => row.OwnerId).IsModified);
        Assert.False(context.Entry(root).Property(row => row.ExpiresAtUtc).IsModified);
        Assert.False(context.Entry(root).Property(row => row.IncarnationId).IsModified);
        Assert.False(context.Entry(root).Property(row => row.CreatedAtUtc).IsModified);
    }

    /// <summary>Отсутствующий провайдер не превращается в SQLite даже при разрешении контекста без startup.</summary>
    [Fact]
    public void ResolvingContextWithoutProviderFailsBeforeDatabase()
    {
        ServiceCollection services = new();
        services.AddTestDatabaseProviders();
        services.AddDatabaseConfiguration(options => options.ConnectionString = "synthetic-connection-secret");
        services.AddAgentBridgePersistence();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>());
        Assert.Contains("Database.Provider обязателен", error.Message);
        Assert.DoesNotContain("synthetic-connection-secret", error.ToString());
    }
}
