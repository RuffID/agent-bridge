using System.Text.Json;
using AgentBridge.Configuration;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Mapping;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;

namespace AgentBridge.Persistence.EfCore.Reading;

/// <inheritdoc/>
/// <remarks>Повторное чтение root отклоняет изменившуюся жизнь/версию без скрытого retry.
/// Это не транзакционный снимок; token требует атомарной проверки сценарием записи этапа 10.</remarks>
public class DialogReader(DialogRecordQueries dialogs, TurnRecordQueries turns, ItemRecordQueries items,
    ModelStepRecordQueries steps, ContextRecordQueries contexts, PersistenceOperationGate gate, SettingsRecordQueries settings,
    DialogRetentionPolicy retention, DialogCatalogQueries? catalogQueries = null) : IDialogReader
{
    /// <summary>Сохраняет прежнюю бинарную сигнатуру reader для legacy roots.</summary>
    public DialogReader(DialogRecordQueries dialogs, TurnRecordQueries turns, ItemRecordQueries items,
        ModelStepRecordQueries steps, ContextRecordQueries contexts, PersistenceOperationGate gate,
        SettingsRecordQueries settings, DialogRetentionPolicy retention)
        : this(dialogs, turns, items, steps, contexts, gate, settings, retention, null) { }

    /// <inheritdoc/>
    public Task<ServiceResult<DialogSnapshot>> ReadCatalogAsync(DialogCatalogAccess access, CancellationToken cancellationToken = default) =>
        ReadCoreAsync(access.Access, access, cancellationToken);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default) =>
        ReadCoreAsync(access, null, cancellationToken);

    private async Task<ServiceResult<DialogSnapshot>> ReadCoreAsync(DialogAccess access, DialogCatalogAccess? catalogAccess,
        CancellationToken cancellationToken)
    {
        using IDisposable lease = gate.Enter();
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();
        Guid dialogId = access.DialogId.Value;
        DialogRecord? dialog = await dialogs.FindAsync(dialogId, cancellationToken);
        if (dialog is null)
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.NotFound, "Диалог не найден."));
        }
        if (!string.Equals(dialog.OwnerId, access.OwnerId.Value, StringComparison.Ordinal))
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.Forbidden, "Диалог принадлежит другому владельцу."));
        }
        DialogCatalogState? metadata = null;
        DialogContinuationState? continuation = null;
        List<DialogRecoveryOperationRecord> recovery = [];
        if (dialog.CatalogRegistered)
        {
            if (catalogAccess is null)
                return ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Forbidden, "catalog_profile_required"));
            if (DialogWriteGuard.IsExpired(dialog, access.NowUtc, retention))
                return ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Expired, "dialog_expired"));
            if (catalogQueries is null) throw new InvalidOperationException("Catalog queries required.");
            DialogCatalogRecord registered = await catalogQueries.FindAsync(dialog.Id, cancellationToken) ??
                throw new InvalidOperationException("Registered catalog missing.");
            if (!DialogCatalogStaging.Matches(registered, catalogAccess.Scope, catalogAccess.Profile))
                return ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Forbidden, "catalog_profile_or_scope_mismatch"));
            if (registered.Deleted || registered.IncarnationId != dialog.IncarnationId || registered.RootRevision != dialog.Revision)
                return ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Conflict, "catalog_revision_divergence"));
            metadata = DialogCatalogStaging.State(registered, access.NowUtc);
            continuation = DialogRuntimeState.Read(dialog).ToState(dialog);
            recovery = await catalogQueries.RecoveryHistoryAsync(dialog.Id, cancellationToken);
        }

        Guid incarnationId = dialog.IncarnationId;
        long revision = dialog.Revision;
        string? runtimeJson = dialog.RuntimeJson;
        string ownerId = dialog.OwnerId;
        DateTimeOffset createdAtUtc = dialog.CreatedAtUtc;
        DateTimeOffset? expiresAtUtc = DialogWriteGuard.Expiry(dialog, retention);
        long contentBytes = dialog.ContentBytes;

        List<DialogTurnRecord> turnRecords = await turns.ReadAsync(dialogId, cancellationToken);
        List<CanonicalItemRecord> itemRecords = await items.ReadAsync(dialogId, cancellationToken);
        List<ModelStepRecord> stepRecords = await steps.ReadAsync(dialogId, cancellationToken);
        DialogContextRecord? context = await contexts.ReadActiveAsync(dialogId, cancellationToken);
        DialogModelSelection? selection = (await settings.FindAsync(dialogId, cancellationToken))?.ToSelection();
        DialogRecord? current = await dialogs.FindAsync(dialogId, cancellationToken);
        if (current is null)
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.NotFound, "Диалог удалён во время чтения."));
        }
        if (!string.Equals(current.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.Forbidden, "Владелец диалога изменился во время чтения."));
        }
        if (current.IncarnationId != incarnationId || current.Revision != revision || current.RuntimeJson != runtimeJson)
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.Conflict, "Диалог изменился во время чтения."));
        }
        DialogModelSelection? currentSelection = (await settings.FindAsync(dialogId, cancellationToken))?.ToSelection();
        if ((currentSelection?.Version ?? 0) != (selection?.Version ?? 0))
            return ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Conflict, "Настройки изменились во время чтения."));
        HashSet<Guid> turnIds = turnRecords.Select(turn => turn.Id).ToHashSet();
        if (itemRecords.Any(item => !turnIds.Contains(item.TurnId)) || stepRecords.Any(step => !turnIds.Contains(step.TurnId)))
        {
            throw new InvalidOperationException("История содержит данные без родительского обращения.");
        }
        if (context is not null && context.Compaction.Status != ModelResponseStatus.Completed)
        {
            throw new InvalidOperationException("Принятый compact должен быть завершён.");
        }
        ILookup<Guid, CanonicalItemRecord> itemsByTurn = itemRecords.ToLookup(record => record.TurnId);
        ILookup<Guid, ModelStepRecord> stepsByTurn = stepRecords.ToLookup(record => record.TurnId);
        List<StoredDialogTurn> history = [];
        foreach (DialogTurnRecord turn in turnRecords)
        {
            history.Add(new StoredDialogTurn(turn.Id, turn.Sequence, turn.Status,
                itemsByTurn[turn.Id].Select(ReadItem),
                stepsByTurn[turn.Id].Select(step => new StoredModelStep(step.Id, step.Response.ToModelResponse(),
                    ToolAttemptMapping.Read(step.ToolAttemptsJson))), TurnSettingsMapping.Read(turn.SettingsJson)));
        }
        StoredDialogContext? active = context is null ? null : new StoredDialogContext(context.Version,
            context.ThroughTurnSequence, context.Compaction.ToModelResponse(), context.SelectedModel, context.RecoveryRevision);
        DialogWriteToken token = new(access.DialogId, incarnationId, revision);
        return ServiceResult<DialogSnapshot>.Ok(new DialogSnapshot(token, DialogOwnerId.From(ownerId),
            createdAtUtc, expiresAtUtc, contentBytes, history, active, selection, metadata, continuation,
            dialog.CatalogRegistered ? DialogRecoveryContextMapping.Apply(history, recovery) : null));
    }

    /// <summary>Копирует полный канонический item независимо от срока жизни документа.</summary>
    private static CanonicalModelItem ReadItem(CanonicalItemRecord record)
    {
        using JsonDocument document = JsonDocument.Parse(record.ContentJson);
        return new CanonicalModelItem(document.RootElement);
    }
}
