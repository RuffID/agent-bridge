using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Responses.Models;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Записывает отдельный compact-контракт без скрытого удаления generation-параметров.</summary>
internal static class CompactRequestWriter
{
    /// <summary>Отклоняет неподдержанный вход и сохраняет канонические items без преобразования.</summary>
    internal static ServiceResult<ResponseRequestBody> Write(ModelRequest request)
    {
        if (request.Continuation is not null || request.Tools.Count != 0)
        {
            return ServiceResult<ResponseRequestBody>.Fail(new(ServiceErrorType.Unsupported,
                "Compact не поддерживает continuation и определения инструментов."));
        }
        Dictionary<string, JsonElement> parameters = new(StringComparer.Ordinal);
        if (request.Parameters is not null)
        {
            foreach (JsonProperty property in request.Parameters.Content.EnumerateObject())
            {
                if (property.Name is not ("reasoning" or "service_tier" or "prompt_cache_key"))
                {
                    return ServiceResult<ResponseRequestBody>.Fail(new(ServiceErrorType.Unsupported,
                        "Параметр не поддерживается compact-адаптером."));
                }
                bool valid = property.Name == "reasoning"
                    ? property.Value.ValueKind == JsonValueKind.Object && !property.Value.TryGetProperty("effort", out _)
                    : property.Value.ValueKind == JsonValueKind.String;
                if (!valid || !parameters.TryAdd(property.Name, property.Value))
                {
                    return ServiceResult<ResponseRequestBody>.Fail(new(ServiceErrorType.Validation,
                        "Некорректные параметры compact."));
                }
            }
        }
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("model", request.Model);
            writer.WriteString("instructions", request.Instructions);
            writer.WriteBoolean("store", false);
            writer.WriteStartArray("input");
            foreach (CanonicalModelItem item in request.Input) { item.Content.WriteTo(writer); }
            writer.WriteEndArray();
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
            writer.WriteEndObject();
        }
        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
        return ServiceResult<ResponseRequestBody>.Ok(new(document.RootElement.Clone()));
    }
}
