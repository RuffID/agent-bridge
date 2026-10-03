using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.UnitOfWork;

namespace AgentBridge.Persistence.EfCore.Reading;

/// <inheritdoc/>
public class ExpiredDialogReader(DialogRecordQueries dialogs, PersistenceOperationGate gate) : IExpiredDialogReader
{
    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<DialogWriteToken>>> ReadAsync(DateTimeOffset nowUtc, int limit,
        CancellationToken cancellationToken = default)
    {
        using IDisposable lease = gate.Enter();
        cancellationToken.ThrowIfCancellationRequested();
        List<DialogRecord> records = await dialogs.ReadExpiredAsync(nowUtc, limit, cancellationToken);
        IReadOnlyList<DialogWriteToken> candidates = Array.AsReadOnly(records.Select(record =>
            new DialogWriteToken(DialogId.From(record.Id), record.IncarnationId, record.Revision)).ToArray());
        return ServiceResult<IReadOnlyList<DialogWriteToken>>.Ok(candidates);
    }
}
