using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using Microsoft.Extensions.Options;

namespace AgentBridge.Application;

/// <summary>Безопасное чтение/выбор settings конкретного диалога, без external I/O внутри write transaction.</summary>
public class AgentSettingsService(IDialogReader dialogs, IDialogSettingsWriter writer, IModelSettingsReader models,
    ContextModelGuard compatibility, IContextTokenCounter counter, TimeProvider time,
    IOptionsSnapshot<DialogRetentionOptions> retention, IOptionsSnapshot<ContextCompactionOptions> compaction,
    IOptionsSnapshot<AgentOptions> agent)
{
    /// <summary>Возвращает effective выбор и безопасные лимиты; авторизация агента принадлежит приложению.</summary>
    public async Task<ServiceResult<AgentSettingsSnapshot>> ReadAsync(ApplicationCallContext call, CancellationToken cancellationToken = default)
    {
        ServiceResult<DialogSnapshot> read = await ReadDialogAsync(call, cancellationToken);
        if (!read.Success) return ServiceResult<AgentSettingsSnapshot>.Fail(read.Error!);
        DialogModelSelection? selection = read.Data!.Selection;
        (TimeSpan? period, long limit, int passes, int steps) = CaptureLimits();
        ServiceResult<ModelSettingsSnapshot> selected = await models.ReadAsync(call.OwnerId, selection?.Model, selection?.Effort, cancellationToken);
        return selected.Success ? ServiceResult<AgentSettingsSnapshot>.Ok(Snapshot(selected.Data!, selection?.Version ?? 0, period, limit, passes, steps, read.Data.Token))
            : ServiceResult<AgentSettingsSnapshot>.Fail(selected.Error!);
    }

    /// <summary>Проверяет новый exact выбор и compatibility до fresh expiry/CAS записи; не изменяет active run.</summary>
    public async Task<ServiceResult<DialogModelSelection>> SelectAsync(ApplicationCallContext call, DialogWriteToken expected,
        long expectedSelectionVersion, string model, string effort, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedSelectionVersion);
        ServiceResult<DialogSnapshot> read = await ReadDialogAsync(call, cancellationToken);
        if (!read.Success) return ServiceResult<DialogModelSelection>.Fail(read.Error!);
        DialogSnapshot dialog = read.Data!;
        if (dialog.IsExpired(time.GetUtcNow())) return ServiceResult<DialogModelSelection>.Fail(new(ServiceErrorType.Expired, "Срок диалога истёк."));
        ArgumentNullException.ThrowIfNull(expected);
        if (!expected.DialogId.Equals(dialog.Token.DialogId) || expected.IncarnationId != dialog.Token.IncarnationId
            || expected.Revision != dialog.Token.Revision || expectedSelectionVersion != (dialog.Selection?.Version ?? 0))
            return ServiceResult<DialogModelSelection>.Fail(new(ServiceErrorType.Conflict, "Версия диалога или настроек устарела."));
        ServiceResult<ModelSettingsSnapshot> selected = await models.ReadAsync(call.OwnerId, model, effort, cancellationToken);
        if (!selected.Success) return ServiceResult<DialogModelSelection>.Fail(selected.Error!);
        ServiceResult compatible = await compatibility.CheckAsync(call, dialog, selected.Data!, cancellationToken);
        if (!compatible.Success) return ServiceResult<DialogModelSelection>.Fail(compatible.Error!);
        cancellationToken.ThrowIfCancellationRequested();
        return await writer.SaveAsync(new(call.DialogId, call.OwnerId, time.GetUtcNow()), expected,
            expectedSelectionVersion, selected.Data!, cancellationToken);
    }

    /// <summary>Читает срок/объём даже после expiry; unknown budget и несовместимость возвращаются отдельно от метаданных.</summary>
    public async Task<ServiceResult<DialogStatus>> GetStatusAsync(ApplicationCallContext call, CancellationToken cancellationToken = default)
    {
        ServiceResult<DialogSnapshot> read = await ReadDialogAsync(call, cancellationToken);
        if (!read.Success) return ServiceResult<DialogStatus>.Fail(read.Error!);
        DialogSnapshot dialog = read.Data!;
        (TimeSpan? period, long limit, int passes, int steps) = CaptureLimits();
        ServiceResult<ModelSettingsSnapshot> selected = await models.ReadAsync(call.OwnerId, dialog.Selection?.Model, dialog.Selection?.Effort, cancellationToken);
        AgentSettingsSnapshot? settings = null;
        ContextTokenCount? size = null;
        ServiceError? error = selected.Error;
        if (selected.Success)
        {
            settings = Snapshot(selected.Data!, dialog.Selection?.Version ?? 0, period, limit, passes, steps, dialog.Token);
            ServiceResult compatible = await compatibility.CheckAsync(call, dialog, selected.Data!, cancellationToken);
            error = compatible.Error;
            // Read-only projection позволяет измерить истёкшую историю, не разрешая generation/write.
            ServiceResult<ModelRequest> prepared = await new ContextBuilder([]).BuildForStatusAsync(call, dialog,
                new(selected.Data!.Model.Id, selected.Data.ReasoningEffort, string.Empty, [], []), time.GetUtcNow(), cancellationToken);
            if (!prepared.Success) error ??= prepared.Error;
            else
            {
                ServiceResult<ContextTokenCount> counted = await counter.CountAsync(prepared.Data!, cancellationToken);
                if (!counted.Success) error ??= counted.Error;
                else
                {
                    size = counted.Data!;
                    if (size.IsApproximateEncoding && compaction.Value.BudgetPolicy != ContextBudgetPolicy.ServerValidation)
                        error ??= new(ServiceErrorType.Unsupported, "Оценочная кодировка модели требует явной серверной проверки бюджета.");
                    else if (size.EstimatedInputTokens is null) error ??= new(ServiceErrorType.Unsupported, "Полный бюджет сохранённого контекста неизвестен.");
                    else if (size.EstimatedInputTokens > (long)selected.Data.Model.InputContextWindow! - selected.Data.InputTokenReserve)
                        error ??= new(ServiceErrorType.Rejected, "Сохранённый контекст превышает входной бюджет.");
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        // Берётся последний report; отсутствие model не заменяется старым или selected именем.
        ModelResponse? last = dialog.Turns.SelectMany(turn => turn.ModelSteps).LastOrDefault()?.Response;
        return ServiceResult<DialogStatus>.Ok(new(dialog.Token, dialog.CreatedAtUtc, dialog.ExpiresAtUtc,
            dialog.IsExpired(time.GetUtcNow()), dialog.ContentBytes, limit, dialog.ActiveContext?.Version ?? 0, settings,
            last is null ? null : ContextModelGuard.ServerModel(last), size, error, dialog.Selection));
    }

    /// <summary>Проверяет принадлежность snapshot даже для пользовательского read port.</summary>
    private async Task<ServiceResult<DialogSnapshot>> ReadDialogAsync(ApplicationCallContext call, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(call);
        ServiceResult<DialogSnapshot> read = await dialogs.ReadAsync(new(call.DialogId, call.OwnerId, time.GetUtcNow()), ct);
        if (!read.Success) return read;
        if (!read.Data!.OwnerId.Equals(call.OwnerId)) return ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Forbidden, "Диалог недоступен владельцу."));
        if (!read.Data.Token.DialogId.Equals(call.DialogId)) return ServiceResult<DialogSnapshot>.Fail(new(ServiceErrorType.Conflict, "Снимок другого диалога."));
        return read;
    }

    /// <summary>Фиксирует лимиты до external I/O; не возвращает instructions/options с секретами.</summary>
    private (TimeSpan? Period, long Limit, int Passes, int Steps) CaptureLimits() =>
        (retention.Value.RetentionPeriod, retention.Value.SoftContentLimitBytes, compaction.Value.MaxPasses, agent.Value.MaxToolSteps);

    /// <summary>Создаёт безопасный immutable UI snapshot.</summary>
    private static AgentSettingsSnapshot Snapshot(ModelSettingsSnapshot selected, long version, TimeSpan? period, long limit, int passes, int steps, DialogWriteToken token) =>
        new(selected, version, period, limit, passes, steps, token);
}
