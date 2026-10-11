using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Авторизованный host resolver доказанного исхода; не вызывает прежний handler для проверки.</summary>
/// <remarks>Вызывается вне storage transaction. Host проверяет полномочия и полную identity из Request;
/// browser reference или read-only признак не являются evidence. После await библиотека повторно проверяет CAS.</remarks>
public interface IDialogRecoveryEvidenceResolver
{
    /// <summary>Подтверждает отсутствие результата либо возвращает неизменный внешний protocol output.</summary>
    Task<ServiceResult<DialogRecoveryEvidence>> ResolveAsync(DialogRecoveryEvidenceRequest request,
        CancellationToken cancellationToken = default);
}
