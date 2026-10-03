using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.CodexLb.Models;

/// <summary>Разбирает только необходимые возможности OpenAI-compatible каталога текущего codex-lb.</summary>
internal static class ModelCatalogJsonReader
{
    /// <summary>Не возвращает raw JSON, неизвестные поля, описания или транспортные метаданные.</summary>
    internal static ModelCatalogSnapshot Read(JsonElement root)
    {
        if (Text(root, "object") != "list")
        {
            throw new JsonException();
        }
        JsonElement data = Property(root, "data");
        Require(data, JsonValueKind.Array);
        List<ModelCapabilities> models = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement item in data.EnumerateArray())
        {
            string id = Text(item, "id");
            if (!ids.Add(id) || Text(item, "object") != "model")
            {
                throw new JsonException();
            }
            if (!item.TryGetProperty("metadata", out JsonElement metadata) || metadata.ValueKind == JsonValueKind.Null)
            {
                models.Add(new(id, false, null, null, null, [], null, [], false, false, false, false, false));
                continue;
            }
            Require(metadata, JsonValueKind.Object);
            int contextWindow = Integer(Property(metadata, "context_window"));
            int? inputWindow = OptionalInteger(metadata, "input_context_window");
            int? maxOutput = OptionalInteger(metadata, "max_output_tokens");
            JsonElement levels = Property(metadata, "supported_reasoning_levels");
            Require(levels, JsonValueKind.Array);
            List<string> efforts = [];
            foreach (JsonElement level in levels.EnumerateArray())
            {
                string effort = Text(level, "effort");
                if (efforts.Contains(effort, StringComparer.Ordinal))
                {
                    throw new JsonException();
                }
                efforts.Add(effort);
            }
            JsonElement modalities = Property(metadata, "input_modalities");
            Require(modalities, JsonValueKind.Array);
            List<string> inputs = [];
            foreach (JsonElement modality in modalities.EnumerateArray())
            {
                Require(modality, JsonValueKind.String);
                inputs.Add(modality.GetString()!);
            }
            models.Add(new(id, true, contextWindow, inputWindow, maxOutput, efforts,
                OptionalText(metadata, "default_reasoning_level"), inputs,
                Boolean(metadata, "supported_in_api", true), Boolean(metadata, "supports_reasoning_summaries", false),
                Boolean(metadata, "supports_parallel_tool_calls", false), Boolean(metadata, "support_verbosity", false),
                Boolean(metadata, "prefer_websockets", false)));
        }
        return new(models);
    }

    /// <summary>Читает обязательное поле объекта.</summary>
    private static JsonElement Property(JsonElement value, string name)
    {
        Require(value, JsonValueKind.Object);
        if (!value.TryGetProperty(name, out JsonElement property))
        {
            throw new JsonException();
        }
        return property;
    }

    /// <summary>Читает обязательную непустую строку.</summary>
    private static string Text(JsonElement value, string name)
    {
        JsonElement property = Property(value, name);
        Require(property, JsonValueKind.String);
        string? text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException();
        }
        return text;
    }

    /// <summary>Читает необязательную строку без подмены неизвестного значения.</summary>
    private static string? OptionalText(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return Text(value, name);
    }

    /// <summary>Читает неотрицательное целое из каталога; ноль не подтверждает доступный бюджет.</summary>
    private static int Integer(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number) || number < 0)
        {
            throw new JsonException();
        }
        return number;
    }

    /// <summary>Сохраняет неизвестный бюджет как null.</summary>
    private static int? OptionalInteger(JsonElement value, string name) =>
        !value.TryGetProperty(name, out JsonElement property) || property.ValueKind == JsonValueKind.Null
            ? null : Integer(property);

    /// <summary>Применяет только schema default текущего codex-lb, без угадывания возможностей.</summary>
    private static bool Boolean(JsonElement value, string name, bool schemaDefault)
    {
        if (!value.TryGetProperty(name, out JsonElement property))
        {
            return schemaDefault;
        }
        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonException();
        }
        return property.GetBoolean();
    }

    /// <summary>Отклоняет нарушенную форму без включения её содержимого в исключение.</summary>
    private static void Require(JsonElement value, JsonValueKind kind)
    {
        if (value.ValueKind != kind)
        {
            throw new JsonException();
        }
    }
}
