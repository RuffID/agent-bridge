using AgentBridge.Application.Models;
using AgentBridge.Configuration;
using AgentBridge.Application.Results;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Repositories;

namespace AgentBridge.Persistence.EfCore.UnitOfWork;

/// <summary>Проверяет внешние условия записи внутри уже открытого scope; root читается с исходными concurrency values.</summary>
public class DialogWriteGuard(DialogRecordQueries dialogs, DialogRetentionPolicy retention)
{
    /// <summary>Сохраняет прежнюю бинарную сигнатуру guard; registered roots требуют lease.</summary>
    public Task<ServiceResult<DialogRecord>> LoadAsync(DialogAccess access, DialogWriteToken expected,
        CancellationToken cancellationToken, bool allowExpired = false) =>
        LoadRunAsync(access, expected, cancellationToken, allowExpired, null, false);

    /// <summary>Проверяет caller, token и срок; явное удаление может разрешать истёкший диалог.</summary>
    public async Task<ServiceResult<DialogRecord>> LoadRunAsync(DialogAccess access, DialogWriteToken expected,
        CancellationToken cancellationToken, bool allowExpired = false, DialogRunLease? lease = null, bool catalogControl = false)
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
        if (!allowExpired && IsExpired(root, access.NowUtc, retention))
        {
            return Fail(ServiceErrorType.Expired, "Срок диалога истёк.");
        }
        if (root.IncarnationId != expected.IncarnationId || root.Revision != expected.Revision)
        {
            return Fail(ServiceErrorType.Conflict, "Версия диалога устарела.");
        }
        if (root.CatalogRegistered && !catalogControl && (lease is null || !DialogRuntimeState.Read(root).Matches(root, lease, access.NowUtc)))
        {
            return Fail(ServiceErrorType.Conflict, "run_lease_required_or_stale");
        }

        return ServiceResult<DialogRecord>.Ok(root);
    }

    /// <summary>Registered root использует immutable expiry; legacy сохраняет текущую retention semantics.</summary>
    public static DateTimeOffset? Expiry(DialogRecord root, DialogRetentionPolicy retention) => root.CatalogRegistered
        ? root.ExpiresAtUtc == DateTimeOffset.MaxValue ? null : root.ExpiresAtUtc
        : retention.CalculateExpiresAtUtc(root.CreatedAtUtc);

    /// <summary>Единая граница expiry для writes/read/cleanup.</summary>
    public static bool IsExpired(DialogRecord root, DateTimeOffset nowUtc, DialogRetentionPolicy retention) =>
        Expiry(root, retention) is { } expiry && nowUtc >= expiry;

    /// <summary>Создаёт ожидаемый отказ без состояния.</summary>
    private static ServiceResult<DialogRecord> Fail(ServiceErrorType type, string message) =>
        ServiceResult<DialogRecord>.Fail(new ServiceError(type, message));
}
