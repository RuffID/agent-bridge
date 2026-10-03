using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Ports;

/// <summary>Выбирает неизменяемый доступ на вызов без проверки ключа сетью и без fallback после ошибки.</summary>
public interface IModelAccessResolver
{
    /// <summary>Использует общий ключ только при отсутствии индивидуального; отсутствие обоих даёт Unauthorized.</summary>
    Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default);
}
