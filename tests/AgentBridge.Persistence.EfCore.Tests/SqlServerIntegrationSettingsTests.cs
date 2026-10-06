using AgentBridge.Persistence.EfCore.Tests.Integration;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Изолированно проверяет fail-fast MSSQL fixture; соединения, DI и native API не создаются.</summary>
public class SqlServerIntegrationSettingsTests
{
    private const string ENDPOINT = "tcp:fixture.invalid,15433";
    private const string DATABASE = "abverify_11111111111111111111111111111111";
    private const string CONNECTION = "Server=tcp:fixture.invalid,15433;Database=abverify_11111111111111111111111111111111;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;ConnectRetryCount=0;Pooling=false";

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
    [InlineData("TrustServerCertificate", null)]
    [InlineData("ConnectRetryCount", null)]
    [InlineData("ConnectRetryCount", "1")]
    [InlineData("Pooling", "true")]
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
}
