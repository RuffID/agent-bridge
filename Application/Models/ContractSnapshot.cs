using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Общие независимые снимки данных контрактов без инфраструктурного преобразования.</summary>
internal static class ContractSnapshot
{
    /// <summary>Копирует коллекцию и запрещает null-элементы.</summary>
    internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        T[] copy = source.ToArray();
        if (copy.Any(item => item is null))
        {
            throw new ArgumentException("Коллекция не должна содержать null.", nameof(source));
        }
        return Array.AsReadOnly(copy);
    }

    /// <summary>Отделяет JSON-значение от срока жизни исходного документа.</summary>
    internal static JsonElement Json(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("JSON-значение не определено.", nameof(value));
        }
        return value.Clone();
    }

    /// <summary>Требует явное UTC, не читая системные часы.</summary>
    internal static void Utc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время должно быть задано в UTC.", nameof(value));
        }
    }
}
