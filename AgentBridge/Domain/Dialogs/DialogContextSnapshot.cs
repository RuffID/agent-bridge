namespace AgentBridge.Domain.Dialogs;

/// <summary>Вход валидирующего восстановления версии контекста.</summary>
/// <param name="Version">Порядковая версия.</param>
/// <param name="ThroughTurnSequence">Непрерывный конечный префикс обращений.</param>
/// <param name="CreatedAtUtc">Время принятия контекста.</param>
public record DialogContextSnapshot(long Version, long ThroughTurnSequence, DateTimeOffset CreatedAtUtc);
