namespace AgentBridge.Application.Models;

/// <summary>Одна bounded страница durable scope feed; 0 означает начальную позицию.</summary>
/// <param name="Scope">Namespace.</param>
/// <param name="Checkpoint">Последняя принятая sequence.</param>
/// <param name="Limit">Предел 1..256.</param>
/// <param name="NowUtc">UTC.</param>
public record DialogCatalogChangeRead(DialogCatalogScope Scope, long Checkpoint, int Limit, DateTimeOffset NowUtc);
