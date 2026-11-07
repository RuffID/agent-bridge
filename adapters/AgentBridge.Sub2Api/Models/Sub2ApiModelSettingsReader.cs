using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Sub2Api.Configuration;
using Microsoft.Extensions.Options;

namespace AgentBridge.Sub2Api.Models;

/// <inheritdoc/>
internal class Sub2ApiModelSettingsReader(IModelAccessResolver accessResolver, IModelCatalog catalog,
    IOptionsSnapshot<Sub2ApiOptions> options, IOptionsSnapshot<ContextCompactionOptions> contextOptions) : IModelSettingsReader
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId,
        string? model = null, string? effort = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ct.ThrowIfCancellationRequested();

        (string selectedModel, string selectedEffort, int threshold, int reserve, ContextBudgetPolicy policy) = Capture(model, effort);
        ServiceResult<ModelAccess> access = await accessResolver.ResolveAsync(ownerId, ct);
        if (!access.Success)
        {
            return ServiceResult<ModelSettingsSnapshot>.Fail(access.Error!);
        }

        return await ReadCatalogAsync(access.Data!, selectedModel, selectedEffort, threshold, reserve, policy, ct);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access,
        string? model = null, string? effort = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(access);
        ct.ThrowIfCancellationRequested();

        (string selectedModel, string selectedEffort, int threshold, int reserve, ContextBudgetPolicy policy) = Capture(model, effort);

        return ReadCatalogAsync(access, selectedModel, selectedEffort, threshold, reserve, policy, ct);
    }

    /// <summary>Фиксирует примитивы до source/catalog I/O; настройки не меняются посреди чтения.</summary>
    private (string Model, string Effort, int Threshold, int Reserve, ContextBudgetPolicy Policy) Capture(string? model, string? effort)
    {
        Sub2ApiOptions configured = options.Value;
        ContextCompactionOptions context = contextOptions.Value;
        string selectedModel = model ?? configured.Model
            ?? throw new InvalidOperationException("Модель sub2api не настроена.");
        string selectedEffort = effort ?? configured.ReasoningEffort;

        return (selectedModel, selectedEffort, context.TokenThreshold, context.InputTokenReserve, context.BudgetPolicy);
    }

    /// <summary>Не обходит validator: обычный каталог без доказанного бюджета даёт Unsupported.</summary>
    private async Task<ServiceResult<ModelSettingsSnapshot>> ReadCatalogAsync(ModelAccess access, string model,
        string effort, int threshold, int reserve, ContextBudgetPolicy policy, CancellationToken ct)
    {
        ServiceResult<ModelCatalogSnapshot> result = await catalog.ReadAsync(access, ct);
        if (!result.Success)
        {
            return ServiceResult<ModelSettingsSnapshot>.Fail(result.Error!);
        }

        ct.ThrowIfCancellationRequested();

        return ModelSelectionValidator.Validate(result.Data!, model, effort, threshold, reserve, policy);
    }
}
