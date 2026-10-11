namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Полный канонический item истории, включая сообщения, reasoning, вызовы и результаты инструментов.</summary>
public class CanonicalItemRecord
{
    /// <summary>Server UTC принимаемого user/assistant сообщения; null для legacy/tools/reasoning.</summary>
    public DateTimeOffset? SavedAtUtc { get; set; }
    /// <summary>Диалог родительского обращения.</summary>
    public Guid DialogId { get; set; }
    /// <summary>Обращение; составной FK исключает связь с другим диалогом.</summary>
    public Guid TurnId { get; set; }
    /// <summary>Стабильный порядок item внутри обращения, начиная с единицы; не время события.</summary>
    public long Sequence { get; set; }
    /// <summary>Полный JSON-объект, включая call_id/opaque/неизвестные поля; не предназначен для логов.</summary>
    public string ContentJson { get; set; } = string.Empty;
}
