namespace AgentBridge.Application.Models;

/// <summary>Явные параметры генерации изображения, без неявного выбора модели или повторов.</summary>
public class ModelImageRequest
{
    /// <summary>Модель изображений.</summary>
    public string Model { get; init; } = string.Empty;
    /// <summary>Описание результата.</summary>
    public string Prompt { get; init; } = string.Empty;
    /// <summary>Число изображений.</summary>
    public int Count { get; init; } = 1;
    /// <summary>Размер.</summary>
    public string Size { get; init; } = "auto";
    /// <summary>Качество.</summary>
    public string Quality { get; init; } = "auto";
    /// <summary>Фон.</summary>
    public string Background { get; init; } = "auto";
    /// <summary>Выходной формат.</summary>
    public string OutputFormat { get; init; } = "png";
}

