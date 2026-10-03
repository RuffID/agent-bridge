namespace AgentBridge.Application.Results;

/// <summary>Ожидаемая прикладная ошибка; не предназначена для хранения raw HTTP или исключения.</summary>
public class ServiceError
{
    /// <summary>Создаёт ошибку с семантическим типом и безопасным описанием.</summary>
    public ServiceError(ServiceErrorType type, string message)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Type = type;
        Message = message;
    }

    /// <summary>Тип отказа, независимо от транспорта.</summary>
    public ServiceErrorType Type { get; }
    /// <summary>Описание без секретов и исходного driver/upstream message; обеспечивает вызывающий код.</summary>
    public string Message { get; }
}
