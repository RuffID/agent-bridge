using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.Options;

namespace AgentBridge.CodexLb.Models;

/// <inheritdoc/>
public class CodexLbModelAccessResolver(
    IIndividualModelKeySource keySource, IOptionsSnapshot<CodexLbOptions> options) : IModelAccessResolver
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ct.ThrowIfCancellationRequested();
        CodexLbOptions configured = options.Value;
        if (configured.KeySource is not { } mode || !Enum.IsDefined(mode))
        {
            return ServiceResult<ModelAccess>.Fail(new(ServiceErrorType.Validation, "CodexLb.KeySource: required_or_invalid."));
        }
        string? individual = await keySource.GetKeyAsync(ownerId, ct);
        ct.ThrowIfCancellationRequested();
        string? key = individual ?? (mode == ModelKeySourceMode.Shared ? configured.SharedApiKey : null);
        if (key is null)
        {
            return ServiceResult<ModelAccess>.Fail(new(ServiceErrorType.Unauthorized, "Ключ доступа не предоставлен."));
        }
        if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
        {
            return ServiceResult<ModelAccess>.Fail(new(ServiceErrorType.Validation, "Заданный ключ доступа имеет недопустимый формат."));
        }
        return ServiceResult<ModelAccess>.Ok(new(key));
    }
}
