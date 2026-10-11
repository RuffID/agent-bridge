namespace AgentBridge.Application.Models;

/// <summary>Append-only audit provenance и effective output, отдельно от original history.</summary>
/// <param name="RecoveryId">Recovery operation.</param>
/// <param name="Revision">Recovery revision.</param>
/// <param name="Position">Original identity.</param>
/// <param name="Output">Truthful error либо trusted external output; не saved assistant message.</param>
/// <param name="Kind">Принятый вид resolution.</param>
/// <param name="EvidenceReference">Audit reference согласия либо проверенного evidence.</param>
public record DialogRecoveryRecord(Guid RecoveryId, long Revision, DialogPendingCall Position, CanonicalModelItem Output,
    DialogRecoveryKind Kind, string? EvidenceReference);
