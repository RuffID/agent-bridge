using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.Options;

namespace AgentBridge.Application;

/// <summary>Сжимает только сохраняемую завершённую историю вне UoW и принимает окно после атомарного save.</summary>
public class ContextCompactor(ContextBuilder builder, IContextTokenCounter counter, IModelGateway gateway,
    IDialogContextWriter writer, TimeProvider time, IOptionsSnapshot<ContextCompactionOptions> options)
{
    /// <summary>Сжимает по настроенному автоматическому порогу; сохраняет прежнюю бинарную сигнатуру.</summary>
    public Task<ServiceResult<ContextCompactionResult>> CompactAsync(ApplicationCallContext call,
        DialogSnapshot dialog, ModelRequest newRequest, ModelSettingsSnapshot settings, ModelAccess access,
        CancellationToken cancellationToken = default) =>
        CompactAsync(call, dialog, newRequest, settings, access, cancellationToken, force: false);

    /// <summary>Фиксирует provider-вклады один раз; ошибка позднего прохода оставляет последний успешный save.</summary>
    /// <remarks>Ok означает получение отчёта, а не разрешение генерации: проверяются Status/Error и отдельный guard17.
    /// Неожиданные исключения и caller cancellation распространяются; сохранённые ранее проходы не откатываются.</remarks>
    public async Task<ServiceResult<ContextCompactionResult>> CompactAsync(ApplicationCallContext call,
        DialogSnapshot dialog, ModelRequest newRequest, ModelSettingsSnapshot settings, ModelAccess access,
        CancellationToken cancellationToken, bool force)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(newRequest);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();
        int maxPasses = options.Value.MaxPasses;
        ContextBudgetPolicy budgetPolicy = options.Value.BudgetPolicy;
        if (maxPasses <= 0 || newRequest.Model != settings.Model.Id || newRequest.ReasoningEffort != settings.ReasoningEffort)
        {
            return ServiceResult<ContextCompactionResult>.Fail(new(ServiceErrorType.Validation,
                "Некорректные лимиты или модельные настройки compact."));
        }
        if (newRequest.Continuation is not null)
        {
            return ServiceResult<ContextCompactionResult>.Fail(new(ServiceErrorType.Unsupported,
                "Сжатие сохраняемой истории требует полного input без server continuation."));
        }
        ServiceResult<ModelSettingsSnapshot> selection = ModelSelectionValidator.Validate(new([settings.Model]),
            settings.Model.Id, settings.ReasoningEffort, settings.TokenThreshold, settings.InputTokenReserve);
        if (!selection.Success) { return ServiceResult<ContextCompactionResult>.Fail(selection.Error!); }
        ServiceResult<ModelRequest> built = await builder.BuildAsync(call, dialog, newRequest, time.GetUtcNow(), cancellationToken);
        if (!built.Success) { return ServiceResult<ContextCompactionResult>.Fail(built.Error!); }
        ModelRequest prepared = built.Data!;
        DialogWriteToken token = dialog.Token;
        StoredDialogContext? active = dialog.ActiveContext;
        long originalThrough = active?.ThroughTurnSequence ?? 0;
        long through = originalThrough;
        List<CanonicalModelItem> history = active is null ? [] : [.. active.Items];
        foreach (StoredDialogTurn turn in dialog.Turns.Where(turn => turn.Sequence > originalThrough))
        {
            if (turn.Status == DialogTurnStatus.InProgress) { break; }
            history.AddRange(turn.Items);
            through = turn.Sequence;
        }
        int originalHistoryCount = (active?.Items.Count ?? 0) + dialog.Turns
            .Where(turn => turn.Sequence > originalThrough).Sum(turn => turn.Items.Count);
        int providerCount = prepared.Input.Count - originalHistoryCount - newRequest.Input.Count;
        CanonicalModelItem[] providers = prepared.Input.Take(providerCount).ToArray();
        CanonicalModelItem[] tail = dialog.Turns.Where(turn => turn.Sequence > through)
            .SelectMany(turn => turn.Items).Concat(newRequest.Input).ToArray();
        ContextTokenCount? count = null;
        ModelResponse? lastResponse = null;
        int passes = 0;

        // Отчёт фиксирует только последнее принятое окно; неподтверждённый кандидат остаётся LastResponse.
        ServiceResult<ContextCompactionResult> Report(ContextCompactionStatus status, ServiceError? error = null) =>
            ServiceResult<ContextCompactionResult>.Ok(new(status, token, active, prepared, count, passes, error, lastResponse));

        ServiceResult<ContextTokenCount> counted = await counter.CountAsync(prepared, cancellationToken);
        if (!counted.Success) { return Report(ContextCompactionStatus.Failed, counted.Error!); }
        cancellationToken.ThrowIfCancellationRequested();
        count = counted.Data!;
        long? estimate = count.EstimatedInputTokens;
        if (estimate is null && (budgetPolicy != ContextBudgetPolicy.ServerValidation || !count.HasOpaqueContent))
            return Report(ContextCompactionStatus.UnknownBudget);
        if (!force && (estimate ?? count.KnownTokens) < settings.TokenThreshold)
            return Report(estimate is null ? ContextCompactionStatus.UnknownBudget : ContextCompactionStatus.NotRequired);
        if (history.Count == 0) { return Report(ContextCompactionStatus.NoPersistableHistory); }

        while (passes < maxPasses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dialog.IsExpired(time.GetUtcNow()))
            {
                return Report(ContextCompactionStatus.Failed, new(ServiceErrorType.Expired, "Срок доступности диалога истёк."));
            }
            ServiceError? pairError = ContextBuilder.ValidateFunctionPairs(history, cancellationToken);
            if (pairError is not null) { return Report(ContextCompactionStatus.Failed, pairError); }
            ModelRequest compactRequest = new(prepared.Model, prepared.ReasoningEffort, prepared.Instructions,
                history, [], parameters: CompactParameters(prepared.Parameters));
            ServiceResult<ContextBudgetAssessment> compactBudget = await new ContextBudgetGuard(counter, budgetPolicy)
                .CheckAsync(compactRequest, settings, cancellationToken);
            if (!compactBudget.Success) { return Report(ContextCompactionStatus.Failed, compactBudget.Error!); }
            passes++;
            ServiceResult<ModelResponse> compacted = await gateway.CompactAsync(call, compactRequest, access, cancellationToken);
            if (!compacted.Success) { return Report(ContextCompactionStatus.Failed, compacted.Error!); }
            lastResponse = compacted.Data!;
            if (lastResponse.Status != ModelResponseStatus.Completed)
            {
                return Report(ContextCompactionStatus.Failed, lastResponse.Error ?? new(ServiceErrorType.Rejected,
                    "Compact не подтвердил завершение."));
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (lastResponse.Output.Count == 0)
            {
                return Report(ContextCompactionStatus.Failed, new(ServiceErrorType.Rejected,
                    "Compact вернул пустое окно для непустой истории."));
            }
            if (lastResponse.Continuation is not null)
            {
                return Report(ContextCompactionStatus.Failed, new(ServiceErrorType.Unsupported,
                    "Compact вернул неподдержанное продолжение."));
            }
            pairError = CompactFunctionPairInspector.Validate(history, lastResponse.Output, cancellationToken);
            if (pairError is not null) { return Report(ContextCompactionStatus.Failed, pairError); }
            ModelRequest candidate = new(prepared.Model, prepared.ReasoningEffort, prepared.Instructions,
                providers.Concat(lastResponse.Output).Concat(tail), prepared.Tools, parameters: prepared.Parameters);
            pairError = ContextBuilder.ValidateFunctionPairs(candidate.Input, cancellationToken);
            if (pairError is not null) { return Report(ContextCompactionStatus.Failed, pairError); }
            counted = await counter.CountAsync(candidate, cancellationToken);
            if (!counted.Success) { return Report(ContextCompactionStatus.Failed, counted.Error!); }
            cancellationToken.ThrowIfCancellationRequested();
            ContextTokenCount candidateCount = counted.Data!;
            if (estimate is long previousEstimate && candidateCount.EstimatedInputTokens is long candidateEstimate
                && candidateEstimate >= previousEstimate)
            {
                return Report(ContextCompactionStatus.NoReduction);
            }
            DateTimeOffset nowUtc = time.GetUtcNow();
            if (dialog.IsExpired(nowUtc))
            {
                return Report(ContextCompactionStatus.Failed, new(ServiceErrorType.Expired, "Срок доступности диалога истёк."));
            }
            long nextVersion = checked((active?.Version ?? 0) + 1);
            ServiceResult<DialogWriteToken> saved = await writer.SaveAsync(new(call.DialogId, call.OwnerId, nowUtc),
                token, through, lastResponse, cancellationToken);
            if (!saved.Success) { return Report(ContextCompactionStatus.Failed, saved.Error!); }
            token = saved.Data!;
            active = new(nextVersion, through, lastResponse);
            prepared = candidate;
            count = candidateCount;
            // Успешный save уже состоялся даже при поздней отмене; не выдаём старое окно за актуальное.
            cancellationToken.ThrowIfCancellationRequested();
            if (count.EstimatedInputTokens is not long known) { return Report(ContextCompactionStatus.UnknownBudget); }
            estimate = known;
            if (estimate < settings.TokenThreshold) { return Report(ContextCompactionStatus.TargetReached); }
            history = [.. lastResponse.Output];
        }
        return Report(ContextCompactionStatus.PassLimitReached);
    }

    /// <summary>Проецирует только подтверждённые compact controls; полный generation request остаётся неизменным.</summary>
    private static ModelRequestParameters? CompactParameters(ModelRequestParameters? parameters)
    {
        if (parameters is null) { return null; }
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in parameters.Content.EnumerateObject())
            {
                if (property.Name is "reasoning" or "service_tier" or "prompt_cache_key") { property.WriteTo(writer); }
            }
            writer.WriteEndObject();
        }
        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
        return new(document.RootElement);
    }
}
