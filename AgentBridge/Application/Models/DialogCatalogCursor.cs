namespace AgentBridge.Application.Models;

/// <summary>Последний examined key; подпись транспортного cursor принадлежит host.</summary>
/// <param name="Scope">Привязка namespace.</param>
/// <param name="SortTimeUtc">SortTime descending.</param>
/// <param name="DialogId">UUID big-endian descending.</param>
public record DialogCatalogCursor(DialogCatalogScope Scope, DateTimeOffset SortTimeUtc, Guid DialogId);
