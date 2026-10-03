using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AgentBridge.Persistence.EfCore.Mapping;

/// <summary>Точное хранение UTC ticks для одинаковой сортировки срока в SQLite/PostgreSQL без timestamp precision loss.</summary>
public class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
{
    /// <summary>Настраивает проверку UTC на запись и восстановление нулевого смещения.</summary>
    public UtcTicksConverter() : base(value => ToTicks(value), ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
    {
    }

    /// <summary>Отклоняет ненулевое смещение до передачи значения провайдеру.</summary>
    private static long ToTicks(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Время хранения должно быть задано в UTC.", nameof(value));
        }
        return value.Ticks;
    }
}
