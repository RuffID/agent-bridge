namespace AgentBridge.Application.Models;

/// <summary>Явная версия deterministic projector и бюджеты до материализации разрешённого текста.</summary>
/// <param name="Key">Ключ projector.</param>
/// <param name="Version">Версия.</param>
/// <param name="MaxTitleScalars">Предел Unicode scalars заголовка.</param>
/// <param name="MaxSnippetScalars">Предел Unicode scalars snippet.</param>
/// <param name="MaxMessageBytes">Предел UTF-8 canonical сообщения.</param>
public record DialogProjectionPolicy(string Key, int Version, int MaxTitleScalars, int MaxSnippetScalars, int MaxMessageBytes);
