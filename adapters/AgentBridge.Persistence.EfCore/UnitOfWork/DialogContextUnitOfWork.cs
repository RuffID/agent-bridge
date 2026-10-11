using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class DialogContextUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogStateLoader stateLoader,
    RecordStaging<DialogRecord> dialogs, RecordStaging<DialogContextRecord> contexts, DialogCatalogStaging? catalog = null) : IDialogContextWriter
{
    /// <summary>Сохраняет прежнюю бинарную сигнатуру compact writer.</summary>
    public DialogContextUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogStateLoader stateLoader,
        RecordStaging<DialogRecord> dialogs, RecordStaging<DialogContextRecord> contexts)
        : this(scope, guard, stateLoader, dialogs, contexts, null) { }

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> SaveWithModelAsync(DialogRunWriteAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, string selectedModel, CancellationToken cancellationToken = default) =>
        SaveCoreAsync(access.Access, expected, throughTurnSequence, compaction, selectedModel, cancellationToken, access.Lease);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, CancellationToken cancellationToken = default)
        => SaveCoreAsync(access, expected, throughTurnSequence, compaction, null, cancellationToken);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> SaveWithModelAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, string selectedModel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedModel);
        return SaveCoreAsync(access, expected, throughTurnSequence, compaction, selectedModel, cancellationToken);
    }

    /// <summary>Общее атомарное сохранение с optional provenance legacy primitive вызова.</summary>
    private Task<ServiceResult<DialogWriteToken>> SaveCoreAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, string? selectedModel, CancellationToken cancellationToken, DialogRunLease? lease = null)
    {
        ArgumentNullException.ThrowIfNull(compaction);
        if (compaction.Status != ModelResponseStatus.Completed)
        {
            return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new ServiceError(ServiceErrorType.Validation, "Compact должен быть завершён.")));
        }
        return scope.ExecuteAsync<DialogWriteToken>(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(access, expected, ct, lease: lease);
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
            if (root.CatalogRegistered)
            {
                DialogRuntimeState runtime = DialogRuntimeState.Read(root);
                if (runtime.Calls.Any(call => call.OutputItemIndex is null && call.ResolutionJson is null))
                    return ServiceResult<DialogWriteToken>.Fail(new(ServiceErrorType.Conflict, "compact_unresolved_calls"));
                if (catalog is null) throw new InvalidOperationException("Catalog staging required.");
                await catalog.UpdateAsync(root, "run", ct);
            }

            contexts.StageCreate(new DialogContextRecord
            {
                DialogId = root.Id, Version = context.Version, ThroughTurnSequence = context.ThroughTurnSequence,
                CreatedAtUtc = context.CreatedAtUtc, Compaction = response, SelectedModel = selectedModel,
                RecoveryRevision = root.CatalogRegistered ? DialogRuntimeState.Read(root).RecoveryRevision : 0
            });
            dialogs.StageUpdate(root);
            return ServiceResult<DialogWriteToken>.Ok(next);
        }, cancellationToken);
    }
}
