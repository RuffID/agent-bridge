using AgentBridge.Application.Results;

namespace AgentBridge.Application.Models;

/// <summary>Неизменяемый отчёт попытки; Unknown/NotStarted не имеют выдуманного canonical output.</summary>
public class ToolExecutionResult
{
    internal ToolExecutionResult(ToolExecutionIdentity identity, ToolInvocation invocation, ToolExecutionStatus status,
        CanonicalModelItem? output = null, ServiceError? error = null)
    {
        Identity = identity;
        Invocation = invocation;
        Status = status;
        Output = output;
        Error = error;
    }

    /// <summary>Идентичность для последующего журнала.</summary>
    public ToolExecutionIdentity Identity { get; }
    /// <summary>Исходный вызов; чувствительные аргументы не предназначены для логов.</summary>
    public ToolInvocation Invocation { get; }
    /// <summary>Подтверждённость исхода.</summary>
    public ToolExecutionStatus Status { get; }
    /// <summary>Canonical function_call_output только для подтверждённых исходов.</summary>
    public CanonicalModelItem? Output { get; }
    /// <summary>Безопасный semantic отказ без сообщения исключения/handler.</summary>
    public ServiceError? Error { get; }
}
