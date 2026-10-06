using System.Text.Json;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Проверяет известные вложенные controls, не преобразуя неизвестные поля и произвольный JSON schema.</summary>
internal static class NestedControlsValidator
{
    /// <summary>Проверяет summary и запрещает override effort, принадлежащего выбору модели.</summary>
    internal static bool Reasoning(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) { return false; }
        bool summarySeen = false;
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Name == "effort") { return false; }
            if (property.Name != "summary") { continue; }
            if (summarySeen || !StringOrNull(property.Value)) { return false; }
            summarySeen = true;
        }
        return true;
    }

    /// <summary>Проверяет verbosity и format; неизвестные поля вне известного пути не обходятся.</summary>
    internal static bool Text(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) { return false; }
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Name is not ("verbosity" or "format")) { continue; }
            if (!seen.Add(property.Name)) { return false; }
            if (property.Name == "verbosity")
            {
                if (!StringOrNull(property.Value)) { return false; }
            }
            else if (property.Value.ValueKind != JsonValueKind.Null && !Format(property.Value)) { return false; }
        }
        return true;
    }

    /// <summary>Проверяет типы и повторы type/name/strict/schema без проверки содержимого schema.</summary>
    private static bool Format(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) { return false; }
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Name is not ("type" or "name" or "strict" or "schema")) { continue; }
            if (!seen.Add(property.Name)) { return false; }
            if (property.Name is "type" or "name")
            {
                if (!StringOrNull(property.Value)) { return false; }
            }
            else if (property.Name == "strict"
                && property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)) { return false; }
        }
        return true;
    }

    /// <summary>Сохраняет nullable строковый контракт без enum и нормализации значения.</summary>
    private static bool StringOrNull(JsonElement value) => value.ValueKind is JsonValueKind.String or JsonValueKind.Null;
}
