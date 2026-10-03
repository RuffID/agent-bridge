using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc cref="IDialogDeletion"/>
/// <remarks>Общий набор удаления для явного запроса и одного кандидата очистки; расписания и batch orchestration здесь нет.</remarks>
public class DialogDeletionUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard,
    DialogRecordQueries dialogs, RecordStaging<DialogRecord> staging) : IDialogDeletion, IExpiredDialogDeletion
{
    /// <inheritdoc/>
    public Task<ServiceResult> DeleteAsync(DialogAccess access, DialogWriteToken expected, CancellationToken cancellationToken = default) =>
        scope.ExecuteAsync(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await guard.LoadAsync(access, expected, ct, allowExpired: true);
            if (!loaded.Success)
            {
                return ServiceResult.Fail(loaded.Error!);
            }
            staging.StageDelete(loaded.Data!);
            return ServiceResult.Ok();
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<ServiceResult> DeleteAsync(DialogWriteToken expected, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время должно быть UTC.", nameof(nowUtc));
        }
        return scope.ExecuteAsync(async ct =>
        {
            DialogRecord? root = await dialogs.FindAsync(expected.DialogId.Value, ct, trackChanges: true);
            if (root is null)
            {
                return ServiceResult.Fail(new ServiceError(ServiceErrorType.NotFound, "Диалог не найден."));
            }
            if (root.IncarnationId != expected.IncarnationId || root.Revision != expected.Revision || nowUtc < root.ExpiresAtUtc)
            {
                return ServiceResult.Fail(new ServiceError(ServiceErrorType.Conflict, "Кандидат очистки неактуален или ещё доступен."));
            }
            staging.StageDelete(root);
            return ServiceResult.Ok();
        }, cancellationToken);
    }
}
