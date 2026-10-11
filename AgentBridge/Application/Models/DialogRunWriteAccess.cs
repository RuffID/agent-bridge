namespace AgentBridge.Application.Models;

/// <summary>Epoch-aware access короткой записи; token дополнительно проверяется транзакционно.</summary>
/// <param name="Access">Owner/dialog/UTC.</param>
/// <param name="Lease">Persisted lease.</param>
public record DialogRunWriteAccess(DialogAccess Access, DialogRunLease Lease);
