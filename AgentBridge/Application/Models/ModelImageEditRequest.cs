namespace AgentBridge.Application.Models;

/// <summary>Генерация изображения по описанию и входным изображениям.</summary>
public class ModelImageEditRequest : ModelImageRequest
{
    /// <summary>Входные изображения; адаптер копирует коллекцию и байты до отправки.</summary>
    public IReadOnlyList<ModelFileUpload> Images { get; init; } = [];
}

