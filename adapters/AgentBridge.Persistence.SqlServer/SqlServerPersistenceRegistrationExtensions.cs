using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Persistence.EfCore.Reading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Persistence.SqlServer;

/// <summary>Явное подключение SqlServer без регистрации других провайдеров, подключения к БД или запуска обслуживания.</summary>
public static class SqlServerPersistenceRegistrationExtensions
{
    /// <summary>Явно включает bounded materialization во всех библиотечных read/write queries одной frozen generation.</summary>
    /// <remarks>Вызывать вместе с AddAgentBridgeSqlServer и AddAgentBridgePersistence/AddAgentBridge.
    /// Пределы обязательны; повторная регистрация не заменяет policy скрыто. IBoundedDialogReader и RunBoundedAsync
    /// позволяют consumer потребовать capability до чтения. Не подключает БД, startup или maintenance.</remarks>
    public static IServiceCollection AddAgentBridgeSqlServerBoundedHistory(this IServiceCollection services, DialogHistoryLoadLimits limits)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(limits);
        if (services.Any(item => item.ServiceType == typeof(DialogHistoryLoadLimits)))
        {
            throw new InvalidOperationException("bounded_history_policy_already_registered");
        }

        if (services.Any(item => item.ServiceType == typeof(IHistoryRecordSizeProvider)))
        {
            throw new InvalidOperationException("bounded_history_size_provider_already_registered");
        }

        services.AddSingleton(limits);
        services.AddScoped<HistoryLoadBudget>();
        services.AddScoped<HistoryQueryGuard>();
        services.AddSingleton<IHistoryRecordSizeProvider, SqlServerHistoryRecordSizeProvider>();
        services.AddScoped<IBoundedDialogReader>(provider =>
        {
            IBoundedDialogReader reader = provider.GetRequiredService<IDialogReader>() as IBoundedDialogReader ??
                throw new InvalidOperationException("bounded_history_reader_required");
            _ = reader.LoadLimits;
            return reader;
        });

        return services;
    }

    /// <summary>Регистрирует модуль SqlServer; Database.Provider должен выбрать его перед созданием контекста.</summary>
    public static IServiceCollection AddAgentBridgeSqlServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAgentBridgeDatabaseProvider<SqlServerDatabaseProvider>();

        return services;
    }
}
