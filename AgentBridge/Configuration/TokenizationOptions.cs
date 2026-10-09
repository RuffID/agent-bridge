namespace AgentBridge.Configuration;

/// <summary>Явные дополнительные соответствия моделей встроенным offline словарям tokenizer.</summary>
public class TokenizationOptions
{
    /// <summary>Точный ID модели → o200k_base или cl100k_base; пустой список сохраняет встроенные соответствия.</summary>
    /// <remarks>Приложение подтверждает кодировку новой модели. Конфликт со встроенным соответствием запрещён.
    /// Offline counter копирует список при создании; изменение конфигурации требует пересоздания процесса.</remarks>
    public Dictionary<string, string> ModelEncodings { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Необязательная кодировка для приблизительной оценки моделей без подтверждённого соответствия.</summary>
    /// <remarks>o200k_base/cl100k_base используются как оценочный словарь, а не доказательство кодировки модели.
    /// Отправку такой оценки разрешает только явная политика ServerValidation. Null сохраняет строгий отказ.</remarks>
    public string? UnknownModelEstimateEncoding { get; set; }
}
