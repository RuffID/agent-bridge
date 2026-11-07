namespace AgentBridge.Configuration;

/// <summary>Явная политика неизвестного полного бюджета, без вымышленных оценок opaque-содержимого.</summary>
public enum ContextBudgetPolicy
{
    /// <summary>Строгий режим: неизвестная локальная оценка отклоняет отправку.</summary>
    RequireLocalEstimate,
    /// <summary>Локальный подсчёт/оценка управляет порогом; полный бюджет opaque и оценочной кодировки проверяет сервер.</summary>
    ServerValidation,
    /// <summary>Явно разрешает неизвестное входное окно; порог только локальный, резерв не гарантирован сервером.</summary>
    ServerValidationWithUnknownWindow
}
