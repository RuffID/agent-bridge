using AgentBridge.Application.Models;
using AgentBridge.Application.Results;

namespace AgentBridge.Application.Ports;

/// <summary>Независимый шлюз генерации и сжатия; JSON/SSE и авторизация принадлежат адаптеру.</summary>
public interface IModelGateway
{
    /// <summary>Возвращает lifecycle-отчёт либо ожидаемый отказ без отчёта; неожиданные исключения распространяются.</summary>
    /// <remarks>Обновления вызываются последовательно и ожидаются до возврата. После возврата callbacks запрещены.
    /// Caller cancellation до получения отчёта распространяется как OperationCanceledException с исходным токеном;
    /// при наличии отчёта его статус Canceled. Deadline не подменяет caller cancellation.</remarks>
    Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default);

    /// <summary>Возвращает каноническое новое окно; принимать его можно только при Completed.</summary>
    /// <remarks>Неподдерживаемый compact возвращает явный отказ, без скрытой подмены генерацией или другой моделью.</remarks>
    Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        CancellationToken cancellationToken = default);
}
