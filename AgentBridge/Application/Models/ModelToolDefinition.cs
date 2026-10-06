using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Описание разрешённого приложением инструмента без полей HTTP-запроса.</summary>
public class ModelToolDefinition
{
    /// <summary>Фиксирует имя, описание и независимую схему параметров.</summary>
    public ModelToolDefinition(string name, string description, JsonElement parameters, bool strict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(description);
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Схема параметров должна быть объектом.", nameof(parameters));
        }
        Name = name;
        Description = description;
        Parameters = ContractSnapshot.Json(parameters);
        Strict = strict;
    }

    /// <summary>Имя зарегистрированного инструмента.</summary>
    public string Name { get; }
    /// <summary>Назначение инструмента для модели.</summary>
    public string Description { get; }
    /// <summary>Полная схема, включая дополнительные поля.</summary>
    public JsonElement Parameters { get; }
    /// <summary>Требуется ли строгая проверка схемы адаптером.</summary>
    public bool Strict { get; }
}
