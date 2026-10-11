using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Durable run fencing и readiness; внешний I/O всегда вне transaction.</summary>
public interface IDialogRunLifecycle
{
    /// <summary>Durable run fencing и readiness; внешний I/O всегда вне transaction.</summary>
    Task<ServiceResult<DialogRunState>> BeginAsync(DialogRunBegin request, CancellationToken cancellationToken = default);
    /// <summary>Продлевает ту же live lease; retention и history revision не меняются.</summary>
    Task<ServiceResult<DialogRunLease>> RenewAsync(DialogAccess access, DialogRunLease expected, TimeSpan leasePeriod, CancellationToken cancellationToken = default);
    /// <summary>Atomic terminal и release после awaited workers.</summary>
    Task<ServiceResult<DialogContinuationState>> FinalizeAsync(DialogRunFinalize request, CancellationToken cancellationToken = default);
    /// <summary>Применяет fence по expired lease либо trusted quiescence; Unknown не превращается в NotStarted.</summary>
    Task<ServiceResult<DialogContinuationState>> FenceAsync(DialogRunFence request, CancellationToken cancellationToken = default);
}
