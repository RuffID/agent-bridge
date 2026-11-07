using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.Responses;

/// <summary>Читает canonical output, сохраняя полные объекты независимо от lifecycle и identity шлюза.</summary>
internal static class CanonicalOutputReader
{
    /// <summary>Отличает отсутствующий массив от пустого; не восстанавливает и не преобразует элементы.</summary>
    internal static List<CanonicalModelItem> Read(JsonElement body, out bool hasOutput)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        List<CanonicalModelItem> output = [];
        hasOutput = body.TryGetProperty("output", out JsonElement items);
        if (!hasOutput)
        {
            return output;
        }

        if (items.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException();
        }

        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            output.Add(new CanonicalModelItem(item));
        }

        return output;
    }
}
