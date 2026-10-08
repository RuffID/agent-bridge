using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Проверяет поставку собственных DLL и обязательных NuGet-зависимостей без загрузки сборок.</summary>
public class NuGetDeliveryTests
{
    /// <summary>Каждая платформа сохраняет все собственные DLL/XML и прямые пакетные зависимости SDK-графа.</summary>
    [Theory]
    [InlineData("SqlServer", "win-x64")]
    [InlineData("SqlServer", "linux-x64")]
    [InlineData("SqlServer", "linux-arm64")]
    [InlineData("Sqlite", "win-x64")]
    [InlineData("PostgreSql", "win-x64")]
    public void LocalClosureAndRequiredPackagesMatchSourceGraph(string provider, string rid)
    {
        string root = Path.Combine(Root(), provider);
        string sourceRoot = Environment.GetEnvironmentVariable("AGENTBRIDGE_DELIVERY_ROOT")
            ?? throw new InvalidOperationException("Нужен каталог исходных SDK-комплектов.");
        string source = Path.Combine(sourceRoot, provider, rid);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "delivery.manifest.json")));
        using JsonDocument sourceManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "delivery.manifest.json")));
        using JsonDocument deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "evidence", "AgentBridge.Delivery.deps.json")));
        JsonElement libraries = deps.RootElement.GetProperty("libraries");
        JsonElement target = deps.RootElement.GetProperty("targets").GetProperty(deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!);
        Assert.Equal(3, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("dll-nuget", manifest.RootElement.GetProperty("layout").GetString());
        Assert.Equal(provider, manifest.RootElement.GetProperty("provider").GetString());
        Assert.Contains(rid, manifest.RootElement.GetProperty("rids").EnumerateArray().Select(item => item.GetString()));

        Dictionary<string, JsonElement> expectedFiles = sourceManifest.RootElement.GetProperty("files").EnumerateArray()
            .Where(entry => entry.GetProperty("kind").GetString() is "managed" or "xml")
            .Where(entry => libraries.GetProperty(entry.GetProperty("origin").GetString()!).GetProperty("type").GetString() != "package")
            .ToDictionary(entry => entry.GetProperty("path").GetString()!, StringComparer.Ordinal);
        JsonElement[] actualFiles = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Where(entry => entry.GetProperty("kind").GetString() is "managed" or "xml").ToArray();
        Assert.Equal(expectedFiles.Keys.Order(StringComparer.Ordinal), actualFiles.Select(entry => entry.GetProperty("path").GetString()!).Order(StringComparer.Ordinal));
        foreach (JsonElement entry in actualFiles)
        {
            string path = entry.GetProperty("path").GetString()!;
            Assert.Equal(expectedFiles[path].GetProperty("sha256").GetString(), Hash(Path.Combine(root, path)));
        }

        Dictionary<string, string> packages = libraries.EnumerateObject().Where(item => item.Value.GetProperty("type").GetString() == "package")
            .ToDictionary(item => item.Name.Split('/')[0], item => item.Name.Split('/')[1], StringComparer.Ordinal);
        Dictionary<string, string> expectedPackages = new(StringComparer.Ordinal);
        foreach (JsonProperty project in target.EnumerateObject())
        {
            if (libraries.GetProperty(project.Name).GetProperty("type").GetString() != "project"
                || !project.Value.TryGetProperty("dependencies", out JsonElement dependencies)) continue;

            foreach (JsonProperty dependency in dependencies.EnumerateObject())
            {
                if (packages.TryGetValue(dependency.Name, out string? version)) expectedPackages[dependency.Name] = version;
            }
        }

        Dictionary<string, string> required = manifest.RootElement.GetProperty("requiredPackages").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.GetProperty("version").GetString()!, StringComparer.Ordinal);
        Assert.Equal(expectedPackages.OrderBy(item => item.Key, StringComparer.Ordinal), required.OrderBy(item => item.Key, StringComparer.Ordinal));
        XDocument packageProps = XDocument.Load(Path.Combine(root, "AgentBridge.Dependencies.props"));
        Dictionary<string, string> declared = packageProps.Descendants("PackageReference").Where(item => item.Attribute("Include") != null)
            .ToDictionary(item => item.Attribute("Include")!.Value, item => item.Attribute("Version")!.Value, StringComparer.Ordinal);
        Assert.Equal(required.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => KeyValuePair.Create(item.Key, "[" + item.Value + "]")),
            declared.OrderBy(item => item.Key, StringComparer.Ordinal));
    }

    /// <summary>Состав комплекта полностью учтён manifest; бинарные ссылки указывают только на собственные сборки.</summary>
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Sqlite")]
    [InlineData("PostgreSql")]
    public void BundleContainsOnlyLocalAssetsAndPortableReferences(string provider)
    {
        string root = Path.Combine(Root(), provider);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "delivery.manifest.json")));
        JsonElement[] entries = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        foreach (JsonElement entry in entries)
        {
            string path = entry.GetProperty("path").GetString()!;
            Assert.DoesNotContain('\\', path);
            Assert.DoesNotContain(':', path);
            Assert.DoesNotContain(path.Split('/'), part => part is "" or "." or "..");
            string file = Path.Combine(root, path);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(file));
            Assert.Equal(entry.GetProperty("size").GetInt64(), new FileInfo(file).Length);
            Assert.Contains(entry.GetProperty("kind").GetString(), new[] { "managed", "xml", "metadata" });
        }

        string[] actual = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToArray();
        Assert.Equal(entries.Select(entry => entry.GetProperty("path").GetString()!).Append("delivery.manifest.json").Order(StringComparer.Ordinal),
            actual.Order(StringComparer.Ordinal));
        XDocument references = XDocument.Load(Path.Combine(root, "AgentBridge.References.props"));
        string[] managed = entries.Where(entry => entry.GetProperty("kind").GetString() == "managed")
            .Select(entry => Path.GetFullPath(Path.Combine(root, entry.GetProperty("path").GetString()!))).ToArray();
        string[] hintPaths = references.Descendants("HintPath").Select(item =>
            Path.GetFullPath(item.Value.Replace("$(MSBuildThisFileDirectory)", root + Path.DirectorySeparatorChar, StringComparison.Ordinal))).ToArray();
        Assert.Equal(managed.Order(StringComparer.Ordinal), hintPaths.Order(StringComparer.Ordinal));
        Assert.Equal(10, managed.Length);
        foreach (string file in managed)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            Assert.True(name.StartsWith("AgentBridge", StringComparison.Ordinal) || name.StartsWith("EFCoreLibrary", StringComparison.Ordinal)
                || name == "HttpClientLibrary", name);
        }
    }

    /// <summary>Получает корень подготовленной матрицы DLL/NuGet-комплектов.</summary>
    private static string Root() => Environment.GetEnvironmentVariable("AGENTBRIDGE_NUGET_DELIVERY_ROOT")
        ?? throw new InvalidOperationException("Нужен каталог DLL/NuGet-поставки.");

    /// <summary>Считает SHA256 фактических файлов без загрузки сборки.</summary>
    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
}
