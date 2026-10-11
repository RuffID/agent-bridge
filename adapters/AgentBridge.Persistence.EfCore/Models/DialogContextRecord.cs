namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Принятый compact; активным считается максимальная Version, прежние состояния не удаляются.</summary>
public class DialogContextRecord
{
    /// <summary>Recovery revision модели, из которой принято окно.</summary>
    public long RecoveryRevision { get; set; }
    /// <summary>Выбранная модель compact; null при неизвестном provenance.</summary>
    public string? SelectedModel { get; set; }
    /// <summary>Обязательный диалог.</summary>
    public Guid DialogId { get; set; }
    /// <summary>Последовательная версия принятого состояния, начиная с единицы.</summary>
    public long Version { get; set; }
    /// <summary>Непрерывный terminal prefix, включая ноль; не cutoff канонических items.</summary>
    public long ThroughTurnSequence { get; set; }
    /// <summary>Время принятия в UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>Полный Completed-отчёт compact, включая окно и metadata, отдельно от исходной истории.</summary>
    public ModelResponseRecord Compaction { get; set; } = new();
}
