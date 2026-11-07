using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Configuration;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Version-aware primitive настройки обращения; не canonical/envelope и не ModelAccess.</summary>
public static class TurnSettingsMapping
{
    /// <summary>Сериализует безопасный snapshot; null сохраняет legacy отсутствие.</summary>
    public static string? Write(TurnModelSettings? settings)
    {
        if (settings is null)
        {
            return null;
        }

        if (settings.BudgetPolicy == ContextBudgetPolicy.RequireLocalEstimate)
        {
            return JsonSerializer.Serialize(new { version = 1, settings = new
            {
                settings.Model, settings.Effort, settings.TokenThreshold, settings.InputTokenReserve,
                settings.InputContextWindow
            } });
        }

        return JsonSerializer.Serialize(new { version = 2, settings = new
        {
            settings.Model, settings.Effort, settings.TokenThreshold, settings.InputTokenReserve,
            InputContextWindow = settings.DeclaredInputContextWindow, settings.BudgetPolicy
        } });
    }
    /// <summary>Отклоняет повреждённый/неизвестный формат без defaults или восстановления истории.</summary>
    public static TurnModelSettings? Read(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out JsonElement version)
            || !version.TryGetInt32(out int number) || number is not (1 or 2)
            || !root.TryGetProperty("settings", out JsonElement settings) || settings.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Формат snapshot настроек не поддерживается.");
        }

        if (number == 1)
        {
            return new(settings.GetProperty("Model").GetString()!, settings.GetProperty("Effort").GetString()!,
                settings.GetProperty("TokenThreshold").GetInt32(), settings.GetProperty("InputTokenReserve").GetInt32(),
                settings.GetProperty("InputContextWindow").GetInt32());
        }

        JsonElement window = settings.GetProperty("InputContextWindow");
        int? declaredWindow = null;
        if (window.ValueKind != JsonValueKind.Null)
        {
            declaredWindow = window.GetInt32();
        }

        return new(settings.GetProperty("Model").GetString()!, settings.GetProperty("Effort").GetString()!,
            settings.GetProperty("TokenThreshold").GetInt32(), settings.GetProperty("InputTokenReserve").GetInt32(),
            declaredWindow, (ContextBudgetPolicy)settings.GetProperty("BudgetPolicy").GetInt32());
    }
}
