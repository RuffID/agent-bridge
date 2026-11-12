namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Настройки явно вызываемого обслуживания; не запускают backup и не управляют удалением копий.</summary>
public class DatabaseBackupOptions
{
    /// <summary>Обязательный абсолютный каталог локальных копий; доступность проверяет provider при операции.</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>Для SQL Server: обязательный абсолютный каталог на сервере БД, независимо от ОС приложения.</summary>
    public string? SqlServerBackupDirectory { get; set; }

    /// <summary>Обязательный положительный срок, выбранный приложением; применять retention и удалять копии обязано приложение.</summary>
    public TimeSpan? BackupRetentionPeriod { get; set; }

    /// <summary>Для PostgreSQL: обязательный абсолютный путь pg_dump, без поиска в PATH.</summary>
    public string? PostgreSqlDumpExecutablePath { get; set; }

    /// <summary>Для PostgreSQL: явно выбранный major сервера 10+; совпадение с сервером и pg_dump проверяет provider.</summary>
    public int? PostgreSqlServerMajorVersion { get; set; }

    /// <summary>Для PostgreSQL: обязательный конечный положительный budget остановки процесса и очистки.</summary>
    public TimeSpan? PostgreSqlCleanupTimeout { get; set; }
}
