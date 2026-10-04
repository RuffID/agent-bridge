using System.Text.Json;
using System.Text.Json.Nodes;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Собирает canonical output отдельно от исходных envelope и bound continuation.</summary>
internal class ResponseSseState(ModelContinuation? previous, string binding)
{
    private readonly SortedDictionary<int, JsonObject> items = new();
    private CanonicalModelEnvelope? envelope;
    private ModelContinuation? continuation;
    private ServiceError? failure;
    private ModelResponseStatus status = ModelResponseStatus.Incomplete;

    /// <summary>Получен хотя бы один известный элемент/response/delta, позволяющий сохранить отчёт.</summary>
    internal bool HasData { get; private set; }
    /// <summary>Получено terminal событие; дальнейшее чтение/callback прекращается.</summary>
    internal bool Terminal { get; private set; }

    /// <summary>Обрабатывает известные события, не теряя неизвестные поля их canonical объектов.</summary>
    internal ModelStreamUpdate? Apply(JsonElement payload, string? eventName, IReadOnlyDictionary<string, string[]> headers)
    {
        if (payload.ValueKind != JsonValueKind.Object) { throw new JsonException(); }
        string? type = String(payload, "type") ?? eventName;
        bool hasOutput = false;
        string? responseStatus = null;
        if (payload.TryGetProperty("response", out JsonElement response))
        {
            if (response.ValueKind != JsonValueKind.Object) { throw new JsonException(); }
            envelope = new(response);
            continuation = ResponseContinuationMapper.Create(previous, response, headers, binding);
            HasData = true;
            responseStatus = String(response, "status");
            hasOutput = response.TryGetProperty("output", out JsonElement output);
            if (hasOutput)
            {
                if (output.ValueKind != JsonValueKind.Array) { throw new JsonException(); }
                if (output.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object)) { throw new JsonException(); }
                // Непустой canonical output авторитетен; только absent/empty допускает backfill item events.
                if (output.GetArrayLength() > 0) { items.Clear(); }
                int index = 0;
                foreach (JsonElement item in output.EnumerateArray()) { SetItem(index++, item); }
            }
            if (response.TryGetProperty("error", out JsonElement error) && error.ValueKind != JsonValueKind.Null)
            {
                failure = ResponseErrorReader.Read(error, null);
            }
            else if (responseStatus == "failed") { failure = ResponseErrorReader.Read(default, null); }
        }
        if (type is "error" or "response.failed")
        {
            if (!payload.TryGetProperty("response", out _))
            {
                envelope = new(payload);
                continuation = ResponseContinuationMapper.Create(previous, payload, headers, binding);
            }
            JsonElement error = payload.TryGetProperty("error", out JsonElement nested) ? nested : payload;
            failure ??= ResponseErrorReader.Read(error, null);
            HasData = true;
        }
        if (failure is not null || type is "response.completed" or "response.failed" or "response.incomplete")
        {
            Terminal = true;
            status = failure is not null ? ModelResponseStatus.Failed
                : type == "response.completed" && responseStatus == "completed" && (hasOutput || items.Count > 0)
                    ? ModelResponseStatus.Completed : ModelResponseStatus.Incomplete;
            return null;
        }
        if (type is "response.output_item.added" or "response.output_item.done")
        {
            int index = Index(payload, "output_index");
            if (!payload.TryGetProperty("item", out JsonElement item)) { throw new JsonException(); }
            SetItem(index, item);
            HasData = true;
            return type == "response.output_item.done" ? new(null, new(item)) : null;
        }
        if (type is "response.content_part.added" or "response.content_part.done"
            or "response.reasoning_summary_part.added" or "response.reasoning_summary_part.done")
        {
            JsonObject item = Item(payload);
            bool summary = type.StartsWith("response.reasoning_summary", StringComparison.Ordinal);
            int index = Index(payload, summary ? "summary_index" : "content_index");
            if (!payload.TryGetProperty("part", out JsonElement part) || part.ValueKind != JsonValueKind.Object)
            { throw new JsonException(); }
            Parts(item, summary ? "summary" : "content", index)[index] = JsonNode.Parse(part.GetRawText());
            HasData = true;
            return null;
        }
        if (type is "response.function_call_arguments.delta" or "response.function_call_arguments.done")
        {
            JsonObject item = Item(payload);
            bool done = type.EndsWith(".done", StringComparison.Ordinal);
            string value = RequiredString(payload, done ? "arguments" : "delta");
            item["arguments"] = done ? value : NodeString(item, "arguments") + value;
            HasData = true;
            return null;
        }
        if (type is "response.output_text.delta" or "response.output_text.done"
            or "response.refusal.delta" or "response.refusal.done"
            or "response.reasoning_text.delta" or "response.reasoning_text.done"
            or "response.reasoning_summary_text.delta" or "response.reasoning_summary_text.done")
        {
            bool summary = type.StartsWith("response.reasoning_summary", StringComparison.Ordinal);
            bool refusal = type.StartsWith("response.refusal", StringComparison.Ordinal);
            bool done = type.EndsWith(".done", StringComparison.Ordinal);
            int index = Index(payload, summary ? "summary_index" : "content_index");
            JsonArray parts = Parts(Item(payload), summary ? "summary" : "content", index);
            JsonObject part = parts[index] as JsonObject ?? throw new JsonException();
            string property = refusal ? "refusal" : "text";
            string value = RequiredString(payload, done ? property : "delta");
            part[property] = done ? value : NodeString(part, property) + value;
            HasData = true;
            return !done && type == "response.output_text.delta" ? new(value, null) : null;
        }
        return null;
    }

    /// <summary>Возвращает независимый отчёт; terminal raw envelope не переписывается локальной сборкой.</summary>
    internal ModelResponse Report()
    {
        CanonicalModelItem[] output = items.Values.Select(item => new CanonicalModelItem(JsonSerializer.SerializeToElement(item))).ToArray();
        return status switch
        {
            ModelResponseStatus.Completed => ModelResponse.Completed(output, envelope, continuation),
            ModelResponseStatus.Failed => ModelResponse.Failed(output, failure!, envelope, continuation),
            ModelResponseStatus.Canceled => ModelResponse.Canceled(output, envelope, continuation),
            _ => ModelResponse.Incomplete(output, envelope, continuation)
        };
    }

    /// <summary>Сохраняет данные при caller cancellation, не подменяя установленный explicit failure.</summary>
    internal ModelResponse Cancel()
    {
        if (failure is null) { status = ModelResponseStatus.Canceled; }
        return Report();
    }

    /// <summary>Сохраняет данные с typed transport/deadline failure без замены уже полученного отказа.</summary>
    internal ModelResponse Fail(ServiceError error)
    {
        failure ??= error;
        status = ModelResponseStatus.Failed;
        return Report();
    }

    /// <summary>Копирует объект целиком без текстовой проекции.</summary>
    private void SetItem(int index, JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) { throw new JsonException(); }
        items[index] = JsonNode.Parse(item.GetRawText())!.AsObject();
    }

    /// <summary>Delta требует известного item; неизвестная связь отклоняется вместо выдумывания output.</summary>
    private JsonObject Item(JsonElement payload)
    {
        int index = Index(payload, "output_index");
        if (!items.TryGetValue(index, out JsonObject? item)) { throw new JsonException(); }
        string? id = String(payload, "item_id");
        if (id is not null && id != NodeString(item, "id")) { throw new JsonException(); }
        return item;
    }

    /// <summary>Создаёт только следующую protocol part; дыры/нарушенные формы не исправляются молча.</summary>
    private static JsonArray Parts(JsonObject item, string property, int index)
    {
        JsonArray parts;
        if (item[property] is null) { parts = new(); item[property] = parts; }
        else { parts = item[property] as JsonArray ?? throw new JsonException(); }
        if (index > parts.Count) { throw new JsonException(); }
        if (index == parts.Count) { parts.Add(new JsonObject()); }
        return parts;
    }

    /// <summary>Извлекает неотрицательный canonical index без преобразования строк.</summary>
    private static int Index(JsonElement payload, string name) => payload.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int index) && index >= 0 ? index : throw new JsonException();
    /// <summary>Извлекает строку либо отсутствие поля.</summary>
    private static string? String(JsonElement payload, string name) => payload.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    /// <summary>Требует строку для известных delta/done событий.</summary>
    private static string RequiredString(JsonElement payload, string name) => String(payload, name) ?? throw new JsonException();
    /// <summary>Читает накопленную строку без преобразования других JSON видов.</summary>
    private static string NodeString(JsonObject item, string property) => item[property] is null ? ""
        : item[property] is JsonValue value && value.TryGetValue(out string? text) ? text : throw new JsonException();
}
