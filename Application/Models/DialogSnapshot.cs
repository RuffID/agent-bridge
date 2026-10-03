using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Неизменяемые данные чтения диалога; не являются восстановленным доменным агрегатом.</summary>
public class DialogSnapshot
{
    /// <summary>Фиксирует метаданные, всю историю и активное окно без фильтрации по terminal prefix.</summary>
    public DialogSnapshot(DialogWriteToken token, DialogOwnerId ownerId, DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc, long contentBytes, IEnumerable<StoredDialogTurn> turns, StoredDialogContext? activeContext)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(ownerId);
        ContractSnapshot.Utc(createdAtUtc);
        ContractSnapshot.Utc(expiresAtUtc);
        ArgumentOutOfRangeException.ThrowIfNegative(contentBytes);
        Token = token;
        OwnerId = ownerId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ContentBytes = contentBytes;
        Turns = ContractSnapshot.Copy(turns);
        ActiveContext = activeContext;
    }

    /// <summary>Сохраняемое условие актуальности прочитанных данных.</summary>
    public DialogWriteToken Token { get; }
    /// <summary>Владелец диалога.</summary>
    public DialogOwnerId OwnerId { get; }
    /// <summary>Фиксированное время создания.</summary>
    public DateTimeOffset CreatedAtUtc { get; }
    /// <summary>Фиксированное время истечения, рассчитанное при создании.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }
    /// <summary>Байты сохраняемого содержимого, без overhead провайдера.</summary>
    public long ContentBytes { get; }
    /// <summary>Вся история в порядке начала обращений; без поиска и скрытого отбрасывания элементов.</summary>
    public IReadOnlyList<StoredDialogTurn> Turns { get; }
    /// <summary>Последнее принятое окно либо отсутствие сжатия.</summary>
    public StoredDialogContext? ActiveContext { get; }
    /// <summary>Проверяет истечение для интерфейса на явном UTC; равенство сроку означает истечение.</summary>
    public bool IsExpired(DateTimeOffset nowUtc)
    {
        ContractSnapshot.Utc(nowUtc);
        return nowUtc >= ExpiresAtUtc;
    }
}
