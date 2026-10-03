using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Проверяет внешние условия записи внутри уже открытого scope; root читается с исходными concurrency values.</summary>
public class DialogWriteGuard(DialogRecordQueries dialogs)
{
    /// <summary>Проверяет caller, token и срок; явное удаление может разрешать истёкший диалог.</summary>
    public async Task<ServiceResult<DialogRecord>> LoadAsync(DialogAccess access, DialogWriteToken expected,
        CancellationToken cancellationToken, bool allowExpired = false)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(expected);
        if (!access.DialogId.Equals(expected.DialogId))
        {
            return Fail(ServiceErrorType.Conflict, "Token относится к другому диалогу.");
        }
        DialogRecord? root = await dialogs.FindAsync(access.DialogId.Value, cancellationToken, trackChanges: true);
        if (root is null)
        {
            return Fail(ServiceErrorType.NotFound, "Диалог не найден.");
        }
        if (!string.Equals(root.OwnerId, access.OwnerId.Value, StringComparison.Ordinal))
        {
            return Fail(ServiceErrorType.Forbidden, "Диалог принадлежит другому владельцу.");
        }
        if (!allowExpired && access.NowUtc >= root.ExpiresAtUtc)
        {
            return Fail(ServiceErrorType.Expired, "Срок диалога истёк.");
        }
        if (root.IncarnationId != expected.IncarnationId || root.Revision != expected.Revision)
        {
            return Fail(ServiceErrorType.Conflict, "Версия диалога устарела.");
        }
        return ServiceResult<DialogRecord>.Ok(root);
    }

    /// <summary>Создаёт ожидаемый отказ без состояния.</summary>
    private static ServiceResult<DialogRecord> Fail(ServiceErrorType type, string message) =>
        ServiceResult<DialogRecord>.Fail(new ServiceError(type, message));
}
