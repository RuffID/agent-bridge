namespace AgentBridge.Application.Models;

/// <summary>Явные границы materialization одного persistence-сценария, включая повторные чтения метаданных.</summary>
/// <remarks>MaxRecordBytes ограничивает консервативный storage payload строки, включая reports/provenance.
/// Полный payload ограничен MaxRecords × MaxRecordBytes; scalar preflight не загружает строки истории.
/// Пределы не являются token budget или лимитом накопленного хранения.</remarks>
public class DialogHistoryLoadLimits
{
    /// <summary>Фиксирует положительные пределы без рабочих значений по умолчанию.</summary>
    public DialogHistoryLoadLimits(int maxRecords, long maxRecordBytes, int maxQueries)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRecords);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRecordBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxQueries);
        if (maxRecords == int.MaxValue || maxRecordBytes > long.MaxValue / maxRecords)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRecords));
        }

        MaxRecords = maxRecords;
        MaxRecordBytes = maxRecordBytes;
        MaxQueries = maxQueries;
    }

    /// <summary>Максимальное число полных строк за одну операцию, включая повторные root/settings reads.</summary>
    public int MaxRecords { get; }
    /// <summary>Максимальный консервативный размер одной строки; единицы описывает provider.</summary>
    public long MaxRecordBytes { get; }
    /// <summary>Максимальное число query pairs: один scalar preflight и один bounded payload query.</summary>
    public int MaxQueries { get; }
    /// <summary>Доказанная верхняя граница storage payload, не точный объём CLR heap или БД.</summary>
    public long MaxPayloadBytes => MaxRecords * MaxRecordBytes;
}
