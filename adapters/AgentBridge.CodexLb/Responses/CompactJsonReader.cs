using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Читает compact discriminator и сохраняет окно отдельно от полного envelope.</summary>
internal static class CompactJsonReader
{
    /// <summary>Допускает отсутствие status только для подтверждённого compact object с output.</summary>
    internal static ModelResponse Read(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) { throw new JsonException(); }
        List<CanonicalModelItem> output = [];
        bool hasOutput = body.TryGetProperty("output", out JsonElement items);
        if (hasOutput)
        {
            if (items.ValueKind != JsonValueKind.Array) { throw new JsonException(); }
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) { throw new JsonException(); }
                output.Add(new(item));
            }
        }
        CanonicalModelEnvelope envelope = new(body);
        bool hasStatus = body.TryGetProperty("status", out JsonElement state) && state.ValueKind != JsonValueKind.Null;
        if (hasStatus && state.ValueKind != JsonValueKind.String) { throw new JsonException(); }
        string? status = hasStatus ? state.GetString() : null;
        bool hasError = body.TryGetProperty("error", out JsonElement error) && error.ValueKind != JsonValueKind.Null;
        if (status == "failed" || hasError)
        {
            return ModelResponse.Failed(output, ResponseErrorReader.Read(error, null), envelope);
        }
        if (!body.TryGetProperty("object", out JsonElement discriminator) || discriminator.ValueKind != JsonValueKind.String
            || !discriminator.GetString()!.Trim().StartsWith("response.compact", StringComparison.Ordinal))
        {
            throw new JsonException();
        }
        return hasOutput && status is null or "completed"
            ? ModelResponse.Completed(output, envelope)
            : ModelResponse.Incomplete(output, envelope);
    }
}
