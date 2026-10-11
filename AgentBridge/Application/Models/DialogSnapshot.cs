using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Неизменяемые данные чтения диалога; не являются восстановленным доменным агрегатом.</summary>
public class DialogSnapshot
{
    /// <summary>Фиксирует метаданные, всю историю и активное окно без фильтрации по terminal prefix.</summary>
    public DialogSnapshot(DialogWriteToken token, DialogOwnerId ownerId, DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc, long contentBytes, IEnumerable<StoredDialogTurn> turns, StoredDialogContext? activeContext,
        DialogModelSelection? selection = null)
        : this(token, ownerId, createdAtUtc, expiresAtUtc, contentBytes, turns, activeContext, selection, null, null, null)
    {
    }

    /// <summary>Сохраняет originals отдельно от recovery-aware effective history и durable gate.</summary>
    public DialogSnapshot(DialogWriteToken token, DialogOwnerId ownerId, DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc, long contentBytes, IEnumerable<StoredDialogTurn> turns, StoredDialogContext? activeContext,
        DialogModelSelection? selection, DialogCatalogState? catalog, DialogContinuationState? continuation,
        IEnumerable<StoredDialogTurn>? effectiveTurns)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(ownerId);
        ContractSnapshot.Utc(createdAtUtc);
        if (expiresAtUtc is { } expiry)
        {
            ContractSnapshot.Utc(expiry);
        }
        ArgumentOutOfRangeException.ThrowIfNegative(contentBytes);
        Token = token;
        OwnerId = ownerId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ContentBytes = contentBytes;
        Turns = ContractSnapshot.Copy(turns);
        ActiveContext = activeContext;
        Selection = selection;
        Catalog = catalog;
        Continuation = continuation;
        EffectiveTurns = effectiveTurns is null ? Turns : ContractSnapshot.Copy(effectiveTurns);
    }

    /// <summary>Сохраняемое условие актуальности прочитанных данных.</summary>
    public DialogWriteToken Token { get; }
    /// <summary>Владелец диалога.</summary>
    public DialogOwnerId OwnerId { get; }
    /// <summary>Фиксированное время создания.</summary>
    public DateTimeOffset CreatedAtUtc { get; }
    /// <summary>Срок по текущей политике для этого снимка; null означает бессрочное хранение.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; }
    /// <summary>Байты сохраняемого содержимого, без overhead провайдера.</summary>
    public long ContentBytes { get; }
    /// <summary>Вся история в порядке начала обращений; без поиска и скрытого отбрасывания элементов.</summary>
    public IReadOnlyList<StoredDialogTurn> Turns { get; }
    /// <summary>Последнее принятое окно либо отсутствие сжатия.</summary>
    public StoredDialogContext? ActiveContext { get; }
    /// <summary>Сохранённый выбор с независимой версией; null использует defaults приложения.</summary>
    public DialogModelSelection? Selection { get; }
    /// <summary>Полный immutable profile/scope; legacy root имеет null.</summary>
    public DialogCatalogState? Catalog { get; }
    /// <summary>Durable readiness/fencing, legacy root имеет null.</summary>
    public DialogContinuationState? Continuation { get; }
    /// <summary>Lease захватывает только библиотечная сессия после accepted Begin; reader её не выдаёт как разрешение model step.</summary>
    internal DialogRunLease? OwnedRunLease { get; init; }
    /// <summary>Library-owned context projection, не изменяющая исходные Turns.</summary>
    public IReadOnlyList<StoredDialogTurn> EffectiveTurns { get; }
    /// <summary>Старое окно не применяется поверх более новой recovery revision.</summary>
    public StoredDialogContext? EffectiveContext => ActiveContext is null ||
        ActiveContext.RecoveryRevision < (Continuation?.RecoveryRevision ?? 0) ? null : ActiveContext;
    /// <summary>Проверяет истечение для интерфейса на явном UTC; равенство сроку означает истечение.</summary>
    public bool IsExpired(DateTimeOffset nowUtc)
    {
        ContractSnapshot.Utc(nowUtc);
        return ExpiresAtUtc is { } expiry && nowUtc >= expiry;
    }
}
