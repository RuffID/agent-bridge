using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Integration;

/// <summary>Фиксирует выбор, включая неявный default facade, без разрешения сервисов.</summary>
internal record AgentBridgeConnectorSelection(AgentBridgeConnectorKind Kind)
{
    /// <summary>Читает только собственный instance marker из descriptors.</summary>
    internal static AgentBridgeConnectorSelection? Find(IServiceCollection services)
    {
        return services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(AgentBridgeConnectorSelection))
            ?.ImplementationInstance as AgentBridgeConnectorSelection;
    }
}
