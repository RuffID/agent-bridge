namespace AgentBridge.Application.Models;

/// <summary>Явный полный scope/profile и lease period registered run; передаёт trusted host.</summary>
/// <param name="Scope">Namespace.</param>
/// <param name="Profile">Полный профиль.</param>
/// <param name="LeasePeriod">Положительный период.</param>
public record DialogRunOptions(DialogCatalogScope Scope, CatalogAccessProfile Profile, TimeSpan LeasePeriod);
