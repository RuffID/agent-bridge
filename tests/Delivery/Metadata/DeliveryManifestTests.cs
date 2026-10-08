using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Проверяет immutable состав, portable paths и одинаковый AgentBridge API в MSSQL комплектах.</summary>
public class DeliveryManifestTests
{
    /// <summary>Все файлы и SDK runtime/native entries имеют точный case, размер, hash и выбранный RID.</summary>
    [Theory]
    [InlineData("SqlServer", "win-x64")]
    [InlineData("SqlServer", "linux-x64")]
    [InlineData("SqlServer", "linux-arm64")]
    [InlineData("Sqlite", "win-x64")]
    [InlineData("PostgreSql", "win-x64")]
    public void ManifestMatchesCompleteSdkClosure(string provider, string rid)
    {
        string root = GetKit(provider, rid);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "delivery.manifest.json")));
        JsonElement document = manifest.RootElement;
        Assert.Equal(rid, document.GetProperty("rid").GetString());
        Assert.Equal(provider, document.GetProperty("provider").GetString());
        Assert.True(document.GetProperty("frameworkDependent").GetBoolean());
        Dictionary<string, JsonElement> entries = document.GetProperty("files").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("path").GetString()!, StringComparer.Ordinal);
        foreach ((string relative, JsonElement entry) in entries)
        {
            ValidateRelativePath(relative);
            Assert.DoesNotContain(':', entry.GetProperty("asset").GetString()!);
            VerifyFile(root, relative, entry.GetProperty("size").GetInt64(), entry.GetProperty("sha256").GetString()!);
            string file = Path.Combine(root, relative);
            Assert.True(File.Exists(file), $"Missing asset: {relative}");
            Assert.Equal(entry.GetProperty("size").GetInt64(), new FileInfo(file).Length);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(file));
            if (entry.GetProperty("kind").GetString() is "managed" or "resource")
            {
                using FileStream stream = File.OpenRead(file);
                using PEReader pe = new(stream);
                Assert.True(pe.HasMetadata);
                Machine targetMachine = rid == "linux-arm64" ? Machine.Arm64 : Machine.Amd64;
                Assert.True(pe.PEHeaders.CoffHeader.Machine == Machine.I386 || pe.PEHeaders.CoffHeader.Machine == targetMachine);
                if (pe.PEHeaders.CoffHeader.Machine == Machine.I386)
                {
                    Assert.Equal((CorFlags)0, pe.PEHeaders.CorHeader!.Flags & CorFlags.Requires32Bit);
                }
                if (entry.GetProperty("kind").GetString() == "resource")
                {
                    MetadataReader metadata = pe.GetMetadataReader();
                    string culture = metadata.GetString(metadata.GetAssemblyDefinition().Culture);
                    Assert.Equal(entry.GetProperty("culture").GetString(), culture);
                    Assert.Equal("resources/" + culture + "/" + Path.GetFileName(file), relative);
                }
            }
        }
        string[] actual = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => file != "delivery.manifest.json").Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(entries.Keys.Order(StringComparer.Ordinal), actual);
        using JsonDocument deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "evidence", "AgentBridge.Delivery.deps.json")));
        string targetName = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
        Assert.EndsWith('/' + rid, targetName);
        foreach (JsonProperty library in deps.RootElement.GetProperty("targets").GetProperty(targetName).EnumerateObject())
        {
            if (library.Name.StartsWith("AgentBridge.Delivery/", StringComparison.Ordinal)) continue;
            foreach (string kind in new[] { "runtime", "native", "resources" })
            {
                if (!library.Value.TryGetProperty(kind, out JsonElement assets)) continue;
                foreach (JsonProperty asset in assets.EnumerateObject())
                {
                    if (asset.Name.EndsWith("/_._", StringComparison.Ordinal)) continue;
                    string relative = kind switch
                    {
                        "runtime" => "lib/" + Path.GetFileName(asset.Name),
                        "resources" => "resources/" + asset.Value.GetProperty("locale").GetString() + "/" + Path.GetFileName(asset.Name),
                        _ => $"native/{rid}/" + Path.GetFileName(asset.Name)
                    };
                    Assert.True(entries.ContainsKey(relative), $"SDK closure missing {library.Name}: {asset.Name}");
                }
            }
        }
        Assert.Equal(provider == "SqlServer" ? 13 : 0, entries.Values.Count(entry => entry.GetProperty("kind").GetString() == "resource"));
    }

    /// <summary>Cross-build сохраняет public types/method signatures и generated XML шести AgentBridge assembly выбранного провайдера.</summary>
    [Fact]
    public void SqlServerManagedApiAndXmlAreIdenticalAcrossRids()
    {
        string baseline = Path.Combine(GetKit("SqlServer", "win-x64"), "lib");
        string[] names = Directory.GetFiles(baseline, "AgentBridge*.dll").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(6, names.Length);
        foreach (string rid in new[] { "linux-x64", "linux-arm64" })
        {
            string other = Path.Combine(GetKit("SqlServer", rid), "lib");
            Assert.Equal(names, Directory.GetFiles(other, "AgentBridge*.dll").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal));
            foreach (string name in names)
            {
                Assert.Equal(PublicSignatures(Path.Combine(baseline, name)), PublicSignatures(Path.Combine(other, name)));
                Assert.Equal(Hash(Path.Combine(baseline, Path.ChangeExtension(name, ".xml"))), Hash(Path.Combine(other, Path.ChangeExtension(name, ".xml"))));
            }
        }
    }

    /// <summary>Обнаруживает escape/absolute paths без изменения actual kit.</summary>
    [Theory]
    [InlineData("../e_sqlite3.dll")]
    [InlineData("native\\win-x64\\e_sqlite3.dll")]
    [InlineData("C:/native/e_sqlite3.dll")]
    [InlineData("/native/e_sqlite3.dll")]
    public void NonPortablePathsAreRejected(string path) => Assert.Throws<InvalidDataException>(() => ValidateRelativePath(path));

    /// <summary>Missing native и неверный filename case отклоняются и на нечувствительном к регистру filesystem.</summary>
    [Fact]
    public void RequiredNativeAssetUsesExactCaseAndCannotBeMissing()
    {
        string root = GetKit("SqlServer", "win-x64");
        RequireExactFile(root, "native/win-x64/Microsoft.Data.SqlClient.SNI.dll");
        Assert.Throws<InvalidDataException>(() => RequireExactFile(root, "native/win-x64/MICROSOFT.DATA.SQLCLIENT.SNI.DLL"));
        Assert.Throws<InvalidDataException>(() => RequireExactFile(root, "native/win-x64/missing.dll"));
    }

    /// <summary>Ресурс сохраняет culture/case; missing asset и неверный SHA отклоняются без изменения комплекта.</summary>
    [Theory]
    [InlineData("ru")]
    [InlineData("pt-BR")]
    [InlineData("zh-Hans")]
    public void ResourceRejectsMissingCaseAndHashMismatch(string culture)
    {
        string root = GetKit("SqlServer", "win-x64");
        string relative = $"resources/{culture}/Microsoft.Data.SqlClient.resources.dll";
        string file = Path.Combine(root, relative);
        long size = new FileInfo(file).Length;
        string hash = Hash(file);
        VerifyFile(root, relative, size, hash);
        Assert.Throws<InvalidDataException>(() => VerifyFile(root, relative.Replace("Microsoft", "MICROSOFT", StringComparison.Ordinal), size, hash));
        Assert.Throws<InvalidDataException>(() => VerifyFile(root, $"resources/{culture}/missing.dll", size, hash));
        Assert.Throws<InvalidDataException>(() => VerifyFile(root, relative, size, new string('0', 64)));
    }

    /// <summary>Читает public metadata signatures без загрузки типов.</summary>
    private static string[] PublicSignatures(string file)
    {
        using FileStream stream = File.OpenRead(file);
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        List<string> signatures = [];
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            TypeAttributes visibility = type.Attributes & TypeAttributes.VisibilityMask;
            if (visibility is not (TypeAttributes.Public or TypeAttributes.NestedPublic)) continue;
            string typeName = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
            signatures.Add(typeName + ":" + type.Attributes);
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                MethodAttributes access = method.Attributes & MethodAttributes.MemberAccessMask;
                if (access is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem)
                    signatures.Add(typeName + ":" + metadata.GetString(method.Name) + ":" + method.Attributes + ":" + Convert.ToHexString(metadata.GetBlobBytes(method.Signature)));
            }
            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                FieldDefinition field = metadata.GetFieldDefinition(fieldHandle);
                if ((field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public)
                    signatures.Add(typeName + ":" + metadata.GetString(field.Name) + ":" + Convert.ToHexString(metadata.GetBlobBytes(field.Signature)));
            }
        }
        return signatures.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Отклоняет непереносимые пути до обращения к filesystem.</summary>
    private static void ValidateRelativePath(string path)
    {
        if (path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || path.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Требуется portable relative path.");
    }

    /// <summary>Проверяет каждый сегмент пути по фактическим именам, сохраняя Linux case contract на Windows.</summary>
    private static void RequireExactFile(string root, string relative)
    {
        ValidateRelativePath(relative);
        string current = root;
        foreach (string segment in relative.Split('/'))
        {
            string? child = Directory.EnumerateFileSystemEntries(current).SingleOrDefault(entry => Path.GetFileName(entry).Equals(segment, StringComparison.Ordinal));
            current = child ?? throw new InvalidDataException("Отсутствует asset с точным case: " + relative);
        }
        if (!File.Exists(current)) throw new InvalidDataException("Ожидается файл: " + relative);
    }

    /// <summary>Отклоняет missing/case/size/hash disagreement actual файла и manifest.</summary>
    private static void VerifyFile(string root, string relative, long size, string hash)
    {
        RequireExactFile(root, relative);
        string file = Path.Combine(root, relative);
        if (new FileInfo(file).Length != size || Hash(file) != hash)
            throw new InvalidDataException("Размер или SHA256 не совпадает с manifest: " + relative);
    }

    /// <summary>Вычисляет SHA256 исходных bytes.</summary>
    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    /// <summary>Выбирает проектный SDK-комплект нужного провайдера и RID.</summary>
    private static string GetKit(string provider, string rid) => Path.Combine(DeliveryTestPaths.SdkRoot, provider, rid);
}
