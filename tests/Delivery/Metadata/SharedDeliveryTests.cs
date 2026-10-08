using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Проверки общей поставки: полный SDK состав каждого RID, dedup, native metadata и переносимые MSBuild paths.</summary>
public class SharedDeliveryTests
{
    /// <summary>Каждый RID сохраняет все runtime assets исходного manifest и не получает другие провайдеры БД.</summary>
    [Theory]
    [InlineData("win-x64")]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    public void PlatformMatchesSourceClosureAndPortableBuildItems(string rid)
    {
        string root = Root();
        string platform = Path.Combine(root, "platform", rid);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(platform, "delivery.manifest.json")));
        JsonElement document = manifest.RootElement;
        Assert.Equal(2, document.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("shared", document.GetProperty("layout").GetString());
        Assert.Equal("Release", document.GetProperty("configuration").GetString());
        Assert.Equal("SqlServer", document.GetProperty("provider").GetString());
        Assert.Equal(rid, document.GetProperty("rid").GetString());
        JsonElement[] entries = document.GetProperty("files").EnumerateArray().ToArray();
        foreach (JsonElement entry in entries)
        {
            string relative = entry.GetProperty("path").GetString()!;
            Assert.DoesNotContain('\\', relative);
            Assert.DoesNotContain(':', relative);
            Assert.DoesNotContain(relative.Split('/'), part => part is "" or "." or "..");
            Assert.True(relative.StartsWith("common/", StringComparison.Ordinal) || relative.StartsWith("platform/" + rid + "/", StringComparison.Ordinal)
                || relative == "AgentBridge.Delivery.props");
            string file = Path.Combine(root, relative);
            Assert.True(File.Exists(file), relative);
            Assert.Equal(entry.GetProperty("size").GetInt64(), new FileInfo(file).Length);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(file));
            if (entry.GetProperty("kind").GetString() == "native")
            {
                NativeAssetMetadata.Validate(File.ReadAllBytes(file), rid);
                Assert.StartsWith("platform/" + rid + "/native/", relative);
            }
        }

        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(Path.Combine(platform, "evidence", "source.manifest.json")));
        Dictionary<string, JsonElement> sourceAssets = source.RootElement.GetProperty("files").EnumerateArray()
            .Where(entry => entry.GetProperty("kind").GetString() is not ("props" or "deps"))
            .ToDictionary(entry => entry.GetProperty("path").GetString()!, StringComparer.Ordinal);
        Dictionary<string, JsonElement> mapped = entries.Where(entry => entry.TryGetProperty("sourcePath", out _))
            .ToDictionary(entry => entry.GetProperty("sourcePath").GetString()!, StringComparer.Ordinal);
        Assert.Equal(sourceAssets.Keys.Order(StringComparer.Ordinal), mapped.Keys.Order(StringComparer.Ordinal));
        foreach ((string path, JsonElement entry) in mapped)
        {
            Assert.Equal(sourceAssets[path].GetProperty("sha256").GetString(), entry.GetProperty("sha256").GetString());
            Assert.Equal(sourceAssets[path].GetProperty("kind").GetString(), entry.GetProperty("kind").GetString());
        }
        string[] managed = mapped.Values.Where(entry => entry.GetProperty("kind").GetString() == "managed")
            .Select(entry => entry.GetProperty("assemblyName").GetString()!).ToArray();
        Assert.Contains("AgentBridge.Persistence.SqlServer", managed);
        Assert.DoesNotContain(managed, name => name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PostgreSql", StringComparison.OrdinalIgnoreCase) || name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));

        XDocument props = XDocument.Load(Path.Combine(platform, "AgentBridge.Delivery.variant.props"));
        Assert.Equal(rid, props.Descendants("AgentBridgeDeliveryRid").Single().Value);
        HashSet<string> expected = mapped.Values.Where(entry => entry.GetProperty("kind").GetString() == "managed")
            .Select(entry => Path.GetFullPath(Path.Combine(root, entry.GetProperty("path").GetString()!))).ToHashSet(StringComparer.Ordinal);
        string[] references = props.Descendants("HintPath").Select(element =>
            Path.GetFullPath(element.Value.Replace("$(MSBuildThisFileDirectory)", platform + Path.DirectorySeparatorChar, StringComparison.Ordinal))).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), references.Order(StringComparer.Ordinal));
        Assert.Equal(managed.Length, props.Descendants("Reference").Count());
        foreach (XElement item in props.Descendants("None"))
        {
            string include = item.Attribute("Include")!.Value.Replace("$(MSBuildThisFileDirectory)", platform + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            Assert.True(File.Exists(include));
            Assert.Equal("PreserveNewest", item.Attribute("CopyToPublishDirectory")!.Value);
        }
    }

    /// <summary>Совпадающий nonnative asset любых двух RID физически хранится один раз; лишних файлов нет.</summary>
    [Fact]
    public void SharedAssetsAreStoredOnceAndBundleHasNoUnlistedFiles()
    {
        string root = Root();
        Dictionary<string, string> locations = new(StringComparer.Ordinal);
        HashSet<string> expected = new(StringComparer.Ordinal);
        int shared = 0;
        foreach (string rid in new[] { "win-x64", "linux-x64", "linux-arm64" })
        {
            string manifestPath = $"platform/{rid}/delivery.manifest.json";
            expected.Add(manifestPath);
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, manifestPath)));
            foreach (JsonElement entry in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                string path = entry.GetProperty("path").GetString()!;
                expected.Add(path);
                if (entry.GetProperty("kind").GetString() is "native" or "metadata") continue;
                string key = entry.GetProperty("sourcePath").GetString() + "|" + entry.GetProperty("sha256").GetString();
                if (locations.TryGetValue(key, out string? previous))
                {
                    Assert.Equal(previous, path);
                    Assert.StartsWith("common/", path);
                    shared++;
                }
                else locations.Add(key, path);
            }
        }

        Assert.True(shared > 0);
        string[] actual = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    /// <summary>Выбирает подготовленную проектную поставку без загрузки DLL и исполнения native.</summary>
    private static string Root() => DeliveryTestPaths.SharedRoot;

    /// <summary>Считает SHA256 фактических bytes.</summary>
    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
}
