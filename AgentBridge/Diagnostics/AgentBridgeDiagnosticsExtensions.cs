using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Diagnostics;

/// <summary>Регистрация диагностики в контейнере подключающего приложения.</summary>
public static class AgentBridgeDiagnosticsExtensions
{
    /// <summary>Подключает ILogger и диагностику, сохраняя провайдеры, уровни и фабрику приложения.</summary>
    /// <remarks>Не создаёт Serilog logger, sinks, файлы или host. Без провайдера события не выводятся.</remarks>
    public static IServiceCollection AddAgentBridgeDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        services.TryAddSingleton<AgentBridgeDiagnostics>();
        return services;
    }
}
