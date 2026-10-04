using System.Text.Encodings.Web;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using Microsoft.ML.Tokenizers;

namespace AgentBridge.Tokenization;

/// <inheritdoc/>
/// <remarks>Точная BPE-токенизация известных payload; JSON framing только оценка, не server count.</remarks>
public class ContextTokenCounter : IContextTokenCounter
{
    private static readonly JsonSerializerOptions jsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <inheritdoc/>
    public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        string? encoding = ModelEncodingMap.Find(request.Model);
        if (encoding is null)
        {
            return Task.FromResult(ServiceResult<ContextTokenCount>.Fail(
                new(ServiceErrorType.Unsupported, "Кодировка выбранной модели не подтверждена.")));
        }
        TiktokenTokenizer tokenizer = OrdinaryTokenizerFactory.Get(encoding);
        PayloadCount payload = new(tokenizer, cancellationToken);
        payload.Text(request.Instructions);
        foreach (CanonicalModelItem item in request.Input) { payload.Item(item.Content); }
        foreach (ModelToolDefinition tool in request.Tools)
        {
            payload.Text(tool.Name);
            payload.Text(tool.Description);
            payload.Json(tool.Parameters);
        }
        if (request.Parameters is not null) { payload.Parameters(request.Parameters.Content); }
        // Anchor может дополнять input скрытым server state; его ID/metadata не являются payload.
        bool opaque = payload.HasOpaqueContent || request.Continuation is not null;
        long? estimate = null;
        if (!opaque)
        {
            string framing = JsonSerializer.Serialize(new
            {
                instructions = request.Instructions,
                input = request.Input.Select(item => item.Content),
                tools = request.Tools.Select(tool => new
                {
                    type = "function", name = tool.Name, description = tool.Description,
                    parameters = tool.Parameters, strict = tool.Strict
                }),
                parameters = payload.InputParameters
            }, jsonOptions);
            estimate = Math.Max(payload.KnownTokens, payload.CountText(framing));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ServiceResult<ContextTokenCount>.Ok(new(encoding, payload.KnownTokens, estimate, opaque)));
    }

    /// <summary>Локальное состояние одного подсчёта, без содержимого в ошибках или логах.</summary>
    private class PayloadCount
    {
        private readonly TiktokenTokenizer tokenizer;
        private readonly CancellationToken cancellationToken;
        internal long KnownTokens { get; private set; }
        internal bool HasOpaqueContent { get; private set; }
        internal Dictionary<string, JsonElement> InputParameters { get; } = new(StringComparer.Ordinal);

        internal PayloadCount(TiktokenTokenizer tokenizer, CancellationToken cancellationToken)
        {
            this.tokenizer = tokenizer;
            this.cancellationToken = cancellationToken;
        }

        /// <summary>Проверяет отмену до и после неделимого библиотечного BPE вызова.</summary>
        internal int CountText(string text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = tokenizer.CountTokens(text);
            cancellationToken.ThrowIfCancellationRequested();
            return count;
        }

        /// <summary>Суммирует текст отдельного payload; резерв сюда не входит.</summary>
        internal void Text(string text) => KnownTokens = checked(KnownTokens + CountText(text));

        /// <summary>Считает весь schema/input JSON с именами полей, числами и вложенными данными.</summary>
        internal void Json(JsonElement value) => Text(JsonSerializer.Serialize(value, jsonOptions));

        /// <summary>Извлекает только известный текст; неизвестные поля сохраняют неопределённость полного бюджета.</summary>
        internal void Item(JsonElement item)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? type = String(item, "type");
            switch (type)
            {
                case null when !item.TryGetProperty("type", out _):
                case "message":
                    Fields(item, ["type", "role", "content", "id", "status"]);
                    if (String(item, "role") is not ("user" or "assistant" or "system" or "developer")) { HasOpaqueContent = true; }
                    Content(item, "content");
                    break;
                case "function_call":
                    Fields(item, ["type", "name", "arguments", "call_id", "id", "status"]);
                    TextField(item, "name");
                    TextField(item, "arguments");
                    break;
                case "function_call_output":
                    Fields(item, ["type", "call_id", "output", "id", "status"]);
                    Content(item, "output");
                    break;
                case "reasoning":
                    HasOpaqueContent = true;
                    if (item.TryGetProperty("summary", out JsonElement summary) && summary.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement part in summary.EnumerateArray())
                        {
                            if (String(part, "type") == "summary_text") { TextField(part, "text"); }
                        }
                    }
                    break;
                default:
                    HasOpaqueContent = true;
                    break;
            }
        }

        /// <summary>Считает текст и известные content parts; файлы/изображения/unknown parts не получают вымышленного веса.</summary>
        private void Content(JsonElement item, string name)
        {
            if (!item.TryGetProperty(name, out JsonElement content)) { HasOpaqueContent = true; return; }
            if (content.ValueKind == JsonValueKind.String) { Text(content.GetString()!); return; }
            if (content.ValueKind != JsonValueKind.Array) { HasOpaqueContent = true; return; }
            foreach (JsonElement part in content.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (part.ValueKind != JsonValueKind.Object) { HasOpaqueContent = true; continue; }
                switch (String(part, "type"))
                {
                    case "input_text":
                    case "output_text":
                        Fields(part, ["type", "text", "annotations", "logprobs"]);
                        TextField(part, "text");
                        NonEmptyMetadata(part, "annotations");
                        NonEmptyMetadata(part, "logprobs");
                        break;
                    case "refusal":
                        Fields(part, ["type", "refusal"]);
                        TextField(part, "refusal");
                        break;
                    default:
                        HasOpaqueContent = true;
                        break;
                }
            }
        }

        /// <summary>Учитывает prompt-affecting format/tool choice; известные transport controls не являются input.</summary>
        internal void Parameters(JsonElement parameters)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty property in parameters.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(property.Name)) { HasOpaqueContent = true; }
                switch (property.Name)
                {
                    case "text":
                    case "tool_choice":
                        Json(property.Value);
                        InputParameters[property.Name] = property.Value;
                        if (property.Name == "text") { TextParameter(property.Value); }
                        else if (property.Value.ValueKind == JsonValueKind.Object)
                        {
                            Fields(property.Value, ["type", "name"]);
                            if (String(property.Value, "type") != "function" || String(property.Value, "name") is null)
                            { HasOpaqueContent = true; }
                        }
                        else if (property.Value.ValueKind != JsonValueKind.String
                            || property.Value.GetString() is not ("auto" or "none" or "required")) { HasOpaqueContent = true; }
                        break;
                    case "parallel_tool_calls":
                    case "include":
                    case "service_tier":
                    case "truncation":
                    case "prompt_cache_key":
                        break;
                    case "reasoning":
                        Fields(property.Value, ["summary"]);
                        break;
                    default:
                        HasOpaqueContent = true;
                        break;
                }
            }
        }

        /// <summary>Распознаёт текстовые output constraints; неизвестные вложенные controls оставляют estimate неизвестной.</summary>
        private void TextParameter(JsonElement value)
        {
            Fields(value, ["format", "verbosity"]);
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("format", out JsonElement format)) { return; }
            Fields(format, ["type", "name", "description", "schema", "strict"]);
            if (String(format, "type") is not ("text" or "json_object" or "json_schema")) { HasOpaqueContent = true; }
        }

        /// <summary>Непустые неизвестные annotations/logprobs не выдаются за нулевой вклад.</summary>
        private void NonEmptyMetadata(JsonElement item, string name)
        {
            if (item.TryGetProperty(name, out JsonElement value)
                && !(value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0)) { HasOpaqueContent = true; }
        }

        /// <summary>Распознаёт известные поля; дубликаты или неизвестные поля делают полный бюджет неизвестным.</summary>
        private void Fields(JsonElement value, string[] allowed)
        {
            if (value.ValueKind != JsonValueKind.Object) { HasOpaqueContent = true; return; }
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal)) { HasOpaqueContent = true; }
            }
        }

        /// <summary>Отсутствующий/нестроковый текст не заменяется пустым payload.</summary>
        private void TextField(JsonElement item, string name)
        {
            if (String(item, name) is string text) { Text(text); }
            else { HasOpaqueContent = true; }
        }

        /// <summary>Читает строку только из объекта, без преобразования неизвестного содержимого.</summary>
        private static string? String(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
