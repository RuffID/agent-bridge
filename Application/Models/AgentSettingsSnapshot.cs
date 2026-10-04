namespace AgentBridge.Application.Models;

/// <summary>Безопасное представление эффективного выбора и лимитов приложения, без секретов и инструкций.</summary>
public class AgentSettingsSnapshot(ModelSettingsSnapshot model, long selectionVersion, TimeSpan retentionPeriod,
    long softContentLimitBytes, int maxCompactionPasses, int maxToolSteps, DialogWriteToken token)
{
    /// <summary>Безопасная версия истории того же read snapshot для SelectAsync; не является разрешением записи.</summary>
    public DialogWriteToken Token { get; } = token;
    /// <summary>Проверенный динамическим каталогом выбор.</summary>
    public ModelSettingsSnapshot Model { get; } = model;
    /// <summary>Версия сохранённого выбора; 0 означает отсутствие выбора.</summary>
    public long SelectionVersion { get; } = selectionVersion;
    /// <summary>Период для будущих диалогов; сохранённый expiry не пересчитывается.</summary>
    public TimeSpan RetentionPeriod { get; } = retentionPeriod;
    /// <summary>Мягкий порог содержимого.</summary>
    public long SoftContentLimitBytes { get; } = softContentLimitBytes;
    /// <summary>Предел проходов compact.</summary>
    public int MaxCompactionPasses { get; } = maxCompactionPasses;
    /// <summary>Предел шагов tools.</summary>
    public int MaxToolSteps { get; } = maxToolSteps;
}
