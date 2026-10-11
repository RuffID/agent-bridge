using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Atomic terminal/finalization после awaited внешних workers; unresolved оставляет RecoveryRequired.</summary>
/// <param name="Access">Access.</param>
/// <param name="Expected">CAS.</param>
/// <param name="Lease">Lease.</param>
/// <param name="Status">Terminal status.</param>
public record DialogRunFinalize(DialogAccess Access, DialogWriteToken Expected, DialogRunLease Lease, DialogTurnStatus Status);
