namespace AgentBridge.Domain.Dialogs;

/// <summary>Неизменяемое состояние обращения, создаваемое и заменяемое только корнем диалога.</summary>
public class DialogTurn
{
    /// <summary>Создаёт дочернее состояние внутри границы агрегата.</summary>
    internal DialogTurn(Guid id, long sequence, DateTimeOffset startedAtUtc, DialogTurnStatus status, DateTimeOffset? finishedAtUtc)
    {
        Id = id;
        Sequence = sequence;
        StartedAtUtc = startedAtUtc;
        Status = status;
        FinishedAtUtc = finishedAtUtc;
    }

    /// <summary>Идентичность обращения внутри диалога.</summary>
    public Guid Id { get; }
    /// <summary>Порядок начала обращения, не зависящий от времени завершения.</summary>
    public long Sequence { get; }
    /// <summary>Время начала в UTC.</summary>
    public DateTimeOffset StartedAtUtc { get; }
    /// <summary>Текущее состояние на момент создания этого снимка.</summary>
    public DialogTurnStatus Status { get; }
    /// <summary>Время конечного состояния; отсутствует у выполняющегося обращения.</summary>
    public DateTimeOffset? FinishedAtUtc { get; }
}
