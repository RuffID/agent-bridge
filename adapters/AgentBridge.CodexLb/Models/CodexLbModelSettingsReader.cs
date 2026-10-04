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
        (string selectedModel, string selectedEffort, int threshold, int reserve) = CaptureSelection(model, effort);
        ServiceResult<ModelAccess> access = await accessResolver.ResolveAsync(ownerId, ct);
        if (!access.Success) return ServiceResult<ModelSettingsSnapshot>.Fail(access.Error!);
        return await ReadCatalogAsync(access.Data!, selectedModel, selectedEffort, threshold, reserve, ct);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access,
        string? model = null, string? effort = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(access);
        ct.ThrowIfCancellationRequested();
        (string selectedModel, string selectedEffort, int threshold, int reserve) = CaptureSelection(model, effort);
        return await ReadCatalogAsync(access, selectedModel, selectedEffort, threshold, reserve, ct);
    }

    /// <summary>Фиксирует primitive настройки до первого I/O без изменения существующего reader-контракта.</summary>
    private (string Model, string Effort, int Threshold, int Reserve) CaptureSelection(string? model, string? effort)
    {
        CodexLbOptions configured = options.Value;
        ContextCompactionOptions context = contextOptions.Value;
        string selectedModel = model ?? configured.Model
            ?? throw new InvalidOperationException("Модель codex-lb не настроена.");
        string selectedEffort = effort ?? configured.ReasoningEffort;
        return (selectedModel, selectedEffort, context.TokenThreshold, context.InputTokenReserve);
    }

    /// <summary>Проверяет уже зафиксированный выбор тем же доступом, сохраняя typed failure перед поздней отменой.</summary>
    private async Task<ServiceResult<ModelSettingsSnapshot>> ReadCatalogAsync(ModelAccess access, string selectedModel,
        string selectedEffort, int threshold, int reserve, CancellationToken ct)
    {
        ServiceResult<ModelCatalogSnapshot> result = await catalog.ReadAsync(access, ct);
        if (!result.Success)
        {
            return ServiceResult<ModelSettingsSnapshot>.Fail(result.Error!);
        }
        ct.ThrowIfCancellationRequested();
        return ModelSelectionValidator.Validate(result.Data!, selectedModel, selectedEffort, threshold, reserve);
    }
}
