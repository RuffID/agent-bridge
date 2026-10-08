using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Ports;

/// <summary>Короткий сценарий создания нового диалога без сетевого ожидания внутри транзакции.</summary>
public interface IDialogCreator
{
    /// <summary>Атомарно создаёт диалог с фиксированными датами и новым сохраняемым incarnation; существующий ID — Conflict.</summary>
    Task<ServiceResult<DialogWriteToken>> CreateAsync(DialogId dialogId, DialogOwnerId ownerId,
        DateTimeOffset createdAtUtc, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken = default);
}
