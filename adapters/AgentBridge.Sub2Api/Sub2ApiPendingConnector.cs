using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;

namespace AgentBridge.Sub2Api;

/// <summary>Явная граница незавершённого транспорта: ни один порт не выдаёт фиктивного успеха.</summary>
internal class Sub2ApiPendingConnector : IModelGateway, IModelCatalog, IModelAccessResolver, IModelSettingsReader
{
    /// <summary>Создаёт только локальную границу этапа02, без HTTP factory и секретов.</summary>
    public Sub2ApiPendingConnector()
    {
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
    {
        return Unsupported<ModelResponse>(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        return Unsupported<ModelResponse>(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelCatalogSnapshot>> ReadAsync(ModelAccess access, CancellationToken ct = default)
    {
        return Unsupported<ModelCatalogSnapshot>(ct);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default)
    {
        return Unsupported<ModelAccess>(ct);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null,
        string? effort = null, CancellationToken ct = default)
    {
        return Unsupported<ModelSettingsSnapshot>(ct);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access,
        string? model = null, string? effort = null, CancellationToken ct = default)
    {
        return Unsupported<ModelSettingsSnapshot>(ct);
    }

    /// <summary>Отмена сохраняется, незавершённая возможность не вызывает другие коннекторы.</summary>
    private static Task<ServiceResult<T>> Unsupported<T>(CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ServiceResult<T>.Fail(new(ServiceErrorType.Unsupported,
            "Транспорт sub2api ещё не реализован: требуется этап03 подключения.")));
    }
}
