using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Читает текущие объявленные возможности моделей для уже выбранного доступа.</summary>
public interface IModelCatalog
{
    /// <summary>Возвращает независимый снимок без секретов; пустой каталог допустим, metadata может отсутствовать.</summary>
    Task<ServiceResult<ModelCatalogSnapshot>> ReadAsync(ModelAccess access, CancellationToken ct = default);
}
