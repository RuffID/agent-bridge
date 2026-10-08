using System.Runtime.ExceptionServices;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

namespace AgentBridge.Application;

/// <inheritdoc/>
public class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly TimeProvider _timeProvider;

    /// <summary>Использует registry отдельных scopes и явные часы; хранения и внешнего транспорта здесь нет.</summary>
    public ToolExecutor(IToolRegistry registry, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _registry = registry;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ToolExecutionSession CreateSession(ApplicationCallContext call, DialogWriteToken token, DateTimeOffset? expiresAtUtc,
        IEnumerable<string> selectedToolNames, ToolExecutionLimits limits, IToolExecutionCheckpoint? checkpoint = null) =>
        new(call, token, expiresAtUtc, selectedToolNames, limits, _timeProvider, checkpoint);

    /// <inheritdoc/>
    public async Task<ServiceResult<ToolExecutionBatch>> ExecuteAsync(ToolExecutionSession session, StoredModelStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(step);
        cancellationToken.ThrowIfCancellationRequested();
        ServiceResult<List<ToolExecutionResult>> parsed = Parse(session, step, cancellationToken);
        if (!parsed.Success) return ServiceResult<ToolExecutionBatch>.Fail(parsed.Error!);
        ToolExecutionResult[] results = parsed.Data!.ToArray();
        ServiceError? error = session.Enter(step.StepId, results.Length);
        if (error is not null) return ServiceResult<ToolExecutionBatch>.Fail(error);

        // Общий monotonic budget и fixed expiry. Срок не обновляется после каждого handler.
        TimeSpan remaining = session.Remaining;
        TimeSpan untilExpiry = session.ExpiresAtUtc is { } expiry
            ? expiry - session.TimeProvider.GetUtcNow() : session.Remaining;
        TimeSpan budget = remaining < untilExpiry ? remaining : untilExpiry;
        if (budget <= TimeSpan.Zero)
        {
            ToolExecutionBatch expired = new(results, session.CheckTime());
            session.Finish(expired);
            return ServiceResult<ToolExecutionBatch>.Ok(expired);
        }
        using CancellationTokenSource deadline = new(budget, session.TimeProvider);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        ExceptionDispatchInfo? unexpected = null;
        ToolExecutionBatch batch;
        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, results.Length), new ParallelOptions
            {
                MaxDegreeOfParallelism = session.Limits.MaxConcurrency,
                CancellationToken = linked.Token
            }, async (index, workerToken) =>
            {
                try
                {
                    results[index] = await ExecuteOneAsync(session, results[index], workerToken,
                        result => results[index] = result);
                    if (results[index].Status is ToolExecutionStatus.Unknown or ToolExecutionStatus.NotStarted
                        && results[index].Error is not null) linked.Cancel();
                }
                catch (Exception exception)
                {
                    if (exception is not OperationCanceledException || !workerToken.IsCancellationRequested)
                        Interlocked.CompareExchange(ref unexpected, ExceptionDispatchInfo.Capture(exception), null);
                    // Parallel.ForEachAsync ждёт активные workers до распространения исключения.
                    linked.Cancel();
                    throw;
                }
            });
            error = session.CheckTime();
            if (results.Any(result => result.Output is null))
                error ??= results.FirstOrDefault(result => result.Output is null && result.Error is not null)?.Error
                    ?? SafeError(ServiceErrorType.Conflict);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (unexpected is null && !cancellationToken.IsCancellationRequested
            && (deadline.IsCancellationRequested || session.CheckTime() is not null
                || results.Any(result => result.Output is null && result.Error is not null)))
        {
            error = session.CheckTime() ?? results.FirstOrDefault(result => result.Output is null && result.Error is not null)?.Error
                ?? SafeError(ServiceErrorType.Conflict);
        }
        catch
        {
            error = SafeError(ServiceErrorType.Conflict);
            unexpected?.Throw();
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            throw;
        }
        finally
        {
            // В finally уже нет живых tasks: outputs доступны после отмены/неожиданной ошибки.
            if (cancellationToken.IsCancellationRequested) error ??= SafeError(ServiceErrorType.Conflict);
            batch = new ToolExecutionBatch(results, error);
            session.Finish(batch);
        }
        return ServiceResult<ToolExecutionBatch>.Ok(batch);
    }

    /// <summary>Проверяет вызов в отдельном scope и сохраняет подтверждённость исхода до освобождения ресурсов.</summary>
    private async Task<ToolExecutionResult> ExecuteOneAsync(ToolExecutionSession session, ToolExecutionResult attempt,
        CancellationToken cancellationToken, Action<ToolExecutionResult> record)
    {
        bool started = false;
        ToolExecutionResult current = attempt;
        IToolHandlerScope? scope = null;
        Exception? primary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ServiceError? timeError = session.CheckTime();
            if (timeError is not null) return current;
            if (!session.IsSelected(attempt.Invocation.Name))
                return current = Reject(attempt, ServiceErrorType.Forbidden);
            scope = _registry.OpenScope(attempt.Invocation.Name);
            if (scope is null) return current = Reject(attempt, ServiceErrorType.NotFound);
            // Scoped handler создаётся только в своём scope; Definition не подменяет описание registry.
            IToolHandler handler = scope.Handler;
            ModelToolDefinition definition = scope.Definition;
            ModelToolDefinition actual = handler.Definition;
            if (!StringComparer.Ordinal.Equals(actual.Name, definition.Name)
                || !StringComparer.Ordinal.Equals(actual.Description, definition.Description)
                || actual.Strict != definition.Strict
                || actual.Parameters.GetRawText() != definition.Parameters.GetRawText())
                throw new InvalidOperationException("Описание handler не совпадает с регистрацией инструмента.");

            ServiceResult validation = await scope.Validator.ValidateAsync(definition, attempt.Invocation, cancellationToken);
            if (!validation.Success) return current = Reject(attempt, validation.Error!.Type);
            cancellationToken.ThrowIfCancellationRequested();
            if (session.CheckTime() is not null) return current;
            if (session.Checkpoint is not null)
            {
                ServiceResult checkpoint = await session.Checkpoint.BeforeExecuteAsync(attempt.Identity, attempt.Invocation, cancellationToken);
                if (!checkpoint.Success)
                    return current = new(attempt.Identity, attempt.Invocation, ToolExecutionStatus.NotStarted,
                        error: SafeError(checkpoint.Error!.Type));
                cancellationToken.ThrowIfCancellationRequested();
                if (session.CheckTime() is not null) return current;
            }
            started = true;
            ServiceResult<ToolOutput> executed = await handler.ExecuteAsync(attempt.Invocation, cancellationToken);
            if (!executed.Success)
            {
                if (executed.Error!.Type == ServiceErrorType.Timeout)
                    return current = new(attempt.Identity, attempt.Invocation, ToolExecutionStatus.Unknown,
                        error: SafeError(ServiceErrorType.Timeout));
                return current = Reject(attempt, executed.Error.Type);
            }
            CanonicalModelItem output = Output(attempt.Invocation.CallId, executed.Data!.Content.GetRawText());
            // Подтверждённый результат сохраняется даже при поздней отмене.
            return current = new(attempt.Identity, attempt.Invocation, ToolExecutionStatus.Succeeded, output);
        }
        catch (Exception exception)
        {
            primary = exception;
            // Dispose failure после подтверждённого результата не превращает его в неизвестный.
            if (started && current.Status == ToolExecutionStatus.NotStarted)
                current = new(attempt.Identity, attempt.Invocation, ToolExecutionStatus.Unknown,
                    error: SafeError(ServiceErrorType.Conflict));
            throw;
        }
        finally
        {
            record(current);
            if (scope is not null)
            {
                try { await scope.DisposeAsync(); }
                catch (Exception cleanup) when (primary is not null)
                {
                    throw new AggregateException("Ошибка инструмента и освобождения его scope.", primary, cleanup);
                }
            }
        }
    }

    /// <summary>Проверяет весь шаг до действий и исключает закрытые FIFO пары без глобальной дедупликации ID.</summary>
    private static ServiceResult<List<ToolExecutionResult>> Parse(ToolExecutionSession session, StoredModelStep step,
        CancellationToken cancellationToken)
    {
        if (step.Response.Status != ModelResponseStatus.Completed)
            return ServiceResult<List<ToolExecutionResult>>.Fail(SafeError(ServiceErrorType.Conflict));
        Dictionary<string, Queue<ToolExecutionResult>> pending = new(StringComparer.Ordinal);
        List<ToolExecutionResult> ordered = [];
        int index = 0;
        foreach (CanonicalModelItem item in step.Response.Output)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement content = item.Content;
            if (TryString(content, "type", out string? type) && type is "function_call" or "function_call_output")
            {
                if (!TryString(content, "call_id", out string? callId)) return Invalid();
                if (type == "function_call_output")
                {
                    if (!pending.TryGetValue(callId!, out Queue<ToolExecutionResult>? queue) || queue.Count == 0
                        || !content.TryGetProperty("output", out JsonElement output)
                        || output.ValueKind is not (JsonValueKind.String or JsonValueKind.Array)) return Invalid();
                    ordered.Remove(queue.Dequeue());
                }
                else
                {
                    if (!TryString(content, "name", out string? name)
                        || !TryString(content, "arguments", out string? arguments)) return Invalid();
                    if (content.TryGetProperty("status", out JsonElement status)
                        && (status.ValueKind != JsonValueKind.String || status.GetString() != "completed")) return Invalid();
                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(arguments!);
                        if (document.RootElement.ValueKind != JsonValueKind.Object) return Invalid();
                        ToolInvocation invocation = new(session.Call, callId!, name!, document.RootElement);
                        ToolExecutionResult attempt = new(new(session.Call, session.IncarnationId, step.StepId, index),
                            invocation, ToolExecutionStatus.NotStarted);
                        if (!pending.TryGetValue(callId!, out Queue<ToolExecutionResult>? queue))
                            pending.Add(callId!, queue = new Queue<ToolExecutionResult>());
                        queue.Enqueue(attempt);
                        ordered.Add(attempt);
                    }
                    catch (JsonException) { return Invalid(); }
                }
            }
            index++;
        }
        return ServiceResult<List<ToolExecutionResult>>.Ok(ordered);
    }

    /// <summary>Читает обязательную непустую строку canonical элемента без нормализации.</summary>
    private static bool TryString(JsonElement content, string name, out string? value)
    {
        value = null;
        if (!content.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>Возвращает безопасный отказ протокольного разбора без исходных данных.</summary>
    private static ServiceResult<List<ToolExecutionResult>> Invalid() =>
        ServiceResult<List<ToolExecutionResult>>.Fail(SafeError(ServiceErrorType.Validation));

    /// <summary>Создаёт явный error output только для подтверждённого отказа.</summary>
    private static ToolExecutionResult Reject(ToolExecutionResult attempt, ServiceErrorType type)
    {
        ServiceError safe = SafeError(type);
        string content = JsonSerializer.Serialize(new { error = new { type = safe.Type.ToString(), message = safe.Message } });
        return new(attempt.Identity, attempt.Invocation, ToolExecutionStatus.Rejected, Output(attempt.Invocation.CallId, content), safe);
    }

    /// <summary>Связывает сериализованный JSON результата с исходным call_id.</summary>
    private static CanonicalModelItem Output(string callId, string content) =>
        new(JsonSerializer.SerializeToElement(new { type = "function_call_output", call_id = callId, output = content }));

    /// <summary>Сохраняет semantic тип без сообщения приложения/исключения.</summary>
    private static ServiceError SafeError(ServiceErrorType type) => new(type, "Вызов инструмента не завершён успешно.");
}
