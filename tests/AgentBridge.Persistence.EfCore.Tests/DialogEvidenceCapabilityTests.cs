using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Trusted evidence, quiescence и повторный CAS actual public API без БД или внешнего I/O.</summary>
public class DialogEvidenceCapabilityTests
{
    /// <summary>Подтверждённый внешний output копируется неизменным только в recovery; duplicates не повторяют resolver.</summary>
    [Theory]
    [InlineData(DialogRecoveryKind.KnownCanceled)]
    [InlineData(DialogRecoveryKind.ConfirmedExternalOutcome)]
    public async Task TrustedEvidencePreservesOriginalsAndIdempotency(DialogRecoveryKind kind)
    {
        Evidence resolver = new();
        CatalogWriteFixture fixture = new(evidenceResolver: resolver);
        DialogContinuationState pending = await UnknownAsync(fixture);
        string original = History(fixture);
        DialogRecoveryRequest request = Request(fixture, pending, kind);
        string external = "{\"type\":\"function_call_output\",\"call_id\":\"unknown\",\"output\":\" confirmed external result \",\"extra\":42}";
        resolver.Resolve = async (evidence, ct) =>
        {
            Assert.Equal("clear", fixture.Session.Events.Last());
            fixture.Reload();
            Assert.Equal(DialogReadiness.RecoveryRequired, (await fixture.Continuation.ReadAsync(fixture.Access(pending.Token), ct)).Data!.Readiness);
            Assert.Same(request, evidence.Request);
            using JsonDocument document = JsonDocument.Parse(external);
            return ServiceResult<DialogRecoveryEvidence>.Ok(new(kind, evidence.Resolution.AcknowledgementReference!,
                kind == DialogRecoveryKind.ConfirmedExternalOutcome ? new(document.RootElement) : null));
        };

        DialogRecoveryResult accepted = (await fixture.Recovery.RecoverAsync(request, TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(DialogReadiness.Ready, accepted.Continuation.Readiness);
        DialogRecoveryRecord record = Assert.Single(accepted.Records);
        Assert.Equal(kind, record.Kind);
        Assert.Equal("trusted-evidence", record.EvidenceReference);
        if (kind == DialogRecoveryKind.ConfirmedExternalOutcome) Assert.Equal(external, record.Output.Content.GetRawText());
        else Assert.Contains("tool_canceled", record.Output.Content.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(original, History(fixture));
        DialogSnapshot effective = await fixture.SnapshotAsync(accepted.Continuation.Token);
        Assert.Contains(effective.EffectiveTurns[0].Items, item => item.Content.GetRawText() == record.Output.Content.GetRawText());
        Assert.Single(effective.Turns[0].Items, item => item.Content.GetProperty("type").GetString() == "function_call");
        Assert.Equal(1, resolver.Calls);

        string committed = JsonSerializer.Serialize(fixture.Committed);
        Assert.True((await fixture.Recovery.RecoverAsync(request, TestContext.Current.CancellationToken)).Success);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(committed, JsonSerializer.Serialize(fixture.Committed));
        DialogRecoveryRequest altered = new(request.Access, request.Expected, request.RecoveryId, request.ExpectedRecoveryRevision,
            request.ExpectedEpoch, request.TurnId, request.Resolutions.Select(item => item with { AcknowledgementReference = "other" }));
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Recovery.RecoverAsync(altered, TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(1, resolver.Calls);
    }

    /// <summary>Evidence отказ/OCE/ошибка/mismatch не открывают continuation и не сохраняют частичный batch.</summary>
    [Theory]
    [InlineData("refusal")]
    [InlineData("exception")]
    [InlineData("cancel")]
    [InlineData("reference")]
    [InlineData("wrong-call")]
    [InlineData("oversize")]
    public async Task EvidenceFailuresLeaveRecoveryRequired(string failure)
    {
        Evidence resolver = new();
        CatalogWriteFixture fixture = new(evidenceResolver: resolver);
        DialogContinuationState pending = await UnknownAsync(fixture, mixed: true);
        DialogRecoveryRequest request = Request(fixture, pending, DialogRecoveryKind.ConfirmedExternalOutcome);
        string before = JsonSerializer.Serialize(fixture.Committed);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IOException primary = new("evidence unavailable");
        resolver.Resolve = (evidence, ct) =>
        {
            if (failure == "exception") throw primary;
            if (failure == "cancel") { caller.Cancel(); throw new OperationCanceledException(ct); }
            if (failure == "refusal") return Task.FromResult(ServiceResult<DialogRecoveryEvidence>.Fail(new(ServiceErrorType.Forbidden, "evidence_refused")));
            CanonicalModelItem output = new(JsonSerializer.SerializeToElement(new
            {
                type = "function_call_output", call_id = failure == "wrong-call" ? "different" : "unknown",
                output = failure == "oversize" ? new string('x', 1048577) : "actual output"
            }));
            return Task.FromResult(ServiceResult<DialogRecoveryEvidence>.Ok(new(evidence.Resolution.Kind,
                failure == "reference" ? "wrong" : "trusted-evidence", output)));
        };

        if (failure == "exception") Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => fixture.Recovery.RecoverAsync(request, caller.Token)));
        else if (failure == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Recovery.RecoverAsync(request, caller.Token));
        else
        {
            ServiceResult<DialogRecoveryResult> result = await fixture.Recovery.RecoverAsync(request, caller.Token);
            Assert.False(result.Success);
            Assert.Equal(failure == "refusal" ? ServiceErrorType.Forbidden : failure == "reference" ? ServiceErrorType.Conflict :
                ServiceErrorType.Validation, result.Error!.Type);
        }

        Assert.Equal(before, JsonSerializer.Serialize(fixture.Committed));
        CatalogWriteFixture fresh = new(fixture.Committed);
        fresh.Reload();
        Assert.Equal(DialogReadiness.RecoveryRequired, (await fresh.Continuation.ReadAsync(fresh.Access(pending.Token),
            TestContext.Current.CancellationToken)).Data!.Readiness);
        Assert.Empty(fixture.Committed.Operations);
    }

    /// <summary>Recovery/Delete winner во время awaited evidence отклоняет поздний результат CAS, без второй записи.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvidenceCannotCommitAfterConcurrentRecoveryOrDelete(bool delete)
    {
        Evidence resolver = new();
        CatalogWriteFixture fixture = new(evidenceResolver: resolver);
        DialogContinuationState pending = await UnknownAsync(fixture);
        CatalogWriteFixture other = new(fixture.Committed);
        resolver.Resolve = async (evidence, ct) =>
        {
            if (delete) Assert.True((await other.Deletion.DeleteAsync(other.Access(pending.Token), pending.Token, ct)).Success);
            else
            {
                DialogRecoveryRequest winner = Request(other, pending, DialogRecoveryKind.AcknowledgedUnknown);
                Assert.True((await other.Recovery.RecoverAsync(winner, ct)).Success);
            }

            return ServiceResult<DialogRecoveryEvidence>.Ok(new(DialogRecoveryKind.KnownCanceled, "trusted-evidence", null));
        };

        ServiceResult<DialogRecoveryResult> loser = await fixture.Recovery.RecoverAsync(Request(fixture, pending,
            DialogRecoveryKind.KnownCanceled), TestContext.Current.CancellationToken);
        Assert.Equal(delete ? ServiceErrorType.NotFound : ServiceErrorType.Conflict, loser.Error!.Type);
        Assert.Equal(delete ? 0 : 1, fixture.Committed.Operations.Count);
        Assert.Equal(1, resolver.Calls);
    }

    /// <summary>Неизвестный outcome не принимается без resolver; live/stale recovery отклоняется до evidence I/O.</summary>
    [Fact]
    public async Task MissingResolverAndActiveRunCannotInventEvidence()
    {
        CatalogWriteFixture missing = new();
        DialogContinuationState pending = await UnknownAsync(missing);
        Assert.Equal(ServiceErrorType.Unsupported, (await missing.Recovery.RecoverAsync(Request(missing, pending,
            DialogRecoveryKind.KnownCanceled), TestContext.Current.CancellationToken)).Error!.Type);

        Evidence resolver = new();
        CatalogWriteFixture fixture = new(evidenceResolver: resolver);
        DialogCatalogState created = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(created.Token);
        DialogRecoveryRequest active = new(fixture.Access(run.Token), run.Token, Guid.NewGuid(), 0, run.Lease.Epoch,
            run.Lease.TurnId, [new(run.Lease.TurnId, Guid.NewGuid(), 0, DialogRecoveryKind.KnownCanceled, "trusted-evidence")]);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Recovery.RecoverAsync(active, TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(0, resolver.Calls);
        Assert.Empty(fixture.Committed.Operations);
    }

    /// <summary>ConfirmedQuiescence до deadline требует host verifier; fence не устанавливает outcome.</summary>
    [Fact]
    public async Task QuiescenceFenceIsVerifiedOutsideTransactionAndIdempotent()
    {
        Quiescence verifier = new();
        CatalogWriteFixture fixture = new(quiescenceVerifier: verifier);
        DialogCatalogState created = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(created.Token);
        DialogRunFence fence = new(fixture.Access(run.Token), run.Token, run.Lease, Guid.NewGuid(),
            DialogRunFenceReason.ConfirmedQuiescence, "executor-stopped");
        verifier.Verify = (_, _) =>
        {
            Assert.Equal("clear", fixture.Session.Events.Last());
            return Task.FromResult(ServiceResult.Ok());
        };

        DialogContinuationState result = (await fixture.Lifecycle.FenceAsync(fence, TestContext.Current.CancellationToken)).Data!;
        Assert.Equal(run.Lease.Epoch + 1, result.FencingEpoch);
        Assert.Equal(DialogReadiness.Ready, result.Readiness);
        Assert.Null(result.Terminal);
        Assert.Equal(DialogTurnStatus.InProgress, fixture.Committed.Turns[0].Status);
        Assert.True((await fixture.Lifecycle.FenceAsync(fence, TestContext.Current.CancellationToken)).Success);
        Assert.Equal(1, verifier.Calls);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Lifecycle.FenceAsync(fence with { EvidenceReference = "different" },
            TestContext.Current.CancellationToken)).Error!.Type);
        Assert.Equal(1, verifier.Calls);
        Assert.Equal(ServiceErrorType.Conflict, (await fixture.Writer.AppendAsync(new DialogRunWriteAccess(fixture.Access(result.Token), run.Lease),
            result.Token, run.Lease.TurnId, [], [], TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Отказ host verifier и race с renew не позволяют старому evidence повысить epoch.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("refusal")]
    [InlineData("race")]
    public async Task QuiescenceRefusalOrConcurrentFencePreservesActualState(string point)
    {
        Quiescence verifier = new();
        CatalogWriteFixture fixture = new(quiescenceVerifier: point == "missing" ? null : verifier);
        DialogCatalogState created = await fixture.CreateAsync();
        DialogRunState run = await fixture.BeginAsync(created.Token);
        DialogRunFence request = new(fixture.Access(run.Token), run.Token, run.Lease, Guid.NewGuid(),
            DialogRunFenceReason.ConfirmedQuiescence, "executor-stopped");
        verifier.Verify = async (_, ct) =>
        {
            if (point == "refusal") return ServiceResult.Fail(new(ServiceErrorType.Forbidden, "not_proven"));
            CatalogWriteFixture other = new(fixture.Committed);
            other.Time.Now = run.Lease.DeadlineUtc;
            Assert.True((await other.Lifecycle.FenceAsync(new(other.Access(run.Token), run.Token, run.Lease, Guid.NewGuid()), ct)).Success);
            return ServiceResult.Ok();
        };

        ServiceResult<DialogContinuationState> result = await fixture.Lifecycle.FenceAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(point == "missing" ? ServiceErrorType.Unsupported : point == "refusal" ? ServiceErrorType.Forbidden :
            ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(point == "race" ? 1 : 0, fixture.Committed.Operations.Count);
        Assert.DoesNotContain(fixture.Committed.Operations, operation => operation.Id == request.OperationId);
    }

    /// <summary>Builder не использует compact старой recovery revision и принимает effective pairs только после Ready.</summary>
    [Fact]
    public async Task ContextBuilderInvalidatesOldCompactAfterRecovery()
    {
        CatalogWriteFixture fixture = new();
        DialogContinuationState pending = await UnknownAsync(fixture);
        ContextBuilder builder = new([]);
        ApplicationCallContext call = new(pending.Token.DialogId, CatalogWriteFixture.SCOPE.OwnerId, Guid.NewGuid(), "agent");
        ModelRequest input = new("gpt-5", "high", "instructions", [], []);
        DialogSnapshot blocked = await fixture.SnapshotAsync(pending.Token);
        Assert.Equal(ServiceErrorType.Conflict, (await builder.BuildAsync(call, blocked, input, CatalogWriteFixture.NOW,
            TestContext.Current.CancellationToken)).Error!.Type);

        DialogRecoveryResult recovered = (await fixture.Recovery.RecoverAsync(Request(fixture, pending,
            DialogRecoveryKind.AcknowledgedUnknown), TestContext.Current.CancellationToken)).Data!;
        DialogSnapshot ready = await fixture.SnapshotAsync(recovered.Continuation.Token);
        StoredDialogContext obsolete = new(1, 1, ModelResponse.Completed([CatalogWriteFixture.Assistant("obsolete compact")]), "gpt-5", 0);
        DialogSnapshot staleWindow = new(ready.Token, ready.OwnerId, ready.CreatedAtUtc, ready.ExpiresAtUtc, ready.ContentBytes,
            ready.Turns, obsolete, ready.Selection, ready.Catalog, ready.Continuation, ready.EffectiveTurns);
        Assert.Null(staleWindow.EffectiveContext);
        ServiceResult<ModelRequest> built = await builder.BuildAsync(call, staleWindow, input, CatalogWriteFixture.NOW,
            TestContext.Current.CancellationToken);
        Assert.True(built.Success, built.Error?.Message);
        Assert.Contains(built.Data!.Input, item => item.Content.GetRawText().Contains("tool_outcome_unknown", StringComparison.Ordinal));
        Assert.DoesNotContain(built.Data.Input, item => item.Content.GetRawText().Contains("obsolete compact", StringComparison.Ordinal));
        Assert.Same(obsolete, staleWindow.ActiveContext);
        Assert.Equal(blocked.Turns[0].Status, ready.Turns[0].Status);
    }

    private static async Task<DialogContinuationState> UnknownAsync(CatalogWriteFixture fixture, bool mixed = false)
    {
        await using CatalogRunnerFixture runner = new(fixture);
        DialogCatalogState created = await fixture.CreateAsync();
        runner.Responses.Enqueue(ModelResponse.Completed(mixed ? [CatalogWriteFixture.Call("unknown"), CatalogWriteFixture.Call("not-started")] :
            [CatalogWriteFixture.Call("unknown")]));
        runner.HandlerAction = (_, _) => Task.FromResult(ServiceResult<ToolOutput>.Fail(new(ServiceErrorType.Timeout, "unknown")));
        AgentRunResult result = await runner.RunAsync(created.Token, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.True(result.TerminalSaved);
        Assert.Equal(1, runner.HandlerCalls);
        return (await fixture.SnapshotAsync(result.Token!)).Continuation!;
    }

    private static DialogRecoveryRequest Request(CatalogWriteFixture fixture, DialogContinuationState pending, DialogRecoveryKind kind) =>
        new(fixture.Access(pending.Token), pending.Token, Guid.NewGuid(), pending.RecoveryRevision, pending.FencingEpoch, pending.TurnId!.Value,
            pending.Pending.Select(item => new DialogRecoveryResolution(item.TurnId, item.StepId, item.OutputIndex,
                item.State == ToolAttemptState.Unknown ? kind : DialogRecoveryKind.NotStarted,
                item.State == ToolAttemptState.Unknown ? "trusted-evidence" : null)));

    private static string History(CatalogWriteFixture fixture) => JsonSerializer.Serialize(new
    {
        fixture.Committed.Items, fixture.Committed.Steps, fixture.Committed.Turns, fixture.Committed.Contexts
    });

    /// <inheritdoc/>
    private class Evidence : IDialogRecoveryEvidenceResolver
    {
        public int Calls { get; private set; }
        public Func<DialogRecoveryEvidenceRequest, CancellationToken, Task<ServiceResult<DialogRecoveryEvidence>>> Resolve { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected evidence I/O.");
        public Task<ServiceResult<DialogRecoveryEvidence>> ResolveAsync(DialogRecoveryEvidenceRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Resolve(request, cancellationToken);
        }
    }

    /// <inheritdoc/>
    private class Quiescence : IDialogRunQuiescenceVerifier
    {
        public int Calls { get; private set; }
        public Func<DialogRunFence, CancellationToken, Task<ServiceResult>> Verify { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected quiescence I/O.");
        public Task<ServiceResult> VerifyAsync(DialogRunFence request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Verify(request, cancellationToken);
        }
    }
}
