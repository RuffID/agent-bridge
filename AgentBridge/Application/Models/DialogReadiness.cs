namespace AgentBridge.Application.Models;

/// <summary>Durable разрешение следующего нового обращения, отдельное от terminal/model context.</summary>
public enum DialogReadiness
{
    /// <summary>Финализировано, пары закрыты.</summary>
    Ready,
    /// <summary>Живой lease; deadline сам по себе не меняет состояние.</summary>
    Active,
    /// <summary>Требуется durable fence/finalization/recovery.</summary>
    RecoveryRequired
}
