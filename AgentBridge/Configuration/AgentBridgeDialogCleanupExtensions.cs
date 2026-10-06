using AgentBridge.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Configuration;

/// <summary>Явно регистрирует очистку диалогов без открытия БД и автозапуска.</summary>
public static class AgentBridgeDialogCleanupExtensions
{
    /// <summary>Приложение предоставляет scoped read/deletion ports, авторизацию вызова и расписание.</summary>
    public static IServiceCollection AddAgentBridgeDialogCleanup(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ExpiredDialogCleanup>();
        return services;
    }
}
