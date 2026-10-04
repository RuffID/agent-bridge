using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Версионированная сериализация явного журнала, отдельно от model payload.</summary>
internal static class ToolAttemptMapping
{
    /// <summary>Исторический null означает отсутствие записанных попыток, но не разрешение replay.</summary>
    public static IReadOnlyList<StoredToolAttempt> Read(string? json)
    {
        if (json is null) return Array.Empty<StoredToolAttempt>();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1)
            throw new InvalidOperationException("Неизвестный формат журнала инструментов.");
        List<StoredToolAttempt> attempts = [];
        foreach (JsonElement item in root.GetProperty("attempts").EnumerateArray())
            attempts.Add(new(item.GetProperty("position").GetInt32(), item.GetProperty("agent").GetString()!,
                (ToolAttemptState)item.GetProperty("state").GetInt32()));
        if (attempts.Select(attempt => attempt.OutputIndex).Distinct().Count() != attempts.Count)
            throw new InvalidOperationException("Журнал содержит повтор позиции.");
        return attempts.AsReadOnly();
    }

    /// <summary>Сохраняет только явные поля версии и identity, без скрытого изменения canonical output.</summary>
    public static string Write(IEnumerable<StoredToolAttempt> attempts) => JsonSerializer.Serialize(new
    {
        version = 1,
        attempts = attempts.OrderBy(attempt => attempt.OutputIndex).Select(attempt => new
        {
            position = attempt.OutputIndex, agent = attempt.AgentId, state = (int)attempt.State
        })
    });
}
