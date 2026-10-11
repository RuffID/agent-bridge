namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Append-only компактный change, без canonical history.</summary>
public class DialogCatalogChangeRecord
{
    /// <summary>Scope.</summary>
    public string ScopeKey { get; set; } = string.Empty;
    /// <summary>Ordered scope sequence.</summary>
    public long Sequence { get; set; }
    /// <summary>Событие.</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>Компактная проекция либо tombstone.</summary>
    public string StateJson { get; set; } = string.Empty;
}
