using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentBridge.Configuration;

/// <summary>Программно регистрирует описания и отдельные scoped бизнес-обработчики без запуска операций.</summary>
public static class AgentBridgeToolsExtensions
{
    /// <summary>Подключает registry/executor; приложение явно создаёт сессию с limits и выбранными именами.</summary>
    public static IServiceCollection AddAgentBridgeTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IToolRegistry, ToolRegistry>();
        services.TryAddSingleton<IToolExecutor, ToolExecutor>();
        return services;
    }

    /// <summary>Регистрирует обязательный validator полной схемы/прав и scoped handler с точным описанием.</summary>
    public static IServiceCollection AddAgentBridgeTool<THandler, TValidator>(this IServiceCollection services,
        ModelToolDefinition definition) where THandler : class, IToolHandler where TValidator : class, IToolInvocationValidator
    {
        ArgumentNullException.ThrowIfNull(services);
        ToolRegistration registration = new(definition, typeof(THandler), typeof(TValidator));
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ToolRegistration)
            && descriptor.ImplementationInstance is ToolRegistration existing
            && StringComparer.Ordinal.Equals(existing.Definition.Name, definition.Name)))
            throw new ArgumentException("Имя инструмента уже зарегистрировано.", nameof(definition));
        // AddScoped, а не singleton reuse: новый invocation scope не делит handler/DbContext с соседними tasks.
        services.AddScoped<THandler>();
        services.AddScoped<TValidator>();
        services.AddSingleton(registration);
        return services.AddAgentBridgeTools();
    }
}
