namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Настройки выбранной БД; не открывают подключение и не являются безопасным снимком.</summary>
public class DatabaseOptions
{
    /// <summary>Обязательный явный выбор провайдера; SQLite не выбирается по умолчанию.</summary>
    public DatabaseProvider? Provider { get; set; }

    /// <summary>Обязательная секретная строка подключения приложения.</summary>
    public string? ConnectionString { get; set; }
}
