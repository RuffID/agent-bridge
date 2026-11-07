using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.Sub2Api;

/// <inheritdoc/>
/// <remarks>Без доказанного server profile T05 generation/compact не отправляют HTTP даже при custom settings.</remarks>
internal class Sub2ApiModelGateway : IModelGateway
{
    /// <inheritdoc/>
    public Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Unsupported,
            "Генерация sub2api заблокирована: не подтверждены серверный профиль T05 и совместимость opaque-контекста.")));
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Unsupported,
            "Сжатие sub2api заблокировано: не подтверждены фактическая модель и серверный профиль compact T05.")));
    }
}
