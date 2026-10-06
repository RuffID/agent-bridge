using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Короткие атомарные записи журнала инструментов с owner/expiry/incarnation/revision guards.</summary>
public interface IDialogToolAttemptWriter
{
    /// <summary>Сохраняет Started до handler; повтор identity/позиции запрещён даже после restart.</summary>
    Task<ServiceResult<DialogWriteToken>> StartAsync(DialogAccess access, DialogWriteToken expected,
        ToolExecutionIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Атомарно сохраняет outcomes и confirmed canonical outputs; Unknown не получает output.</summary>
    Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken = default);
}
