using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Неизменяемый namespace каталога; права на site/agent подтверждает приложение.</summary>
/// <param name="OwnerId">Ordinal владелец.</param>
/// <param name="SiteId">Явный сайт.</param>
/// <param name="AgentId">Явный агент.</param>
public record DialogCatalogScope(DialogOwnerId OwnerId, string SiteId, string AgentId);
