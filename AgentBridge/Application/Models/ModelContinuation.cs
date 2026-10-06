using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Непрозрачный независимый снимок продолжения, связанный с диалогом и выбранным upstream-владением.</summary>
/// <remarks>Интерпретация принадлежит адаптеру. Не является глобальной настройкой и не заменяет локальную историю.</remarks>
public class ModelContinuation
{
    /// <summary>Сохраняет полные метаданные продолжения без удаления неизвестных полей.</summary>
    public ModelContinuation(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Метаданные продолжения должны быть объектом.", nameof(content));
        }
        Content = ContractSnapshot.Json(content);
    }

    /// <summary>Чувствительные метаданные; не предназначены для UI-снимка настроек и логов.</summary>
    public JsonElement Content { get; }
}
