using AgentBridge.Application.Results;
using AgentBridge.Application.Ports;

namespace AgentBridge.Application.Models;

/// <summary>Ограниченная session-memory защита; создание новой сессии и restart требуют durable recovery этапа20.</summary>
public class ToolExecutionSession
{
    private readonly object _gate = new();
    private readonly HashSet<Guid> _attemptedSteps = [];
    private readonly HashSet<string> _selectedNames;
    private readonly long _startedAt;
    private bool _running;
    private bool _stopped;
    private ToolExecutionBatch? _lastResult;

    /// <summary>Фиксирует принадлежность и limits; не создаёт timers, транзакций или бизнес-сервисов.</summary>
    internal ToolExecutionSession(ApplicationCallContext call, DialogWriteToken token, DateTimeOffset? expiresAtUtc,
        IEnumerable<string> selectedToolNames, ToolExecutionLimits limits, TimeProvider timeProvider, IToolExecutionCheckpoint? checkpoint)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(selectedToolNames);
        if (!token.DialogId.Equals(call.DialogId) || (expiresAtUtc is { } expiry && expiry.Offset != TimeSpan.Zero))
        {
            throw new ArgumentException("Требуются тот же диалог и срок в UTC.");
        }
        _selectedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in selectedToolNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!_selectedNames.Add(name)) throw new ArgumentException("Имена выбранных инструментов повторяются.");
        }
        Call = call;
        IncarnationId = token.IncarnationId;
        ExpiresAtUtc = expiresAtUtc;
        Limits = limits;
        TimeProvider = timeProvider;
        Checkpoint = checkpoint;
        _startedAt = timeProvider.GetTimestamp();
    }

    /// <summary>Принадлежность, которую handler получает без замены другим caller.</summary>
    public ApplicationCallContext Call { get; }
    /// <summary>Идентичность жизни исходного диалога.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Фиксированный срок, не продлеваемый шагами.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; }
    /// <summary>Неизменяемые ограничения сессии.</summary>
    public ToolExecutionLimits Limits { get; }
    /// <summary>Последний полный отчёт, включая исходы перед исключением/отменой; не логировать аргументы/outputs.</summary>
    public ToolExecutionBatch? LastResult { get { lock (_gate) return _lastResult; } }

    internal TimeProvider TimeProvider { get; }
    internal IToolExecutionCheckpoint? Checkpoint { get; }
    /// <summary>Проверяет exact выбор приложения без расширения разрешений.</summary>
    internal bool IsSelected(string name) => _selectedNames.Contains(name);
    internal TimeSpan Remaining => Limits.Timeout - TimeProvider.GetElapsedTime(_startedAt);
    /// <summary>Проверяет fixed expiry и общий monotonic бюджет.</summary>
    internal ServiceError? CheckTime()
    {
        if (ExpiresAtUtc is { } expiry && TimeProvider.GetUtcNow() >= expiry) return new(ServiceErrorType.Expired, "Срок диалога истёк.");
        return Remaining <= TimeSpan.Zero ? new(ServiceErrorType.Timeout, "Истёк срок выполнения инструментов.") : null;
    }

    /// <summary>Атомарно резервирует один шаг только в памяти сессии.</summary>
    internal ServiceError? Enter(Guid stepId, int count)
    {
        lock (_gate)
        {
            if (_running || _stopped || _attemptedSteps.Contains(stepId))
                return new(ServiceErrorType.Conflict, "Сессия занята, остановлена или шаг уже выполнялся.");
            ServiceError? timeError = CheckTime();
            if (timeError is not null) return timeError;
            if (count > Limits.MaxCallsPerStep || (count > 0 && _attemptedSteps.Count >= Limits.MaxSteps))
                return new(ServiceErrorType.Rejected, "Превышено ограничение инструментов.");
            _running = true;
            if (count > 0) _attemptedSteps.Add(stepId);
            return null;
        }
    }

    /// <summary>Публикует независимый отчёт и запрещает дальнейшие действия при неполном исходе.</summary>
    internal void Finish(ToolExecutionBatch result)
    {
        lock (_gate)
        {
            _lastResult = result;
            _stopped |= !result.CanContinue;
            _running = false;
        }
    }
}
