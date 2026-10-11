namespace AgentBridge.Application.Models;

/// <summary>CAS fence истёкшего lease либо исполнителя с trusted evidence; исход tools остаётся неизвестным.</summary>
/// <param name="Access">Access/server UTC.</param>
/// <param name="Expected">CAS.</param>
/// <param name="Lease">Прежний lease.</param>
/// <param name="OperationId">Idempotency key.</param>
/// <param name="Reason">Причина fence.</param>
/// <param name="EvidenceReference">Trusted host evidence для ConfirmedQuiescence.</param>
public record DialogRunFence(DialogAccess Access, DialogWriteToken Expected, DialogRunLease Lease, Guid OperationId,
    DialogRunFenceReason Reason = DialogRunFenceReason.ExpiredLease, string? EvidenceReference = null);
