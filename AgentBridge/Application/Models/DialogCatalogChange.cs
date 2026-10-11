namespace AgentBridge.Application.Models;

/// <summary>Ordered durable change; projection либо доминирующий tombstone той же incarnation.</summary>
/// <param name="Sequence">Scope sequence.</param>
/// <param name="Kind">create/message/run/recovery/delete/expiry.</param>
/// <param name="State">Компактный authoritative snapshot на момент commit.</param>
public record DialogCatalogChange(long Sequence, string Kind, DialogCatalogState State);
