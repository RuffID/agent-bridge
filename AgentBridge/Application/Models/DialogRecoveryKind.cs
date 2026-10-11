namespace AgentBridge.Application.Models;

/// <summary>Решения recovery; внешний исход требует отдельного trusted evidence resolver.</summary>
public enum DialogRecoveryKind
{
    /// <summary>Журнал доказывает отсутствие начала.</summary>
    NotStarted,
    /// <summary>Владелец явно оставляет исход неизвестным без повтора.</summary>
    AcknowledgedUnknown,
    /// <summary>Trusted evidence доказывает отмену без внешнего результата.</summary>
    KnownCanceled,
    /// <summary>Trusted evidence содержит подтверждённый внешний protocol output.</summary>
    ConfirmedExternalOutcome
}
