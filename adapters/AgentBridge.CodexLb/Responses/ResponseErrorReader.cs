using System.Text.Json;
using AgentBridge.Application.Results;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Извлекает только закрытый безопасный набор status/type/code/param из полного error envelope.</summary>
internal static class ResponseErrorReader
{
    /// <summary>Не использует preview и не доверяет truncated/невалидному телу.</summary>
    internal static CodexLbServiceError Read(HttpRequestFailedException failure)
    {
        int status = (int)failure.StatusCode;
        JsonElement error = default;
        if (failure.ErrorResponse is { BodyState: HttpErrorBodyState.Complete, BodyText: not null } details)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(details.BodyText);
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out JsonElement candidate)
                    && candidate.ValueKind == JsonValueKind.Object)
                {
                    error = candidate.Clone();
                }
            }
            catch (JsonException)
            {
                // Полнота транспорта не означает корректность JSON.
            }
        }
        return Read(error, status);
    }

    /// <summary>Возвращает стабильную безопасную проекцию explicit model error без чтения message.</summary>
    internal static CodexLbServiceError Read(JsonElement error, int? status)
    {
        ServiceErrorType type = status switch
        {
            400 or 422 => ServiceErrorType.Validation,
            401 => ServiceErrorType.Unauthorized,
            403 => ServiceErrorType.Forbidden,
            404 => ServiceErrorType.NotFound,
            409 => ServiceErrorType.Conflict,
            408 or 504 => ServiceErrorType.Timeout,
            _ => ServiceErrorType.Rejected
        };
        string? apiType = Known(error, "type", ["invalid_request_error", "authentication_error", "permission_error",
            "rate_limit_error", "server_error", "api_error", "insufficient_quota"]);
        string? code = Known(error, "code", ["invalid_request_error", "invalid_api_key", "model_not_found",
            "context_length_exceeded", "rate_limit_exceeded", "insufficient_quota", "server_error",
            "previous_response_not_found", "invalid_function_parameters", "unsupported_parameter",
            "invalid_value", "invalid_type"]);
        string? param = Known(error, "param", ["model", "input", "instructions", "tools", "tool_choice",
            "parallel_tool_calls", "reasoning", "reasoning.effort", "reasoning.summary", "include", "text",
            "text.format", "service_tier", "truncation", "prompt_cache_key", "previous_response_id", "conversation"]);
        return new(type, status, apiType, code, param);
    }

    /// <summary>Пропускает только точное известное строковое значение, не произвольный token-подобный текст.</summary>
    private static string? Known(JsonElement error, string property, string[] allowed)
    {
        if (error.ValueKind != JsonValueKind.Object || !error.TryGetProperty(property, out JsonElement value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        string? text = value.GetString();
        return allowed.Contains(text, StringComparer.Ordinal) ? text : null;
    }
}
