using System.Text.Json;
using AgentBridge.Application.Models;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Version-aware primitive настройки обращения; не canonical/envelope и не ModelAccess.</summary>
public static class TurnSettingsMapping
{
    /// <summary>Сериализует безопасный snapshot; null сохраняет legacy отсутствие.</summary>
    public static string? Write(TurnModelSettings? settings) => settings is null ? null : JsonSerializer.Serialize(new { version = 1, settings });
    /// <summary>Отклоняет повреждённый/неизвестный формат без defaults или восстановления истории.</summary>
    public static TurnModelSettings? Read(string? json)
    {
        if (json is null) return null;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out JsonElement version)
            || !version.TryGetInt32(out int number) || number != 1 || !root.TryGetProperty("settings", out JsonElement settings))
            throw new InvalidOperationException("Формат snapshot настроек не поддерживается.");
        return settings.Deserialize<TurnModelSettings>() ?? throw new InvalidOperationException("Snapshot настроек отсутствует.");
    }
}
