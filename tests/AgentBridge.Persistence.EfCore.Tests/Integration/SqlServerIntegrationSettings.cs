using Microsoft.Data.SqlClient;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Проверяет ресурсы собственного MSSQL-контейнера до создания DI и подключения к БД.</summary>
public class SqlServerIntegrationSettings
{
    /// <summary>Фиксирует точный loopback endpoint, уникальную БД, TLS и серверный каталог.</summary>
    public SqlServerIntegrationSettings(string connectionString, string expectedEndpoint, string expectedDatabase,
        string serverBackupDirectory, TimeSpan operationBudget)
    {
        SqlConnectionStringBuilder settings;
        try { settings = new(connectionString); }
        catch (ArgumentException) { throw new InvalidOperationException("Некорректная MSSQL конфигурация; значения скрыты."); }
        if (string.IsNullOrWhiteSpace(expectedEndpoint) || !expectedEndpoint.StartsWith("tcp:", StringComparison.Ordinal) ||
            expectedEndpoint.Split(',').Length != 2 || expectedEndpoint.IndexOf(',') <= 4 ||
            expectedEndpoint.Contains('\\') || expectedEndpoint.Contains(';') ||
            expectedEndpoint.Split(',')[0] != "tcp:127.0.0.1" ||
            !int.TryParse(expectedEndpoint.Split(',').Last(), out int port) || port is < 1 or > 65535 ||
            !string.Equals(settings.DataSource, expectedEndpoint, StringComparison.Ordinal) ||
            !expectedDatabase.StartsWith("abverify_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(expectedDatabase[9..], "N", out Guid databaseId) || databaseId == Guid.Empty ||
            !string.Equals(settings.InitialCatalog, expectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("Требуются точный TCP endpoint с портом и собственное имя abverify_<GUID N>.");
        if (!settings.ShouldSerialize("Encrypt") || settings.Encrypt != SqlConnectionEncryptOption.Mandatory ||
            !settings.ShouldSerialize("TrustServerCertificate") || !settings.ShouldSerialize("Pooling") ||
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
    /// <summary>Каталог внутри собственного контейнера; удаляется вместе с контейнером.</summary>
    public string ServerBackupDirectory { get; }
    /// <summary>Явный конечный бюджет одной maintenance операции.</summary>
    public TimeSpan OperationBudget { get; }

    /// <summary>Формирует конфигурацию из контейнера, сохраняя секрет только в памяти.</summary>
    internal static SqlServerIntegrationSettings FromContainer(string containerConnectionString, ushort port,
        string database, string serverBackupDirectory)
    {
        string endpoint = "tcp:127.0.0.1," + port;
        SqlConnectionStringBuilder connection;
        try { connection = new(containerConnectionString); }
        catch (ArgumentException) { throw new InvalidOperationException("Некорректная MSSQL конфигурация; значения скрыты."); }

        connection.DataSource = endpoint;
        connection.InitialCatalog = database;
        connection.Encrypt = SqlConnectionEncryptOption.Mandatory;
        // Сертификат временного SQL Server самоподписанный; TLS остаётся обязательным.
        connection.TrustServerCertificate = true;
        connection.Pooling = false;
        connection.ConnectRetryCount = 0;
        connection.ConnectTimeout = 15;

        return new(connection.ConnectionString, endpoint, database, serverBackupDirectory, TimeSpan.FromMinutes(2));
    }
}
