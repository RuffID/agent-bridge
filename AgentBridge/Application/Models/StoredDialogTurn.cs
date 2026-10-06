using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Снимок сохранённого обращения для чтения, без выдачи изменяемого агрегата.</summary>
public class StoredDialogTurn
{
    /// <summary>Копирует каноническое содержимое обращения в сохранённом порядке.</summary>
    public StoredDialogTurn(Guid id, long sequence, DialogTurnStatus status, IEnumerable<CanonicalModelItem> items,
        IEnumerable<StoredModelStep> modelSteps, TurnModelSettings? settings = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Идентичность обращения обязательна.", nameof(id));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
        Id = id;
        Sequence = sequence;
        Status = status;
        Items = ContractSnapshot.Copy(items);
        ModelSteps = ContractSnapshot.Copy(modelSteps);
        Settings = settings;
    }

    /// <summary>Идентичность обращения.</summary>
    public Guid Id { get; }
    /// <summary>Порядок начала, независимый от порядка завершения.</summary>
    public long Sequence { get; }
    /// <summary>Текущее состояние; InProgress не входит в terminal prefix.</summary>
    public DialogTurnStatus Status { get; }
    /// <summary>Все сохранённые сообщения, output и результаты инструментов этого обращения.</summary>
    public IReadOnlyList<CanonicalModelItem> Items { get; }
    /// <summary>Результаты шагов в порядке выполнения с полными metadata/envelope, отдельно от input-items.</summary>
    public IReadOnlyList<StoredModelStep> ModelSteps { get; }
    /// <summary>Настройки начала обращения; null для legacy или primitive Begin без snapshot.</summary>
    public TurnModelSettings? Settings { get; }
}
