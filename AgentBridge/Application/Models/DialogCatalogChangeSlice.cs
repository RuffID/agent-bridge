namespace AgentBridge.Application.Models;

/// <summary>Bounded feed без скрытого drain-loop; gaps возвращаются отдельным Unsupported.</summary>
public class DialogCatalogChangeSlice
{
    /// <summary>Копирует durable changes.</summary>
    public DialogCatalogChangeSlice(IEnumerable<DialogCatalogChange> changes, long nextCheckpoint)
    {
        Changes = ContractSnapshot.Copy(changes);
        NextCheckpoint = nextCheckpoint;
    }

    /// <summary>Ordered changes.</summary>
    public IReadOnlyList<DialogCatalogChange> Changes { get; }
    /// <summary>Последняя examined sequence.</summary>
    public long NextCheckpoint { get; }
}
