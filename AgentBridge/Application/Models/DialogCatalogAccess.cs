namespace AgentBridge.Application.Models;

/// <summary>Полный immutable scope/profile для registered history и model gate.</summary>
/// <param name="Access">Owner/dialog/server UTC.</param>
/// <param name="Scope">Namespace.</param>
/// <param name="Profile">Полный trusted host профиль.</param>
public record DialogCatalogAccess(DialogAccess Access, DialogCatalogScope Scope, CatalogAccessProfile Profile);
