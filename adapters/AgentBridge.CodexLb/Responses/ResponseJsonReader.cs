using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Responses;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Читает полный JSON отчёт, сохраняя output отдельно от envelope и продолжения.</summary>
internal static class ResponseJsonReader
{
    /// <summary>Подтверждает Completed только по terminal status без explicit error.</summary>
    internal static ModelResponse Read(JsonElement body, IReadOnlyDictionary<string, string[]> headers,
        ModelContinuation? previous, string binding)
    {
        List<CanonicalModelItem> output = CanonicalOutputReader.Read(body, out bool hasOutput);
        CanonicalModelEnvelope envelope = new(body);
        ModelContinuation continuation = ResponseContinuationMapper.Create(previous, body, headers, binding);
        string? status = null;
        if (body.TryGetProperty("status", out JsonElement state) && state.ValueKind == JsonValueKind.String)
        {
            status = state.GetString();
        }

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
