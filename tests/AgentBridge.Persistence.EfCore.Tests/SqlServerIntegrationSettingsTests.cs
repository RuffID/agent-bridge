using AgentBridge.Persistence.EfCore.Tests.Integration;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Изолированно проверяет fail-fast MSSQL fixture; соединения, DI и native API не создаются.</summary>
public class SqlServerIntegrationSettingsTests
{
    private const string ENDPOINT = "tcp:127.0.0.1,15433";
    private const string DATABASE = "abverify_11111111111111111111111111111111";
    private const string CONNECTION = "Server=tcp:127.0.0.1,15433;Database=abverify_11111111111111111111111111111111;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;ConnectRetryCount=0;Pooling=false";

    /// <summary>Явный TLS и серверные пути разных ОС проверяются без проверки локальной файловой системы.</summary>
    [Theory]
    [InlineData("/srv/abverify/backups")]
    [InlineData("D:\\abverify\\backups")]
    public void AcceptsExplicitResourcesWithoutIo(string directory)
    {
        SqlServerIntegrationSettings settings = new(CONNECTION, ENDPOINT, DATABASE, directory, TimeSpan.FromSeconds(45));
        Assert.Equal(directory, settings.ServerBackupDirectory);
        Assert.Equal(TimeSpan.FromSeconds(45), settings.OperationBudget);
    }

    /// <summary>Defaults TLS/retry и нестабильные подключения отклоняются до I/O.</summary>
    [Theory]
    [InlineData("Encrypt", null)]
    [InlineData("Encrypt", "false")]
    [InlineData("TrustServerCertificate", null)]
    [InlineData("ConnectRetryCount", null)]
    [InlineData("ConnectRetryCount", "1")]
    [InlineData("Pooling", "true")]
    [InlineData("Pooling", null)]
    [InlineData("MultiSubnetFailover", "true")]
    [InlineData("Failover Partner", "other.invalid")]
    [InlineData("ApplicationIntent", "ReadOnly")]
    [InlineData("AttachDBFilename", "D:\\foreign.mdf")]
    [InlineData("Server", "tcp:other.invalid,15433")]
    [InlineData("Database", "master")]
    public void RejectsUnsafeOrImplicitConnection(string key, string? value)
    {
        SqlConnectionStringBuilder connection = new(CONNECTION);
        if (value is null) connection.Remove(key);
        else connection[key] = value;
        Assert.Throws<InvalidOperationException>(() => new SqlServerIntegrationSettings(connection.ConnectionString,
            ENDPOINT, DATABASE, "/srv/abverify/backups", TimeSpan.FromSeconds(45)));
    }

    /// <summary>Ресурсы не заменяются значениями по умолчанию и не угадываются по префиксу.</summary>
    [Theory]
    [InlineData("fixture.invalid", DATABASE, "/srv/abverify/backups", 45)]
    [InlineData("tcp:,15433", DATABASE, "/srv/abverify/backups", 45)]
    [InlineData("tcp:foreign.invalid,15433", DATABASE, "/srv/abverify/backups", 45)]
    [InlineData("tcp:127.0.0.1,0", DATABASE, "/srv/abverify/backups", 45)]
    [InlineData(ENDPOINT, "abverify_shared", "/srv/abverify/backups", 45)]
    [InlineData(ENDPOINT, DATABASE, "backups", 45)]
    [InlineData(ENDPOINT, DATABASE, "/srv/abverify/\n", 45)]
    [InlineData(ENDPOINT, DATABASE, "/srv/abverify/backups", 0)]
    [InlineData(ENDPOINT, DATABASE, "/srv/abverify/backups", 301)]
    public void RejectsUnboundedOrUnidentifiedResources(string endpoint, string database, string directory, int seconds)
    {
        Assert.Throws<InvalidOperationException>(() => new SqlServerIntegrationSettings(CONNECTION,
            endpoint, database, directory, TimeSpan.FromSeconds(seconds)));
    }

    /// <summary>Ошибка разбора не раскрывает исходную строку или секрет через InnerException.</summary>
    [Fact]
    public void MalformedConnectionErrorIsSanitized()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new SqlServerIntegrationSettings(
            "Password=secret-marker;unsupported-secret-marker=value", ENDPOINT, DATABASE, "/srv/abverify/backups", TimeSpan.FromSeconds(45)));
        Assert.DoesNotContain("secret-marker", error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>Ресурсы контейнера задают точный endpoint/БД и явные TLS/budget без переменных окружения.</summary>
    [Fact]
    public void ContainerResourcesProduceBoundedConfiguration()
    {
        SqlServerIntegrationSettings settings = SqlServerIntegrationSettings.FromContainer(
            "Server=localhost,9999;Database=master;User ID=sa;Password=temporary-secret", 15433,
            DATABASE, "/var/opt/mssql/abverify_backups");
        SqlConnectionStringBuilder connection = new(settings.ConnectionString);

        Assert.Equal(ENDPOINT, connection.DataSource);
        Assert.Equal(DATABASE, connection.InitialCatalog);
        Assert.Equal("temporary-secret", connection.Password);
        Assert.Equal("sa", connection.UserID);
        Assert.False(connection.IntegratedSecurity);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, connection.Encrypt);
        Assert.True(connection.TrustServerCertificate);
        Assert.False(connection.Pooling);
        Assert.Equal(0, connection.ConnectRetryCount);
        Assert.Equal(15, connection.ConnectTimeout);
        Assert.Equal(TimeSpan.FromMinutes(2), settings.OperationBudget);
    }

    /// <summary>Формирование настроек не ослабляет проверки имени БД, порта и серверного пути.</summary>
    [Theory]
    [InlineData(0, DATABASE, "/var/opt/mssql/backups")]
    [InlineData(15433, "master", "/var/opt/mssql/backups")]
    [InlineData(15433, DATABASE, "relative")]
    public void ContainerResourcesRejectInvalidBoundary(ushort port, string database, string path)
    {
        Assert.Throws<InvalidOperationException>(() => SqlServerIntegrationSettings.FromContainer(
            "User ID=sa;Password=temporary-secret", port, database, path));
    }
}
