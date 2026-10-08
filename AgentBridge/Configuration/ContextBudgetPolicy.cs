namespace AgentBridge.Configuration;

/// <summary>Явная политика неизвестного полного бюджета, без вымышленных оценок opaque-содержимого.</summary>
public enum ContextBudgetPolicy
{
    /// <summary>Строгий режим: неизвестная локальная оценка отклоняет отправку.</summary>
    RequireLocalEstimate,
    /// <summary>Известная часть проверяется локально; полный бюджет фото, файлов и opaque-состояния проверяет сервер.</summary>
    ServerValidation
}
