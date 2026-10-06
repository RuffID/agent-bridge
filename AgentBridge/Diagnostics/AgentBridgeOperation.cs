namespace AgentBridge.Diagnostics;

/// <summary>Закрытые имена диагностируемых операций; наличие имени не означает реализации сценария.</summary>
public enum AgentBridgeOperation
{
    /// <summary>Явная проверка конфигурации подключающим приложением.</summary>
    ConfigurationValidation,

    /// <summary>Обращение к модели.</summary>
    ModelRequest,

    /// <summary>Сжатие рабочего контекста.</summary>
    ContextCompaction,

    /// <summary>Выполнение инструмента приложения.</summary>
    ToolExecution,

    /// <summary>Операция хранения диалога.</summary>
    DialogStorage,

    /// <summary>Обслуживание базы данных.</summary>
    DatabaseMaintenance
}
