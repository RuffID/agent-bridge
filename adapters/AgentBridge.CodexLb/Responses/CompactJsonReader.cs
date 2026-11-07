using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Responses;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Читает compact discriminator и сохраняет окно отдельно от полного envelope.</summary>
internal static class CompactJsonReader
{
    /// <summary>Допускает отсутствие status только для подтверждённого compact object с output.</summary>
    internal static ModelResponse Read(JsonElement body)
    {
        List<CanonicalModelItem> output = CanonicalOutputReader.Read(body, out bool hasOutput);
        CanonicalModelEnvelope envelope = new(body);
        bool hasStatus = body.TryGetProperty("status", out JsonElement state) && state.ValueKind != JsonValueKind.Null;
        if (hasStatus && state.ValueKind != JsonValueKind.String)
        {
            throw new JsonException();
        }

        string? status = null;
        if (hasStatus)
        {
            status = state.GetString();
        }

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

        if (hasOutput && status is null or "completed")
        {
            return ModelResponse.Completed(output, envelope);
        }

        return ModelResponse.Incomplete(output, envelope);
    }
}
