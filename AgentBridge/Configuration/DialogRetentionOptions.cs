namespace AgentBridge.Configuration;

/// <summary>Политика хранения полной истории, независимая от сжатия рабочего контекста.</summary>
public class DialogRetentionOptions
{
    /// <summary>Период хранения от создания будущего диалога.</summary>
    public TimeSpan RetentionPeriod { get; set; }

    /// <summary>Мягкий порог объёма содержимого на диалог в байтах; не основание для удаления.</summary>
    public long SoftContentLimitBytes { get; set; }

    /// <summary>Вычисляет срок истечения из времени создания и текущего настроенного периода.</summary>
    /// <param name="createdAtUtc">Время создания с нулевым смещением UTC.</param>
    /// <returns>Время истечения будущего диалога в UTC.</returns>
    /// <exception cref="ArgumentException">Время создания задано не в UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Период неположителен или дата выходит за допустимый диапазон.</exception>
    public DateTimeOffset CalculateExpiresAtUtc(DateTimeOffset createdAtUtc)
    {
        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время создания должно быть задано в UTC.", nameof(createdAtUtc));
        }

        if (RetentionPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RetentionPeriod), "Период хранения должен быть положительным.");
        }

        return createdAtUtc.Add(RetentionPeriod);
    }
}
