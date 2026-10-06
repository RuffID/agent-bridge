using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace AgentBridge.RuntimeProbe;

/// <summary>Вход выделенной проверки поставки; не запускает host или внешние операции.</summary>
public static class Program
{
    /// <summary>Проверяет managed загрузку и локальный граф, возвращая ненулевой код при любом отказе.</summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Expected provider and RID.");
        string actualRid = RuntimeInformation.RuntimeIdentifier;
        if (!StringComparer.Ordinal.Equals(actualRid, args[1]))
            throw new InvalidOperationException($"Runtime RID {actualRid} does not match {args[1]}.");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "delivery.manifest.json")));
        JsonElement metadata = manifest.RootElement;
        if (metadata.GetProperty("schemaVersion").GetInt32() != 1
            || metadata.GetProperty("framework").GetString() != "net10.0"
            || !metadata.GetProperty("frameworkDependent").GetBoolean()
            || metadata.GetProperty("provider").GetString() != args[0]
            || metadata.GetProperty("rid").GetString() != args[1]
            || Environment.Version.Major != 10)
            throw new InvalidOperationException("Manifest schema/framework/provider/RID does not match the runtime probe.");
        List<(string Path, string Kind)> files = [];
        foreach (JsonElement entry in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            string relative = entry.GetProperty("path").GetString() ?? throw new InvalidOperationException("Missing path.");
            string kind = entry.GetProperty("kind").GetString() ?? throw new InvalidOperationException("Missing kind.");
            if (kind is not ("managed" or "xml" or "native" or "resource")) continue;
            string outputRelative = relative.StartsWith("resources/", StringComparison.Ordinal) ? relative[10..] : Path.GetFileName(relative);
            string path = Path.Combine(AppContext.BaseDirectory, outputRelative);
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.LongLength != entry.GetProperty("size").GetInt64()
                || !StringComparer.OrdinalIgnoreCase.Equals(Convert.ToHexString(SHA256.HashData(bytes)), entry.GetProperty("sha256").GetString()))
                throw new InvalidOperationException($"Output hash mismatch: {outputRelative}");
            files.Add((path, kind));
        }
        List<object> loaded = [];
        foreach ((string path, string kind) in files.Where(file => file.Kind == "managed"))
        {
            Assembly assembly = Assembly.LoadFrom(path);
            _ = assembly.GetTypes();
            loaded.Add(new { name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), location = assembly.Location });
        }
        List<string> nativeLoaded = [];
        foreach ((string path, string kind) in files.Where(file => file.Kind == "native"))
        {
            nint handle = NativeLibrary.Load(path);
            try { nativeLoaded.Add(Path.GetFileName(path)); }
            finally { NativeLibrary.Free(handle); }
        }
        object checks = await ProbeChecks.RunAsync(args[0]);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            os = RuntimeInformation.OSDescription, osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), rid = actualRid,
            framework = RuntimeInformation.FrameworkDescription, provider = args[0], loaded, checks,
            nativeLoaded, databaseOpened = false, hostingStarted = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
