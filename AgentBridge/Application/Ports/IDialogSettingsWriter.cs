using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Короткая атомарная запись выбора с independent CAS версии настроек и защищённым owner/incarnation/expiry.</summary>
public interface IDialogSettingsWriter
{
    /// <summary>Проверяет исходную версию истории и выбора; не изменяет revision истории или выполняющийся snapshot.</summary>
    /// <remarks>Каталог/совместимость проверяются до transaction. expectedVersion=0 означает отсутствие выбора.
    /// Нет refresh/retry; stale root или settings дают Conflict. Срок не продлевается.</remarks>
    Task<ServiceResult<DialogModelSelection>> SaveAsync(DialogAccess access, DialogWriteToken expected,
        long expectedVersion, ModelSettingsSnapshot settings, CancellationToken cancellationToken = default);
}
