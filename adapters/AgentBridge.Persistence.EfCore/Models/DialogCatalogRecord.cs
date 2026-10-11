namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Компактный library-owned индекс; tombstone сохраняет ID навсегда, без FK каскада root.</summary>
public class DialogCatalogRecord
{
    /// <summary>Глобальный dialog ID.</summary>
    public Guid Id { get; set; }
    /// <summary>Ordinal hash namespace; actual значения дополнительно сверяются.</summary>
    public string ScopeKey { get; set; } = string.Empty;
    /// <summary>RFC UUID bytes hex для одинакового provider/cursor порядка.</summary>
    public string IdSortKey { get; set; } = string.Empty;
    /// <summary>Owner ordinal.</summary>
    public string OwnerId { get; set; } = string.Empty;
    /// <summary>Сайт.</summary>
    public string SiteId { get; set; } = string.Empty;
    /// <summary>Агент.</summary>
    public string AgentId { get; set; } = string.Empty;
    /// <summary>Жизнь.</summary>
    public Guid IncarnationId { get; set; }
    /// <summary>Root revision.</summary>
    public long RootRevision { get; set; }
    /// <summary>Projection CAS.</summary>
    public long Revision { get; set; }
    /// <summary>Создание.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>Фиксированный nullable срок.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    /// <summary>Последнее saved сообщение.</summary>
    public DateTimeOffset? LastMessageAtUtc { get; set; }
    /// <summary>Keyset sort.</summary>
    public DateTimeOffset SortTimeUtc { get; set; }
    /// <summary>Первый saved user position.</summary>
    public string? FirstQuestionPosition { get; set; }
    /// <summary>Последний saved user/assistant position.</summary>
    public string? LastMessagePosition { get; set; }
    /// <summary>Title.</summary>
    public string Title { get; set; } = "Новый чат";
    /// <summary>Title-only search key.</summary>
    public string SearchKey { get; set; } = "НОВЫЙ ЧАТ";
    /// <summary>Разрешённый текст; пусто кодирует отсутствие.</summary>
    public string Snippet { get; set; } = string.Empty;
    /// <summary>Opaque immutable profile.</summary>
    public string ProfileJson { get; set; } = string.Empty;
    /// <summary>Policy snapshot.</summary>
    public string PolicyJson { get; set; } = string.Empty;
    /// <summary>Tombstone.</summary>
    public bool Deleted { get; set; }
    /// <summary>Durable readiness.</summary>
    public int Readiness { get; set; }
    /// <summary>Epoch.</summary>
    public long Epoch { get; set; }
    /// <summary>Recovery revision.</summary>
    public long RecoveryRevision { get; set; }
    /// <summary>Последний original turn.</summary>
    public Guid? LastTurnId { get; set; }
    /// <summary>Original status; recovery не превращает его в Completed.</summary>
    public int? LastTurnStatus { get; set; }
}
