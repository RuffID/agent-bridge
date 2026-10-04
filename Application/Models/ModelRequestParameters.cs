using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Независимый канонический снимок дополнительных контролей модели; поддержку проверяет адаптер.</summary>
/// <remarks>Не содержит HTTP-типов и не разрешает переопределять обязательные поля ModelRequest.</remarks>
public class ModelRequestParameters
{
    /// <summary>Копирует полный объект контролей, включая вложенные неизвестные поля.</summary>
    public ModelRequestParameters(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Параметры модели должны быть объектом.", nameof(content));
        }
        Content = ContractSnapshot.Json(content);
    }

    /// <summary>Канонические контроли без связи со сроком жизни исходного документа; не предназначены для логов.</summary>
    public JsonElement Content { get; }
}
