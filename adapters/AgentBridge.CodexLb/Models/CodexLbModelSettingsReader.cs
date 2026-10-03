using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.Options;

namespace AgentBridge.CodexLb.Models;

/// <inheritdoc/>
public class CodexLbModelSettingsReader(IModelAccessResolver accessResolver, IModelCatalog catalog,
    IOptionsSnapshot<CodexLbOptions> options, IOptionsSnapshot<ContextCompactionOptions> contextOptions) : IModelSettingsReader
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId,
        string? model = null, string? effort = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ct.ThrowIfCancellationRequested();
        CodexLbOptions configured = options.Value;
        ContextCompactionOptions context = contextOptions.Value;
        string selectedModel = model ?? configured.Model
            ?? throw new InvalidOperationException("Модель codex-lb не настроена.");
        string selectedEffort = effort ?? configured.ReasoningEffort;
        int threshold = context.TokenThreshold;
        int reserve = context.InputTokenReserve;
        ServiceResult<ModelAccess> access = await accessResolver.ResolveAsync(ownerId, ct);
        if (!access.Success)
        {
            return ServiceResult<ModelSettingsSnapshot>.Fail(access.Error!);
        }
        ServiceResult<ModelCatalogSnapshot> result = await catalog.ReadAsync(access.Data!, ct);
        if (!result.Success)
        {
            return ServiceResult<ModelSettingsSnapshot>.Fail(result.Error!);
        }
        ct.ThrowIfCancellationRequested();
        return ModelSelectionValidator.Validate(result.Data!, selectedModel, selectedEffort, threshold, reserve);
    }
}
