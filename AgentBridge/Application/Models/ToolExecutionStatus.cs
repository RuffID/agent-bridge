namespace AgentBridge.Application.Models;

/// <summary>Достоверность исхода одного вызова инструмента.</summary>
public enum ToolExecutionStatus
{
    /// <summary>Действие завершено и полный результат получен.</summary>
    Succeeded,
    /// <summary>Подтверждённый отказ, представленный явным error output.</summary>
    Rejected,
    /// <summary>Handler начат, достоверного результата нет; повтор запрещён.</summary>
    Unknown,
    /// <summary>Действие не начиналось из-за остановки сессии.</summary>
    NotStarted
}
