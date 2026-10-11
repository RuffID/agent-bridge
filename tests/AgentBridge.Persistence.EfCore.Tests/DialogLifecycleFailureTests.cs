using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Отказы, CAS/races, cancellation и unknown storage acknowledgement actual public API.</summary>
public class DialogLifecycleFailureTests
{
    /// <summary>Два instances не могут принять один root token/Ready в Begin; проигравший не оставляет input/projection.</summary>
    [Fact]
    public async Task TwoInstancesCompeteThroughPersistedBeginCas()
    {
        CatalogWriteFixture first = new();
        DialogCatalogState empty = await first.CreateAsync();
        CatalogWriteFixture second = new(first.Committed);
        DialogRunState? winner = null;
        first.BeforeSave = () => winner = second.BeginAsync(empty.Token, question: "Вопрос победителя").GetAwaiter().GetResult();
        ServiceResult<DialogRunState> loser = await first.Lifecycle.BeginAsync(new(first.Access(empty.Token), empty.Token, Guid.NewGuid(),
            [CatalogWriteFixture.Question("Вопрос проигравшего")], new("gpt-5", "high", 900, 10, 1000), TimeSpan.FromMinutes(1),
            CatalogWriteFixture.SCOPE, CatalogWriteFixture.PROFILE), TestContext.Current.CancellationToken);
        Assert.NotNull(winner);
        Assert.Equal(ServiceErrorType.Conflict, loser.Error!.Type);
        Assert.Single(first.Committed.Turns);
        Assert.Equal("Вопрос победителя", Assert.Single(first.Committed.Catalog).Title);
        Assert.Single(first.Committed.Items);
        Assert.Equal(2, first.Committed.Changes.Count);
    }

    /// <summary>Full profile и agent/site gate выполняются перед чтением canonical children.</summary>
    [Fact]
    public async Task ChangedProfileOrNamespaceCannotReadHistoryOrStartRun()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState state = await fixture.CreateAsync();
        CatalogAccessProfile revoked = new("profile", 1, JsonSerializer.SerializeToElement(new { permission = "none", consent = true }));
        fixture.Reload();
        int reads = fixture.CanonicalReads;
        Assert.Equal(ServiceErrorType.Forbidden, (await fixture.Reader.ReadCatalogAsync(new(fixture.Access(state.Token),
            CatalogWriteFixture.SCOPE, revoked), TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(reads, fixture.CanonicalReads);
        Assert.Equal(ServiceErrorType.Forbidden, (await fixture.Reader.ReadCatalogAsync(new(fixture.Access(state.Token),
            CatalogWriteFixture.SCOPE with { AgentId = "other" }, CatalogWriteFixture.PROFILE), TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.Forbidden, (await fixture.Reader.ReadAsync(fixture.Access(state.Token),
            TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.Forbidden, (await fixture.Lifecycle.BeginAsync(new(fixture.Access(state.Token), state.Token, Guid.NewGuid(),
            [CatalogWriteFixture.Question("Denied")], new("gpt-5", "high", 900, 10, 1000), TimeSpan.FromMinutes(1),
            CatalogWriteFixture.SCOPE, revoked), TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Empty(fixture.Committed.Turns);
        Assert.Single(fixture.Committed.Changes);
    }

    /// <summary>Legacy Begin/Append/Finish не обходят registered root lifecycle даже с актуальным token.</summary>
    [Fact]
    public async Task LegacyWritesCannotBypassRegisteredFencing()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState created = await fixture.CreateAsync();
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.BeginAsync(fixture.Access(created.Token), created.Token,
            Guid.NewGuid(), [], TestContext.Current.CancellationToken)).Error!.Type);
        DialogRunState run = await fixture.BeginAsync(created.Token);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.AppendAsync(fixture.Access(run.Token), run.Token,
            run.Lease.TurnId, [], [], TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.FinishAsync(fixture.Access(run.Token), run.Token,
            run.Lease.TurnId, DialogTurnStatus.Completed, [], [], TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.AppendAsync(new DialogRunWriteAccess(fixture.Access(run.Token), run.Lease),
            run.Token, Guid.NewGuid(), [], [], TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Lease renew меняет только persisted deadline, не retention/history revision; expiry не делает Ready.</summary>
    [Fact]
    public async Task RenewalPreservesRetentionAndRevisionAndExpiredLeaseStaysActive()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState created = await fixture.CreateAsync(expires: CatalogWriteFixture.NOW.AddHours(1));
        DialogRunState run = await fixture.BeginAsync(created.Token);
        fixture.Time.Now = CatalogWriteFixture.NOW.AddSeconds(10);
        DialogRunLease renewed = (await fixture.Lifecycle.RenewAsync(fixture.Access(run.Token), run.Lease, TimeSpan.FromMinutes(2),
            TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(run.Token.Revision, fixture.Committed.Roots[0].Revision);
        Assert.Equal(created.ExpiresAtUtc, fixture.Committed.Catalog[0].ExpiresAtUtc);
        Assert.Equal(fixture.Time.Now.AddMinutes(2), renewed.DeadlineUtc);
        fixture.Time.Now = renewed.DeadlineUtc;
        fixture.Reload();
        Assert.Equal(DialogReadiness.Active, (await fixture.Continuation.ReadAsync(fixture.Access(run.Token),
            TestContext.Current.CancellationToken)).Data!.Readiness);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Lifecycle.RenewAsync(fixture.Access(run.Token), renewed,
            TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>До transaction cancellation ничего не меняет; после confirmed commit accepted факт возвращается.</summary>
    [Theory]
    [InlineData("before")]
    [InlineData("save")]
    [InlineData("after-commit")]
    public async Task CancellationDoesNotPretendCommittedBeginWasRolledBack(string point)
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState created = await fixture.CreateAsync();
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (point == "before") caller.Cancel();
        if (point == "save") fixture.BeforeSave = caller.Cancel;
        if (point == "after-commit") fixture.AfterCommit = caller.Cancel;
        DialogRunBegin begin = new(fixture.Access(created.Token), created.Token, Guid.NewGuid(),
            [CatalogWriteFixture.Question("Вопрос")], new("gpt-5", "high", 900, 10, 1000), TimeSpan.FromMinutes(1),
            CatalogWriteFixture.SCOPE, CatalogWriteFixture.PROFILE);
        if (point == "after-commit")
        {
            ServiceResult<DialogRunState> accepted = await fixture.Lifecycle.BeginAsync(begin, caller.Token);
            Assert.True(accepted.Success);
            Assert.Single(fixture.Committed.Turns);
            Assert.Equal(2, fixture.Committed.Changes.Count);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Lifecycle.BeginAsync(begin, caller.Token));
            Assert.Empty(fixture.Committed.Turns);
            Assert.Empty(fixture.Committed.Items);
            Assert.Single(fixture.Committed.Changes);
        }
    }

    /// <summary>Failed/unknown finalization требует durable read/fence; terminal/readiness не выдумываются.</summary>
    [Theory]
    [InlineData("save", false)]
    [InlineData("commit", false)]
    [InlineData("commit", true)]
    public async Task FinalizationFailureKeepsTruthfulLifecycle(string point, bool applied)
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState created = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(created.Token);
        IOException primary = new("finalization unknown");
        if (point == "save") fixture.BeforeSave = () => throw primary;
        else if (applied) fixture.AfterCommit = () => throw primary;
        else fixture.BeforeCommit = () => throw primary;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => fixture.Lifecycle.FinalizeAsync(new(fixture.Access(run.Token),
            run.Token, run.Lease, DialogTurnStatus.Canceled), TestContext.Current.CancellationToken)));
        CatalogWriteFixture restarted = new(fixture.Committed);
        restarted.Reload();
        DialogContinuationState state = (await restarted.Continuation.ReadAsync(restarted.Access(run.Token),
            TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(applied ? DialogReadiness.Ready : DialogReadiness.Active, state.Readiness);
        Assert.Equal(applied ? DialogTurnStatus.Canceled : DialogTurnStatus.InProgress, restarted.Committed.Turns[0].Status);
        Assert.Equal(applied, state.Finalized);
    }

    /// <summary>Unknown outcomes-save после action не повторяет handler; fence conservatively сохраняет Unknown до ack.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutcomesUnknownCommitNeverReplaysHandler(bool applied)
    {
        await using CatalogRunnerFixture runner = new();
        CatalogWriteFixture fixture = runner.Storage;
        DialogCatalogState created = await fixture.CreateAsync();
        runner.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Call("x")]));
        IOException primary = new("outcome unknown");
        int commits = 0;
        fixture.BeforeCommit = () =>
        {
            if (++commits == 4 && !applied) throw primary; // Begin, Append, Started, Outcomes.
        };
        fixture.AfterCommit = () => { if (commits == 4 && applied) throw primary; };
        await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(created.Token, Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal(1, runner.HandlerCalls);
        CatalogWriteFixture restarted = new(fixture.Committed);
        restarted.Reload();
        DialogContinuationState active = (await restarted.Continuation.ReadAsync(restarted.Access(created.Token),
            TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(DialogReadiness.Active, active.Readiness);
        restarted.Time.Now = active.Lease!.DeadlineUtc;
        DialogContinuationState fenced = (await restarted.Lifecycle.FenceAsync(new(restarted.Access(active.Token), active.Token,
            active.Lease, Guid.NewGuid()), TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(applied ? DialogReadiness.Ready : DialogReadiness.RecoveryRequired, fenced.Readiness);
        if (!applied) Assert.Equal(ToolAttemptState.Unknown, Assert.Single(fenced.Pending).State);
        Assert.Equal(applied ? 1 : 0, restarted.Committed.Items.Count(item => item.ContentJson.Contains("function_call_output", StringComparison.Ordinal)));
        Assert.Equal(1, runner.HandlerCalls);
    }

    /// <summary>ContextBuilder не разрешает произвольный model step по read snapshot активного run даже с тем же TurnId.</summary>
    [Fact]
    public async Task ContextBuilderRequiresOwnedRunOrDurableReady()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState created = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(created.Token);
        DialogSnapshot active = await fixture.SnapshotAsync(run.Token);
        ContextBuilder builder = new([]);
        ServiceResult<ModelRequest> result = await builder.BuildAsync(new(created.Token.DialogId, CatalogWriteFixture.SCOPE.OwnerId,
            run.Lease.TurnId, "agent"), active, new("gpt-5", "high", "instructions", [], []),
            CatalogWriteFixture.NOW, TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
    }

    /// <summary>Pending count overflow отклоняется до canonical/projection commit, без скрытой обрезки.</summary>
    [Fact]
    public async Task PendingOverflowRollsBackEntireAppend()
    {
        CatalogWriteFixture fixture = new();
        DialogCatalogState created = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(created.Token);
        StoredModelStep oversized = new(Guid.NewGuid(), ModelResponse.Completed(Enumerable.Range(0, 1025).Select(index =>
            CatalogWriteFixture.Call("call-" + index))));
        string before = JsonSerializer.Serialize(fixture.Committed);
        ServiceResult<DialogWriteToken> result = await fixture.Writer.AppendAsync(new DialogRunWriteAccess(fixture.Access(run.Token), run.Lease),
            run.Token, run.Lease.TurnId, oversized.Response.Output, [oversized], TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Committed));
    }
}
