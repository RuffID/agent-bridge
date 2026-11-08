namespace AgentBridge.LiveAcceptance;

/// <summary>Нарушенная проверка с фиксированным кодом без внешнего текста.</summary>
internal class AcceptanceCheckException(string code) : Exception("Acceptance check failed.")
{
    public string Code { get; } = code;
}
