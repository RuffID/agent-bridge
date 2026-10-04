using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Полный ordered отчёт шага, доступный и после частичного прерывания.</summary>
public class ToolExecutionBatch
{
    internal ToolExecutionBatch(IEnumerable<ToolExecutionResult> results, ServiceError? error)
    {
        Results = Array.AsReadOnly(results.ToArray());
        Outputs = Array.AsReadOnly(Results.Where(result => result.Output is not null).Select(result => result.Output!).ToArray());
        Error = error;
    }

    /// <summary>Попытки в исходном порядке output, независимо от порядка завершения tasks.</summary>
    public IReadOnlyList<ToolExecutionResult> Results { get; }
    /// <summary>Только подтверждённые canonical outputs для отдельной короткой записи приложением.</summary>
    public IReadOnlyList<CanonicalModelItem> Outputs { get; }
    /// <summary>Остановка всего шага, включая deadline/expiry; не содержит raw exceptions.</summary>
    public ServiceError? Error { get; }
    /// <summary>Все pending calls получили output; это не разрешение generation без ContextBudgetGuard.</summary>
    public bool CanContinue => Error is null && Results.All(result => result.Output is not null);
}
