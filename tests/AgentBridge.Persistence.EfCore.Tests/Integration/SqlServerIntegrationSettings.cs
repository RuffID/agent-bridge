using Microsoft.Data.SqlClient;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Проверяет явные ресурсы отдельного MSSQL запуска до создания DI и любого I/O.</summary>
public class SqlServerIntegrationSettings
{
    /// <summary>Фиксирует согласованный endpoint, уникальную собственную БД, TLS и серверный каталог.</summary>
    public SqlServerIntegrationSettings(string connectionString, string expectedEndpoint, string expectedDatabase,
        string serverBackupDirectory, TimeSpan operationBudget)
    {
        SqlConnectionStringBuilder settings;
        try { settings = new(connectionString); }
        catch (ArgumentException) { throw new InvalidOperationException("Некорректная MSSQL конфигурация; значения скрыты."); }
        if (string.IsNullOrWhiteSpace(expectedEndpoint) || !expectedEndpoint.StartsWith("tcp:", StringComparison.Ordinal) ||
            expectedEndpoint.Split(',').Length != 2 || expectedEndpoint.IndexOf(',') <= 4 ||
            expectedEndpoint.Contains('\\') || expectedEndpoint.Contains(';') ||
            !int.TryParse(expectedEndpoint.Split(',').Last(), out int port) || port is < 1 or > 65535 ||
            !string.Equals(settings.DataSource, expectedEndpoint, StringComparison.Ordinal) ||
            !expectedDatabase.StartsWith("abverify_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(expectedDatabase[9..], "N", out Guid databaseId) || databaseId == Guid.Empty ||
            !string.Equals(settings.InitialCatalog, expectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("Требуются точный TCP endpoint с портом и собственное имя abverify_<GUID N>.");
        if (!settings.ShouldSerialize("Encrypt") || !settings.ShouldSerialize("TrustServerCertificate") ||
            !settings.ShouldSerialize("ConnectRetryCount") || settings.ConnectRetryCount != 0 ||
            settings.Pooling || settings.MultiSubnetFailover || !string.IsNullOrEmpty(settings.FailoverPartner) ||
            !string.IsNullOrEmpty(settings.AttachDBFilename) || settings.ApplicationIntent != ApplicationIntent.ReadWrite)
            throw new InvalidOperationException("Требуются явные TLS/ConnectRetryCount=0, Pooling=false и стабильный endpoint.");
        if (string.IsNullOrWhiteSpace(serverBackupDirectory) ||
            !(serverBackupDirectory.StartsWith('/') || serverBackupDirectory.StartsWith("\\\\", StringComparison.Ordinal) ||
              serverBackupDirectory.Length > 2 && char.IsAsciiLetter(serverBackupDirectory[0]) &&
              serverBackupDirectory[1] == ':' && serverBackupDirectory[2] == '\\') ||
            serverBackupDirectory.Any(char.IsControl) || operationBudget <= TimeSpan.Zero || operationBudget > TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("Требуются абсолютный серверный backup каталог и бюджет в пределах пяти минут.");
        ConnectionString = settings.ConnectionString;
        ServerBackupDirectory = serverBackupDirectory;
        OperationBudget = operationBudget;
    }

    /// <summary>Подключение с секретами; запрещено включать в evidence и сообщения.</summary>
    internal string ConnectionString { get; }
    /// <summary>Каталог на сервере; fixture его не создаёт и не удаляет.</summary>
    public string ServerBackupDirectory { get; }
    /// <summary>Явный конечный бюджет одной maintenance операции.</summary>
    public TimeSpan OperationBudget { get; }

    /// <summary>Читает только отдельные MSSQL параметры; общий PostgreSQL opt-in не используется.</summary>
    public static SqlServerIntegrationSettings FromEnvironment()
    {
        if (Environment.GetEnvironmentVariable("AGENTBRIDGE_SQLSERVER_INTEGRATION") != "1")
            throw new InvalidOperationException("MSSQL fixture требует отдельный opt-in и согласованные ресурсы.");
        if (!int.TryParse(Required("AGENTBRIDGE_SQLSERVER_BUDGET_SECONDS"), out int seconds))
            throw new InvalidOperationException("Требуется целый бюджет MSSQL в секундах.");
        return new(Required("AGENTBRIDGE_SQLSERVER_CONNECTION"), Required("AGENTBRIDGE_SQLSERVER_ENDPOINT"),
            Required("AGENTBRIDGE_SQLSERVER_DATABASE"), Required("AGENTBRIDGE_SQLSERVER_BACKUP_DIRECTORY"), TimeSpan.FromSeconds(seconds));
    }

    /// <summary>Не допускает silent skip или печать значения отсутствующей конфигурации.</summary>
    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException("Отсутствует настройка " + name + ".");
}
