using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Общий формат полного отчёта модели, размещаемый в колонках шага или compact без EF-зависимостей ядра.</summary>
public class ModelResponseRecord
{
    /// <summary>Версия формата хранения; неизвестная версия отклоняется, а не читается частично.</summary>
    public int FormatVersion { get; set; } = 1;
    /// <summary>Явный lifecycle, независимо от наличия текста.</summary>
    public ModelResponseStatus Status { get; set; }
    /// <summary>Полный известный output в порядке модели, включая неполный/ошибочный/отменённый результат.</summary>
    public string OutputJson { get; set; } = "[]";
    /// <summary>Полный envelope либо отсутствие полного объекта; не input-item.</summary>
    public string? EnvelopeJson { get; set; }
    /// <summary>Непрозрачное продолжение для того же upstream-владения, без ModelAccess/API key.</summary>
    public string? ContinuationJson { get; set; }
    /// <summary>Семантическая ошибка только при Failed.</summary>
    public ServiceErrorType? ErrorType { get; set; }
    /// <summary>Безопасное описание ожидаемого отказа; raw driver/upstream сообщения сюда не передаются.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Копирует полный прикладной отчёт без потери неизвестных полей; не выполняет запись в БД.</summary>
    public static ModelResponseRecord FromModelResponse(ModelResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new ModelResponseRecord
        {
            Status = response.Status,
            OutputJson = JsonSerializer.Serialize(response.Output.Select(item => item.Content)),
            EnvelopeJson = response.Envelope?.Content.GetRawText(),
            ContinuationJson = response.Continuation?.Content.GetRawText(),
            ErrorType = response.Error?.Type,
            ErrorMessage = response.Error?.Message
        };
    }

    /// <summary>Восстанавливает независимый полный отчёт; повреждённые данные отклоняет без fallback.</summary>
    public ModelResponse ToModelResponse()
    {
        if (FormatVersion != 1 || !Enum.IsDefined(Status))
        {
            throw new InvalidOperationException("Неподдержанный формат отчёта модели.");
        }
        if ((Status == ModelResponseStatus.Failed && (ErrorType is null || string.IsNullOrWhiteSpace(ErrorMessage))) ||
            (Status != ModelResponseStatus.Failed && (ErrorType is not null || ErrorMessage is not null)))
        {
            throw new InvalidOperationException("Ошибка не соответствует lifecycle отчёта.");
        }

        using JsonDocument output = JsonDocument.Parse(OutputJson);
        if (output.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Output отчёта должен быть массивом.");
        }
        List<CanonicalModelItem> items = [];
        foreach (JsonElement item in output.RootElement.EnumerateArray())
        {
            items.Add(new CanonicalModelItem(item));
        }
        CanonicalModelEnvelope? envelope = EnvelopeJson is null ? null : new CanonicalModelEnvelope(ReadObject(EnvelopeJson));
        ModelContinuation? continuation = ContinuationJson is null ? null : new ModelContinuation(ReadObject(ContinuationJson));
        return Status switch
        {
            ModelResponseStatus.Completed => ModelResponse.Completed(items, envelope, continuation),
            ModelResponseStatus.Incomplete => ModelResponse.Incomplete(items, envelope, continuation),
            ModelResponseStatus.Canceled => ModelResponse.Canceled(items, envelope, continuation),
            ModelResponseStatus.Failed when ErrorType is ServiceErrorType type && ErrorMessage is string message =>
                ModelResponse.Failed(items, new ServiceError(type, message), envelope, continuation),
            _ => throw new InvalidOperationException("Неподдержанный lifecycle отчёта.")
        };
    }

    /// <summary>Копирует JSON-объект независимо от документа; форму проверяет прикладной контейнер.</summary>
    private static JsonElement ReadObject(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
