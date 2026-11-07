using System.Text.Json;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Читает зафиксированный API этапа01 из embedded resource без доступа к профилю или рабочим каталогам.</summary>
internal static class LegacyApiBaseline
{
    /// <summary>Возвращает семантические сигнатуры выбранной исторической сборки.</summary>
    internal static HashSet<string> ReadSignatures(string assembly)
    {
        using JsonDocument document = Read();
        JsonElement entry = FindAssembly(document, assembly);

        return entry.GetProperty("signatures").EnumerateArray()
            .Select(signature => signature.GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Возвращает SHA256 исходной исторической DLL для отдельной проверки происхождения эталона.</summary>
    internal static string ReadSourceHash(string assembly)
    {
        using JsonDocument document = Read();

        return FindAssembly(document, assembly).GetProperty("sourceSha256").GetString()!;
    }

    /// <summary>Требует ровно одну запись запрошенной сборки.</summary>
    private static JsonElement FindAssembly(JsonDocument document, string assembly)
    {
        return document.RootElement.GetProperty("assemblies").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == assembly);
    }

    /// <summary>Отсутствующий или неверный встроенный эталон является ошибкой подготовки тестового проекта.</summary>
    private static JsonDocument Read()
    {
        using Stream resource = typeof(LegacyApiBaseline).Assembly.GetManifestResourceStream("AgentBridge.Tests.LegacyApiBaseline.json")
            ?? throw new InvalidOperationException("Embedded legacy API baseline is missing.");
        JsonDocument document = JsonDocument.Parse(resource);
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
        {
            document.Dispose();
            throw new InvalidDataException("Unsupported legacy API baseline schema.");
        }

        return document;
    }
}
