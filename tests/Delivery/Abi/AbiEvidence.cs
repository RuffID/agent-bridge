using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Проверяет historical baseline и fresh manifest до передачи PE metadata в ABI verifier.</summary>
internal static class AbiEvidence
{
    /// <summary>Читает только явно заданные paths, без defaults профиля пользователя.</summary>
    internal static string Root(string key)
    {
        string? path = typeof(AbiEvidence).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == key).Value;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("Explicit absolute MSBuild property is required: " + key);
        }

        return Path.GetFullPath(path);
    }

    /// <summary>Сверяет pinned evidence,127 сохранённых файлов и112 entries старого kit.</summary>
    internal static string VerifyBaseline()
    {
        string root = Root("AgentBridgeLegacyBaselineRoot");
        using Stream resource = typeof(AbiEvidence).Assembly.GetManifestResourceStream("AgentBridge.Tests.BaselineEvidence.json")
            ?? throw new InvalidOperationException("Missing pinned baseline evidence.");
        using JsonDocument evidence = JsonDocument.Parse(resource);

        VerifyEntries(evidence.RootElement.GetProperty("files"), root, 6);
        using JsonDocument preservation = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "preservation.json")));
        VerifyEntries(preservation.RootElement.GetProperty("files"), root, 127);
        VerifyKit(Path.Combine(root, "kit"), expectedCount: 112);

        return root;
    }

    /// <summary>Выбирает один variant; обязательные hashes проверяются и для всех зависимостей.</summary>
    internal static string CurrentKit(string provider, string rid)
    {
        string root = Root("AgentBridgeCurrentDeliveryRoot");
        string kit = Path.Combine(root, "sdk", provider, rid);
        VerifyKit(kit);

        return kit;
    }

    /// <summary>Собирает metadata references одного kit и runtime, исключая assemblies runner.</summary>
    internal static IReadOnlyList<MetadataReference> References(string? kit = null)
    {
        Dictionary<string, string> paths = Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
        if (kit is not null)
        {
            foreach (string path in Directory.GetFiles(Path.Combine(kit, "lib"), "*.dll"))
            {
                paths[Path.GetFileNameWithoutExtension(path)] = path;
            }
        }

        return paths.Values.Where(IsManaged).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }

    /// <summary>Runtime содержит также native DLL: они не являются входами компилятора metadata.</summary>
    private static bool IsManaged(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using PEReader reader = new(stream);

        return reader.HasMetadata;
    }

    /// <summary>Сверяет все manifest entries и запрещает неучтённые файлы.</summary>
    private static void VerifyKit(string kit, int? expectedCount = null)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(kit, "delivery.manifest.json")));
        JsonElement files = manifest.RootElement.GetProperty("files");
        int count = files.GetArrayLength();
        if (expectedCount is { } expected && count != expected)
        {
            throw new InvalidDataException("Unexpected preserved manifest count.");
        }

        VerifyEntries(files, kit, count);
        if (Directory.GetFiles(kit, "*", SearchOption.AllDirectories).Length != count + 1)
        {
            throw new InvalidDataException("Unlisted delivery files.");
        }
    }

    /// <summary>Проверяет containment, уникальность пути, размер и SHA256 без записи в inputs.</summary>
    private static void VerifyEntries(JsonElement files, string root, int expectedCount)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        foreach (JsonElement file in files.EnumerateArray())
        {
            string relative = file.GetProperty("path").GetString()!;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !paths.Add(path))
            {
                throw new InvalidDataException("Unsafe or duplicate inventory path.");
            }

            using FileStream stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            if (stream.Length != file.GetProperty("size").GetInt64() ||
                !hash.Equals(file.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Inventory mismatch: " + relative);
            }
        }

        if (paths.Count != expectedCount)
        {
            throw new InvalidDataException("Inventory count mismatch.");
        }
    }
}
