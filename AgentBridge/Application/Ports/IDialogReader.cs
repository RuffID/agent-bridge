using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Минимальное защищённое чтение без Unit of Work и инфраструктурных query-типов.</summary>
public interface IDialogReader
{
    /// <summary>Registered history требует полный immutable profile/scope перед чтением children.</summary>
    Task<ServiceResult<DialogSnapshot>> ReadCatalogAsync(DialogCatalogAccess access, CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Unsupported, "catalog_history_gate_unsupported")));
    /// <summary>Проверяет существование и владельца; возвращает снимок либо NotFound/Forbidden/Conflict при изменении во время чтения.</summary>
    /// <remarks>До физического удаления допускает чтение метаданных истёкшего диалога для отображения срока.
    /// Получение снимка не разрешает продолжение: изменяющие порты отдельно проверяют срок при каждой записи.</remarks>
    Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default);
}
