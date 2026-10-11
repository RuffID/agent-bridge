namespace AgentBridge.Application.Models;

/// <summary>Ответ авторизованного host resolver. Output допускается только для ConfirmedExternalOutcome.</summary>
/// <param name="Kind">Подтверждённый вид решения.</param>
/// <param name="Reference">Та же проверенная ссылка evidence.</param>
/// <param name="Output">Полный подтверждённый function_call_output, без нормализации.</param>
public record DialogRecoveryEvidence(DialogRecoveryKind Kind, string Reference, CanonicalModelItem? Output);
