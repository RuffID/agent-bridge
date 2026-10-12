namespace AgentBridge.Application.Results;

/// <summary>Безопасный отказ до неограниченной загрузки; canonical state не изменяется чтением.</summary>
/// <remarks>Write-сценарий откатывает transaction; это не свидетельство успешной finalization или Ready.
/// Не содержит payload, идентификаторов, размеров чужого диалога или provider error.</remarks>
public class DialogHistoryLoadLimitException : InvalidOperationException
{
    /// <summary>Создаёт фиксированный машинно распознаваемый отказ без чувствительных данных.</summary>
    public DialogHistoryLoadLimitException() : base("history_load_limit_exceeded") { }

    /// <summary>Semantic outcome для binary consumer; новый чат создаёт только явное действие пользователя.</summary>
    public ServiceError Error => new(ServiceErrorType.Rejected, "history_load_limit_exceeded");
}
