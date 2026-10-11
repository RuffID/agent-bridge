namespace AgentBridge.Application.Models;

/// <summary>Уже принимаемое canonical user/assistant сообщение текущей transaction.</summary>
/// <param name="DialogId">Идентичность диалога.</param>
/// <param name="IncarnationId">Идентичность жизни.</param>
/// <param name="TurnId">Обращение.</param>
/// <param name="TurnSequence">Порядок обращения.</param>
/// <param name="Position">Позиция canonical item.</param>
/// <param name="SavedAtUtc">Server UTC сохранения.</param>
/// <param name="Role">Только user либо assistant.</param>
/// <param name="Message">Canonical snapshot без envelope.</param>
public record SavedDialogMessage(Guid DialogId, Guid IncarnationId, Guid TurnId, long TurnSequence,
    long Position, DateTimeOffset SavedAtUtc, string Role, CanonicalModelItem Message);
