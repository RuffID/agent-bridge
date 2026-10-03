namespace AgentBridge.Domain.Dialogs;

/// <summary>Неизменяемая версия рабочего контекста и покрываемый ею префикс завершённых обращений.</summary>
/// <remarks>Содержимое и cutoff отдельных событий Responses относятся к последующим этапам.</remarks>
public class DialogContextState
{
    /// <summary>Создаёт метаданные контекста внутри границы агрегата.</summary>
    internal DialogContextState(long version, long throughTurnSequence, DateTimeOffset createdAtUtc)
    {
        Version = version;
        ThroughTurnSequence = throughTurnSequence;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Последовательная версия контекста, начиная с единицы.</summary>
    public long Version { get; }
    /// <summary>Последнее покрытое обращение с конечным статусом; ноль означает пустой префикс.</summary>
    public long ThroughTurnSequence { get; }
    /// <summary>Время принятия этой версии в UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; }
}
