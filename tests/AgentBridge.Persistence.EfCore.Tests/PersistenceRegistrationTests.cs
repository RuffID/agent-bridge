using AgentBridge.Application.Ports;
using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
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
        Assert.Null(scope.ServiceProvider.GetService<IDialogCreator>());
        Assert.Null(scope.ServiceProvider.GetService<IDialogTurnWriter>());
        Assert.Null(scope.ServiceProvider.GetService<IDialogContextWriter>());
        Assert.Null(scope.ServiceProvider.GetService<IDialogDeletion>());
        Assert.Null(scope.ServiceProvider.GetService<IExpiredDialogDeletion>());
        using IServiceScope otherScope = provider.CreateScope();
        Assert.NotSame(context, otherScope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>());
        Assert.NotSame(reader, otherScope.ServiceProvider.GetRequiredService<IDialogReader>());
    }

    /// <summary>Отсутствующий провайдер не превращается в SQLite даже при разрешении контекста без startup.</summary>
    [Fact]
    public void ResolvingContextWithoutProviderFailsBeforeDatabase()
    {
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(options => options.ConnectionString = "synthetic-connection-secret");
        services.AddAgentBridgePersistence();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => scope.ServiceProvider.GetRequiredService<AgentBridgeDbContext>());
        Assert.Contains("Database.Provider обязателен", error.Message);
        Assert.DoesNotContain("synthetic-connection-secret", error.ToString());
    }
}
