using System.Text;
using System.Text.Json;

namespace AgentBridge.Sub2Api.Auxiliary;

/// <summary>Проверяет завершённый Images JSON после transport cleanup, включая error с HTTP 200.</summary>
internal static class Sub2ApiImageResultReader
{
    /// <summary>Требует однозначный объект с payload каждого изображения, сохраняя исходный JSON.</summary>
    internal static void Validate(JsonElement root)
    {
        try
        {
            // Отложенное декодирование JsonElement должно проверить UTF-8 также неизвестных полей.
            _ = root.GetRawText();
            ValidateObject(root);
            if (root.TryGetProperty("error", out JsonElement error) && error.ValueKind != JsonValueKind.Null)
            {
                throw new JsonException();
            }

            if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() == 0)
            {
                throw new JsonException();
            }

            foreach (JsonElement image in data.EnumerateArray())
            {
                ValidateObject(image);
                bool hasBase64 = HasPayload(image, "b64_json");
                bool hasUrl = HasPayload(image, "url");
                if (!hasBase64 && !hasUrl)
                {
                    throw new JsonException();
                }
            }
        }
        catch (InvalidOperationException failure) when (failure.InnerException is DecoderFallbackException)
        {
            throw new JsonException();
        }
    }

    /// <summary>Не допускает scalar/array вместо объекта и неоднозначные повторные поля.</summary>
    private static void ValidateObject(JsonElement value)
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

    /// <summary>Проверяет наличие payload без декодирования изображения или скачивания URL.</summary>
    private static bool HasPayload(JsonElement image, string name)
    {
        if (!image.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException();
        }

        return !string.IsNullOrWhiteSpace(value.GetString());
    }
}
