namespace AgentBridge.Application.Models;

/// <summary>Привязка evidence к неизменяемой позиции и CAS; reference не является доказательством сама по себе.</summary>
/// <param name="Request">Полная recovery команда, включая incarnation/revision/epoch.</param>
/// <param name="Resolution">Разрешаемая позиция и kind.</param>
public record DialogRecoveryEvidenceRequest(DialogRecoveryRequest Request, DialogRecoveryResolution Resolution);
