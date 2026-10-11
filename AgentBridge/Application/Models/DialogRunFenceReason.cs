namespace AgentBridge.Application.Models;

/// <summary>Основание durable fencing; ни одно не доказывает исход внешнего действия.</summary>
public enum DialogRunFenceReason
{
    /// <summary>Persisted lease истёк по серверному времени.</summary>
    ExpiredLease,
    /// <summary>Trusted host подтвердил завершение исполнителя.</summary>
    ConfirmedQuiescence
}
