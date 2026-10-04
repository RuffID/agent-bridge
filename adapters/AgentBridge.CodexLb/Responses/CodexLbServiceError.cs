using AgentBridge.Application.Results;

namespace AgentBridge.CodexLb.Responses;

/// <summary>Безопасный semantic отказ с транспортными метаданными только адаптера, без raw сообщения сервера.</summary>
public class CodexLbServiceError : ServiceError
{
    /// <summary>Создаёт только проекцию, отфильтрованную транспортным reader.</summary>
    internal CodexLbServiceError(ServiceErrorType type, int? status, string? apiType, string? code, string? param)
        : base(type, "Сервер отклонил запрос модели.")
    {
        HttpStatus = status;
        ApiType = apiType;
        Code = code;
        Param = param;
    }

    /// <summary>HTTP-статус, если отказ получен по HTTP; не является прикладным типом ошибки.</summary>
    public int? HttpStatus { get; }
    /// <summary>Известный безопасный type либо null; неизвестные значения не публикуются.</summary>
    public string? ApiType { get; }
    /// <summary>Известный безопасный code либо null.</summary>
    public string? Code { get; }
    /// <summary>Известный безопасный param либо null; динамические имена и индексы не публикуются.</summary>
    public string? Param { get; }
}
