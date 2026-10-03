using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Ports;

/// <summary>Читает и проверяет модельные настройки без сохранения выбора или запуска агента.</summary>
public interface IModelSettingsReader
{
    /// <summary>Null overrides используют настройки приложения; непустые значения проверяются без подмены.</summary>
    Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId,
        string? model = null, string? effort = null, CancellationToken ct = default);
}
