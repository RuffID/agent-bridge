namespace AgentBridge.Configuration;

/// <summary>Фиксирует текущую политику на короткий scope; применяется ко всем датам создания.</summary>
public class DialogRetentionPolicy
{
    private readonly DialogRetentionOptions _options;

    /// <summary>Копирует период без зависимости от изменяемого объекта options.</summary>
    public DialogRetentionPolicy(DialogRetentionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RetentionPeriod is { } period && period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Период хранения должен быть положительным.");
        }

        _options = new DialogRetentionOptions { RetentionPeriod = options.RetentionPeriod };
    }

    /// <summary>Период этого scope; null отключает истечение и автоматическое удаление.</summary>
    public TimeSpan? RetentionPeriod => _options.RetentionPeriod;

    /// <summary>Вычисляет текущий срок ранее созданного или нового диалога.</summary>
    public DateTimeOffset? CalculateExpiresAtUtc(DateTimeOffset createdAtUtc) => _options.CalculateExpiresAtUtc(createdAtUtc);

    /// <summary>Проверяет текущий срок на явном UTC.</summary>
    public bool IsExpired(DateTimeOffset createdAtUtc, DateTimeOffset nowUtc) => _options.IsExpired(createdAtUtc, nowUtc);
}
