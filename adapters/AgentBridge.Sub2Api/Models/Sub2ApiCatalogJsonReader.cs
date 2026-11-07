using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.Sub2Api.Models;

/// <summary>Обычный /v1/models подтверждает только точные публичные ID, без provenance metadata.</summary>
internal static class Sub2ApiCatalogJsonReader
{
    /// <summary>Не принимает Codex manifest и не превращает неизвестные metadata в capabilities.</summary>
    internal static ModelCatalogSnapshot Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        try
        {
            // JsonElement откладывает декодирование строк: повреждение даже неизвестного поля не принимается.
            _ = root.GetRawText();
        }
        catch (InvalidOperationException failure) when (failure.InnerException is DecoderFallbackException)
        {
            throw new JsonException();
        }

        RequireObject(root);
        if (Text(root, "object") != "list" || !root.TryGetProperty("data", out JsonElement data)
            || data.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException();
        }

        List<ModelCapabilities> models = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement item in data.EnumerateArray())
        {
            RequireObject(item);
            string id = Text(item, "id");
            if (Text(item, "object") != "model" || !ids.Add(id))
            {
                throw new JsonException();
            }

            models.Add(new(id, false, null, null, null, [], null, [], false, false, false, false, false));
        }

        return new(models);
    }

    /// <summary>Не принимает неоднозначные свойства envelope/model, включая повторное объявление ID.</summary>
    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new JsonException();
            }
        }
    }

    /// <summary>Отклоняет повреждённую строку без публикации исходного значения.</summary>
    private static string Text(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException();
        }

        string? text;
        try
        {
            text = property.GetString();
        }
        catch (InvalidOperationException failure) when (failure.InnerException is DecoderFallbackException)
        {
            throw new JsonException();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException();
        }

        return text;
    }
}
