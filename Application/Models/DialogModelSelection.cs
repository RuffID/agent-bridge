namespace AgentBridge.Application.Models;

/// <summary>Сохранённый выбор конкретного диалога с независимой версией; отсутствие означает defaults приложения.</summary>
public class DialogModelSelection
{
    /// <summary>Фиксирует точные непустые значения и положительную версию.</summary>
    public DialogModelSelection(long version, string model, string effort)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(effort);
        Version = version;
        Model = model;
        Effort = effort;
    }
    /// <summary>Версия выбора, независимая от revision истории.</summary>
    public long Version { get; }
    /// <summary>Выбранное имя, не фактическая модель сервера.</summary>
    public string Model { get; }
    /// <summary>Сохранённый effort; override запроса его не изменяет.</summary>
    public string Effort { get; }
}
