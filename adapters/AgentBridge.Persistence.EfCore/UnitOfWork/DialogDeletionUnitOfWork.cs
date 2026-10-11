using AgentBridge.Application.Models;
using AgentBridge.Configuration;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <inheritdoc cref="IDialogDeletion"/>
/// <remarks>Общий набор удаления для явного запроса и одного кандидата очистки; расписания и batch orchestration здесь нет.</remarks>
public class DialogDeletionUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard,
    DialogRecordQueries dialogs, RecordStaging<DialogRecord> staging, DialogRetentionPolicy retention,
    DialogCatalogStaging? catalog = null) : IDialogDeletion, IExpiredDialogDeletion
{
    /// <summary>Сохраняет прежнюю бинарную сигнатуру удаления.</summary>
    public DialogDeletionUnitOfWork(UnitOfWorkScope scope, DialogWriteGuard guard, DialogRecordQueries dialogs,
        RecordStaging<DialogRecord> staging, DialogRetentionPolicy retention)
        : this(scope, guard, dialogs, staging, retention, null) { }

    /// <inheritdoc/>
    public Task<ServiceResult> DeleteAsync(DialogAccess access, DialogWriteToken expected, CancellationToken cancellationToken = default) =>
        scope.ExecuteAsync(async ct =>
        {
            ServiceResult<DialogRecord> loaded = await guard.LoadRunAsync(access, expected, ct, allowExpired: true, catalogControl: true);
            if (!loaded.Success)
            {
                return ServiceResult.Fail(loaded.Error!);
            }
            DialogRecord root = loaded.Data!;
            if (root.CatalogRegistered)
            {
                if (DialogRuntimeState.Read(root).LeaseId != Guid.Empty)
                    return ServiceResult.Fail(new(ServiceErrorType.Conflict, "delete_active_run"));
                if (catalog is null) throw new InvalidOperationException("Catalog staging required.");
                await catalog.DeleteAsync(root, "delete", ct);
            }

            staging.StageDelete(root);
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
            if (root.IncarnationId != expected.IncarnationId || root.Revision != expected.Revision || !DialogWriteGuard.IsExpired(root, nowUtc, retention))
            {
                return ServiceResult.Fail(new ServiceError(ServiceErrorType.Conflict, "Кандидат очистки неактуален или ещё доступен."));
            }
            if (root.CatalogRegistered)
            {
                if (DialogRuntimeState.Read(root).LeaseId != Guid.Empty)
                    return ServiceResult.Fail(new(ServiceErrorType.Conflict, "expiry_active_run_requires_fence"));
                if (catalog is null) throw new InvalidOperationException("Catalog staging required.");
                await catalog.DeleteAsync(root, "expiry", ct);
            }

            staging.StageDelete(root);
            return ServiceResult.Ok();
        }, cancellationToken);
    }
}
