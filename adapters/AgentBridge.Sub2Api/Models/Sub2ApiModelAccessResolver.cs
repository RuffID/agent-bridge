using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Sub2Api.Configuration;
using Microsoft.Extensions.Options;

namespace AgentBridge.Sub2Api.Models;

/// <inheritdoc/>
internal class Sub2ApiModelAccessResolver(
    IIndividualModelKeySource keySource, IOptionsSnapshot<Sub2ApiOptions> options) : IModelAccessResolver
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ct.ThrowIfCancellationRequested();

        Sub2ApiOptions configured = options.Value;
        Sub2ApiKeySourceMode? mode = configured.KeySource;
        string? shared = configured.SharedApiKey;
        if (mode is null || !Enum.IsDefined(mode.Value))
        {
            return ServiceResult<ModelAccess>.Fail(new(ServiceErrorType.Validation, "Sub2Api.KeySource: required_or_invalid."));
        }

        string? key = await keySource.GetKeyAsync(ownerId, ct);
        ct.ThrowIfCancellationRequested();
        if (key is null && mode == Sub2ApiKeySourceMode.Shared)
        {
            key = shared;
        }

        if (key is null)
        {
            return ServiceResult<ModelAccess>.Fail(new(ServiceErrorType.Unauthorized, "Ключ доступа не предоставлен."));
        }

        if (!Sub2ApiCredential.IsValid(key))
        {
            return ServiceResult<ModelAccess>.Fail(new(ServiceErrorType.Validation, "Заданный ключ доступа имеет недопустимый формат."));
        }

        return ServiceResult<ModelAccess>.Ok(new(key));
    }
}
