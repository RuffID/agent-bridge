using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Responses.Models;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Привязывает opaque continuation к локальному контексту/endpoint/доступу без сохранения ключа.</summary>
internal static class ResponseContinuationMapper
{
    private const string ADAPTER = "codex-lb-json-v1";

    /// <summary>Вычисляет стабильный отпечаток однозначно сериализованных частей без turnId, чтобы продолжить следующий turn.</summary>
    internal static string Binding(ApplicationCallContext call, ModelAccess access, string endpoint)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { call.DialogId.Value.ToString("D"),
            call.OwnerId.Value, call.AgentId, endpoint, access.RevealApiKey() });
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Принимает только собственный формат с совпадающим binding; неизвестные поля не отправляет серверу.</summary>
    internal static ServiceResult<ResponseContinuationState> Read(ModelContinuation? continuation, string binding)
    {
        if (continuation is null) { return ServiceResult<ResponseContinuationState>.Ok(new(null, null)); }
        JsonElement content = continuation.Content;
        if (String(content, "adapter") != ADAPTER)
        {
            return ServiceResult<ResponseContinuationState>.Fail(new(ServiceErrorType.Unsupported, "Формат продолжения не поддерживается JSON-адаптером."));
        }
        if (String(content, "binding") != binding)
        {
            return ServiceResult<ResponseContinuationState>.Fail(new(ServiceErrorType.Conflict, "Продолжение принадлежит другому контексту или доступу."));
        }
        string? previous = String(content, "previous_response_id");
        string? turnState = null;
        if ((content.TryGetProperty("previous_response_id", out _) && !SafeValue(previous))
            || (content.TryGetProperty("headers", out JsonElement headers) && headers.ValueKind != JsonValueKind.Object))
        {
            return Invalid();
        }
        if (content.TryGetProperty("headers", out headers) && headers.TryGetProperty("x-codex-turn-state", out _))
        {
            turnState = String(headers, "x-codex-turn-state");
            if (!SafeValue(turnState)) { return Invalid(); }
        }
        return ServiceResult<ResponseContinuationState>.Ok(new(previous, turnState));
    }

    /// <summary>Копирует прежние неизвестные metadata и сохраняет только разрешённый transport anchor/header.</summary>
    internal static ModelContinuation Create(ModelContinuation? previous, JsonElement envelope,
        IReadOnlyDictionary<string, string[]> headers, string binding)
    {
        JsonObject content = previous is null ? new() : JsonNode.Parse(previous.Content.GetRawText())!.AsObject();
        content["adapter"] = ADAPTER;
        content["binding"] = binding;
        // Новый envelope не разрешает продолжать со старого anchor, если его собственный id отсутствует/непригоден.
        content.Remove("previous_response_id");
        string? id = String(envelope, "id");
        if (SafeValue(id)) { content["previous_response_id"] = id; }
        // Сохраняем независимые headers, но не превращаем неизвестные metadata в исходящие headers.
        JsonObject captured = content["headers"] as JsonObject ?? new();
        if (headers.TryGetValue("x-codex-turn-state", out string[]? values))
        {
            if (values.Length == 1 && SafeValue(values[0])) { captured["x-codex-turn-state"] = values[0]; }
            else { captured.Remove("x-codex-turn-state"); }
        }
        content["headers"] = captured;
        return new(JsonSerializer.SerializeToElement(content));
    }

    /// <summary>Извлекает строку без нормализации opaque значения.</summary>
    private static string? String(JsonElement content, string name) => content.ValueKind == JsonValueKind.Object
        && content.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Отклоняет пустое значение и инъекцию HTTP header без изменения исходного opaque token.</summary>
    private static bool SafeValue(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.All(character => character is >= '!' and <= '~');

    /// <summary>Создаёт безопасный отказ до HTTP.</summary>
    private static ServiceResult<ResponseContinuationState> Invalid() => ServiceResult<ResponseContinuationState>.Fail(
        new(ServiceErrorType.Validation, "Некорректные метаданные продолжения."));
}
