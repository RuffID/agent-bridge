namespace AgentBridge.Application.Models;

/// <summary>Снимок принятого канонического окна и метаданных terminal prefix.</summary>
public class StoredDialogContext
{
    /// <summary>Копирует полное окно; допустимость покрытия проверяется сценарием изменения, а не DTO.</summary>
    public StoredDialogContext(long version, long throughTurnSequence, ModelResponse compaction, string? selectedModel = null)
        : this(version, throughTurnSequence, compaction, selectedModel, 0)
    {
    }

    /// <summary>Фиксирует recovery revision сохраняемого модельного контекста.</summary>
    public StoredDialogContext(long version, long throughTurnSequence, ModelResponse compaction, string? selectedModel, long recoveryRevision)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentOutOfRangeException.ThrowIfNegative(throughTurnSequence);
        ArgumentNullException.ThrowIfNull(compaction);
        Version = version;
        ThroughTurnSequence = throughTurnSequence;
        Compaction = compaction;
        SelectedModel = selectedModel;
        ArgumentOutOfRangeException.ThrowIfNegative(recoveryRevision);
        RecoveryRevision = recoveryRevision;
    }

    /// <summary>Версия принятого рабочего окна.</summary>
    public long Version { get; }
    /// <summary>Непрерывный terminal prefix, включая 0; не является границей отдельных Responses items.</summary>
    public long ThroughTurnSequence { get; }
    /// <summary>Полное окно, включая opaque-состояние.</summary>
    public IReadOnlyList<CanonicalModelItem> Items => Compaction.Output;
    /// <summary>Принятый результат compact с полным envelope и метаданными продолжения, отдельно от окна input.</summary>
    public ModelResponse Compaction { get; }
    /// <summary>Зафиксированный выбор compact; null когда provenance не сохранялся.</summary>
    public string? SelectedModel { get; }
    /// <summary>Revision recovery, учтённая в этом окне.</summary>
    public long RecoveryRevision { get; }
}
