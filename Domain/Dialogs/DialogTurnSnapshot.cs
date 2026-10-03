namespace AgentBridge.Domain.Dialogs;

/// <summary>Вход валидирующего восстановления обращения; сам снимок не является доменной сущностью.</summary>
/// <param name="Id">Локальный ID обращения.</param>
/// <param name="Sequence">Порядок начала.</param>
/// <param name="StartedAtUtc">Время начала.</param>
/// <param name="Status">Состояние обращения.</param>
/// <param name="FinishedAtUtc">Время конечного состояния.</param>
public record DialogTurnSnapshot(Guid Id, long Sequence, DateTimeOffset StartedAtUtc,
    DialogTurnStatus Status, DateTimeOffset? FinishedAtUtc);
