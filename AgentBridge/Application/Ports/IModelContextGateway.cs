using AgentBridge.Application.Models;

namespace AgentBridge.Application.Ports;

/// <summary>Локальная возможность шлюза связывать сохранённый контекст с коннектором без HTTP.</summary>
public interface IModelContextGateway
{
    /// <summary>Фиксирует происхождение текущего вызова без upstream запросов и без сохранения ключа.</summary>
    ModelConnectorProvenance GetContextProvenance(ApplicationCallContext call, ModelAccess access);
}
