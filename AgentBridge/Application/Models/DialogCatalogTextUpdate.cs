namespace AgentBridge.Application.Models;

/// <summary>Projector изменяет только текст; timestamps/profile/token задаёт библиотека.</summary>
/// <param name="Text">Новая компактная проекция.</param>
public record DialogCatalogTextUpdate(DialogCatalogTextState Text);
