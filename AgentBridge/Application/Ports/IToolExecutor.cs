using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Исполняет только completed function calls в ограниченной живой сессии без автоматических повторов.</summary>
public interface IToolExecutor
{
    /// <summary>Создаёт сессию с фиксированной принадлежностью, выбором имён и сроком; не подтверждает авторизацию.</summary>
    ToolExecutionSession CreateSession(ApplicationCallContext call, DialogWriteToken token, DateTimeOffset expiresAtUtc,
        IEnumerable<string> selectedToolNames, ToolExecutionLimits limits, IToolExecutionCheckpoint? checkpoint = null);
    /// <summary>Ожидает все начатые handlers; неожиданные исключения и caller cancellation распространяются после сохранения LastResult.</summary>
    Task<ServiceResult<ToolExecutionBatch>> ExecuteAsync(ToolExecutionSession session, StoredModelStep step,
        CancellationToken cancellationToken = default);
}
