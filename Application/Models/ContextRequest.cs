namespace AgentBridge.Application.Models;

/// <summary>Вход зарегистрированного источника бизнес-контекста приложения.</summary>
public class ContextRequest
{
    /// <summary>Копирует новое сообщение и фиксирует идентичности для проверки прав.</summary>
    public ContextRequest(ApplicationCallContext call, IEnumerable<CanonicalModelItem> newInput)
    {
        ArgumentNullException.ThrowIfNull(call);
        Call = call;
        NewInput = ContractSnapshot.Copy(newInput);
    }

    /// <summary>Идентичности вызова; права проверяет поставщик приложения.</summary>
    public ApplicationCallContext Call { get; }
    /// <summary>Новый вход без автоматического раскрытия всей истории приложению.</summary>
    public IReadOnlyList<CanonicalModelItem> NewInput { get; }
}
