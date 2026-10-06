using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Responses.Models;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Создаёт canonical JSON без текстовых DTO и override обязательных полей.</summary>
internal static class ResponseRequestWriter
{
    /// <summary>Сохраняет items и вложенные контроли полностью; неизвестные top-level параметры отклоняет до HTTP.</summary>
    internal static ServiceResult<ResponseRequestBody> Write(ModelRequest request, string? previousResponseId, bool stream = false)
    {
        Dictionary<string, JsonElement> parameters = new(StringComparer.Ordinal);
        if (request.Parameters is not null)
        {
            foreach (JsonProperty property in request.Parameters.Content.EnumerateObject())
            {
                if (property.Name is not ("tool_choice" or "parallel_tool_calls" or "include" or "service_tier"
                    or "truncation" or "prompt_cache_key" or "text" or "reasoning"))
                {
                    return ServiceResult<ResponseRequestBody>.Fail(new(ServiceErrorType.Unsupported, "Параметр модели не поддерживается JSON-адаптером."));
                }
                if (!parameters.TryAdd(property.Name, property.Value) || !Valid(property.Name, property.Value))
                {
                    return ServiceResult<ResponseRequestBody>.Fail(new(ServiceErrorType.Validation, "Некорректные параметры модели."));
                }
            }
        }
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("model", request.Model);
            writer.WriteString("instructions", request.Instructions);
            writer.WriteBoolean("stream", stream);
            writer.WriteBoolean("store", false);
            writer.WriteStartArray("input");
            foreach (CanonicalModelItem item in request.Input) { item.Content.WriteTo(writer); }
            writer.WriteEndArray();
            writer.WriteStartArray("tools");
            foreach (ModelToolDefinition tool in request.Tools)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "function");
                writer.WriteString("name", tool.Name);
                writer.WriteString("description", tool.Description);
                writer.WritePropertyName("parameters");
                tool.Parameters.WriteTo(writer);
                writer.WriteBoolean("strict", tool.Strict);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            if (previousResponseId is not null) { writer.WriteString("previous_response_id", previousResponseId); }
            if (request.ReasoningEffort is not null || parameters.ContainsKey("reasoning"))
            {
                writer.WriteStartObject("reasoning");
                if (request.ReasoningEffort is not null) { writer.WriteString("effort", request.ReasoningEffort); }
                if (parameters.TryGetValue("reasoning", out JsonElement reasoning))
                {
                    foreach (JsonProperty property in reasoning.EnumerateObject()) { property.WriteTo(writer); }
                }
                writer.WriteEndObject();
            }
            foreach (KeyValuePair<string, JsonElement> parameter in parameters)
            {
                if (parameter.Key == "reasoning") { continue; }
                writer.WritePropertyName(parameter.Key);
                parameter.Value.WriteTo(writer);
            }
            if (!parameters.ContainsKey("include"))
            {
                writer.WriteStartArray("include");
                writer.WriteStringValue("reasoning.encrypted_content");
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
        return ServiceResult<ResponseRequestBody>.Ok(new(document.RootElement.Clone()));
    }

    /// <summary>Проверяет локальную форму контролей без статического списка model/effort либо потери вложенных данных.</summary>
    private static bool Valid(string name, JsonElement value) => name switch
    {
        "parallel_tool_calls" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "include" => value.ValueKind == JsonValueKind.Array
            && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String),
        "tool_choice" => value.ValueKind is JsonValueKind.String or JsonValueKind.Object,
        "text" => NestedControlsValidator.Text(value),
        "reasoning" => NestedControlsValidator.Reasoning(value),
        "truncation" => value.ValueKind == JsonValueKind.String && value.GetString() is "auto" or "disabled",
        _ => value.ValueKind == JsonValueKind.String
    };
}
