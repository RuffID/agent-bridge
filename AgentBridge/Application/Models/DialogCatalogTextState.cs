namespace AgentBridge.Application.Models;

/// <summary>Разрешённый компактный текст, привязанный библиотекой к saved positions.</summary>
/// <param name="Title">Первый saved вопрос либо Новый чат.</param>
/// <param name="SearchKey">Нормализованный ordinal title.</param>
/// <param name="Snippet">Последний разрешённый saved user/assistant текст.</param>
/// <param name="HasSavedQuestion">Первый вопрос уже зафиксирован.</param>
public record DialogCatalogTextState(string Title, string SearchKey, string Snippet, bool HasSavedQuestion);
