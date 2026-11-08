using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Integration;

/// <summary>Выбирает один встроенный набор transport-портов до подключения facade.</summary>
public static class AgentBridgeConnectorExtensions
{
    /// <summary>Выбирает sub2api с собственным разделом Sub2Api; операции транспорта реализуются отдельно.</summary>
    public static IServiceCollection AddAgentBridgeSub2ApiConnector(this IServiceCollection services)
    {
        return Select(services, AgentBridgeConnectorKind.Sub2Api);
    }

    /// <summary>Явно выбирает прежний codex-lb; без выбора facade использует этот же набор.</summary>
    public static IServiceCollection AddAgentBridgeCodexLbConnector(this IServiceCollection services)
    {
        return Select(services, AgentBridgeConnectorKind.CodexLb);
    }

    /// <summary>Проверяет выбор без создания сервисов, вызова фабрик или изменения несовместимой коллекции.</summary>
    private static IServiceCollection Select(IServiceCollection services, AgentBridgeConnectorKind kind)
    {
        ArgumentNullException.ThrowIfNull(services);
        AgentBridgeConnectorSelection? existing = AgentBridgeConnectorSelection.Find(services);
        if (existing is not null && existing.Kind != kind)
        {
            throw new InvalidOperationException("Коннектор уже выбран; используйте отдельную ServiceCollection для другого подключения.");
        }

        EnsureCompatible(services, kind);
        if (existing is null)
        {
            services.AddSingleton(new AgentBridgeConnectorSelection(kind));
        }

        return services;
    }

    /// <summary>Сохраняет произвольные app overrides, но запрещает смешивать известные встроенные порты.</summary>
    internal static void EnsureCompatible(IServiceCollection services, AgentBridgeConnectorKind kind)
    {
        if (kind != AgentBridgeConnectorKind.Sub2Api)
        {
            return;
        }

        Type[] ports = [typeof(IModelGateway), typeof(IModelCatalog), typeof(IModelAccessResolver), typeof(IModelSettingsReader),
            typeof(IModelAuxiliaryGateway)];
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.IsKeyedService || !ports.Contains(descriptor.ServiceType))
            {
                continue;
            }

            Type? implementation = descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
            if (implementation?.Assembly == typeof(CodexLbModelCatalog).Assembly)
            {
                throw new InvalidOperationException("Sub2Api несовместим с прямой регистрацией встроенных CodexLb портов.");
            }
        }
    }
}
