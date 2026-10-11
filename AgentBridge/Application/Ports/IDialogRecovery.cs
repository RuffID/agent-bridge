using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Append-only truthful recovery; никогда не вызывает gateway/handler.</summary>
public interface IDialogRecovery
{
    /// <summary>Append-only truthful recovery; никогда не вызывает gateway/handler.</summary>
    Task<ServiceResult<DialogRecoveryResult>> RecoverAsync(DialogRecoveryRequest request, CancellationToken cancellationToken = default);
    /// <summary>Перечитывает прежний RecoveryId после unknown commit; отсутствие не доказывает внешний исход.</summary>
    Task<ServiceResult<DialogRecoveryResult>> ReadAsync(DialogAccess access, Guid recoveryId, CancellationToken cancellationToken = default);
}
