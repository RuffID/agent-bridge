using AgentBridge.Domain.Dialogs;
using AgentBridge.Configuration;
using AgentBridge.Application.Models;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Передаёт полную историю метаданных валидирующей фабрике Domain; используется только внутри write scope.</summary>
public class DialogStateLoader(TurnRecordQueries turns, ContextRecordQueries contexts, DialogRetentionPolicy retention)
{
    /// <summary>Не проигрывает фиктивные изменения и не подменяет исходный token новой версией.</summary>
    public async Task<Dialog> LoadAsync(DialogRecord root, CancellationToken cancellationToken)
    {
        List<DialogTurnRecord> history = await turns.ReadAsync(root.Id, cancellationToken);
        List<DialogContextRecord> versions = await contexts.ReadAsync(root.Id, cancellationToken);
        if (root.ContentBytes < 0 || versions.Any(context => context.Compaction.Status != ModelResponseStatus.Completed))
        {
            throw new InvalidOperationException("Повреждены метаданные сохранённого диалога.");
        }
        return Dialog.Restore(DialogId.From(root.Id), DialogOwnerId.From(root.OwnerId), root.CreatedAtUtc,
            retention.CalculateExpiresAtUtc(root.CreatedAtUtc), root.Revision, root.LastChangedAtUtc,
            history.Select(turn => new DialogTurnSnapshot(turn.Id, turn.Sequence, turn.StartedAtUtc, turn.Status, turn.FinishedAtUtc)),
            versions.Select(context => new DialogContextSnapshot(context.Version, context.ThroughTurnSequence, context.CreatedAtUtc)));
    }
}
