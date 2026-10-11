namespace AgentBridge.Application.Models;

/// <summary>Persisted fencing identity; истечение deadline не доказывает исход handler.</summary>
/// <param name="IncarnationId">Жизнь.</param>
/// <param name="TurnId">Обращение.</param>
/// <param name="Epoch">Epoch.</param>
/// <param name="LeaseId">Идентичность lease.</param>
/// <param name="DeadlineUtc">Server deadline.</param>
public record DialogRunLease(Guid IncarnationId, Guid TurnId, long Epoch, Guid LeaseId, DateTimeOffset DeadlineUtc);
