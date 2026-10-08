using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Подключает независимые модули БД без зависимости общего адаптера от конкретного провайдера.</summary>
public static class DatabaseProviderRegistrationExtensions
{
    /// <summary>Идемпотентно регистрирует модуль; конфигурация выбирает ровно один из подключённых провайдеров.</summary>
    public static IServiceCollection AddAgentBridgeDatabaseProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IAgentBridgeDatabaseProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentBridgeDatabaseProvider, TProvider>());

        return services;
    }
}
