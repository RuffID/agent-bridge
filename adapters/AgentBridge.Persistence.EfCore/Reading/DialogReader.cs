using System.Text.Json;
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
    ModelStepRecordQueries steps, ContextRecordQueries contexts, PersistenceOperationGate gate) : IDialogReader
{
    /// <inheritdoc/>
    public async Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
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

        Guid incarnationId = dialog.IncarnationId;
        long revision = dialog.Revision;
        string ownerId = dialog.OwnerId;
        DateTimeOffset createdAtUtc = dialog.CreatedAtUtc;
        DateTimeOffset expiresAtUtc = dialog.ExpiresAtUtc;
        long contentBytes = dialog.ContentBytes;

        List<DialogTurnRecord> turnRecords = await turns.ReadAsync(dialogId, cancellationToken);
        List<CanonicalItemRecord> itemRecords = await items.ReadAsync(dialogId, cancellationToken);
        List<ModelStepRecord> stepRecords = await steps.ReadAsync(dialogId, cancellationToken);
        DialogContextRecord? context = await contexts.ReadActiveAsync(dialogId, cancellationToken);
        DialogRecord? current = await dialogs.FindAsync(dialogId, cancellationToken);
        if (current is null)
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.NotFound, "Диалог удалён во время чтения."));
        }
        if (!string.Equals(current.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.Forbidden, "Владелец диалога изменился во время чтения."));
        }
        if (current.IncarnationId != incarnationId || current.Revision != revision)
        {
            return ServiceResult<DialogSnapshot>.Fail(new ServiceError(ServiceErrorType.Conflict, "Диалог изменился во время чтения."));
        }
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
                    ToolAttemptMapping.Read(step.ToolAttemptsJson)))));
        }
        StoredDialogContext? active = context is null ? null : new StoredDialogContext(context.Version,
            context.ThroughTurnSequence, context.Compaction.ToModelResponse());
        DialogWriteToken token = new(access.DialogId, incarnationId, revision);
        return ServiceResult<DialogSnapshot>.Ok(new DialogSnapshot(token, DialogOwnerId.From(ownerId),
            createdAtUtc, expiresAtUtc, contentBytes, history, active));
    }

    /// <summary>Копирует полный канонический item независимо от срока жизни документа.</summary>
    private static CanonicalModelItem ReadItem(CanonicalItemRecord record)
    {
        using JsonDocument document = JsonDocument.Parse(record.ContentJson);
        return new CanonicalModelItem(document.RootElement);
    }
}
