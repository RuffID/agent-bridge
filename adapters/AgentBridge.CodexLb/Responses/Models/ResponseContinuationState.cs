namespace AgentBridge.CodexLb.Responses.Models;

/// <summary>Проверенные разрешённые transport поля продолжения.</summary>
internal class ResponseContinuationState(string? previousId, string? turnState)
{
    /// <summary>Anchor предыдущего ответа.</summary>
    internal string? PreviousId { get; } = previousId;
    /// <summary>Opaque header того же upstream продолжения.</summary>
    internal string? TurnState { get; } = turnState;
}
