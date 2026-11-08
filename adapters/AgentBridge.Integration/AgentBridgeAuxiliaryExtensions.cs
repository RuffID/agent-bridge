using AgentBridge.Sub2Api.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Integration;

/// <summary>Явное подключение auxiliary выбранного sub2api без дополнительных настроек.</summary>
public static class AgentBridgeAuxiliaryExtensions
{
    /// <summary>Подключает usage sub2api через существующий GenerationTimeout и HTTP factory основного bundle.</summary>
    /// <remarks>Сначала выберите AddAgentBridgeSub2ApiConnector и подключите AddAgentBridge.
    /// Файлы и изображения возвращают Unsupported. Регистрация не создаёт HTTP-клиент и сохраняет app override.</remarks>
    public static IServiceCollection AddAgentBridgeSub2ApiAuxiliary(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (AgentBridgeConnectorSelection.Find(services)?.Kind != AgentBridgeConnectorKind.Sub2Api)
        {
            throw new InvalidOperationException("Auxiliary sub2api требует выбранный AddAgentBridgeSub2ApiConnector.");
        }

        AgentBridgeConnectorExtensions.EnsureCompatible(services, AgentBridgeConnectorKind.Sub2Api);
        Sub2ApiComposition.AddAuxiliary(services);

        return services;
    }
}
