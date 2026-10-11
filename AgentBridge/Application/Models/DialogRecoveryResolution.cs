namespace AgentBridge.Application.Models;

/// <summary>Identity позиции и явное решение владельца; никаких raw outputs клиента.</summary>
/// <param name="TurnId">Turn.</param>
/// <param name="StepId">Step.</param>
/// <param name="OutputIndex">Original position.</param>
/// <param name="Kind">Truthful решение.</param>
/// <param name="AcknowledgementReference">Host audit reference согласия либо ссылка на отдельное trusted evidence.</param>
public record DialogRecoveryResolution(Guid TurnId, Guid StepId, int OutputIndex, DialogRecoveryKind Kind,
    string? AcknowledgementReference = null);
