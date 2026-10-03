namespace AgentBridge.Application.Models;

/// <summary>Явный исход lifecycle модели; непустой output не определяет статус.</summary>
public enum ModelResponseStatus
{
    /// <summary>Подтверждено полное завершение.</summary>
    Completed,
    /// <summary>Результат неполный или завершение не подтверждено.</summary>
    Incomplete,
    /// <summary>Получен явный отказ модели.</summary>
    Failed,
    /// <summary>Наблюдалась отмена вызывающим кодом.</summary>
    Canceled
}
