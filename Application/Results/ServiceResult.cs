namespace AgentBridge.Application.Results;

/// <summary>Результат команды без полезных данных; неожиданные исключения не превращаются в Fail.</summary>
public class ServiceResult
{
    private ServiceResult(bool success, ServiceError? error)
    {
        Success = success;
        Error = error;
    }

    /// <summary>Команда успешно выполнена.</summary>
    public bool Success { get; }
    /// <summary>Ожидаемый отказ только при неуспехе.</summary>
    public ServiceError? Error { get; }
    /// <summary>Создаёт успешный результат.</summary>
    public static ServiceResult Ok() => new(true, null);
    /// <summary>Передаёт ожидаемую ошибку целиком.</summary>
    public static ServiceResult Fail(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }
}
