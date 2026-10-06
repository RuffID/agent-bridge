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

    /// <summary>Проверяет каталог тем же fixed доступом, которым run вызовет generation/compact; не resolves ключ повторно.</summary>
    /// <remarks>Старый пользовательский reader без этого контракта получает fail-fast Unsupported, без скрытого fallback.</remarks>
    Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access,
        string? model = null, string? effort = null, CancellationToken ct = default) =>
        Task.FromResult(ServiceResult<ModelSettingsSnapshot>.Fail(new(ServiceErrorType.Unsupported,
            "Reader не поддерживает проверку настроек с фиксированным доступом run.")));
}
