using System.Runtime.ExceptionServices;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.Application;

/// <summary>Полный новый ход агента: fixed settings/providers, compact/guard/model/tools и короткие durable записи.</summary>
/// <remarks>Приложение авторизует выбранного агента и задаёт ordered ContextBuilder.
/// Existing TurnId не переисполняется. Неожиданные exceptions распространяются после попытки честной finalization;
/// отказ/unknown storage блокирует дальнейшие записи без refresh/retry.</remarks>
public class AgentRunner(IServiceScopeFactory scopes, ContextBuilder builder, IModelSettingsReader settingsReader,
    IModelAccessResolver accessResolver, IModelGateway gateway, IContextTokenCounter counter, IToolRegistry registry,
    IToolExecutor executor, TimeProvider time, IOptionsSnapshot<AgentOptions> options,
    IOptionsSnapshot<ContextCompactionOptions> compactionOptions, ContextModelGuard compatibility)
{
    /// <summary>Передаёт предварительные updates и возвращает явный итог, без автоматического восстановления действий.</summary>
    public async Task<AgentRunResult> RunAsync(AgentRunRequest request,
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ModelSettingsSnapshot? settings = null;
        AgentRunSession? session = null;
        ModelResponse? lastResponse = null;
        ToolExecutionBatch? lastTools = null;
        ContextBudgetAssessment? lastBudgetAssessment = null;
        Exception? scopeCleanupFailure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Копии options фиксируются до первого I/O и не перечитываются между шагами.
            AgentOptions configured = options.Value;
            if (configured.InstructionsSource is not { } source || !Enum.IsDefined(source))
                return Report(AgentRunStatus.Failed, new(ServiceErrorType.Validation, "Agent.InstructionsSource: required_or_invalid."));
            string? instructions = request.Instructions ?? (source == AgentInstructionsSource.Configuration ? configured.Instructions : null);
            if (string.IsNullOrWhiteSpace(instructions))
                return Report(AgentRunStatus.Failed, new(ServiceErrorType.Validation, "Agent.Instructions: required — инструкции обращения не предоставлены."));
            int maxSteps = options.Value.MaxToolSteps;
            int maxPasses = compactionOptions.Value.MaxPasses;
            ContextBudgetPolicy budgetPolicy = compactionOptions.Value.BudgetPolicy;
            if (maxSteps <= 0 || maxPasses <= 0) return Report(AgentRunStatus.Failed, new(ServiceErrorType.Validation, "Некорректные ограничения агента."));
            ToolExecutionLimits limits = new(Math.Min(maxSteps, request.ToolLimits.MaxSteps), request.ToolLimits.MaxCallsPerStep,
                request.ToolLimits.MaxConcurrency, request.ToolLimits.Timeout);
            HashSet<string> names = new(request.SelectedToolNames, StringComparer.Ordinal);
            ModelToolDefinition[] tools = request.SelectedToolNames.Select(name => registry.Definitions.SingleOrDefault(tool => tool.Name == name))
                .OfType<ModelToolDefinition>().ToArray();
            if (names.Count != request.SelectedToolNames.Count || tools.Length != names.Count)
                return Report(AgentRunStatus.Failed, new(ServiceErrorType.Validation, "Выбор инструментов содержит повторы или неизвестные имена."));
            ServiceResult<DialogSnapshot> read = await AgentRunScope.ExecuteAsync(scopes,
                provider => provider.GetRequiredService<IDialogReader>().ReadAsync(
                    new(request.Call.DialogId, request.Call.OwnerId, time.GetUtcNow()), cancellationToken),
                cleanup => scopeCleanupFailure = cleanup);
            if (!read.Success) return Report(AgentRunStatus.Failed, read.Error!);
            DialogSnapshot dialog = read.Data!;
            if (!dialog.OwnerId.Equals(request.Call.OwnerId)) return Report(AgentRunStatus.Failed, new(ServiceErrorType.Forbidden, "Диалог недоступен владельцу."));
            if (!dialog.Token.DialogId.Equals(request.Call.DialogId)) return Report(AgentRunStatus.Failed, new(ServiceErrorType.Conflict, "Снимок другого диалога."));
            if (dialog.IsExpired(time.GetUtcNow())) return Report(AgentRunStatus.Failed, new(ServiceErrorType.Expired, "Срок диалога истёк."));
            StoredDialogTurn? existing = dialog.Turns.SingleOrDefault(turn => turn.Id == request.Call.TurnId);
            if (existing is not null)
                return new(AgentRunStatus.Interrupted, false, dialog.Token, null, existing, null,
                    new(ServiceErrorType.Conflict, "Существующее обращение не допускает автоматический replay."));
            ServiceResult<ModelAccess> access = await accessResolver.ResolveAsync(request.Call.OwnerId, cancellationToken);
            if (!access.Success) return Report(AgentRunStatus.Failed, access.Error!);
            ServiceResult<ModelSettingsSnapshot> selected = await settingsReader.ReadWithAccessAsync(request.Call.OwnerId,
                access.Data!, request.Model ?? dialog.Selection?.Model, request.Effort ?? dialog.Selection?.Effort, cancellationToken);
            if (!selected.Success) return Report(AgentRunStatus.Failed, selected.Error!);
            settings = selected.Data!;
            ServiceResult compatible = await compatibility.CheckAsync(request.Call, dialog, settings, cancellationToken);
            if (!compatible.Success) return Report(AgentRunStatus.Failed, compatible.Error!);
            ModelRequest initial = new(settings.Model.Id, settings.ReasoningEffort, instructions, request.Input, tools,
                parameters: request.Parameters);
            ServiceResult<ModelRequest> prepared = await builder.BuildAsync(request.Call, dialog, initial, time.GetUtcNow(), cancellationToken);
            if (!prepared.Success) return Report(AgentRunStatus.Failed, prepared.Error!);
            long through = dialog.ActiveContext?.ThroughTurnSequence ?? 0;
            int historyCount = (dialog.ActiveContext?.Items.Count ?? 0) + dialog.Turns.Where(turn => turn.Sequence > through).Sum(turn => turn.Items.Count);
            CanonicalModelItem[] providerItems = prepared.Data!.Input.Take(prepared.Data.Input.Count - historyCount - request.Input.Count).ToArray();
            ContextBuilder frozen = new([new FrozenProvider(providerItems)]);
            session = new(scopes, request.Call, dialog, time, TurnModelSettings.From(settings),
                cleanup => scopeCleanupFailure = cleanup);
            ServiceResult<DialogWriteToken> begun = await session.BeginAsync(request.Input, cancellationToken);
            if (!begun.Success) return Report(AgentRunStatus.Failed, begun.Error!);
            ToolExecutionSession toolSession = executor.CreateSession(request.Call, session.Snapshot.Token, dialog.ExpiresAtUtc,
                request.SelectedToolNames, limits, session);
            ContextCompactor compactor = new(frozen, counter, gateway, session, time,
                new FrozenOptions<ContextCompactionOptions>(new() { MaxPasses = maxPasses, BudgetPolicy = budgetPolicy }));
            ModelRequest next = new(settings.Model.Id, settings.ReasoningEffort, instructions, [], tools, parameters: request.Parameters);
            while (true)
            {
                lastBudgetAssessment = null;
                cancellationToken.ThrowIfCancellationRequested();
                ServiceResult<ContextCompactionResult> compacted = await compactor.CompactAsync(request.Call, session.Snapshot,
                    next, settings, access.Data!, cancellationToken);
                if (!compacted.Success) return await FinishAsync(AgentRunStatus.Failed, compacted.Error!);
                ContextCompactionResult compact = compacted.Data!;
                // Даже Failed/Unknown/NoReduction не заменяют full generation guard.
                ServiceResult<ContextBudgetAssessment> budget = await new ContextBudgetGuard(counter, budgetPolicy).CheckAsync(
                    compact.PreparedRequest, settings, cancellationToken);
                if (!budget.Success) return await FinishAsync(AgentRunStatus.Failed, budget.Error!);
                lastBudgetAssessment = budget.Data!;
                if (session.Blocked) return Report(AgentRunStatus.Failed, session.Error);
                if (compact.Error is not null) return await FinishAsync(AgentRunStatus.Failed, compact.Error);
                if (session.Snapshot.IsExpired(time.GetUtcNow()))
                    return await FinishAsync(AgentRunStatus.Failed, new(ServiceErrorType.Expired, "Срок диалога истёк."));
                List<CanonicalModelItem> streamed = [];
                ServiceResult<ModelResponse> generated;
                try
                {
                    Func<ModelStreamUpdate, CancellationToken, ValueTask>? stream = onUpdate is null ? null : async (update, ct) =>
                    {
                        if (update.Item is not null) streamed.Add(update.Item);
                        await onUpdate(update, ct);
                    };
                    generated = await gateway.GenerateAsync(request.Call, compact.PreparedRequest, access.Data!, stream, cancellationToken);
                }
                catch (Exception primary)
                {
                    if (streamed.Count > 0)
                    {
                        lastResponse = cancellationToken.IsCancellationRequested ? ModelResponse.Canceled(streamed) : ModelResponse.Incomplete(streamed);
                        try { await session.AppendAsync(new(Guid.NewGuid(), lastResponse)); }
                        catch (Exception cleanup) { throw new AggregateException("Ошибка модели и сохранения partial report.", primary, cleanup); }
                    }
                    throw;
                }
                if (!generated.Success) return await FinishAsync(AgentRunStatus.Failed, generated.Error!);
                lastResponse = generated.Data!;
                StoredModelStep step = new(Guid.NewGuid(), lastResponse);
                ServiceResult<DialogWriteToken> appended = await session.AppendAsync(step);
                if (!appended.Success) return Report(AgentRunStatus.Failed, appended.Error!);
                if (cancellationToken.IsCancellationRequested) return await FinishAsync(AgentRunStatus.Canceled);
                if (lastResponse.Status != ModelResponseStatus.Completed)
                    return await FinishAsync(lastResponse.Status switch
                    {
                        ModelResponseStatus.Canceled => AgentRunStatus.Canceled,
                        ModelResponseStatus.Incomplete => AgentRunStatus.Incomplete,
                        _ => AgentRunStatus.Failed
                    }, lastResponse.Error);
                ServiceResult<ToolExecutionBatch> executed;
                try { executed = await executor.ExecuteAsync(toolSession, step, cancellationToken); }
                catch (Exception primary)
                {
                    ToolExecutionBatch? partial = toolSession.LastResult;
                    if (partial is not null && partial.Results.Count > 0 && partial.Results.All(result => result.Identity.StepId == step.StepId))
                    {
                        lastTools = partial;
                        try { await session.SaveOutcomesAsync(step.StepId, partial); }
                        catch (Exception cleanup) { throw new AggregateException("Ошибка tools и сохранения partial batch.", primary, cleanup); }
                    }
                    throw;
                }
                if (!executed.Success) return await FinishAsync(AgentRunStatus.Interrupted, executed.Error!);
                ToolExecutionBatch batch = executed.Data!;
                lastTools = batch;
                if (batch.Results.Count > 0)
                {
                    ServiceResult<DialogWriteToken> outcomes = await session.SaveOutcomesAsync(step.StepId, batch);
                    if (!outcomes.Success) return Report(AgentRunStatus.Failed, outcomes.Error!);
                }
                if (!batch.CanContinue) return await FinishAsync(AgentRunStatus.Interrupted, batch.Error);
                if (batch.Results.Count == 0) return await FinishAsync(AgentRunStatus.Completed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && scopeCleanupFailure is null)
        {
            return await FinishAsync(AgentRunStatus.Canceled);
        }
        catch (Exception primary)
        {
            // Executor может нормализовать отмену worker в caller OCE; origin установлен владеющим scope, а не токеном.
            if (primary is OperationCanceledException && scopeCleanupFailure is OperationCanceledException)
                primary = scopeCleanupFailure;
            try { await FinishAsync(AgentRunStatus.Interrupted, new(ServiceErrorType.Conflict, "Обращение прервано.")); }
            catch (Exception cleanup) { throw new AggregateException("Ошибка run и finalization.", primary, cleanup); }
            ExceptionDispatchInfo.Capture(primary).Throw();
            throw;
        }
        finally { session?.Dispose(); }

        AgentRunResult Report(AgentRunStatus status, ServiceError? error = null, bool saved = false) =>
            new(status, saved, session?.Snapshot.Token, settings, session?.Turn, lastResponse, error, lastTools, lastBudgetAssessment);

        async Task<AgentRunResult> FinishAsync(AgentRunStatus status, ServiceError? error = null)
        {
            if (status == AgentRunStatus.Completed && cancellationToken.IsCancellationRequested) status = AgentRunStatus.Canceled;
            if (session?.Turn is null || session.Blocked) return Report(status == AgentRunStatus.Completed ? AgentRunStatus.Failed : status,
                session?.Error ?? error);
            DialogTurnStatus terminal = status switch
            {
                AgentRunStatus.Completed => DialogTurnStatus.Completed,
                AgentRunStatus.Canceled => DialogTurnStatus.Canceled,
                AgentRunStatus.Failed => DialogTurnStatus.Failed,
                _ => DialogTurnStatus.Incomplete
            };
            ServiceResult<DialogWriteToken> result = await session.FinishAsync(terminal);
            if (status == AgentRunStatus.Completed && cancellationToken.IsCancellationRequested) status = AgentRunStatus.Canceled;
            return result.Success ? Report(status, error, true) : Report(AgentRunStatus.Failed, result.Error!);
        }
    }

    /// <inheritdoc/>
    private class FrozenProvider(IEnumerable<CanonicalModelItem> items) : IContextProvider
    {
        private readonly ContextContribution _contribution = new(items);
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ContextContribution>.Ok(_contribution));
    }

    /// <inheritdoc/>
    private class FrozenOptions<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        /// <inheritdoc/>
        public T Value => value;
        /// <inheritdoc/>
        public T Get(string? name) => value;
    }
}
