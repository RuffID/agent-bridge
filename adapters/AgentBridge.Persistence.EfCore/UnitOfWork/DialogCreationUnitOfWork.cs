using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class DialogCreationUnitOfWork(UnitOfWorkScope scope, DialogRecordQueries dialogs,
    RecordStaging<DialogRecord> staging) : IDialogCreator
{
    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> CreateAsync(DialogId dialogId, DialogOwnerId ownerId,
        DateTimeOffset createdAtUtc, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken = default)
    {
        Dialog dialog = Dialog.Create(dialogId, ownerId, createdAtUtc, expiresAtUtc);
        return scope.ExecuteAsync<DialogWriteToken>(async ct =>
        {
            if (await dialogs.FindAsync(dialogId.Value, ct) is not null)
            {
                return ServiceResult<DialogWriteToken>.Fail(new ServiceError(ServiceErrorType.Conflict, "Диалог уже существует."));
            }
            DialogRecord root = new()
            {
                Id = dialog.Id.Value, OwnerId = dialog.OwnerId.Value, IncarnationId = Guid.NewGuid(),
                // Legacy NOT NULL столбец хранит срок при создании; текущая политика вычисляется по CreatedAtUtc.
                CreatedAtUtc = dialog.CreatedAtUtc, ExpiresAtUtc = dialog.ExpiresAtUtc ?? DateTimeOffset.MaxValue,
                LastChangedAtUtc = dialog.LastChangedAtUtc, Revision = dialog.Revision
            };
            staging.StageCreate(root);
            return ServiceResult<DialogWriteToken>.Ok(new DialogWriteToken(dialog.Id, root.IncarnationId, root.Revision));
        }, cancellationToken, creatingDialog: true);
    }
}
