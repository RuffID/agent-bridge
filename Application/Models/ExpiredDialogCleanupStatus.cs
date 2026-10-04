namespace AgentBridge.Application.Models;

/// <summary>Итог одного ограниченного пакета; Completed не доказывает отсутствие других истёкших строк.</summary>
public enum ExpiredDialogCleanupStatus
{
    /// <summary>Все прочитанные кандидаты удалены либо пакет пуст.</summary>
    Completed,
    /// <summary>Пакет обработан, но содержит ожидаемые отказы удаления.</summary>
    Partial,
    /// <summary>Чтение кандидатов вернуло ожидаемый отказ.</summary>
    Failed,
    /// <summary>Наблюдалась отмена вызывающего приложения.</summary>
    Canceled,
    /// <summary>Неожиданное исключение прервало обработку и продолжает распространяться.</summary>
    Interrupted
}
