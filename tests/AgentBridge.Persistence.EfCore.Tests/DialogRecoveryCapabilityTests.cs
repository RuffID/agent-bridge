using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual public recovery/readiness/fencing и continuation actual runner; без реального SQL/provider исполнения.</summary>
public class DialogRecoveryCapabilityTests
{
    /// <summary>Unknown/NotStarted mixed batch закрывается append-only, confirmed outputs сохраняются byte-for-byte; новый явный turn получает новый context.</summary>
    [Fact]
    public async Task MixedRepeatedCallIdsRecoverWithoutReplayOrOriginalMutation()
    {
        await using CatalogRunnerFixture fixture = new();
        DialogCatalogState created = await fixture.Storage.CreateAsync();
        Guid turn = Guid.NewGuid();
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Call("same"), CatalogWriteFixture.Call("same"), CatalogWriteFixture.Call("same")]));
        int actions = 0;
        fixture.HandlerAction = (_, _) =>
        {
            if (++actions == 1) return Task.FromResult(ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { confirmed = "UNCHANGED" }))));
            caller.Cancel();
            throw new OperationCanceledException(caller.Token);
        };
        AgentRunResult interrupted = await fixture.RunAsync(created.Token, turn, caller.Token);
        Assert.True(interrupted.TerminalSaved);
        DialogSnapshot before = await fixture.Storage.SnapshotAsync(interrupted.Token!);
        Assert.Equal([ToolAttemptState.Succeeded, ToolAttemptState.Unknown, ToolAttemptState.NotStarted],
            before.Turns[0].ModelSteps[0].ToolAttempts.Select(item => item.State));
        Assert.Equal(DialogReadiness.RecoveryRequired, before.Continuation!.Readiness);
        Assert.Equal(2, before.Continuation.Pending.Count);
        string originals = OriginalHistory(fixture.Storage);
        string confirmed = Assert.Single(before.Turns[0].Items, IsOutput).Content.GetRawText();
        DateTimeOffset? messageTime = before.Catalog!.LastSavedMessageAtUtc;
        int calls = fixture.HandlerCalls;

        AgentRunResult blocked = await fixture.RunAsync(before.Token, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, blocked.Error!.Type);
        Assert.Equal(calls, fixture.HandlerCalls);
        DialogRecoveryRequest recovery = Request(fixture.Storage, before.Continuation, acknowledge: true);
        DialogRecoveryResult result = (await fixture.Storage.Recovery.RecoverAsync(recovery, TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(DialogReadiness.Ready, result.Continuation.Readiness);
        Assert.Equal(before.Turns[0].Status, result.Continuation.Terminal);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(originals, OriginalHistory(fixture.Storage));
        Assert.Equal(calls, fixture.HandlerCalls);
        DialogSnapshot effective = await fixture.Storage.SnapshotAsync(result.Continuation.Token);
        Assert.Equal(messageTime, effective.Catalog!.LastSavedMessageAtUtc);
        Assert.Single(effective.Turns[0].Items, IsOutput);
        Assert.Equal(3, effective.EffectiveTurns[0].Items.Count(IsOutput));
        Assert.Contains(effective.EffectiveTurns[0].Items, item => item.Content.GetRawText() == confirmed);
        Assert.Contains(result.Records, item => item.Output.Content.GetProperty("output").GetString()!.Contains("tool_outcome_unknown", StringComparison.Ordinal));
        Assert.Contains(result.Records, item => item.Output.Content.GetProperty("output").GetString()!.Contains("tool_not_started", StringComparison.Ordinal));

        Guid nextTurn = Guid.NewGuid();
        fixture.PageSnapshot = "page-v2";
        fixture.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Assistant("Готово")]));
        AgentRunResult continued = await fixture.RunAsync(result.Continuation.Token, nextTurn, TestContext.Current.CancellationToken);
        Assert.Equal(AgentRunStatus.Completed, continued.Status);
        Assert.Equal(created.Token.DialogId, continued.Token!.DialogId);
        Assert.Equal(2, fixture.HandlerCalls);
        Assert.Equal(2, fixture.Requests.Count);
        Assert.Contains(fixture.Requests.Last().Input, item => item.Content.TryGetProperty("content", out JsonElement content) &&
            content.ValueKind == JsonValueKind.String && content.GetString() == "page-v2");
        Assert.DoesNotContain(fixture.Requests.Last().Input, item => item.Content.TryGetProperty("content", out JsonElement content) &&
            content.ValueKind == JsonValueKind.String && content.GetString() == "page-v1");
        AgentRunResult duplicate = await fixture.RunAsync(continued.Token, turn, TestContext.Current.CancellationToken);
        Assert.Equal(AgentRunStatus.Interrupted, duplicate.Status);
        Assert.Equal(ServiceErrorType.Conflict, duplicate.Error!.Type);
        Assert.Equal(2, fixture.Requests.Count);
        Assert.Equal(before.Turns[0].Status, (await fixture.Storage.SnapshotAsync(continued.Token)).Turns[0].Status);
    }

    /// <summary>После saved calls/Started до handler или внутри handler pending outcomes имеют правильную достоверность.</summary>
    [Theory]
    [InlineData("after-calls", 0, ToolAttemptState.NotStarted)]
    [InlineData("after-start", 0, ToolAttemptState.NotStarted)]
    [InlineData("inside", 1, ToolAttemptState.Unknown)]
    [InlineData("timeout-readonly", 1, ToolAttemptState.Unknown)]
    public async Task CancellationPositionNeverInventsSuccessfulOutput(string point, int handlers, ToolAttemptState expected)
    {
        await using CatalogRunnerFixture fixture = new();
        DialogCatalogState created = await fixture.Storage.CreateAsync();
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Call("x"), CatalogWriteFixture.Call("y")]));
        bool canceled = false;
        fixture.Storage.AfterCommit = () =>
        {
            if (canceled || fixture.Storage.Committed.Steps.Count == 0) return;
            string? attempts = fixture.Storage.Committed.Steps[0].ToolAttemptsJson;
            if (point == "after-calls" && attempts is null || point == "after-start" && attempts is not null)
            {
                canceled = true;
                caller.Cancel();
            }
        };
        if (point == "inside")
            fixture.HandlerAction = (_, _) => { caller.Cancel(); throw new OperationCanceledException(caller.Token); };
        if (point == "timeout-readonly")
            fixture.HandlerAction = (_, _) => Task.FromResult(ServiceResult<ToolOutput>.Fail(new(ServiceErrorType.Timeout, "read-only timeout")));
        AgentRunResult interrupted = await fixture.RunAsync(created.Token, Guid.NewGuid(), caller.Token);
        Assert.True(interrupted.TerminalSaved);
        Assert.Equal(handlers, fixture.HandlerCalls);
        DialogSnapshot snapshot = await fixture.Storage.SnapshotAsync(interrupted.Token!);
        Assert.DoesNotContain(snapshot.Turns.SelectMany(turn => turn.Items), IsOutput);
        Assert.Equal(DialogReadiness.RecoveryRequired, snapshot.Continuation!.Readiness);
        Assert.Equal(expected, snapshot.Continuation.Pending[0].State);
        Assert.Equal(ToolAttemptState.NotStarted, snapshot.Continuation.Pending[1].State);
        DialogRecoveryRequest request = Request(fixture.Storage, snapshot.Continuation, acknowledge: expected == ToolAttemptState.Unknown);
        DialogRecoveryResult recovered = (await fixture.Storage.Recovery.RecoverAsync(request, TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(DialogReadiness.Ready, recovered.Continuation.Readiness);
        Assert.Equal(handlers, fixture.HandlerCalls);
    }

    /// <summary>Text partial и отсутствие canonical delta finalizes Ready без создания synthetic message.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextCancellationNeedsFinalizationButNoFunctionRecovery(bool partial)
    {
        await using CatalogRunnerFixture fixture = new();
        DialogCatalogState created = await fixture.Storage.CreateAsync();
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (partial) fixture.Responses.Enqueue(ModelResponse.Canceled([CatalogWriteFixture.Assistant("Частичный ответ")]));
        else fixture.Generate = _ => { caller.Cancel(); throw new OperationCanceledException(caller.Token); };
        AgentRunResult interrupted = await fixture.RunAsync(created.Token, Guid.NewGuid(), caller.Token);
        Assert.True(interrupted.TerminalSaved);
        DialogSnapshot snapshot = await fixture.Storage.SnapshotAsync(interrupted.Token!);
        Assert.Equal(DialogReadiness.Ready, snapshot.Continuation!.Readiness);
        Assert.Equal(partial ? 2 : 1, snapshot.Turns[0].Items.Count);
        Assert.Empty(fixture.Storage.Committed.Operations);
        fixture.Generate = null;
        fixture.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Assistant("Новый ответ")]));
        Assert.Equal(AgentRunStatus.Completed, (await fixture.RunAsync(snapshot.Token, Guid.NewGuid(), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(0, fixture.HandlerCalls);
    }

    /// <summary>Unknown не принимается как NotStarted и ordinary next turn/read-only не подтверждают исход.</summary>
    [Fact]
    public async Task UnknownRequiresExplicitAcknowledgementAndRejectsAlteredRecoveryId()
    {
        await using CatalogRunnerFixture fixture = new();
        DialogContinuationState pending = await UnknownAsync(fixture);
        string original = OriginalHistory(fixture.Storage);
        DialogRecoveryRequest falseNotStarted = Request(fixture.Storage, pending, acknowledge: false);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Storage.Recovery.RecoverAsync(falseNotStarted,
            TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(original, OriginalHistory(fixture.Storage));
        Assert.Empty(fixture.Storage.Committed.Operations);
        DialogRecoveryRequest proper = Request(fixture.Storage, pending, acknowledge: true);
        DialogRecoveryResult accepted = (await fixture.Storage.Recovery.RecoverAsync(proper, TestContext.Current.CancellationToken)).Data!;
        string committed = JsonSerializer.Serialize(fixture.Storage.Committed);
        DialogRecoveryResult duplicate = (await fixture.Storage.Recovery.RecoverAsync(proper, TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(accepted.Continuation.Token.Revision, duplicate.Continuation.Token.Revision);
        Assert.Equal(committed, JsonSerializer.Serialize(fixture.Storage.Committed));
        DialogRecoveryRequest altered = new(proper.Access, proper.Expected, proper.RecoveryId, proper.ExpectedRecoveryRevision,
            proper.ExpectedEpoch, proper.TurnId, proper.Resolutions.Select(item => item with { AcknowledgementReference = "different" }));
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Storage.Recovery.RecoverAsync(altered, TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(committed, JsonSerializer.Serialize(fixture.Storage.Committed));
        Assert.Equal(1, fixture.HandlerCalls);
    }

    /// <summary>Recovery failure/timeout/OCE/unknown commit не выдумывают Ready; authoritative reread различает до/после apply.</summary>
    [Theory]
    [InlineData("save", false)]
    [InlineData("cancel", false)]
    [InlineData("timeout", false)]
    [InlineData("commit", false)]
    [InlineData("commit", true)]
    public async Task RecoveryStorageFailuresPreserveFactsAndRequireAuthoritativeRead(string failure, bool applied)
    {
        await using CatalogRunnerFixture fixture = new();
        DialogContinuationState pending = await UnknownAsync(fixture);
        DialogRecoveryRequest request = Request(fixture.Storage, pending, acknowledge: true);
        string original = OriginalHistory(fixture.Storage);
        Exception primary = failure switch
        {
            "cancel" => new OperationCanceledException("save canceled"),
            "timeout" => new TimeoutException("storage timeout"),
            _ => new IOException("storage unknown")
        };
        if (failure == "commit")
        {
            if (applied) fixture.Storage.AfterCommit = () => throw primary;
            else fixture.Storage.BeforeCommit = () => throw primary;
        }
        else fixture.Storage.BeforeSave = () => throw primary;
        Exception caught = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Storage.Recovery.RecoverAsync(request, TestContext.Current.CancellationToken));
        Assert.Same(primary, caught);
        Assert.Equal(original, OriginalHistory(fixture.Storage));
        CatalogWriteFixture restarted = new(fixture.Storage.Committed);
        restarted.Reload();
        DialogContinuationState state = (await restarted.Continuation.ReadAsync(restarted.Access(pending.Token),
            TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(applied ? DialogReadiness.Ready : DialogReadiness.RecoveryRequired, state.Readiness);
        ServiceResult<DialogRecoveryResult> operation = await restarted.Recovery.ReadAsync(restarted.Access(pending.Token),
            request.RecoveryId, TestContext.Current.CancellationToken);
        Assert.Equal(applied, operation.Success);
        if (applied) Assert.Equal(DialogReadiness.Ready, operation.Data!.Continuation.Readiness);
        else Assert.Equal(ServiceErrorType.NotFound, operation.Error!.Type);
        Assert.Equal(1, fixture.HandlerCalls);
    }

    /// <summary>Два instances конкурируют через persisted root CAS; ровно один recovery commit, второй Conflict.</summary>
    [Fact]
    public async Task RecoveryRaceUsesDurableCasAcrossInstances()
    {
        await using CatalogRunnerFixture fixture = new();
        DialogContinuationState pending = await UnknownAsync(fixture);
        CatalogWriteFixture other = new(fixture.Storage.Committed);
        DialogRecoveryRequest first = Request(fixture.Storage, pending, acknowledge: true);
        DialogRecoveryRequest second = Request(other, pending, acknowledge: true);
        ServiceResult<DialogRecoveryResult>? winner = null;
        fixture.Storage.BeforeSave = () =>
        {
            winner = other.Recovery.RecoverAsync(second, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        };
        ServiceResult<DialogRecoveryResult> loser = await fixture.Storage.Recovery.RecoverAsync(first, TestContext.Current.CancellationToken);
        Assert.True(winner!.Success);
        Assert.Equal(ServiceErrorType.Conflict, loser.Error!.Type);
        Assert.Single(fixture.Storage.Committed.Operations);
        Assert.Equal(second.RecoveryId, fixture.Storage.Committed.Operations[0].Id);
        Assert.Equal(1, fixture.HandlerCalls);
    }

    /// <summary>Expired lease не доказывает остановку/NotStarted; fence сохраняет originals, старый epoch не пишет.</summary>
    [Fact]
    public async Task CrashAfterStartedNeedsFenceAndExplicitUnknownResolution()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState created = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(created.Token);
        StoredModelStep step = new(Guid.NewGuid(), ModelResponse.Completed([CatalogWriteFixture.Call("same")]));
        DialogWriteToken appended = (await fixture.Writer.AppendAsync(new DialogRunWriteAccess(fixture.Access(run.Token), run.Lease),
            run.Token, run.Lease.TurnId, step.Response.Output, [step], TestContext.Current.CancellationToken)).Data!;
        ApplicationCallContext call = new(created.Token.DialogId, CatalogWriteFixture.SCOPE.OwnerId, run.Lease.TurnId, "agent");
        await using CatalogRunnerFixture executorFixture = new(fixture);
        using IServiceScope executorScope = executorFixture.Services.CreateScope();
        IToolExecutor executor = executorScope.ServiceProvider.GetRequiredService<IToolExecutor>();
        CrashCheckpoint checkpoint = new(fixture, appended, run.Lease);
        ToolExecutionSession execution = executor.CreateSession(call, appended, null, ["action"],
            new(8, 8, 1, TimeSpan.FromSeconds(2)), checkpoint);
        await Assert.ThrowsAsync<IOException>(() => executor.ExecuteAsync(execution, step, TestContext.Current.CancellationToken));
        DialogWriteToken started = checkpoint.Token;
        Assert.Equal(0, executorFixture.HandlerCalls);
        CatalogWriteFixture restarted = new(fixture.Committed);
        restarted.Time.Now = run.Lease.DeadlineUtc;
        restarted.Reload();
        DialogContinuationState active = (await restarted.Continuation.ReadAsync(restarted.Access(started), TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(DialogReadiness.Active, active.Readiness);
        Assert.Equal(ServiceErrorType.Conflict, (await restarted.Recovery.RecoverAsync(Request(restarted, active, true),
            TestContext.Current.CancellationToken)).Error!.Type);
        DialogRunFence fence = new(restarted.Access(started), started, run.Lease, Guid.NewGuid());
        DialogContinuationState fenced = (await restarted.Lifecycle.FenceAsync(fence, TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(DialogReadiness.RecoveryRequired, fenced.Readiness);
        Assert.Equal(ToolAttemptState.Unknown, Assert.Single(fenced.Pending).State);
        Assert.Null(fenced.Terminal);
        Assert.Equal(DialogTurnStatus.InProgress, Assert.Single(restarted.Committed.Turns).Status);
        Assert.Equal(ServiceErrorType.Conflict, (await restarted.Writer.AppendAsync(new DialogRunWriteAccess(restarted.Access(fenced.Token), run.Lease),
            fenced.Token, run.Lease.TurnId, [], [], TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await restarted.Lifecycle.RenewAsync(restarted.Access(fenced.Token), run.Lease,
            TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)).Error!.Type);
        DialogRecoveryResult recovery = (await restarted.Recovery.RecoverAsync(Request(restarted, fenced, true),
            TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(DialogReadiness.Ready, recovery.Continuation.Readiness);
        Assert.Equal(DialogTurnStatus.InProgress, Assert.Single(restarted.Committed.Turns).Status);
        Assert.Equal(fenced.FencingEpoch, (await restarted.Lifecycle.FenceAsync(fence, TestContext.Current.CancellationToken)).Data!.FencingEpoch);
        DialogRunState next = (await restarted.Lifecycle.BeginAsync(new(restarted.Access(recovery.Continuation.Token), recovery.Continuation.Token,
            Guid.NewGuid(), [CatalogWriteFixture.Question("Явный вопрос")], new("gpt-5", "high", 900, 10, 1000), TimeSpan.FromMinutes(1),
            CatalogWriteFixture.SCOPE, CatalogWriteFixture.PROFILE), TestContext.Current.CancellationToken)).Data!;
        Assert.True(next.Lease.Epoch > run.Lease.Epoch);
    }

    private static async Task<DialogContinuationState> UnknownAsync(CatalogRunnerFixture fixture)
    {
        DialogCatalogState created = await fixture.Storage.CreateAsync();
        fixture.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Call("unknown")]));
        fixture.HandlerAction = (_, _) => Task.FromResult(ServiceResult<ToolOutput>.Fail(new(ServiceErrorType.Timeout, "unknown")));
        AgentRunResult result = await fixture.RunAsync(created.Token, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.True(result.TerminalSaved);
        return (await fixture.Storage.SnapshotAsync(result.Token!)).Continuation!;
    }

    /// <inheritdoc/>
    private class CrashCheckpoint(CatalogWriteFixture fixture, DialogWriteToken token, DialogRunLease lease) : IToolExecutionCheckpoint
    {
        public DialogWriteToken Token { get; private set; } = token;
        public async Task<ServiceResult> BeforeExecuteAsync(ToolExecutionIdentity identity, ToolInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            Token = (await fixture.Attempts.StartAsync(new DialogRunWriteAccess(fixture.Access(Token), lease), Token,
                identity, cancellationToken)).Data!;
            throw new IOException("crash after durable Started");
        }
    }

    private static DialogRecoveryRequest Request(CatalogWriteFixture fixture, DialogContinuationState pending, bool acknowledge) =>
        new(fixture.Access(pending.Token), pending.Token, Guid.NewGuid(), pending.RecoveryRevision, pending.FencingEpoch, pending.TurnId!.Value,
            pending.Pending.Select(item => new DialogRecoveryResolution(item.TurnId, item.StepId, item.OutputIndex,
                item.State == ToolAttemptState.Unknown && acknowledge ? DialogRecoveryKind.AcknowledgedUnknown : DialogRecoveryKind.NotStarted,
                item.State == ToolAttemptState.Unknown && acknowledge ? "owner-explicit-ack" : null)));
    private static bool IsOutput(CanonicalModelItem item) => item.Content.TryGetProperty("type", out JsonElement type) &&
        type.GetString() == "function_call_output";
    private static string OriginalHistory(CatalogWriteFixture fixture) => JsonSerializer.Serialize(new
    {
        fixture.Committed.Items, fixture.Committed.Steps, fixture.Committed.Turns, fixture.Committed.Contexts
    });
}
