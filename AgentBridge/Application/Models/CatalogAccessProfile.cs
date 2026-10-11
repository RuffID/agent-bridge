using System.Text.Json;

namespace AgentBridge.Application.Models;

/// <summary>Неизменяемый полный профиль доступа; не содержит claims или бизнес-типов приложения.</summary>
public class CatalogAccessProfile
{
    /// <summary>Клонирует opaque данные; бюджет дополнительно проверяется на storage boundary.</summary>
    public CatalogAccessProfile(string key, int version, JsonElement data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        Key = key;
        Version = version;
        Data = data.Clone();
    }

    /// <summary>Ключ схемы.</summary>
    public string Key { get; }
    /// <summary>Версия схемы.</summary>
    public int Version { get; }
    /// <summary>Полный неизменяемый профиль для повторного host gate.</summary>
    public JsonElement Data { get; }

    /// <summary>Сравнивает весь профиль без нормализации или частичного совпадения.</summary>
    public bool Matches(CatalogAccessProfile other) => Key == other.Key && Version == other.Version &&
        Data.GetRawText() == other.Data.GetRawText();
}
