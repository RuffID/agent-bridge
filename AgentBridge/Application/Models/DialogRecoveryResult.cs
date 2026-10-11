namespace AgentBridge.Application.Models;

/// <summary>Подтверждённый commit recovery; Ready читается отдельно от факта принятия batch.</summary>
public class DialogRecoveryResult
{
    /// <summary>Копирует accepted append-only records.</summary>
    public DialogRecoveryResult(DialogContinuationState continuation, IEnumerable<DialogRecoveryRecord> records)
    {
        Continuation = continuation;
        Records = ContractSnapshot.Copy(records);
    }

    /// <summary>Authoritative состояние после этой операции.</summary>
    public DialogContinuationState Continuation { get; }
    /// <summary>Accepted provenance.</summary>
    public IReadOnlyList<DialogRecoveryRecord> Records { get; }
}
