using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Application;

/// <inheritdoc cref="IToolExecutionCheckpoint"/>
/// <remarks>Владеет успешными tokens одного run; scopes короткие и serialized, неизвестная запись блокирует дальнейшие writes.</remarks>
internal class AgentRunSession(IServiceScopeFactory scopes, ApplicationCallContext call, DialogSnapshot original,
    TimeProvider time, TurnModelSettings settings, Action<Exception> onCleanupFailure, DialogRunOptions? runOptions = null) : IToolExecutionCheckpoint, IDialogContextWriter, IDisposable
{
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly List<StoredDialogTurn> _turns = [.. original.Turns];
    private StoredDialogContext? _active = original.ActiveContext;
    private DialogWriteToken _token = original.Token;
    private bool _blocked;
    private DialogRunLease? _lease;
    private DialogContinuationState? _continuation = original.Continuation;

    /// <summary>Последняя безопасная storage ошибка либо отсутствие отказа.</summary>
    public ServiceError? Error { get; private set; }
    /// <summary>После любого отказа или unknown исхода дальнейшие writes запрещены.</summary>
    public bool Blocked => _blocked;
    /// <summary>Подтверждённый state для следующего context build; не перечитывает root ради retry.</summary>
    public DialogSnapshot Snapshot => new(_token, original.OwnerId, original.CreatedAtUtc, original.ExpiresAtUtc,
        original.ContentBytes, _turns, _active, original.Selection, original.Catalog, _continuation,
        _turns.Select(turn => original.EffectiveTurns.SingleOrDefault(item => item.Id == turn.Id) ?? turn)) { OwnedRunLease = _lease };
    /// <summary>Сохранённый turn этого run либо отсутствие Begin.</summary>
    public StoredDialogTurn? Turn => _turns.SingleOrDefault(turn => turn.Id == call.TurnId);

    /// <summary>Начинает обращение отдельным scope.</summary>
    public Task<ServiceResult<DialogWriteToken>> BeginAsync(IReadOnlyList<CanonicalModelItem> input, CancellationToken ct)
    {
        Action accepted = () => _turns.Add(new(call.TurnId, _turns.Count + 1L, DialogTurnStatus.InProgress, input, [], settings));
        if (original.Catalog is null)
            return WriteAsync<IDialogTurnWriter>((writer, access, token) => writer.BeginWithSettingsAsync(access, token,
                call.TurnId, input, settings, ct), accepted, ct);

        DialogRunOptions configured = runOptions ?? throw new InvalidOperationException("Registered run options required.");
        return WriteAsync<IDialogRunLifecycle>(async (writer, access, token) =>
        {
            ServiceResult<DialogRunState> begun = await writer.BeginAsync(new(access, token, call.TurnId, input, settings,
                configured.LeasePeriod, configured.Scope, configured.Profile), ct);
            if (!begun.Success) return ServiceResult<DialogWriteToken>.Fail(begun.Error!);
            _lease = begun.Data!.Lease;
            _continuation = new(begun.Data.Token, DialogReadiness.Active, call.TurnId, null, false,
                _lease.Epoch, original.Continuation!.RecoveryRevision, _lease, []);
            return ServiceResult<DialogWriteToken>.Ok(begun.Data.Token);
        }, accepted, ct);
    }

    /// <summary>Сохраняет весь model report/calls до начала handler.</summary>
    public Task<ServiceResult<DialogWriteToken>> AppendAsync(StoredModelStep step) =>
        WriteAsync<IDialogTurnWriter>((writer, access, token) => _lease is null
            ? writer.AppendAsync(access, token, call.TurnId, step.Response.Output, [step], CancellationToken.None)
            : writer.AppendAsync(new DialogRunWriteAccess(access, _lease), token, call.TurnId,
                step.Response.Output, [step], CancellationToken.None), () => ChangeTurn(step.Response.Output, [step]), CancellationToken.None);

    /// <inheritdoc/>
    public async Task<ServiceResult> BeforeExecuteAsync(ToolExecutionIdentity identity, ToolInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ServiceResult<DialogWriteToken> saved = await WriteAsync<IDialogToolAttemptWriter>(
            (writer, access, token) => _lease is null ? writer.StartAsync(access, token, identity, cancellationToken) :
                writer.StartAsync(new DialogRunWriteAccess(access, _lease), token, identity, cancellationToken),
            () => ChangeAttempt(identity.StepId, new(identity.OutputIndex, identity.Call.AgentId, ToolAttemptState.Started)), cancellationToken);
        return saved.Success ? ServiceResult.Ok() : ServiceResult.Fail(saved.Error!);
    }

    /// <summary>Принимает LastResult после всех awaited workers, включая путь exception/cancel.</summary>
    public Task<ServiceResult<DialogWriteToken>> SaveOutcomesAsync(Guid stepId, ToolExecutionBatch batch) =>
        WriteAsync<IDialogToolAttemptWriter>((writer, access, token) => _lease is null ?
            writer.SaveOutcomesAsync(access, token, call.TurnId, stepId, batch, CancellationToken.None) :
            writer.SaveOutcomesAsync(new DialogRunWriteAccess(access, _lease), token, call.TurnId, stepId, batch, CancellationToken.None), () =>
        {
            ChangeTurn(batch.Outputs, []);
            foreach (ToolExecutionResult result in batch.Results)
                ChangeAttempt(stepId, new(result.Identity.OutputIndex, result.Identity.Call.AgentId, result.Status switch
                {
                    ToolExecutionStatus.Succeeded => ToolAttemptState.Succeeded,
                    ToolExecutionStatus.Rejected => ToolAttemptState.Rejected,
                    ToolExecutionStatus.Unknown => ToolAttemptState.Unknown,
                    ToolExecutionStatus.NotStarted => ToolAttemptState.NotStarted,
                    _ => throw new InvalidOperationException("Неизвестный исход инструмента.")
                }));
        }, CancellationToken.None);

    /// <inheritdoc/>
    public Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected,
        long throughTurnSequence, ModelResponse compaction, CancellationToken cancellationToken = default)
    {
        if (expected.IncarnationId != _token.IncarnationId || expected.Revision != _token.Revision)
            throw new InvalidOperationException("Compact передал неподтверждённую версию.");
        return WriteAsync<IDialogContextWriter>((writer, freshAccess, token) => _lease is null ?
            writer.SaveWithModelAsync(freshAccess, token, throughTurnSequence, compaction, settings.Model, cancellationToken) :
            writer.SaveWithModelAsync(new DialogRunWriteAccess(freshAccess, _lease), token,
                throughTurnSequence, compaction, settings.Model, cancellationToken),
            () => _active = new((_active?.Version ?? 0) + 1, throughTurnSequence, compaction, settings.Model,
                _continuation?.RecoveryRevision ?? 0), cancellationToken);
    }

    /// <summary>Finalization не использует отменённый caller token и не повторяет отказавшую запись.</summary>
    public Task<ServiceResult<DialogWriteToken>> FinishAsync(DialogTurnStatus status)
    {
        if (_lease is null)
            return WriteAsync<IDialogTurnWriter>((writer, access, token) => writer.FinishAsync(access, token, call.TurnId,
                status, [], [], CancellationToken.None), () => ChangeTurn([], [], status), CancellationToken.None);

        return WriteAsync<IDialogRunLifecycle>(async (writer, access, token) =>
        {
            ServiceResult<DialogContinuationState> finalized = await writer.FinalizeAsync(new(access, token, _lease, status), CancellationToken.None);
            if (!finalized.Success) return ServiceResult<DialogWriteToken>.Fail(finalized.Error!);
            _continuation = finalized.Data!;
            _lease = null;
            return ServiceResult<DialogWriteToken>.Ok(finalized.Data!.Token);
        }, () => ChangeTurn([], [], status), CancellationToken.None);
    }

    /// <summary>Сериализует scope, fresh UTC, save и local token update; не удерживает контекст между вызовами.</summary>
    private async Task<ServiceResult<DialogWriteToken>> WriteAsync<TPort>(
        Func<TPort, DialogAccess, DialogWriteToken, Task<ServiceResult<DialogWriteToken>>> operation,
        Action accepted, CancellationToken ct) where TPort : notnull
    {
        await _writes.WaitAsync(ct);
        try
        {
            if (_blocked) return ServiceResult<DialogWriteToken>.Fail(Error ?? new(ServiceErrorType.Conflict, "Исход предыдущей записи неизвестен."));
            try
            {
                return await AgentRunScope.ExecuteAsync(scopes, async provider =>
                {
                    ServiceResult<DialogWriteToken> result = await operation(provider.GetRequiredService<TPort>(),
                        new(call.DialogId, call.OwnerId, time.GetUtcNow()), _token);
                    if (!result.Success)
                    {
                        Error = result.Error;
                        _blocked = true;
                        return result;
                    }
                    // При поздней отмене compact факт успешной записи уже захвачен до возвращения в compactor.
                    _token = result.Data!;
                    accepted();
                    return result;
                }, onCleanupFailure);
            }
            catch
            {
                _blocked = true;
                throw;
            }
        }
        finally { _writes.Release(); }
    }

    /// <summary>Обновляет только локальную проекцию подтверждённых items/steps/status.</summary>
    private void ChangeTurn(IEnumerable<CanonicalModelItem> items, IEnumerable<StoredModelStep> steps, DialogTurnStatus? status = null)
    {
        int index = _turns.FindIndex(turn => turn.Id == call.TurnId);
        StoredDialogTurn prior = _turns[index];
        _turns[index] = new(prior.Id, prior.Sequence, status ?? prior.Status, prior.Items.Concat(items), prior.ModelSteps.Concat(steps), prior.Settings);
    }

    /// <summary>Заменяет состояние конкретной исходной позиции без глобальной дедупликации call_id.</summary>
    private void ChangeAttempt(Guid stepId, StoredToolAttempt attempt)
    {
        StoredDialogTurn turn = Turn!;
        StoredModelStep[] steps = turn.ModelSteps.Select(step => step.StepId != stepId ? step :
            new StoredModelStep(step.StepId, step.Response, step.ToolAttempts.Where(item => item.OutputIndex != attempt.OutputIndex).Append(attempt))).ToArray();
        int index = _turns.FindIndex(item => item.Id == call.TurnId);
        _turns[index] = new(turn.Id, turn.Sequence, turn.Status, turn.Items, steps, turn.Settings);
    }

    /// <inheritdoc/>
    public void Dispose() => _writes.Dispose();
}
