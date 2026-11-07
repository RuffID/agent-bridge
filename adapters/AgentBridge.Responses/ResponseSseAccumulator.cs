using System.Text.Json;
using System.Text.Json.Nodes;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Responses;

/// <summary>Накапливает canonical SSE output; ошибки и continuation определяет конкретный коннектор.</summary>
internal class ResponseSseAccumulator(
    Func<JsonElement, IReadOnlyDictionary<string, string[]>, ModelContinuation?> continuationFactory,
    Func<JsonElement, ServiceError> errorReader)
{
    private readonly SortedDictionary<int, JsonObject> items = new();
    private CanonicalModelEnvelope? envelope;
    private ModelContinuation? continuation;
    private ServiceError? failure;
    private ModelResponseStatus status = ModelResponseStatus.Incomplete;

    /// <summary>Получены canonical данные, которые нужно сохранить при отказе.</summary>
    internal bool HasData { get; private set; }
    /// <summary>Получено terminal событие, после которого чтение и callbacks прекращаются.</summary>
    internal bool Terminal { get; private set; }

    /// <summary>Применяет известное событие, сохраняя неизвестные поля полных items/envelope.</summary>
    internal ModelStreamUpdate? Apply(JsonElement payload, string? eventName, IReadOnlyDictionary<string, string[]> headers)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        string? type = String(payload, "type") ?? eventName;
        bool hasOutput = false;
        string? responseStatus = null;
        if (payload.TryGetProperty("response", out JsonElement response))
        {
            if (response.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            envelope = new(response);
            continuation = continuationFactory(response, headers);
            HasData = true;
            responseStatus = String(response, "status");
            hasOutput = response.TryGetProperty("output", out JsonElement output);
            if (hasOutput)
            {
                if (output.ValueKind != JsonValueKind.Array || output.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object))
                {
                    throw new JsonException();
                }

                // Непустой canonical output авторитетен; absent/empty допускает backfill item events.
                if (output.GetArrayLength() > 0)
                {
                    items.Clear();
                }

                int index = 0;
                foreach (JsonElement item in output.EnumerateArray())
                {
                    SetItem(index++, item);
                }
            }

            if (response.TryGetProperty("error", out JsonElement error) && error.ValueKind != JsonValueKind.Null)
            {
                failure = errorReader(error);
            }
            else if (responseStatus == "failed")
            {
                failure = errorReader(default);
            }
        }

        if (type is "error" or "response.failed")
        {
            if (!payload.TryGetProperty("response", out _))
            {
                envelope = new(payload);
                continuation = continuationFactory(payload, headers);
            }

            JsonElement error = payload;
            if (payload.TryGetProperty("error", out JsonElement nested))
            {
                error = nested;
            }

            failure ??= errorReader(error);
            HasData = true;
        }

        if (failure is not null || type is "response.completed" or "response.failed" or "response.incomplete")
        {
            Terminal = true;
            if (failure is not null)
            {
                status = ModelResponseStatus.Failed;
            }
            else if (type == "response.completed" && responseStatus == "completed" && (hasOutput || items.Count > 0))
            {
                status = ModelResponseStatus.Completed;
            }
            else
            {
                status = ModelResponseStatus.Incomplete;
            }

            return null;
        }

        if (type is "response.output_item.added" or "response.output_item.done")
        {
            int index = Index(payload, "output_index");
            if (!payload.TryGetProperty("item", out JsonElement item))
            {
                throw new JsonException();
            }

            SetItem(index, item);
            HasData = true;
            if (type == "response.output_item.done")
            {
                return new(null, new(item));
            }

            return null;
        }

        if (type is "response.content_part.added" or "response.content_part.done"
            or "response.reasoning_summary_part.added" or "response.reasoning_summary_part.done")
        {
            JsonObject item = Item(payload);
            string property = "content";
            string indexProperty = "content_index";
            if (type.StartsWith("response.reasoning_summary", StringComparison.Ordinal))
            {
                property = "summary";
                indexProperty = "summary_index";
            }

            int index = Index(payload, indexProperty);
            if (!payload.TryGetProperty("part", out JsonElement part) || part.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            Parts(item, property, index)[index] = JsonNode.Parse(part.GetRawText());
            HasData = true;
            return null;
        }

        if (type is "response.function_call_arguments.delta" or "response.function_call_arguments.done")
        {
            JsonObject item = Item(payload);
            if (type.EndsWith(".done", StringComparison.Ordinal))
            {
                item["arguments"] = RequiredString(payload, "arguments");
            }
            else
            {
                item["arguments"] = NodeString(item, "arguments") + RequiredString(payload, "delta");
            }

            HasData = true;
            return null;
        }

        if (type is "response.output_text.delta" or "response.output_text.done"
            or "response.refusal.delta" or "response.refusal.done"
            or "response.reasoning_text.delta" or "response.reasoning_text.done"
            or "response.reasoning_summary_text.delta" or "response.reasoning_summary_text.done")
        {
            string partsProperty = "content";
            string indexProperty = "content_index";
            if (type.StartsWith("response.reasoning_summary", StringComparison.Ordinal))
            {
                partsProperty = "summary";
                indexProperty = "summary_index";
            }

            int index = Index(payload, indexProperty);
            JsonArray parts = Parts(Item(payload), partsProperty, index);
            JsonObject part = parts[index] as JsonObject ?? throw new JsonException();
            string property = "text";
            if (type.StartsWith("response.refusal", StringComparison.Ordinal))
            {
                property = "refusal";
            }

            bool done = type.EndsWith(".done", StringComparison.Ordinal);
            if (done)
            {
                part[property] = RequiredString(payload, property);
            }
            else
            {
                string value = RequiredString(payload, "delta");
                part[property] = NodeString(part, property) + value;
                HasData = true;
                if (type == "response.output_text.delta")
                {
                    return new(value, null);
                }
            }

            HasData = true;
        }

        return null;
    }

    /// <summary>Создаёт независимый отчёт, сохраняя terminal envelope без локального переписывания.</summary>
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

    /// <summary>Отмена не подменяет уже полученный explicit failure.</summary>
    internal ModelResponse Cancel()
    {
        if (failure is null)
        {
            status = ModelResponseStatus.Canceled;
        }

        return Report();
    }

    /// <summary>Отказ чтения сохраняет полученный partial output.</summary>
    internal ModelResponse Fail(ServiceError error)
    {
        failure ??= error;
        status = ModelResponseStatus.Failed;
        return Report();
    }

    /// <summary>Копирует полный canonical объект.</summary>
    private void SetItem(int index, JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        items[index] = JsonNode.Parse(item.GetRawText())!.AsObject();
    }

    /// <summary>Delta должна ссылаться на существующий item.</summary>
    private JsonObject Item(JsonElement payload)
    {
        int index = Index(payload, "output_index");
        if (!items.TryGetValue(index, out JsonObject? item))
        {
            throw new JsonException();
        }

        string? id = String(payload, "item_id");
        if (id is not null && id != NodeString(item, "id"))
        {
            throw new JsonException();
        }

        return item;
    }

    /// <summary>Не восстанавливает отсутствующие промежуточные части.</summary>
    private static JsonArray Parts(JsonObject item, string property, int index)
    {
        JsonArray parts;
        if (item[property] is null)
        {
            parts = new();
            item[property] = parts;
        }
        else
        {
            parts = item[property] as JsonArray ?? throw new JsonException();
        }

        if (index > parts.Count)
        {
            throw new JsonException();
        }

        if (index == parts.Count)
        {
            parts.Add(new JsonObject());
        }

        return parts;
    }

    /// <summary>Требует неотрицательный protocol index.</summary>
    private static int Index(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int index) && index >= 0)
        {
            return index;
        }

        throw new JsonException();
    }

    /// <summary>Читает строку либо отсутствие поля.</summary>
    private static string? String(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    /// <summary>Требует строку известного события.</summary>
    private static string RequiredString(JsonElement payload, string name)
    {
        return String(payload, name) ?? throw new JsonException();
    }

    /// <summary>Читает накопленную строку без преобразования других JSON типов.</summary>
    private static string NodeString(JsonObject item, string property)
    {
        if (item[property] is null)
        {
            return "";
        }

        if (item[property] is JsonValue value && value.TryGetValue(out string? text))
        {
            return text!;
        }

        throw new JsonException();
    }
}
