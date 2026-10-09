namespace AgentBridge.Application.Models;

/// <summary>Раздельный локальный подсчёт и оценка полного входного бюджета.</summary>
public class ContextTokenCount
{
    /// <summary>Фиксирует подтверждённую кодировку; сохраняет прежнюю бинарную сигнатуру.</summary>
    public ContextTokenCount(string encoding, long knownTokens, long? estimatedInputTokens, bool hasOpaqueContent)
        : this(encoding, knownTokens, estimatedInputTokens, hasOpaqueContent, false) { }

    /// <summary>Отделяет оценочный словарь неизвестной модели от подтверждённого соответствия и opaque-содержимого.</summary>
    public ContextTokenCount(string encoding, long knownTokens, long? estimatedInputTokens, bool hasOpaqueContent,
        bool isApproximateEncoding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encoding);
        if (knownTokens < 0 || estimatedInputTokens < knownTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(knownTokens), "Оценка не может быть меньше известной части.");
        }
        Encoding = encoding;
        KnownTokens = knownTokens;
        EstimatedInputTokens = estimatedInputTokens;
        HasOpaqueContent = hasOpaqueContent;
        IsApproximateEncoding = isApproximateEncoding;
    }

    /// <summary>Использованный словарь; соответствие модели подтверждено только при IsApproximateEncoding=false.</summary>
    public string Encoding { get; }
    /// <summary>Сумма BPE токенов известных payload; при оценочной кодировке приблизительна также для текста.</summary>
    public long KnownTokens { get; }
    /// <summary>Оценка полного входа до отдельного настроенного резерва; null означает неизвестный полный бюджет.</summary>
    public long? EstimatedInputTokens { get; }
    /// <summary>Есть содержимое, которое локальный текстовый tokenizer точно не считает.</summary>
    public bool HasOpaqueContent { get; }
    /// <summary>Кодировка модели не подтверждена: использованный словарь даёт только приблизительную оценку.</summary>
    public bool IsApproximateEncoding { get; }
}
