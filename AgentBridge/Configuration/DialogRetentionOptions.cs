namespace AgentBridge.Configuration;

/// <summary>Политика хранения полной истории, независимая от сжатия рабочего контекста.</summary>
public class DialogRetentionOptions
{
    /// <summary>Период от создания всех диалогов; null означает бессрочное хранение.</summary>
    public TimeSpan? RetentionPeriod { get; set; }

    /// <summary>Мягкий порог объёма содержимого на диалог в байтах; не основание для удаления.</summary>
    public long SoftContentLimitBytes { get; set; }

    /// <summary>Вычисляет срок истечения из времени создания и текущего настроенного периода.</summary>
    /// <param name="createdAtUtc">Время создания с нулевым смещением UTC.</param>
    /// <returns>Текущий срок в UTC либо null для бессрочного диалога.</returns>
    /// <exception cref="ArgumentException">Время создания задано не в UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Период неположителен или дата выходит за допустимый диапазон.</exception>
    public DateTimeOffset? CalculateExpiresAtUtc(DateTimeOffset createdAtUtc)
    {
        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время создания должно быть задано в UTC.", nameof(createdAtUtc));
        }

        if (RetentionPeriod is null)
        {
            return null;
        }

        if (RetentionPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RetentionPeriod), "Период хранения должен быть положительным.");
        }

        return createdAtUtc.Add(RetentionPeriod.Value);
    }

    /// <summary>Проверяет истечение по текущей политике, включая ранее созданные диалоги.</summary>
    public bool IsExpired(DateTimeOffset createdAtUtc, DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время проверки должно быть задано в UTC.", nameof(nowUtc));
        }

        return CalculateExpiresAtUtc(createdAtUtc) is { } expiry && nowUtc >= expiry;
    }
}
