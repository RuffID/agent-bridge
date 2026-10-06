using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Полный канонический элемент модели, включая неизвестные поля и непрозрачное состояние.</summary>
/// <remarks>Не является wire DTO запроса. Поля не извлекаются и не нормализуются на этом этапе.</remarks>
public class CanonicalModelItem
{
    /// <summary>Фиксирует независимый неизменяемый снимок канонического объекта.</summary>
    public CanonicalModelItem(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Канонический элемент должен быть объектом.", nameof(content));
        }
        Content = ContractSnapshot.Json(content);
    }

    /// <summary>Полное содержимое без сокращения до видимого текста.</summary>
    public JsonElement Content { get; }
}
