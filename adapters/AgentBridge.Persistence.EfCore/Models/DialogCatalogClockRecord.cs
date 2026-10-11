namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Serializable scope feed clock; concurrent writers проверяют CAS вместе с root.</summary>
public class DialogCatalogClockRecord
{
    /// <summary>Scope hash.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Последняя committed sequence.</summary>
    public long Sequence { get; set; }
}
