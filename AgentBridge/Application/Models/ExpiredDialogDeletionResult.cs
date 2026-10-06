using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Безопасный immutable отчёт одного кандидата; token не является правом записи или удаления.</summary>
public class ExpiredDialogDeletionResult
{
    internal ExpiredDialogDeletionResult(DialogWriteToken token, ExpiredDialogDeletionStatus status,
        ServiceError? error = null)
    {
        Token = token;
        Status = status;
        Error = error;
    }

    /// <summary>Исходный token ограниченной выборки; не обновляется после отказа.</summary>
    public DialogWriteToken Token { get; }
    /// <summary>Подтверждённость удаления.</summary>
    public ExpiredDialogDeletionStatus Status { get; }
    /// <summary>Исходный ожидаемый semantic отказ; raw exceptions сюда не помещаются.</summary>
    public ServiceError? Error { get; }
}
