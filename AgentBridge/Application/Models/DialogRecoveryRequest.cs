namespace AgentBridge.Application.Models;

/// <summary>Atomic bounded recovery batch с independent revision/epoch CAS и durable idempotency.</summary>
public class DialogRecoveryRequest
{
    /// <summary>Копирует полный список resolutions.</summary>
    public DialogRecoveryRequest(DialogAccess access, DialogWriteToken expected, Guid recoveryId,
        long expectedRecoveryRevision, long expectedEpoch, Guid turnId, IEnumerable<DialogRecoveryResolution> resolutions)
    {
        Access = access;
        Expected = expected;
        RecoveryId = recoveryId;
        ExpectedRecoveryRevision = expectedRecoveryRevision;
        ExpectedEpoch = expectedEpoch;
        TurnId = turnId;
        Resolutions = ContractSnapshot.Copy(resolutions);
    }

    /// <summary>Access.</summary>
    public DialogAccess Access { get; }
    /// <summary>CAS.</summary>
    public DialogWriteToken Expected { get; }
    /// <summary>Durable idempotency key.</summary>
    public Guid RecoveryId { get; }
    /// <summary>Recovery CAS.</summary>
    public long ExpectedRecoveryRevision { get; }
    /// <summary>Epoch CAS.</summary>
    public long ExpectedEpoch { get; }
    /// <summary>Прежний turn.</summary>
    public Guid TurnId { get; }
    /// <summary>Полный bounded batch.</summary>
    public IReadOnlyList<DialogRecoveryResolution> Resolutions { get; }
}
