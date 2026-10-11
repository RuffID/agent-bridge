using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Компактная durable readiness и все bounded pending positions.</summary>
public interface IDialogContinuationReader
{
    /// <summary>Компактная durable readiness и все bounded pending positions.</summary>
    Task<ServiceResult<DialogContinuationState>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default);
}
