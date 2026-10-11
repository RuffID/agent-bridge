namespace AgentBridge.Application.Models;

/// <summary>Один bounded slice без total/count/full snapshots.</summary>
public class DialogCatalogSlice
{
    /// <summary>Копирует кандидатов; cursor указывает последний examined metadata key.</summary>
    public DialogCatalogSlice(IEnumerable<DialogCatalogState> candidates, DialogCatalogCursor? next)
    {
        Candidates = ContractSnapshot.Copy(candidates);
        Next = next;
    }

    /// <summary>Компактные кандидаты для полного host gate.</summary>
    public IReadOnlyList<DialogCatalogState> Candidates { get; }
    /// <summary>Прогресс, включая короткую/отфильтрованную host страницу.</summary>
    public DialogCatalogCursor? Next { get; }
}
