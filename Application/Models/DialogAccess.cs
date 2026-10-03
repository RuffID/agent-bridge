using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Идентичности защищаемого сценария и явное время проверки доступности.</summary>
public class DialogAccess
{
    /// <summary>Фиксирует запрос; сам DTO не выполняет авторизацию или чтение часов.</summary>
    public DialogAccess(DialogId dialogId, DialogOwnerId ownerId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(dialogId);
        ArgumentNullException.ThrowIfNull(ownerId);
        ContractSnapshot.Utc(nowUtc);
        DialogId = dialogId;
        OwnerId = ownerId;
        NowUtc = nowUtc;
    }

    /// <summary>Запрошенный диалог.</summary>
    public DialogId DialogId { get; }
    /// <summary>Идентичность проверяемого пользователя.</summary>
    public DialogOwnerId OwnerId { get; }
    /// <summary>Явное время UTC для проверки фиксированного срока.</summary>
    public DateTimeOffset NowUtc { get; }
}
