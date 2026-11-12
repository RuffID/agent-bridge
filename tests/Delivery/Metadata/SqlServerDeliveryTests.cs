using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Xunit;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Проверяет восстановленные SQL Server kits без загрузки или исполнения DLL.</summary>
public class SqlServerDeliveryTests
{
    /// <summary>Каждый RID содержит выбранный provider, migrations, SqlClient и полную managed closure без PostgreSQL/SQLite.</summary>
    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    public void SqlServerKitContainsOnlySelectedProviderAndCompleteClosure(string rid)
    {
        // Arrange
        string kit = Path.Combine(DeliveryTestPaths.SdkRoot, "SqlServer", rid);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(kit, "delivery.manifest.json")));
        Dictionary<string, string> files = Directory.GetFiles(Path.Combine(kit, "lib"), "*.dll")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
        HashSet<string> framework = Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        List<string> missingReferences = [];
        foreach (string file in files.Values)
        {
            using FileStream stream = File.OpenRead(file);
            using PEReader pe = new(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                string name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                if (!files.ContainsKey(name) && !framework.Contains(name))
                {
                    missingReferences.Add(name);
                }
            }
        }

        // Assert
        Assert.Equal("SqlServer", manifest.RootElement.GetProperty("provider").GetString());
        Assert.Equal(rid, manifest.RootElement.GetProperty("rid").GetString());
        Assert.Empty(missingReferences);
        Assert.Contains("AgentBridge.Persistence.SqlServer", files.Keys);
        Assert.Contains("AgentBridge.Persistence.Migrations.SqlServer", files.Keys);
        Assert.Contains("EFCoreLibrary.Maintenance.SqlServer", files.Keys);
        Assert.Contains("Microsoft.EntityFrameworkCore.SqlServer", files.Keys);
        Assert.Contains("Microsoft.Data.SqlClient", files.Keys);
        Assert.Single(files.Keys, name => name.StartsWith("AgentBridge.Persistence.Migrations.", StringComparison.Ordinal));
        Assert.Single(files.Keys, name => name.StartsWith("AgentBridge.Persistence.", StringComparison.Ordinal)
            && name != "AgentBridge.Persistence.EfCore" && !name.StartsWith("AgentBridge.Persistence.Migrations.", StringComparison.Ordinal));
        Assert.DoesNotContain(files.Keys, name => name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PostgreSql", StringComparison.OrdinalIgnoreCase) || name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.Design", files.Keys);
    }
}
