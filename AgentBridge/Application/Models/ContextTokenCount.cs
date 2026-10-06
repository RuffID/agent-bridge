namespace AgentBridge.Application.Models;

/// <summary>Раздельный локальный подсчёт и оценка полного входного бюджета.</summary>
public class ContextTokenCount
{
    /// <summary>Фиксирует известную кодировку и оценку, не выдавая opaque-содержимое за точно посчитанное.</summary>
    public ContextTokenCount(string encoding, long knownTokens, long? estimatedInputTokens, bool hasOpaqueContent)
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
    }

    /// <summary>Проверенная кодировка выбранной модели.</summary>
    public string Encoding { get; }
    /// <summary>Сумма BPE токенов известных payload, включая инструкции и tools; не серверный billing count.</summary>
    public long KnownTokens { get; }
    /// <summary>Оценка полного входа до отдельного настроенного резерва; null означает неизвестный полный бюджет.</summary>
    public long? EstimatedInputTokens { get; }
    /// <summary>Есть содержимое, которое локальный текстовый tokenizer точно не считает.</summary>
    public bool HasOpaqueContent { get; }
}
