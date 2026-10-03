using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Независимый полный канонический envelope модели, включая id, usage, discriminator и неизвестные поля.</summary>
/// <remarks>Чувствительные данные не предназначены для логирования. Тип не выполняет mapping или проверку terminal lifecycle.</remarks>
public class CanonicalModelEnvelope
{
    /// <summary>Копирует объект полностью, без связи со сроком жизни исходного JsonDocument.</summary>
    public CanonicalModelEnvelope(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Envelope должен быть объектом.", nameof(content));
        }
        Content = ContractSnapshot.Json(content);
    }

    /// <summary>Полный полученный объект протокола, не только output.</summary>
    public JsonElement Content { get; }
}
