using AgentBridge.Persistence.EfCore.Tests.Integration;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет выбор утилит и version guards без исполнения программ или Docker.</summary>
public class PostgreSqlIntegrationToolsTests
{
    /// <summary>Major 18 принимается у правильной программы, включая platform build suffix.</summary>
    [Theory]
    [InlineData("pg_dump", "pg_dump (PostgreSQL) 18.6\r\n")]
    [InlineData("pg_restore", "pg_restore (PostgreSQL) 18.6\n")]
    [InlineData("pg_dump", "pg_dump (PostgreSQL) 18.6 (Debian 18.6-1.pgdg13+2)\n")]
    public void AcceptsMatchingToolAndMajor(string name, string output) => PostgreSqlIntegrationTools.ValidateVersion(name, output);

    /// <summary>Другая программа, major и malformed stdout отклоняются без публикации исходного вывода.</summary>
    [Theory]
    [InlineData("pg_dump", "pg_dump (PostgreSQL) 17.6\n")]
    [InlineData("pg_restore", "pg_restore (PostgreSQL) 19.1\n")]
    [InlineData("pg_dump", "pg_restore (PostgreSQL) 18.6\n")]
    [InlineData("pg_dump", "pg_dump (PostgreSQL) 180.6\n")]
    [InlineData("pg_dump", "secret-marker")]
    [InlineData("pg_dump", "pg_dump (PostgreSQL) 18.6\nsecret-marker\n")]
    public void RejectsWrongVersionWithoutRawOutput(string name, string output)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => PostgreSqlIntegrationTools.ValidateVersion(name, output));
        Assert.DoesNotContain(output, error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>Явный невалидный override не заменяется существующей программой в search directory.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("pg_dump.exe")]
    public void InvalidOverrideDoesNotFallBack(string configuredPath)
    {
        string root = Path.Combine(Path.GetTempPath(), "abverify_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string filename = "pg_dump" + (OperatingSystem.IsWindows() ? ".exe" : "");
            string candidate = Path.Combine(root, filename);
            File.WriteAllText(candidate, "Неисполняемая заглушка для проверки выбора пути.");

            Assert.Equal(candidate, PostgreSqlIntegrationTools.ResolveExecutable(null, "pg_dump", [root]));
            Assert.Equal(candidate, PostgreSqlIntegrationTools.ResolveExecutable(candidate, "pg_dump", []));
            Assert.Throws<InvalidOperationException>(() => PostgreSqlIntegrationTools.ResolveExecutable(configuredPath, "pg_dump", [root]));
            Assert.Throws<InvalidOperationException>(() => PostgreSqlIntegrationTools.ResolveExecutable(Path.Combine(root, "missing.exe"), "pg_dump", [root]));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
