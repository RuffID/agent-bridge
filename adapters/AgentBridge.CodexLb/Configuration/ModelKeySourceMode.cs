namespace AgentBridge.CodexLb.Configuration;

/// <summary>Явный контракт источника ключа; индивидуальный ключ всегда имеет приоритет.</summary>
public enum ModelKeySourceMode
{
    /// <summary>Общий ключ обязателен и используется только при отсутствии индивидуального.</summary>
    Shared = 0,

    /// <summary>Ключ предоставляет источник приложения; отсутствие не разрешает общий fallback.</summary>
    Individual = 1
}
