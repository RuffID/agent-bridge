using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Явные операции usage, файлов и изображений с уже выбранным per-call доступом.</summary>
/// <remarks>Приложение авторизует вызов и владеет UI. JSON результата содержит пользовательские данные:
/// его нельзя логировать как безопасную диагностику. Ошибки не содержат raw body, signed URL или секретов.
/// Операции не повторяются, partial upload не является успехом и не удаляется автоматически.</remarks>
public interface IModelAuxiliaryGateway
{
    /// <summary>Читает usage и лимиты выбранного доступа.</summary>
    Task<ServiceResult<ModelAuxiliaryResult>> ReadUsageAsync(ModelAccess access, CancellationToken cancellationToken = default);
    /// <summary>Регистрирует, загружает и подтверждает файл; возвращает только подтверждённый идентификатор.</summary>
    Task<ServiceResult<string>> UploadFileAsync(ModelFileUpload file, ModelAccess access, CancellationToken cancellationToken = default);
    /// <summary>Генерирует изображения; успешный результат требует непустого массива data.</summary>
    Task<ServiceResult<ModelAuxiliaryResult>> GenerateImageAsync(ModelImageRequest request, ModelAccess access, CancellationToken cancellationToken = default);
    /// <summary>Редактирует изображения через multipart, без повторной отправки.</summary>
    Task<ServiceResult<ModelAuxiliaryResult>> EditImageAsync(ModelImageEditRequest request, ModelAccess access, CancellationToken cancellationToken = default);
}

