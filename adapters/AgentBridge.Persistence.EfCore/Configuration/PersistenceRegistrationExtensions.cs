using AgentBridge.Application.Ports;
using AgentBridge.Persistence.EfCore.Reading;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using EFCoreLibrary.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Явное подключение общего EF-контекста, базовых адаптеров, read ports и сценарных write UoW; настройку и scope выбирает приложение.</summary>
public static class PersistenceRegistrationExtensions
{
    /// <summary>Использует валидированные DatabaseOptions; не запускает подключение, startup, migrations или сценарии хранения.</summary>
    public static IServiceCollection AddAgentBridgePersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDbContext<AgentBridgeDbContext>((provider, builder) =>
        {
            DatabaseOptions options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("Database.ConnectionString обязателен.");
            }
            switch (options.Provider)
            {
                case DatabaseProvider.SQLite:
                    builder.UseSqlite(options.ConnectionString, sqlite => sqlite
                        .MigrationsAssembly(AgentBridgeMigrationsAssemblies.SQLITE)
                        .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));
                    break;
                case DatabaseProvider.PostgreSql:
                    builder.UseNpgsql(options.ConnectionString, postgres => postgres
                        .MigrationsAssembly(AgentBridgeMigrationsAssemblies.POSTGRESQL)
                        .MigrationsHistoryTable(AgentBridgeMigrationsHistory.TABLE_NAME));
                    break;
                default:
                    throw new InvalidOperationException("Требуется явный провайдер SQLite/PostgreSQL.");
            }
            builder.EnableSensitiveDataLogging(false);
        });
        services.AddEfCoreContext<AgentBridgeDbContext, AgentBridgeContextKey>();
        services.AddEfCoreBaseRepositories<AgentBridgeContextKey>();
        services.AddScoped(typeof(RecordStaging<>));
        services.AddScoped<DialogRecordQueries>();
        services.AddScoped<TurnRecordQueries>();
        services.AddScoped<ItemRecordQueries>();
        services.AddScoped<ModelStepRecordQueries>();
        services.AddScoped<ContextRecordQueries>();
        services.AddScoped<PersistenceOperationGate>();
        services.AddScoped<IUnitOfWorkSession, EfUnitOfWorkSession>();
        services.AddScoped<UnitOfWorkScope>();
        services.AddScoped<DialogWriteGuard>();
        services.AddScoped<DialogStateLoader>();
        services.AddScoped<TurnContentStaging>();
        services.AddScoped<IDialogCreator, DialogCreationUnitOfWork>();
        services.AddScoped<IDialogTurnWriter, DialogTurnUnitOfWork>();
        services.AddScoped<IDialogToolAttemptWriter, DialogToolAttemptUnitOfWork>();
        services.AddScoped<IDialogContextWriter, DialogContextUnitOfWork>();
        services.AddScoped<DialogDeletionUnitOfWork>();
        services.AddScoped<IDialogDeletion>(provider => provider.GetRequiredService<DialogDeletionUnitOfWork>());
        services.AddScoped<IExpiredDialogDeletion>(provider => provider.GetRequiredService<DialogDeletionUnitOfWork>());
        services.AddScoped<IDialogReader, DialogReader>();
        services.AddScoped<IExpiredDialogReader, ExpiredDialogReader>();
        return services;
    }
}
