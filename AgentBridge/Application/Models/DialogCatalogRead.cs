namespace AgentBridge.Application.Models;

/// <summary>Ровно один bounded metadata slice; host фильтрует title/profile и продвигает examined cursor.</summary>
/// <param name="Scope">Namespace.</param>
/// <param name="After">Последний examined key.</param>
/// <param name="Limit">Явный предел кандидатов 1..256.</param>
/// <param name="NowUtc">UTC проверки expiry.</param>
public record DialogCatalogRead(DialogCatalogScope Scope, DialogCatalogCursor? After, int Limit, DateTimeOffset NowUtc);
