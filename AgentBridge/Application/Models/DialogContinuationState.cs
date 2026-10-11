using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Bounded durable readiness; не выводится из context pairs или caller cancellation.</summary>
public class DialogContinuationState
{
    /// <summary>Копирует pending positions.</summary>
    public DialogContinuationState(DialogWriteToken token, DialogReadiness readiness, Guid? turnId,
        DialogTurnStatus? terminal, bool finalized, long fencingEpoch, long recoveryRevision,
        DialogRunLease? lease, IEnumerable<DialogPendingCall> pending)
    {
        Token = token;
        Readiness = readiness;
        TurnId = turnId;
        Terminal = terminal;
        Finalized = finalized;
        FencingEpoch = fencingEpoch;
        RecoveryRevision = recoveryRevision;
        Lease = lease;
        Pending = ContractSnapshot.Copy(pending);
    }

    /// <summary>CAS.</summary>
    public DialogWriteToken Token { get; }
    /// <summary>Durable readiness.</summary>
    public DialogReadiness Readiness { get; }
    /// <summary>Прежний turn.</summary>
    public Guid? TurnId { get; }
    /// <summary>Исходный terminal; fence InProgress его не переписывает.</summary>
    public DialogTurnStatus? Terminal { get; }
    /// <summary>Durable finalization либо fence.</summary>
    public bool Finalized { get; }
    /// <summary>Epoch.</summary>
    public long FencingEpoch { get; }
    /// <summary>Recovery revision.</summary>
    public long RecoveryRevision { get; }
    /// <summary>Active lease либо null после finalization/fence.</summary>
    public DialogRunLease? Lease { get; }
    /// <summary>Все bounded unresolved positions.</summary>
    public IReadOnlyList<DialogPendingCall> Pending { get; }
}
