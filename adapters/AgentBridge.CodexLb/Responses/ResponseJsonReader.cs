using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Читает полный JSON отчёт, сохраняя output отдельно от envelope и продолжения.</summary>
internal static class ResponseJsonReader
{
    /// <summary>Подтверждает Completed только по terminal status без explicit error.</summary>
    internal static ModelResponse Read(JsonElement body, IReadOnlyDictionary<string, string[]> headers,
        ModelContinuation? previous, string binding)
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
        ModelContinuation continuation = ResponseContinuationMapper.Create(previous, body, headers, binding);
        string? status = body.TryGetProperty("status", out JsonElement state) && state.ValueKind == JsonValueKind.String
            ? state.GetString() : null;
        bool hasError = body.TryGetProperty("error", out JsonElement error) && error.ValueKind != JsonValueKind.Null;
        if (status == "failed" || hasError)
        {
            return ModelResponse.Failed(output, ResponseErrorReader.Read(error, null), envelope, continuation);
        }
        return status switch
        {
            "completed" when hasOutput => ModelResponse.Completed(output, envelope, continuation),
            _ => ModelResponse.Incomplete(output, envelope, continuation)
        };
    }
}
