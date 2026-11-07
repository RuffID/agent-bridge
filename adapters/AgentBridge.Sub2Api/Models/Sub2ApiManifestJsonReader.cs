using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.Sub2Api.Models;

/// <summary>Читает объявленные поля existing Codex manifest; не подтверждает upstream pool или входной бюджет.</summary>
internal static class Sub2ApiManifestJsonReader
{
    /// <summary>Неполные объявления остаются неподтверждёнными, неизвестные budgets и flags не выводятся из defaults.</summary>
    internal static ModelCatalogSnapshot Read(JsonElement root)
    {
        try
        {
            _ = root.GetRawText();
        }
        catch (InvalidOperationException failure) when (failure.InnerException is DecoderFallbackException)
        {
            throw new JsonException();
        }

        RequireObject(root);
        if (!root.TryGetProperty("models", out JsonElement models) || models.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException();
        }

        List<ModelCapabilities> result = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement model in models.EnumerateArray())
        {
            RequireObject(model);
            string id = Text(model, "slug");
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            {
                throw new JsonException();
            }

            List<string> efforts = [];
            if (model.TryGetProperty("supported_reasoning_levels", out JsonElement levels))
            {
                if (levels.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException();
                }

                foreach (JsonElement level in levels.EnumerateArray())
                {
                    RequireObject(level);
                    string effort = Text(level, "effort");
                    if (string.IsNullOrWhiteSpace(effort) || efforts.Contains(effort, StringComparer.Ordinal))
                    {
                        throw new JsonException();
                    }

                    efforts.Add(effort);
                }
            }

            bool available = false;
            if (model.TryGetProperty("supported_in_api", out JsonElement supported))
            {
                if (supported.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new JsonException();
                }

                available = supported.GetBoolean();
            }

            // Все прочие сведения намеренно неизвестны: даже input_context_window не имеет установленной семантики.
            result.Add(new(id, true, null, null, null, efforts, null, [], available, false, false, false, false));
        }

        return new(result);
    }

    /// <summary>Требует строковое поле без преобразования неверной формы.</summary>
    private static string Text(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException();
        }

        return property.GetString() ?? throw new JsonException();
    }

    /// <summary>Не принимает неоднозначные поля в объектах проецируемого контракта.</summary>
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
}
