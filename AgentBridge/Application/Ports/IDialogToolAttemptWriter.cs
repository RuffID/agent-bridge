using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Короткие атомарные записи журнала инструментов с owner/expiry/incarnation/revision guards.</summary>
public interface IDialogToolAttemptWriter
{
    /// <summary>Epoch-aware checkpoint до handler.</summary>
    Task<ServiceResult<DialogWriteToken>> StartAsync(DialogRunWriteAccess access, DialogWriteToken expected,
        ToolExecutionIdentity identity, CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new(ServiceErrorType.Unsupported, "run_fencing_unsupported")));
    /// <summary>Epoch-aware atomic outcomes/outputs.</summary>
    Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(DialogRunWriteAccess access, DialogWriteToken expected,
        Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new(ServiceErrorType.Unsupported, "run_fencing_unsupported")));
    /// <summary>Сохраняет Started до handler; повтор identity/позиции запрещён даже после restart.</summary>
    Task<ServiceResult<DialogWriteToken>> StartAsync(DialogAccess access, DialogWriteToken expected,
        ToolExecutionIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Атомарно сохраняет outcomes и confirmed canonical outputs; Unknown не получает output.</summary>
    Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, Guid stepId, ToolExecutionBatch batch, CancellationToken cancellationToken = default);
}
