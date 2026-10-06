namespace AgentBridge.Application.Results;

/// <summary>Смысл ожидаемого отказа без привязки к HTTP-статусу.</summary>
public enum ServiceErrorType
{
    /// <summary>Некорректные входные данные.</summary>
    Validation,
    /// <summary>Требуется аутентификация.</summary>
    Unauthorized,
    /// <summary>Операция запрещена владельцу запроса.</summary>
    Forbidden,
    /// <summary>Запрошенные данные отсутствуют.</summary>
    NotFound,
    /// <summary>Состояние изменилось или операция уже завершена.</summary>
    Conflict,
    /// <summary>Срок доступности истёк.</summary>
    Expired,
    /// <summary>Возможность или кодировка модели не поддерживается.</summary>
    Unsupported,
    /// <summary>Получен явный отказ внешней модели или инструмента.</summary>
    Rejected,
    /// <summary>Истёк бюджет времени, отличный от отмены вызывающим кодом.</summary>
    Timeout
}
