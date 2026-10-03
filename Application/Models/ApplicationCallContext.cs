using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Application.Models;

/// <summary>Идентичности вызова для проверки прав приложением; не является доказательством авторизации.</summary>
public class ApplicationCallContext
{
    /// <summary>Фиксирует пользователя, диалог, обращение и выбранного агента без секретов.</summary>
    public ApplicationCallContext(DialogId dialogId, DialogOwnerId ownerId, Guid turnId, string agentId)
    {
        ArgumentNullException.ThrowIfNull(dialogId);
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        if (turnId == Guid.Empty)
        {
            throw new ArgumentException("Идентичность обращения обязательна.", nameof(turnId));
        }
        DialogId = dialogId;
        OwnerId = ownerId;
        TurnId = turnId;
        AgentId = agentId;
    }

    /// <summary>Диалог запроса.</summary>
    public DialogId DialogId { get; }
    /// <summary>Идентичность пользователя, предоставленная приложением.</summary>
    public DialogOwnerId OwnerId { get; }
    /// <summary>Обращение, внутри которого выполняется вызов.</summary>
    public Guid TurnId { get; }
    /// <summary>Идентичность выбранного агента приложения.</summary>
    public string AgentId { get; }
}
