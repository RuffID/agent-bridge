namespace AgentBridge.Application.Models;

/// <summary>Неизменяемый выбранный ключ конкретного обращения, отдельно от безопасных настроек.</summary>
/// <remarks>Выбор индивидуального/общего ключа относится к этапу 13. Содержимое не предназначено для логирования.</remarks>
public class ModelAccess
{
    private readonly string _apiKey;

    /// <summary>Фиксирует уже выбранный ключ без HTTP-типов и общего изменяемого состояния клиента.</summary>
    public ModelAccess(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _apiKey = apiKey;
    }

    /// <summary>Явно раскрывает ключ исключительно для авторизации транспорта; результат нельзя логировать.</summary>
    public string RevealApiKey() => _apiKey;

    /// <inheritdoc/>
    public override string ToString() => nameof(ModelAccess);
}
