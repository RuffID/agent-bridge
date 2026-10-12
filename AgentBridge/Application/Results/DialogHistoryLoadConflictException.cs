namespace AgentBridge.Application.Results;

/// <summary>Изменение между bounded preflight и payload query; частичный снимок не возвращается и чтение не повторяется.</summary>
public class DialogHistoryLoadConflictException : InvalidOperationException
{
    /// <summary>Создаёт безопасный отказ без содержимого и driver metadata.</summary>
    public DialogHistoryLoadConflictException() : base("history_load_changed_during_query") { }
}
