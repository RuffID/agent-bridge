namespace AgentBridge.Application.Models;

/// <summary>Подтверждённый Begin с новым root token и lease.</summary>
/// <param name="Token">CAS.</param>
/// <param name="Lease">Persisted fencing identity.</param>
public record DialogRunState(DialogWriteToken Token, DialogRunLease Lease);
