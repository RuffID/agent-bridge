namespace AgentBridge.Application.Models;

/// <summary>Provenance одной части opaque-контекста: selected и server model не приравниваются.</summary>
public class ContextModelSource(string? selectedModel, string? serverModel, IEnumerable<CanonicalModelItem> items)
{
    /// <summary>Зафиксированный выбор источника; null для legacy/неизвестного.</summary>
    public string? SelectedModel { get; } = selectedModel;
    /// <summary>Фактическое имя из envelope; null при отсутствии метаданных.</summary>
    public string? ServerModel { get; } = serverModel;
    /// <summary>Исходные canonical элементы; чувствительны и не предназначены для UI/лога.</summary>
    public IReadOnlyList<CanonicalModelItem> Items { get; } = ContractSnapshot.Copy(items);
}
