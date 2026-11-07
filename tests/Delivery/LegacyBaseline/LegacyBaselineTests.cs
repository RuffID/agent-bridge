using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AgentBridge.Sub2Api.Tests;
using Xunit;

namespace AgentBridge.Delivery.LegacyBaseline.Tests;

/// <summary>Явная read-only проверка исторического комплекта и происхождения переносимого API-эталона.</summary>
public class LegacyBaselineTests
{
    /// <summary>Описи и frozen consumer DLL сохраняют зафиксированные на этапе01 размеры и SHA256.</summary>
    [Fact]
    public void PreservedBaselineStillMatchesInventories()
    {
        // Arrange
        string baseline = BaselineRoot();

        // Act
        (int preserved, int kit, int evidence) = VerifyBaseline(baseline);

        // Assert
        Assert.Equal(127, preserved);
        Assert.Equal(112, kit);
        Assert.Equal(6, evidence);
    }

    /// <summary>Переносимый текстовый эталон точно соответствует сохранённой DLL, а не новой сборке исходников.</summary>
    [Theory]
    [InlineData("AgentBridge")]
    [InlineData("AgentBridge.CodexLb")]
    [InlineData("AgentBridge.Integration")]
    public void PortableApiSnapshotMatchesPreservedAssembly(string assembly)
    {
        // Arrange
        string baseline = BaselineRoot();
        VerifyBaseline(baseline);
        string file = Path.Combine(baseline, "kit", "lib", assembly + ".dll");
        HashSet<string> expected = LegacyApiBaseline.ReadSignatures(assembly);
        string expectedHash = LegacyApiBaseline.ReadSourceHash(assembly);

        // Act
        HashSet<string> actual = PeSurfaceReader.Read(file);
        string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

        // Assert
        Assert.NotEmpty(expected);
        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    /// <summary>Сначала проверяет pinned hashes описи, затем все сохранённые файлы, не изменяя комплект.</summary>
    private static (int Preserved, int Kit, int Evidence) VerifyBaseline(string root)
    {
        using Stream resource = typeof(LegacyBaselineTests).Assembly.GetManifestResourceStream("AgentBridge.Tests.BaselineEvidence.json")
            ?? throw new InvalidOperationException("Embedded stage01 evidence is missing.");
        using JsonDocument evidence = JsonDocument.Parse(resource);
        int evidenceCount = VerifyEntries(evidence.RootElement.GetProperty("files"), root);
        using JsonDocument preservation = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "preservation.json")));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "kit", "delivery.manifest.json")));
        int preservedCount = VerifyEntries(preservation.RootElement.GetProperty("files"), root);
        int kitCount = VerifyEntries(manifest.RootElement.GetProperty("files"), Path.Combine(root, "kit"));

        return (preservedCount, kitCount, evidenceCount);
    }

    /// <summary>Читает только пути внутри явно выбранного исторического комплекта.</summary>
    private static int VerifyEntries(JsonElement entries, string root)
    {
        int count = 0;
        string prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string file = Path.GetFullPath(Path.Combine(root, entry.GetProperty("path").GetString()!));
            Assert.StartsWith(prefix, file, StringComparison.OrdinalIgnoreCase);
            using FileStream stream = File.OpenRead(file);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            Assert.Equal(entry.GetProperty("sha256").GetString(), hash, ignoreCase: true);
            Assert.Equal(entry.GetProperty("size").GetInt64(), stream.Length);
            count++;
        }

        return count;
    }

    /// <summary>Требует явный путь; нет поиска в профиле, автоматического восстановления или silent skip.</summary>
    private static string BaselineRoot()
    {
        string? root = typeof(LegacyBaselineTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "AgentBridgeLegacyBaselineRoot").Value;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
        {
            throw new InvalidOperationException("Build this explicit audit with -p:AgentBridgeLegacyBaselineRoot=<absolute preserved baseline path>.");
        }

        string path = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!File.Exists(Path.Combine(path, "preservation.json")))
        {
            throw new DirectoryNotFoundException("Preserved stage01 baseline is unavailable at the explicit AgentBridgeLegacyBaselineRoot. Restore the original files; never regenerate them from current sources.");
        }

        return path;
    }
}
