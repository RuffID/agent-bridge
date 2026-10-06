namespace AgentBridge.Application.Results;

/// <summary>Результат с ненулевыми данными при успехе и без данных при ожидаемом отказе.</summary>
public class ServiceResult<T> where T : class
{
    private ServiceResult(bool success, T? data, ServiceError? error)
    {
        Success = success;
        Data = data;
        Error = error;
    }

    /// <summary>Данные получены; lifecycle модели проверяется отдельно в ModelResponse.</summary>
    public bool Success { get; }
    /// <summary>Ненулевые данные только при успехе.</summary>
    public T? Data { get; }
    /// <summary>Ожидаемый отказ только при неуспехе.</summary>
    public ServiceError? Error { get; }
    /// <summary>Создаёт успех с обязательными данными.</summary>
    public static ServiceResult<T> Ok(T data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new(true, data, null);
    }
    /// <summary>Передаёт ошибку без копирования и без данных.</summary>
    public static ServiceResult<T> Fail(ServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, null, error);
    }
}
