using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.Tokenization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Configuration;

/// <summary>Явная регистрация offline счётчика и проверки бюджета в composition root приложения.</summary>
public static class AgentBridgeTokenizationExtensions
{
    /// <summary>Сохраняет явные регистрации приложения; не запускает tokenizer/HTTP/DB или compact.</summary>
    public static IServiceCollection AddAgentBridgeTokenization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IContextTokenCounter, ContextTokenCounter>();
        services.TryAddTransient<ContextBudgetGuard>();
        return services;
    }
}
