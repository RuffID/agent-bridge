namespace AgentBridge.Persistence.EfCore.Models;

/// <summary>Append-only durable operation: recovery либо fence, idempotency/result/provenance.</summary>
public class DialogRecoveryOperationRecord
{
    /// <summary>Диалог.</summary>
    public Guid DialogId { get; set; }
    /// <summary>OperationId.</summary>
    public Guid Id { get; set; }
    /// <summary>Жизнь.</summary>
    public Guid IncarnationId { get; set; }
    /// <summary>Recovery revision.</summary>
    public long Revision { get; set; }
    /// <summary>recovery/fence.</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>Digest полного immutable command payload.</summary>
    public string PayloadHash { get; set; } = string.Empty;
    /// <summary>Durable result на момент commit.</summary>
    public string ResultJson { get; set; } = string.Empty;
    /// <summary>Resolved positions и confirmed output binding, отдельно от originals.</summary>
    public string PairsJson { get; set; } = "[]";
}
