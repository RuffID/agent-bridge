using Microsoft.Extensions.Logging;

namespace AgentBridge.Diagnostics;

/// <summary>Создаёт наблюдения операций, направляя безопасные события через ILogger приложения.</summary>
/// <remarks>Не выполняет сценарии, не создаёт таймеры и не управляет отменой или исключениями.</remarks>
public class AgentBridgeDiagnostics
{
    private readonly ILogger<AgentBridgeDiagnostics> _logger;

    /// <summary>Получает типизированный logger из pipeline приложения.</summary>
    public AgentBridgeDiagnostics(ILogger<AgentBridgeDiagnostics> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Начинает измерение операции с безопасным идентификатором корреляции.</summary>
    /// <param name="operation">Имя из закрытого набора; текст, URL и настройки не принимаются.</param>
    /// <param name="correlationId">Непустой GUID корреляции, общий для связанных операций.</param>
    /// <param name="callerCancellation">Исходный токен вызывающего кода, без локального deadline.</param>
    /// <param name="deadlineCancellation">Отдельный токен локального deadline, не объединённый с caller-токеном.</param>
    /// <returns>Наблюдение, которое владелец сценария явно завершает через Complete или Fail.</returns>
    public AgentBridgeDiagnosticOperation BeginOperation(
        AgentBridgeOperation operation,
        Guid correlationId,
        CancellationToken callerCancellation = default,
        CancellationToken deadlineCancellation = default)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Нужен непустой GUID корреляции.", nameof(correlationId));
        }

        if (callerCancellation.CanBeCanceled && callerCancellation == deadlineCancellation)
        {
            throw new ArgumentException("Caller и локальный deadline требуют отдельных исходных токенов.", nameof(deadlineCancellation));
        }

        return new AgentBridgeDiagnosticOperation(_logger, operation, correlationId, callerCancellation, deadlineCancellation);
    }
}
