using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration.Sub2Api;

/// <summary>Настоящие PostgreSQL restart/fence/recovery без live HTTP, client retries и повторов инструментов.</summary>
[Trait("Dependency", "Database")]
[Collection("DatabaseIntegration")]
public class Sub2ApiPersistenceRestartTests(DatabaseIntegrationFixture environment)
{
    /// <summary>Новый facade root восстанавливает original payload/provenance и продолжает JSON/SSE без replay.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedToolAndFullContextSurviveNewRoot(bool stream)
    {
        // Arrange
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(DatabaseProvider.PostgreSql);
        await database.InitializeAsync();
        DialogWriteToken token;
        Guid firstTurn = Guid.NewGuid();
        Guid executionId;
        string originals;
        string savedTurn;
        CanonicalModelItem opaque = Sub2ApiPersistenceFixture.Opaque();
        await using (Sub2ApiPersistenceFixture first = new(database))
        {
            token = await first.CreateAsync();
            first.Transport.Enqueue([opaque, Sub2ApiPersistenceFixture.Call()], sse: stream);
            first.Transport.Enqueue([Sub2ApiPersistenceFixture.Message("Первый ответ", "assistant")], sse: stream);

            // Act
            AgentRunResult completed = await first.RunAsync(token, firstTurn, "Первый вопрос", stream,
                TestContext.Current.CancellationToken);
            Assert.Equal(AgentRunStatus.Completed, completed.Status);
            Assert.True(completed.TerminalSaved);
            token = completed.Token!;
            DialogSnapshot written = await first.ReadAsync(token);
            savedTurn = JsonSerializer.Serialize(Assert.Single(written.Turns));
            originals = await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, firstTurn);
            executionId = first.ExecutionId!.Value;

            // Assert
            Assert.Equal(1, first.HandlerCalls);
            Assert.Equal(2, first.Transport.Bodies.Count);
            Assert.All(written.Turns[0].ModelSteps, step =>
                Assert.Equal("sub2api", step.Response.ConnectorProvenance!.ConnectorId));
            Assert.Equal(ToolAttemptState.Succeeded, Assert.Single(written.Turns[0].ModelSteps[0].ToolAttempts).State);
            Assert.Null(written.ActiveContext);
        }

        await using Sub2ApiPersistenceFixture next = new(database);

        // Act
        DialogSnapshot restored = await next.ReadAsync(token);
        Assert.Equal(savedTurn, JsonSerializer.Serialize(Assert.Single(restored.Turns)));
        next.Transport.Enqueue([Sub2ApiPersistenceFixture.Message("Второй ответ", "assistant")], sse: stream);
        AgentRunResult continued = await next.RunAsync(restored.Token, Guid.NewGuid(), "Второй вопрос", stream,
            TestContext.Current.CancellationToken);
        DialogSnapshot after = await next.ReadAsync(continued.Token!);

        // Assert
        Assert.Equal(DialogReadiness.Ready, restored.Continuation!.Readiness);
        Assert.Equal(AgentRunStatus.Completed, continued.Status);
        Assert.True(continued.TerminalSaved);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Equal(2, after.Turns.Count);
        Assert.Null(after.ActiveContext);
        Assert.Equal(originals, await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, firstTurn));
        JsonElement body = Assert.Single(next.Transport.Bodies);
        JsonElement[] input = body.GetProperty("input").EnumerateArray().ToArray();
        Assert.Contains(input, item => JsonElement.DeepEquals(opaque.Content, item));
        Assert.Single(input, item => Type(item) == "function_call");
        JsonElement output = Assert.Single(input, item => Type(item) == "function_call_output");
        Assert.Contains(executionId.ToString(), output.GetRawText());
        Assert.Contains(input, item => JsonElement.DeepEquals(Sub2ApiPersistenceFixture.Message("Первый вопрос").Content, item));
        Assert.Contains(input, item => JsonElement.DeepEquals(Sub2ApiPersistenceFixture.Message("Первый ответ", "assistant").Content, item));
        Assert.Contains(input, item => JsonElement.DeepEquals(Sub2ApiPersistenceFixture.Message("Второй вопрос").Content, item));
        Assert.False(body.TryGetProperty("previous_response_id", out _));
        Assert.All(next.Transport.Urls, url => Assert.DoesNotContain("compact", url));
        Assert.All(next.Transport.Responses, response => Assert.True(response.Disposed));
    }

    /// <summary>Actual commit с потерянным подтверждением требует fence; Started требует explicit Unknown recovery.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostCommitAcknowledgementCannotReplayToolAfterFence(bool afterAction)
    {
        // Arrange
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(DatabaseProvider.PostgreSql);
        await database.InitializeAsync();
        ToolAttemptState expected = ToolAttemptState.Started;
        int actions = 0;
        if (afterAction)
        {
            expected = ToolAttemptState.Succeeded;
            actions = 1;
        }

        DialogWriteToken token;
        Guid originalTurn = Guid.NewGuid();
        string originals;
        await using (Sub2ApiPersistenceFixture first = new(database, expected))
        {
            token = await first.CreateAsync();
            first.Transport.Enqueue([Sub2ApiPersistenceFixture.Opaque(), Sub2ApiPersistenceFixture.Call()]);

            // Act
            AggregateException error = await Assert.ThrowsAsync<AggregateException>(() => first.RunAsync(token, originalTurn,
                cancellationToken: TestContext.Current.CancellationToken));
            DialogSnapshot committed = await first.ReadAsync(token);
            token = committed.Token;
            originals = await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, originalTurn);

            // Assert
            Assert.Collection(error.InnerExceptions,
                primary => Assert.Same(first.CommitFailure, primary),
                cleanup => Assert.IsType<InvalidOperationException>(cleanup));
            Assert.Equal(1, first.CommitFailures);
            Assert.Equal(actions, first.HandlerCalls);
            Assert.Single(first.Transport.Bodies);
            Assert.Equal(DialogReadiness.Active, committed.Continuation!.Readiness);
            Assert.Equal(expected, Assert.Single(committed.Turns[0].ModelSteps[0].ToolAttempts).State);
        }

        await using Sub2ApiPersistenceFixture next = new(database);
        DialogSnapshot restarted = await next.ReadAsync(token);
        DialogContinuationState active = restarted.Continuation!;
        DialogRunLease lease = active.Lease!;
        Guid fenceId = Guid.NewGuid();
        await using AsyncServiceScope scope = next.Services.CreateAsyncScope();
        IDialogRunLifecycle lifecycle = scope.ServiceProvider.GetRequiredService<IDialogRunLifecycle>();

        // Act
        AgentRunResult blocked = await next.RunAsync(token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);
        ServiceResult<DialogContinuationState> liveFence = await lifecycle.FenceAsync(
            new(next.Access(token), token, lease, fenceId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Conflict, blocked.Error!.Type);
        Assert.Equal(ServiceErrorType.Conflict, liveFence.Error!.Type);
        Assert.Equal(0, next.Transport.CatalogCalls);
        Assert.Empty(next.Transport.Urls);
        Assert.Equal(originals, await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, originalTurn));

        // Act
        next.Time.Now = lease.DeadlineUtc;
        ServiceResult<DialogContinuationState> fence = await lifecycle.FenceAsync(
            new(next.Access(token), token, lease, fenceId), TestContext.Current.CancellationToken);
        Assert.True(fence.Success, fence.Error?.Message);
        DialogContinuationState current = fence.Data!;
        ServiceResult<DialogRunLease> stale = await lifecycle.RenewAsync(next.Access(current.Token), lease,
            TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Conflict, stale.Error!.Type);
        Assert.Null(current.Lease);
        Assert.True(current.FencingEpoch > active.FencingEpoch);
        if (afterAction)
        {
            Assert.Equal(DialogReadiness.Ready, current.Readiness);
            Assert.Empty(current.Pending);
        }
        else
        {
            Assert.Equal(DialogReadiness.RecoveryRequired, current.Readiness);
            Assert.Equal(ToolAttemptState.Unknown, Assert.Single(current.Pending).State);

            // Act
            AgentRunResult pending = await next.RunAsync(current.Token, Guid.NewGuid(),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(ServiceErrorType.Conflict, pending.Error!.Type);
            Assert.Empty(next.Transport.Urls);
            DialogRecoveryResult recovered = await RecoverAsync(next, current);
            current = recovered.Continuation;

            // Assert
            Assert.Equal(DialogReadiness.Ready, current.Readiness);
            Assert.Single(recovered.Records);
        }

        // Act
        next.Transport.Enqueue([Sub2ApiPersistenceFixture.Message("После восстановления", "assistant")]);
        AgentRunResult continued = await next.RunAsync(current.Token, Guid.NewGuid(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Completed, continued.Status);
        Assert.True(continued.TerminalSaved);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Equal(originals, await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, originalTurn));
        JsonElement body = Assert.Single(next.Transport.Bodies);
        Assert.Contains("synthetic-opaque-restart", body.GetRawText());
        string output = Assert.Single(body.GetProperty("input").EnumerateArray(), item => Type(item) == "function_call_output").GetRawText();
        if (afterAction)
        {
            Assert.Contains("executionId", output);
            Assert.DoesNotContain("tool_outcome_unknown", output);
        }
        else
        {
            Assert.Contains("tool_outcome_unknown", output);
        }
    }

    /// <summary>Unknown handler outcome остаётся blocked после restart; append-only recovery читается следующим root.</summary>
    [Fact]
    public async Task UnknownOutcomeRequiresDurableExplicitRecoveryBeforeNewTurn()
    {
        // Arrange
        await using IntegrationDatabase database = await environment.CreateDatabaseAsync(DatabaseProvider.PostgreSql);
        await database.InitializeAsync();
        DialogWriteToken token;
        Guid originalTurn = Guid.NewGuid();
        string originals;
        await using (Sub2ApiPersistenceFixture first = new(database))
        {
            token = await first.CreateAsync();
            using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            first.Action = _ =>
            {
                caller.Cancel();
                throw new OperationCanceledException(caller.Token);
            };
            first.Transport.Enqueue([Sub2ApiPersistenceFixture.Call()]);

            // Act
            AgentRunResult canceled = await first.RunAsync(token, originalTurn, cancellationToken: caller.Token);
            token = canceled.Token!;
            originals = await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, originalTurn);

            // Assert
            Assert.Equal(AgentRunStatus.Canceled, canceled.Status);
            Assert.True(canceled.TerminalSaved);
            Assert.Equal(1, first.HandlerCalls);
        }

        Guid recoveryId;
        await using (Sub2ApiPersistenceFixture next = new(database))
        {
            // Act
            DialogSnapshot restored = await next.ReadAsync(token);
            AgentRunResult blocked = await next.RunAsync(token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(DialogReadiness.RecoveryRequired, restored.Continuation!.Readiness);
            Assert.Equal(ToolAttemptState.Unknown, Assert.Single(restored.Continuation.Pending).State);
            Assert.Equal(ServiceErrorType.Conflict, blocked.Error!.Type);
            Assert.Equal(0, next.Transport.CatalogCalls);
            Assert.Empty(next.Transport.Urls);

            // Act
            recoveryId = Guid.NewGuid();
            DialogRecoveryResult recovered = await RecoverAsync(next, restored.Continuation, recoveryId);
            token = recovered.Continuation.Token;

            // Assert
            Assert.Equal(DialogReadiness.Ready, recovered.Continuation.Readiness);
            Assert.Equal(0, next.HandlerCalls);
            Assert.Equal(originals, await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, originalTurn));
        }

        await using Sub2ApiPersistenceFixture third = new(database);
        await using AsyncServiceScope scope = third.Services.CreateAsyncScope();

        // Act
        ServiceResult<DialogRecoveryResult> durable = await scope.ServiceProvider.GetRequiredService<IDialogRecovery>()
            .ReadAsync(third.Access(token), recoveryId, TestContext.Current.CancellationToken);
        DialogSnapshot ready = await third.ReadAsync(token);
        third.Transport.Enqueue([Sub2ApiPersistenceFixture.Message("Продолжение", "assistant")]);
        AgentRunResult continued = await third.RunAsync(ready.Token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(durable.Success, durable.Error?.Message);
        Assert.Single(durable.Data!.Records);
        Assert.Equal(DialogReadiness.Ready, ready.Continuation!.Readiness);
        Assert.Equal(AgentRunStatus.Completed, continued.Status);
        Assert.True(continued.TerminalSaved);
        Assert.Equal(0, third.HandlerCalls);
        Assert.Contains("tool_outcome_unknown", Assert.Single(third.Transport.Bodies).GetRawText());
        Assert.Equal(originals, await Sub2ApiPersistenceFixture.OriginalRowsAsync(database, originalTurn));
    }

    /// <summary>Явно подтверждает Unknown без выдачи его за NotStarted или подтверждённый business output.</summary>
    private static async Task<DialogRecoveryResult> RecoverAsync(Sub2ApiPersistenceFixture fixture,
        DialogContinuationState pending, Guid? recoveryId = null)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DialogRecoveryRequest request = new(fixture.Access(pending.Token), pending.Token, recoveryId ?? Guid.NewGuid(),
            pending.RecoveryRevision, pending.FencingEpoch, pending.TurnId!.Value,
            pending.Pending.Select(call => new DialogRecoveryResolution(call.TurnId, call.StepId, call.OutputIndex,
                DialogRecoveryKind.AcknowledgedUnknown, "synthetic-explicit-recovery")));
        ServiceResult<DialogRecoveryResult> result = await scope.ServiceProvider.GetRequiredService<IDialogRecovery>()
            .RecoverAsync(request, TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Error?.Message);
        return result.Data!;
    }

    /// <summary>Читает canonical discriminator без интерпретации opaque-содержимого.</summary>
    private static string? Type(JsonElement item)
    {
        return item.GetProperty("type").GetString();
    }
}
