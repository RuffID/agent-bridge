using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.Mapping;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc/>
public class DialogTurnUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogStateLoader stateLoader,
    TurnContentStaging content, RecordStaging<DialogRecord> dialogs, RecordStaging<DialogTurnRecord> turns,
    TurnRecordQueries turnQueries) : IDialogTurnWriter
{
    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> BeginWithSettingsAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, IReadOnlyList<CanonicalModelItem> input, TurnModelSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ChangeAsync(access, expected, turnId, input, [], null, true, cancellationToken, settings);
    }
    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> BeginAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, IReadOnlyList<CanonicalModelItem> input, CancellationToken cancellationToken = default) =>
        ChangeAsync(access, expected, turnId, input, [], null, true, cancellationToken);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> AppendAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> modelSteps,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(access, expected, turnId, items, modelSteps, null, false, cancellationToken);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> FinishAsync(DialogAccess access, DialogWriteToken expected,
        Guid turnId, DialogTurnStatus status, IReadOnlyList<CanonicalModelItem> newItems, IReadOnlyList<StoredModelStep> modelSteps,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status) || status == DialogTurnStatus.InProgress)
        {
            return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new ServiceError(ServiceErrorType.Validation, "Необходим конечный статус.")));
        }
        return ChangeAsync(access, expected, turnId, newItems, modelSteps, status, false, cancellationToken);
    }

    /// <summary>Координирует внешние guards, локальные Domain-инварианты и согласованный пакет строк одной transaction.</summary>
    private Task<ServiceResult<DialogWriteToken>> ChangeAsync(DialogAccess access, DialogWriteToken expected, Guid turnId,
        IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> modelSteps, DialogTurnStatus? terminal,
        bool begin, CancellationToken cancellationToken, TurnModelSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(modelSteps);
        if (turnId == Guid.Empty)
        {
            return Task.FromResult(ServiceResult<DialogWriteToken>.Fail(new ServiceError(ServiceErrorType.Validation, "ID обращения обязателен.")));
        }
        // Копии списков фиксируются до первого await; их элементы уже являются независимыми immutable snapshots.
        CanonicalModelItem[] itemSnapshot = items.ToArray();
        StoredModelStep[] stepSnapshot = modelSteps.ToArray();
        return scope.ExecuteAsync<DialogWriteToken>(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await guard.LoadAsync(access, expected, ct);
            if (!loaded.Success)
            {
                return ServiceResult<DialogWriteToken>.Fail(loaded.Error!);
            }
            DialogRecord root = loaded.Data!;
            Dialog dialog = await stateLoader.LoadAsync(root, ct);
            DialogMutationResult captured = dialog.TryCaptureVersion(access.OwnerId, access.NowUtc, out DialogStateVersion? version);
            if (captured != DialogMutationResult.Success)
            {
                return ServiceResult<DialogWriteToken>.Fail(DialogWriteResults.Error(captured));
            }
            DialogMutationResult mutation = begin
                ? dialog.TryBeginTurn(access.OwnerId, turnId, access.NowUtc, out _)
                : terminal is DialogTurnStatus status
                    ? dialog.TryFinishTurn(access.OwnerId, version!, turnId, status, access.NowUtc)
                    : dialog.TryAppendTurn(access.OwnerId, version!, turnId, access.NowUtc);
            if (mutation != DialogMutationResult.Success)
            {
                return ServiceResult<DialogWriteToken>.Fail(DialogWriteResults.Error(mutation));
            }
            ServiceResult<PreparedTurnContent> prepared = await content.PrepareAsync(root.Id, turnId, itemSnapshot, stepSnapshot, ct);
            if (!prepared.Success)
            {
                return ServiceResult<DialogWriteToken>.Fail(prepared.Error!);
            }
            string? settingsJson = begin ? TurnSettingsMapping.Write(settings) :
                (await turnQueries.FindAsync(root.Id, turnId, ct))?.SettingsJson;
            DialogWriteToken next = DialogWriteResults.Apply(root, dialog, prepared.Data!.AddedBytes);
            DialogTurn turn = dialog.Turns.Single(item => item.Id == turnId);
            DialogTurnRecord record = new()
            {
                DialogId = root.Id, Id = turn.Id, Sequence = turn.Sequence, Status = turn.Status,
                StartedAtUtc = turn.StartedAtUtc, FinishedAtUtc = turn.FinishedAtUtc,
                SettingsJson = settingsJson
            };
            if (begin) { turns.StageCreate(record); }
            else if (terminal is not null) { turns.StageUpdate(record); }
            content.Stage(prepared.Data);
            dialogs.StageUpdate(root);
            return ServiceResult<DialogWriteToken>.Ok(next);
        }, cancellationToken);
    }
}
