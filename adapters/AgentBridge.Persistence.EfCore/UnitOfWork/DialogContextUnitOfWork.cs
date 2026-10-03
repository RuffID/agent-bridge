using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class DialogContextUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogStateLoader stateLoader,
    RecordStaging<DialogRecord> dialogs, RecordStaging<DialogContextRecord> contexts) : IDialogContextWriter
{
    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compaction);
        if (compaction.Status != ModelResponseStatus.Completed)
        {
            return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new ServiceError(ServiceErrorType.Validation, "Compact должен быть завершён.")));
        }
        return scope.ExecuteAsync<DialogWriteToken>(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await guard.LoadAsync(access, expected, ct);
            if (!loaded.Success) { return ServiceResult<DialogWriteToken>.Fail(loaded.Error!); }
            DialogRecord root = loaded.Data!;
            Dialog dialog = await stateLoader.LoadAsync(root, ct);
            DialogMutationResult captured = dialog.TryCaptureVersion(access.OwnerId, access.NowUtc, out DialogStateVersion? version);
            if (captured != DialogMutationResult.Success)
            {
                return ServiceResult<DialogWriteToken>.Fail(DialogWriteResults.Error(captured));
            }
            DialogMutationResult mutation;
            try { mutation = dialog.TryApplyContext(access.OwnerId, version!, throughTurnSequence, access.NowUtc); }
            catch (ArgumentOutOfRangeException error) when (error.ParamName == nameof(throughTurnSequence))
            {
                return ServiceResult<DialogWriteToken>.Fail(new ServiceError(ServiceErrorType.Validation, "Недопустимый префикс контекста."));
            }
            if (mutation != DialogMutationResult.Success)
            {
                return ServiceResult<DialogWriteToken>.Fail(DialogWriteResults.Error(mutation));
            }
            DialogContextState context = dialog.ActiveContext!;
            ModelResponseRecord response = ModelResponseRecord.FromModelResponse(compaction);
            DialogWriteToken next = DialogWriteResults.Apply(root, dialog, StoredContentSize.Of(response));
            contexts.StageCreate(new DialogContextRecord
            {
                DialogId = root.Id, Version = context.Version, ThroughTurnSequence = context.ThroughTurnSequence,
                CreatedAtUtc = context.CreatedAtUtc, Compaction = response
            });
            dialogs.StageUpdate(root);
            return ServiceResult<DialogWriteToken>.Ok(next);
        }, cancellationToken);
    }
}
