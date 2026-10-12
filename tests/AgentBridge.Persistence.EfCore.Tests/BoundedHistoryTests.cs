using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual reader/runner/lifecycle/recovery/write paths над bounded base I/O doubles; без SQL/модели/host.</summary>
public class BoundedHistoryTests
{
    /// <summary>Полная история многих малых turns сохраняется при limit; limit+1 не выдаёт усечённую историю.</summary>
    [Theory]
    [InlineData(44, true)]
    [InlineData(43, false)]
    public async Task ManySmallTurnsShareOperationCountBoundary(int maxRecords, bool success)
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        for (int number = 0; number < 20; number++)
        {
            DialogRunState begun = await seed.BeginAsync(token);
            ServiceResult<DialogContinuationState> finished = await seed.Lifecycle.FinalizeAsync(new(seed.Access(begun.Token), begun.Token,
                begun.Lease, DialogTurnStatus.Completed), TestContext.Current.CancellationToken);
            Assert.True(finished.Success, finished.Error?.Message);
            token = finished.Data!.Token;
        }

        string before = JsonSerializer.Serialize(seed.Committed);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(maxRecords, 65536, 32));
        storage.Reload();
        ServiceResult<DialogSnapshot> result = await storage.Reader.ReadCatalogAsync(storage.FullAccess(token), TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        if (success)
        {
            Assert.Equal(20, result.Data!.Turns.Count);
            Assert.All(result.Data.Turns, turn => Assert.Single(turn.Items));
        }
        else
        {
            AssertOverflow(result);
        }

        Assert.Equal(before, JsonSerializer.Serialize(seed.Committed));
        Assert.Equal(20, storage.TurnRows.HistoryQuery.PayloadRows);
        Assert.Equal(20, storage.ItemRows.HistoryQuery.PayloadRows);
    }

    /// <summary>Крупный item/report/compact/provenance/metadata/root отклоняется scalar preflight до полного payload/JSON parsing.</summary>
    [Theory]
    [InlineData("item")]
    [InlineData("report")]
    [InlineData("attempts")]
    [InlineData("compact")]
    [InlineData("recovery-pairs")]
    [InlineData("recovery-result")]
    [InlineData("root")]
    [InlineData("profile")]
    public async Task OversizedColumnsFailBeforeTheirPayloadIsLoaded(string area)
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        DialogRunState begun = await seed.BeginAsync(token);
        string oversized = new('Ж', 100000);
        switch (area)
        {
            case "item":
                seed.Committed.Items[0].ContentJson = oversized;
                break;
            case "report":
            case "attempts":
                seed.Committed.Steps.Add(new()
                {
                    DialogId = token.DialogId.Value, TurnId = begun.Lease.TurnId, Id = Guid.NewGuid(), Sequence = 1,
                    Response = new() { EnvelopeJson = area == "report" ? oversized : "{}" },
                    ToolAttemptsJson = area == "attempts" ? oversized : null
                });
                break;
            case "compact":
                seed.Committed.Contexts.Add(new()
                {
                    DialogId = token.DialogId.Value, Version = 1, Compaction = new() { EnvelopeJson = oversized }
                });
                break;
            case "recovery-pairs":
            case "recovery-result":
                seed.Committed.Operations.Add(new()
                {
                    DialogId = token.DialogId.Value, Id = Guid.NewGuid(), Kind = "recovery", Revision = 1,
                    PairsJson = area == "recovery-pairs" ? oversized : "[]",
                    ResultJson = area == "recovery-result" ? oversized : "{}"
                });
                break;
            case "root":
                seed.Committed.Roots[0].RuntimeJson = oversized;
                break;
            case "profile":
                seed.Committed.Catalog[0].ProfileJson = oversized;
                break;
        }

        string before = JsonSerializer.Serialize(seed.Committed);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(100, 16384, 32));
        storage.Reload();
        AssertOverflow(await storage.Reader.ReadCatalogAsync(storage.FullAccess(token), TestContext.Current.CancellationToken));
        Assert.Equal(before, JsonSerializer.Serialize(seed.Committed));
        int areaPayload = area switch
        {
            "item" => storage.ItemRows.HistoryQuery.PayloadRows,
            "report" or "attempts" => storage.StepRows.HistoryQuery.PayloadRows,
            "compact" => storage.ContextRows.HistoryQuery.PayloadRows,
            "recovery-pairs" or "recovery-result" => storage.OperationRows.HistoryQuery.PayloadRows,
            "root" => storage.RootRows.HistoryQuery.PayloadRows,
            _ => storage.CatalogQueries.HistoryQuery.PayloadRows
        };
        Assert.Equal(0, areaPayload);
        Assert.DoesNotContain("save", storage.Session.Events);
    }

    /// <summary>Byte boundary настоящего stored item; reader не проверяет размер только после full snapshot.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualReaderByteBoundaryIsInclusive(bool boundary)
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        await seed.BeginAsync(token, question: new string('a', 3000));
        long size = FakeHistoryQueryRepository<CanonicalItemRecord>.Size(seed.Committed.Items[0]);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(100, boundary ? size : size - 1, 32));
        storage.Reload();
        ServiceResult<DialogSnapshot> result = await storage.Reader.ReadCatalogAsync(storage.FullAccess(token), TestContext.Current.CancellationToken);
        Assert.Equal(boundary, result.Success);
        if (boundary)
        {
            Assert.Equal(seed.Committed.Items[0].ContentJson, Assert.Single(Assert.Single(result.Data!.Turns).Items).Content.GetRawText());
        }
        else
        {
            AssertOverflow(result);
            Assert.Empty(storage.ItemRows.HistoryQuery.PayloadQueries);
        }
    }

    /// <summary>Авторизация/immutable profile/scope/expiry/tombstone выполняются до canonical materialization.</summary>
    [Theory]
    [InlineData("owner", ServiceErrorType.Forbidden)]
    [InlineData("site", ServiceErrorType.Forbidden)]
    [InlineData("agent", ServiceErrorType.Forbidden)]
    [InlineData("profile", ServiceErrorType.Forbidden)]
    [InlineData("expired", ServiceErrorType.Expired)]
    [InlineData("tombstone", ServiceErrorType.Conflict)]
    public async Task GateRejectsBeforeHistory(string denial, ServiceErrorType expected)
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        await seed.BeginAsync(token);
        if (denial == "expired")
        {
            seed.Committed.Roots[0].ExpiresAtUtc = CatalogWriteFixture.NOW;
            seed.Committed.Catalog[0].ExpiresAtUtc = CatalogWriteFixture.NOW;
        }

        if (denial == "tombstone")
        {
            seed.Committed.Catalog[0].Deleted = true;
        }

        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(100, 65536, 32));
        storage.Reload();
        DialogOwnerId owner = denial == "owner" ? DialogOwnerId.From("other") : CatalogWriteFixture.SCOPE.OwnerId;
        DialogCatalogScope scope = new(owner, denial == "site" ? "other" : "site", denial == "agent" ? "other" : "agent");
        CatalogAccessProfile profile = denial == "profile" ? new("profile", 1, JsonSerializer.SerializeToElement(new { other = true })) : CatalogWriteFixture.PROFILE;
        ServiceResult<DialogSnapshot> result = await storage.Reader.ReadCatalogAsync(new(new(token.DialogId, owner, CatalogWriteFixture.NOW), scope, profile),
            TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(expected, result.Error!.Type);
        Assert.Empty(storage.TurnRows.HistoryQuery.ScalarQueries);
        Assert.Empty(storage.ItemRows.HistoryQuery.ScalarQueries);
        Assert.Empty(storage.OperationRows.HistoryQuery.ScalarQueries);
    }

    /// <summary>Root/profile/delete/expiry/CAS изменения не раскрывают snapshot; late recheck не делает retry/refill.</summary>
    [Theory]
    [InlineData("revision", ServiceErrorType.Conflict)]
    [InlineData("incarnation", ServiceErrorType.Conflict)]
    [InlineData("runtime", ServiceErrorType.Conflict)]
    [InlineData("delete", ServiceErrorType.NotFound)]
    [InlineData("profile", ServiceErrorType.Forbidden)]
    [InlineData("tombstone", ServiceErrorType.Conflict)]
    [InlineData("expiry", ServiceErrorType.Expired)]
    public async Task FinalGateRejectsChangedState(string change, ServiceErrorType expected)
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        await seed.BeginAsync(token);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(100, 65536, 32));
        storage.Reload();
        storage.ContextRows.HistoryQuery.BeforeExecute = scalar =>
        {
            if (scalar)
            {
                switch (change)
                {
                    case "revision":
                        storage.RootRows.Records[0].Revision++;
                        break;
                    case "incarnation":
                        storage.RootRows.Records[0].IncarnationId = Guid.NewGuid();
                        break;
                    case "runtime":
                        storage.RootRows.Records[0].RuntimeJson = "{}";
                        break;
                    case "delete":
                        storage.RootRows.Records.Clear();
                        break;
                    case "profile":
                        storage.CatalogQueries.Records[0].ProfileJson = JsonSerializer.Serialize(new { Key = "other", Version = 1, Data = new { } });
                        break;
                    case "tombstone":
                        storage.CatalogQueries.Records[0].Deleted = true;
                        break;
                    case "expiry":
                        storage.CatalogQueries.Records[0].ExpiresAtUtc = CatalogWriteFixture.NOW;
                        break;
                }
            }
        };
        ServiceResult<DialogSnapshot> result = await storage.Reader.ReadCatalogAsync(storage.FullAccess(token), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(expected, result.Error!.Type);
        Assert.Equal(2, storage.RootRows.HistoryQuery.ScalarQueries.Count);
        Assert.Single(storage.ItemRows.HistoryQuery.PayloadQueries);
    }

    /// <summary>Actual bounded runner не достигает provider/model при overflow начальной истории; canonical не меняется.</summary>
    [Fact]
    public async Task RunnerRejectsOversizedHistoryBeforeProvidersOrModel()
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        seed.Committed.Roots[0].RuntimeJson = new string(' ', 100000);
        string before = JsonSerializer.Serialize(seed.Committed);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(100, 16384, 32));
        await using CatalogRunnerFixture runner = new(storage);
        AgentRunResult result = await runner.RunAsync(token, Guid.NewGuid(), TestContext.Current.CancellationToken, bounded: true);
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal("history_load_limit_exceeded", result.Error!.Message);
        Assert.False(result.TerminalSaved);
        Assert.Equal(0, runner.ProviderCalls);
        Assert.Equal(0, runner.HandlerCalls);
        Assert.Empty(runner.Requests);
        Assert.Equal(before, JsonSerializer.Serialize(seed.Committed));
    }

    /// <summary>Strict runner отказывает без явной policy до первого reader I/O, providers и model.</summary>
    [Fact]
    public async Task StrictRunnerRequiresPolicyBeforeHistoryIo()
    {
        CatalogWriteFixture storage = new();
        DialogWriteToken token = (await storage.CreateAsync()).Token;
        int reads = storage.RootRows.ReadCalls;
        string before = JsonSerializer.Serialize(storage.Committed);
        await using CatalogRunnerFixture runner = new(storage);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(token, Guid.NewGuid(), TestContext.Current.CancellationToken, bounded: true));
        Assert.Equal("bounded_history_policy_required", error.Message);
        Assert.Equal(reads, storage.RootRows.ReadCalls);
        Assert.Equal(0, runner.ProviderCalls);
        Assert.Empty(runner.Requests);
        Assert.Equal(before, JsonSerializer.Serialize(storage.Committed));
    }

    /// <summary>Истёкший legacy root с B03 не загружает children даже при oversized canonical JSON.</summary>
    [Fact]
    public async Task BoundedLegacyReadRejectsExpiryBeforeHistory()
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        await seed.BeginAsync(token);
        seed.Committed.Roots[0].CatalogRegistered = false;
        seed.Committed.Roots[0].CreatedAtUtc = CatalogWriteFixture.NOW.AddDays(-1);
        seed.Committed.Items[0].ContentJson = new string(' ', 100000);
        string before = JsonSerializer.Serialize(seed.Committed);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(100, 16384, 32));
        storage.Reload();

        ServiceResult<DialogSnapshot> result = await storage.Reader.ReadAsync(
            new(token.DialogId, CatalogWriteFixture.SCOPE.OwnerId, CatalogWriteFixture.NOW), TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Expired, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Empty(storage.ItemRows.HistoryQuery.ScalarQueries);
        Assert.Empty(storage.TurnRows.HistoryQuery.ScalarQueries);
        Assert.Empty(storage.OperationRows.HistoryQuery.ScalarQueries);
        Assert.Equal(before, JsonSerializer.Serialize(seed.Committed));
    }

    /// <summary>Накопленный provenance ограничивается явным record budget до десериализации malformed pairs.</summary>
    [Fact]
    public async Task ManyRecoveryRecordsRefuseBeforePayloadOrCanonicalReads()
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        seed.Committed.Operations.AddRange(Enumerable.Range(1, 5000).Select(revision => new DialogRecoveryOperationRecord
        {
            Id = Guid.NewGuid(), DialogId = token.DialogId.Value, Kind = "recovery", Revision = revision,
            PairsJson = "malformed", ResultJson = "{}"
        }));
        string before = JsonSerializer.Serialize(seed.Committed);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(10, 16384, 32));
        storage.Reload();

        AssertOverflow(await storage.Reader.ReadCatalogAsync(storage.FullAccess(token), TestContext.Current.CancellationToken));
        Assert.Equal(9, Assert.Single(storage.OperationRows.HistoryQuery.ScalarRows));
        Assert.Empty(storage.OperationRows.HistoryQuery.PayloadQueries);
        Assert.Empty(storage.ItemRows.HistoryQuery.ScalarQueries);
        Assert.Equal(before, JsonSerializer.Serialize(seed.Committed));
    }

    /// <summary>Reader active compact не скрывает старые oversized версии: actual Begin loader ограничен до materialization.</summary>
    [Fact]
    public async Task RunnerWriteLoaderCannotLoadUnboundedOldCompacts()
    {
        CatalogWriteFixture seed = new();
        DialogWriteToken token = (await seed.CreateAsync()).Token;
        seed.Committed.Contexts.Add(new()
        {
            DialogId = token.DialogId.Value, Version = 1, Compaction = new() { EnvelopeJson = new string(' ', 100000) }
        });
        seed.Committed.Contexts.Add(new()
        {
            DialogId = token.DialogId.Value, Version = 2, Compaction = new() { Status = ModelResponseStatus.Completed }
        });
        string before = JsonSerializer.Serialize(seed.Committed);
        CatalogWriteFixture storage = new(seed.Committed, loadLimits: new(100, 16384, 64));
        await using CatalogRunnerFixture runner = new(storage);
        AgentRunResult result = await runner.RunAsync(token, Guid.NewGuid(), TestContext.Current.CancellationToken, bounded: true);
        Assert.Equal("history_load_limit_exceeded", result.Error!.Message);
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.False(result.TerminalSaved);
        Assert.Empty(runner.Requests);
        Assert.Equal(before, JsonSerializer.Serialize(seed.Committed));
        Assert.Contains("rollback", storage.Session.Events);
        Assert.DoesNotContain("save", storage.Session.Events);
        Assert.Equal(1, storage.ContextRows.HistoryQuery.PayloadRows);
    }

    /// <summary>Unknown/function pair recovery, old-turn dedup и fencing проходят actual bounded путь без replay.</summary>
    [Fact]
    public async Task RecoveryAndExplicitNewTurnPreserveUnknownAndOriginalHistory()
    {
        CatalogWriteFixture storage = new(loadLimits: new(256, 1048576, 128));
        DialogWriteToken token = (await storage.CreateAsync()).Token;
        await using CatalogRunnerFixture runner = new(storage);
        Guid firstTurn = Guid.NewGuid();
        runner.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Call("same"), CatalogWriteFixture.Call("same")]));
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        runner.HandlerAction = (_, _) =>
        {
            caller.Cancel();
            throw new OperationCanceledException(caller.Token);
        };
        AgentRunResult interrupted = await runner.RunAsync(token, firstTurn, caller.Token, bounded: true);
        Assert.True(interrupted.TerminalSaved);
        DialogSnapshot original = await storage.SnapshotAsync(interrupted.Token!);
        Assert.Equal(DialogReadiness.RecoveryRequired, original.Continuation!.Readiness);
        Assert.Contains(original.Continuation.Pending, call => call.State == ToolAttemptState.Unknown);
        string originals = JsonSerializer.Serialize(new { storage.Committed.Items, storage.Committed.Steps, storage.Committed.Turns });
        DialogRecoveryResolution[] resolutions = original.Continuation.Pending.Select(call => new DialogRecoveryResolution(
            call.TurnId, call.StepId, call.OutputIndex,
            call.State == ToolAttemptState.NotStarted ? DialogRecoveryKind.NotStarted : DialogRecoveryKind.AcknowledgedUnknown,
            call.State == ToolAttemptState.NotStarted ? null : "explicit-owner-ack")).ToArray();
        Guid recoveryId = Guid.NewGuid();
        ServiceResult<DialogRecoveryResult> recovered = await storage.Recovery.RecoverAsync(new(storage.Access(original.Token), original.Token,
            recoveryId, original.Continuation.RecoveryRevision, original.Continuation.FencingEpoch, firstTurn, resolutions), TestContext.Current.CancellationToken);
        Assert.True(recovered.Success, recovered.Error?.Message);
        Assert.Equal(DialogReadiness.Ready, recovered.Data!.Continuation.Readiness);
        Assert.Equal(originals, JsonSerializer.Serialize(new { storage.Committed.Items, storage.Committed.Steps, storage.Committed.Turns }));
        DialogSnapshot effective = await storage.SnapshotAsync(recovered.Data.Continuation.Token);
        Assert.True(effective.EffectiveTurns[0].Items.Count > effective.Turns[0].Items.Count);
        int calls = runner.HandlerCalls;
        AgentRunResult duplicate = await runner.RunAsync(effective.Token, firstTurn, TestContext.Current.CancellationToken, bounded: true);
        Assert.Equal(ServiceErrorType.Conflict, duplicate.Error!.Type);
        Assert.Equal(calls, runner.HandlerCalls);
        runner.Responses.Enqueue(ModelResponse.Completed([CatalogWriteFixture.Assistant("Ответ нового явного turn")]));
        runner.HandlerAction = (_, _) => throw new InvalidOperationException("old handler replay");
        AgentRunResult next = await runner.RunAsync(effective.Token, Guid.NewGuid(), TestContext.Current.CancellationToken, bounded: true);
        Assert.Equal(AgentRunStatus.Completed, next.Status);
        Assert.True(next.TerminalSaved);
        Assert.Equal(calls, runner.HandlerCalls);
        Assert.Contains(runner.Requests[^1].Input, item => item.Content.GetRawText().Contains("tool_outcome_unknown", StringComparison.Ordinal));
    }

    /// <summary>Проверяет semantic отказ без частичной истории и raw payload.</summary>
    private static void AssertOverflow(ServiceResult<DialogSnapshot> result)
    {
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Equal("history_load_limit_exceeded", result.Error.Message);
    }

    /// <summary>Expired lease не становится Ready; fence отвергает поздний writer и не меняет original InProgress.</summary>
    [Fact]
    public async Task BoundedLifecyclePreservesFencingAndExplicitRecovery()
    {
        CatalogWriteFixture storage = new(loadLimits: new(256, 1048576, 128));
        DialogWriteToken token = (await storage.CreateAsync()).Token;
        DialogRunState begun = await storage.BeginAsync(token);
        storage.Time.Now = storage.Time.Now.AddMinutes(2);
        ServiceResult<DialogContinuationState> fenced = await storage.Lifecycle.FenceAsync(new(storage.Access(begun.Token), begun.Token,
            begun.Lease, Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.True(fenced.Success, fenced.Error?.Message);
        Assert.True(fenced.Data!.FencingEpoch > begun.Lease.Epoch);
        Assert.Equal(DialogTurnStatus.InProgress, storage.Committed.Turns[0].Status);
        string before = JsonSerializer.Serialize(storage.Committed);
        ServiceResult<DialogWriteToken> late = await storage.Writer.AppendAsync(new DialogRunWriteAccess(storage.Access(begun.Token), begun.Lease),
            fenced.Data.Token, begun.Lease.TurnId, [CatalogWriteFixture.Assistant("late")], [], TestContext.Current.CancellationToken);
        Assert.False(late.Success);
        Assert.Equal(ServiceErrorType.Conflict, late.Error!.Type);
        Assert.Equal(before, JsonSerializer.Serialize(storage.Committed));
        Assert.Equal("fence", Assert.Single(storage.Committed.Operations).Kind);
    }

    /// <summary>Неизвестный commit не маскируется overflow/Ready; authoritative lookup и old turn сохраняют no-replay.</summary>
    [Fact]
    public async Task BoundedUnknownCommitDoesNotPermitAutomaticReplay()
    {
        CatalogWriteFixture storage = new(loadLimits: new(256, 1048576, 128));
        DialogWriteToken token = (await storage.CreateAsync()).Token;
        InvalidOperationException uncertain = new("isolated-unknown-commit");
        storage.Session.Transaction.CommitError = uncertain;
        Guid turn = Guid.NewGuid();
        Assert.Same(uncertain, await Assert.ThrowsAsync<InvalidOperationException>(() => storage.BeginAsync(token, turn)));
        Assert.Single(storage.Committed.Turns);
        Assert.Equal(turn, storage.Committed.Turns[0].Id);
        CatalogWriteFixture fresh = new(storage.Committed, loadLimits: new(256, 1048576, 128));
        await using CatalogRunnerFixture runner = new(fresh);
        AgentRunResult blocked = await runner.RunAsync(token, turn, TestContext.Current.CancellationToken, bounded: true);
        Assert.Equal(ServiceErrorType.Conflict, blocked.Error!.Type);
        Assert.False(blocked.TerminalSaved);
        Assert.Empty(runner.Requests);
        Assert.Equal(0, runner.ProviderCalls);
        Assert.Single(storage.Committed.Turns);
    }
}
