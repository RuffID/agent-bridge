namespace AgentBridge.Application.Models;

/// <summary>Разрешённые бизнес-данные источника, передаваемые сборщику контекста без готового HTTP payload.</summary>
public class ContextContribution
{
    /// <summary>Фиксирует полный упорядоченный вклад; пустой вклад является допустимым результатом.</summary>
    public ContextContribution(IEnumerable<CanonicalModelItem> items) => Items = ContractSnapshot.Copy(items);

    /// <summary>Неизменяемые данные; роли и включение в контекст проверяет будущий сборщик.</summary>
    public IReadOnlyList<CanonicalModelItem> Items { get; }
}
