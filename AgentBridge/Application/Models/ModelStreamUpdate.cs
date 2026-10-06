namespace AgentBridge.Application.Models;

/// <summary>Предварительный фрагмент для приложения; сам по себе не подтверждает завершение.</summary>
public class ModelStreamUpdate
{
    /// <summary>Фиксирует текстовый delta и/или завершённый канонический элемент без потери его полей.</summary>
    public ModelStreamUpdate(string? textDelta, CanonicalModelItem? item)
    {
        if (textDelta is null && item is null)
        {
            throw new ArgumentException("Обновление должно содержать данные.");
        }
        TextDelta = textDelta;
        Item = item;
    }

    /// <summary>Фрагмент видимого текста, если доступен.</summary>
    public string? TextDelta { get; }
    /// <summary>Полный элемент, если доступен; delta не заменяет итоговый output.</summary>
    public CanonicalModelItem? Item { get; }
}
