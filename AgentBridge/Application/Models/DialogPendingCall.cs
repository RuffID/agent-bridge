namespace AgentBridge.Application.Models;

/// <summary>Identity конкретной original позиции; call_id не ключ дедупликации.</summary>
/// <param name="TurnId">Turn.</param>
/// <param name="StepId">Step.</param>
/// <param name="OutputIndex">Original output position.</param>
/// <param name="ItemIndex">Original canonical item position внутри turn.</param>
/// <param name="CallId">Protocol metadata.</param>
/// <param name="State">Durable attempt либо NotStarted только после финализации/fence без Started.</param>
public record DialogPendingCall(Guid TurnId, Guid StepId, int OutputIndex, int ItemIndex, string CallId, ToolAttemptState State);
