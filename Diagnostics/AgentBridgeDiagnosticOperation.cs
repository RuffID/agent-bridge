using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AgentBridge.Diagnostics;

/// <summary>Одно наблюдение операции с монотонным измерением длительности и единственным итоговым событием.</summary>
/// <remarks>Не является scope логирования: не переносит произвольные данные в ambient scope и не выводит исключения.</remarks>
public class AgentBridgeDiagnosticOperation
{
    private const int COMPLETED_EVENT_ID = 4100;
    private readonly ILogger<AgentBridgeDiagnostics> _logger;
    private readonly AgentBridgeOperation _operation;
    private readonly Guid _correlationId;
    private readonly Guid _operationId = Guid.NewGuid();
    private readonly CancellationToken _callerCancellation;
    private readonly CancellationToken _deadlineCancellation;
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private int _completed;

    /// <summary>Сохраняет только закрытые метаданные и исходные токены для классификации отмены.</summary>
    internal AgentBridgeDiagnosticOperation(
        ILogger<AgentBridgeDiagnostics> logger,
        AgentBridgeOperation operation,
        Guid correlationId,
        CancellationToken callerCancellation,
        CancellationToken deadlineCancellation)
    {
        _logger = logger;
        _operation = operation;
        _correlationId = correlationId;
        _callerCancellation = callerCancellation;
        _deadlineCancellation = deadlineCancellation;
    }

    /// <summary>Фиксирует подтверждённый владельцем успешный результат.</summary>
    /// <remarks>Поздняя отмена токена не меняет уже полученный успешный результат.</remarks>
    public void Complete() => Finish(LogLevel.Information, "Succeeded", "None");

    /// <summary>Наблюдает ошибку, не передавая исключение logger и не меняя его для вызывающего кода.</summary>
    /// <remarks>
    /// Для OperationCanceledException caller имеет приоритет; отдельный сработавший deadline даёт DeadlineExceeded.
    /// Без сработавших исходных токенов отмена считается неатрибутированной ошибкой, а не таймаутом.
    /// Владелец сценария сам возвращает или повторно выбрасывает исходное исключение.
    /// </remarks>
    public void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is OperationCanceledException)
        {
            if (_callerCancellation.IsCancellationRequested)
            {
                Finish(LogLevel.Information, "Canceled", "CallerCanceled");
                return;
            }

            if (_deadlineCancellation.IsCancellationRequested)
            {
                Finish(LogLevel.Warning, "DeadlineExceeded", "DeadlineExceeded");
                return;
            }

            Finish(LogLevel.Error, "Failed", "UnattributedCancellation");
            return;
        }

        Finish(LogLevel.Error, "Failed", "UnexpectedFailure");
    }

    /// <summary>Записывает одно событие, отклоняя повторное завершение без утечки исходных данных.</summary>
    private void Finish(LogLevel level, string status, string errorCode)
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
        {
            throw new InvalidOperationException("Наблюдение операции уже завершено.");
        }

        double durationMs = Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;
        _logger.Log(level, new EventId(COMPLETED_EVENT_ID, "AgentBridgeOperationCompleted"),
            "AgentBridge operation {Operation} ({OperationId}), correlation {CorrelationId}: {Status}, code {ErrorCode}, duration {DurationMs} ms",
            _operation.ToString(), _operationId, _correlationId, status, errorCode, durationMs);
    }
}
