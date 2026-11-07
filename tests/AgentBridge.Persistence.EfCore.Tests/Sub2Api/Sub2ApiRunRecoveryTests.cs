using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Sub2Api;

/// <summary>Неизвестные записи и recovery проходят настоящий lifecycle без повторного business tool.</summary>
public class Sub2ApiRunRecoveryTests
{
    /// <summary>Потеря acknowledgement start/outcomes commit блокирует run; новый контейнер не переигрывает действие.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownCommitRequiresRecoveryInsteadOfAutomaticReplay(bool afterAction)
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync()).Token;
        Guid turn = Guid.NewGuid();
        fixture.Transport.Enqueue([CatalogWriteFixture.Call("call-one")]);
        InvalidOperationException uncertain = new("isolated-commit-ack-lost");
        fixture.Storage.AfterCommit = () =>
        {
            if (fixture.Storage.Committed.Steps.Count == 0 || fixture.Storage.Committed.Steps[0].ToolAttemptsJson is null)
            {
                return;
            }

            using JsonDocument journal = JsonDocument.Parse(fixture.Storage.Committed.Steps[0].ToolAttemptsJson!);
            JsonElement attempts = journal.RootElement.GetProperty("attempts");
            if (attempts.GetArrayLength() == 0)
            {
                return;
            }

            JsonElement attempt = attempts[0];
            int expected = (int)ToolAttemptState.Started;
            if (afterAction)
            {
                expected = (int)ToolAttemptState.Succeeded;
            }

            if (attempt.GetProperty("state").GetInt32() == expected)
            {
                throw uncertain;
            }
        };

        // Act
        Exception error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(token, turn,
            cancellationToken: TestContext.Current.CancellationToken));
        CatalogWriteFixture fresh = fixture.RestartStorage();
        await using Sub2ApiRunFixture next = new(fresh);
        AgentRunResult blocked = await next.RunAsync(token, turn, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(uncertain, error);
        int expectedCalls = 0;
        if (afterAction)
        {
            expectedCalls = 1;
        }

        Assert.Equal(expectedCalls, fixture.HandlerCalls);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Equal(0, next.KeyCalls);
        Assert.Equal(0, next.ProviderCalls);
        Assert.Equal(ServiceErrorType.Conflict, blocked.Error!.Type);
        Assert.False(blocked.TerminalSaved);
        Assert.Empty(next.Transport.Urls);
        Assert.Single(fixture.Transport.Bodies);
        Assert.Single(fresh.Committed.Turns);
        Assert.Single(fresh.Committed.Steps);
    }

    /// <summary>Отменённый tool остаётся Unknown после restart; explicit recovery добавляет результат без повторного действия.</summary>
    [Fact]
    public async Task UnknownBusinessOutcomeNeedsExplicitRecoveryBeforeNewTurn()
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync()).Token;
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Handle = _ =>
        {
            caller.Cancel();
            throw new OperationCanceledException(caller.Token);
        };
        fixture.Transport.Enqueue([CatalogWriteFixture.Call("call-one")]);
        Guid originalTurn = Guid.NewGuid();
        AgentRunResult canceled = await fixture.RunAsync(token, originalTurn, cancellationToken: caller.Token);
        Assert.Equal(AgentRunStatus.Canceled, canceled.Status);
        Assert.True(canceled.TerminalSaved);
        Assert.Equal(1, fixture.HandlerCalls);
        CatalogWriteFixture fresh = fixture.RestartStorage();
        await using Sub2ApiRunFixture next = new(fresh);

        // Act
        AgentRunResult blocked = await next.RunAsync(canceled.Token!, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);
        DialogSnapshot unresolved = await fresh.SnapshotAsync(canceled.Token!);
        DialogPendingCall pending = Assert.Single(unresolved.Continuation!.Pending);
        string originals = JsonSerializer.Serialize(new { fresh.Committed.Turns, fresh.Committed.Items, fresh.Committed.Steps });
        ServiceResult<DialogRecoveryResult> recovered = await fresh.Recovery.RecoverAsync(new(fresh.Access(unresolved.Token), unresolved.Token,
            Guid.NewGuid(), unresolved.Continuation.RecoveryRevision, unresolved.Continuation.FencingEpoch, originalTurn,
            [new(pending.TurnId, pending.StepId, pending.OutputIndex, DialogRecoveryKind.AcknowledgedUnknown, "synthetic-explicit-ack")]),
            TestContext.Current.CancellationToken);
        Assert.True(recovered.Success, recovered.Error?.Message);
        next.Transport.Enqueue([CatalogWriteFixture.Assistant("Продолжение после recovery")]);
        AgentRunResult continued = await next.RunAsync(recovered.Data!.Continuation.Token, Guid.NewGuid(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialogReadiness.RecoveryRequired, unresolved.Continuation.Readiness);
        Assert.Equal(ToolAttemptState.Unknown, pending.State);
        Assert.Equal(ServiceErrorType.Conflict, blocked.Error!.Type);
        Assert.Equal(AgentRunStatus.Completed, continued.Status);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Single(next.Transport.Bodies);
        Assert.Contains("tool_outcome_unknown", next.Transport.Bodies[0].GetRawText());
        Assert.Equal(originals, JsonSerializer.Serialize(new
        {
            Turns = fresh.Committed.Turns.Where(turn => turn.Id == originalTurn).ToList(),
            Items = fresh.Committed.Items.Where(item => item.TurnId == originalTurn).ToList(),
            Steps = fresh.Committed.Steps.Where(step => step.TurnId == originalTurn).ToList()
        }));
    }

    /// <summary>Ошибка cleanup после commit не становится успехом или повторной generation.</summary>
    [Fact]
    public async Task CommittedModelReportSurvivesCleanupFailureWithoutRetry()
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync()).Token;
        fixture.Transport.Enqueue([CatalogWriteFixture.Call("call-one")]);
        InvalidOperationException cleanup = new("isolated-transaction-cleanup");
        fixture.Transport.Replies.Enqueue(_ =>
        {
            fixture.Storage.Session.Transaction.DisposeError = cleanup;
            return Task.FromResult(fixture.Transport.Json("""{"status":"completed","output":[]}"""));
        });

        // Act
        Exception error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(token, Guid.NewGuid(),
            cancellationToken: TestContext.Current.CancellationToken));
        CatalogWriteFixture fresh = fixture.RestartStorage();
        await using Sub2ApiRunFixture next = new(fresh);
        AgentRunResult refused = await next.RunAsync(token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(cleanup, error);
        Assert.Equal(1, fixture.HandlerCalls);
        Assert.Equal(2, fixture.Transport.Bodies.Count);
        Assert.Equal(2, fresh.Committed.Steps.Count);
        Assert.Equal(ServiceErrorType.Conflict, refused.Error!.Type);
        Assert.Empty(next.Transport.Urls);
        Assert.Equal(0, next.HandlerCalls);
        Assert.All(fixture.Transport.Responses, body => Assert.True(body.Disposed));
    }
}
