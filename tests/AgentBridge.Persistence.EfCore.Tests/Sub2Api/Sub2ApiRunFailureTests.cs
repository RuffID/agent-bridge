using System.Net;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Sub2Api;

/// <summary>Модельные отказы после инструментов не повторяют бизнес-действие и не стирают подтверждённый результат.</summary>
public class Sub2ApiRunFailureTests
{
    /// <summary>HTTP отказ, incomplete SSE и caller cancellation после tool сохраняют подтверждённый output без повторной отправки.</summary>
    [Theory]
    [InlineData("http", AgentRunStatus.Failed)]
    [InlineData("partial", AgentRunStatus.Incomplete)]
    [InlineData("eof", AgentRunStatus.Incomplete)]
    [InlineData("cancel", AgentRunStatus.Canceled)]
    public async Task ModelFailureAfterToolNeverReplaysBusinessAction(string mode, AgentRunStatus expected)
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync()).Token;
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Transport.Enqueue([CatalogWriteFixture.Call("call-one")]);
        if (mode == "partial")
        {
            fixture.Transport.Enqueue([CatalogWriteFixture.Assistant("Частичный ответ")], "incomplete", sse: true);
        }
        else if (mode == "eof")
        {
            fixture.Transport.Replies.Enqueue(_ => Task.FromResult(fixture.Transport.Json(
                "data: {\"type\":\"response.output_item.added\",\"output_index\":0,\"item\":{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"partial\"}]}}\n\n",
                mediaType: "text/event-stream")));
        }
        else
        {
            fixture.Transport.Replies.Enqueue(_ =>
            {
                if (mode == "cancel")
                {
                    caller.Cancel();
                    throw new OperationCanceledException(caller.Token);
                }

                return Task.FromResult(fixture.Transport.Json("""{"error":{"type":"invalid_request_error","code":"context_length_exceeded"}}""",
                    HttpStatusCode.BadRequest));
            });
        }

        Guid turn = Guid.NewGuid();

        // Act
        AgentRunResult failed = await fixture.RunAsync(token, turn, cancellationToken: caller.Token);
        DialogSnapshot saved = await fixture.Storage.SnapshotAsync(failed.Token!);
        AgentRunResult replay = await fixture.RunAsync(saved.Token, turn, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, failed.Status);
        Assert.True(failed.TerminalSaved);
        Assert.Equal(ServiceErrorType.Conflict, replay.Error!.Type);
        Assert.Equal(1, fixture.HandlerCalls);
        Assert.Equal(1, fixture.KeyCalls);
        Assert.Equal(2, fixture.Transport.Bodies.Count);
        Assert.Single(saved.Turns);
        Assert.Equal(ToolAttemptState.Succeeded, Assert.Single(saved.Turns[0].ModelSteps[0].ToolAttempts).State);
        Assert.Contains(saved.Turns[0].Items, item => item.Content.GetRawText().Contains("business-result", StringComparison.Ordinal));
        Assert.All(fixture.Transport.Responses, body => Assert.True(body.Disposed));
        if (mode == "partial" || mode == "eof")
        {
            Assert.Equal(ModelResponseStatus.Incomplete, saved.Turns[0].ModelSteps[^1].Response.Status);
            Assert.Equal("sub2api", saved.Turns[0].ModelSteps[^1].Response.ConnectorProvenance!.ConnectorId);
            Assert.Equal(2, saved.Turns[0].ModelSteps.Count);
        }
    }

    /// <summary>Удаление или expiry во время HTTP запрещает позднюю запись и запуск полученного tool call.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DialogInvalidatedDuringHttpCannotBeResurrected(bool delete)
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync(expires: CatalogWriteFixture.NOW.AddMinutes(5))).Token;
        fixture.Transport.Replies.Enqueue(async ct =>
        {
            DialogSnapshot current = await fixture.Storage.SnapshotAsync(token);
            if (delete)
            {
                ServiceResult active = await fixture.Storage.Deletion.DeleteAsync(fixture.Storage.Access(current.Token), current.Token, ct);
                Assert.Equal(ServiceErrorType.Conflict, active.Error!.Type);
                Assert.Equal("delete_active_run", active.Error.Message);
                fixture.Storage.Time.Now = CatalogWriteFixture.NOW.AddMinutes(2);
                ServiceResult<DialogContinuationState> fenced = await fixture.Storage.Lifecycle.FenceAsync(new(
                    fixture.Storage.Access(current.Token), current.Token, current.Continuation!.Lease!, Guid.NewGuid()), ct);
                Assert.True(fenced.Success, fenced.Error?.Message);
                ServiceResult removed = await fixture.Storage.Deletion.DeleteAsync(fixture.Storage.Access(fenced.Data!.Token), fenced.Data.Token, ct);
                Assert.True(removed.Success, removed.Error?.Message);
            }
            else
            {
                fixture.Storage.Time.Now = CatalogWriteFixture.NOW.AddMinutes(5);
            }

            return fixture.Transport.Json(JsonSerializer.Serialize(new
            {
                status = "completed", output = new[] { CatalogWriteFixture.Call("late-call").Content }
            }));
        });

        // Act
        AgentRunResult result = await fixture.RunAsync(token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.False(result.TerminalSaved);
        Assert.Equal(0, fixture.HandlerCalls);
        Assert.Single(fixture.Transport.Bodies);
        Assert.Empty(fixture.Storage.Committed.Steps);
        Assert.All(fixture.Transport.Responses, body => Assert.True(body.Disposed));
        if (delete)
        {
            Assert.Equal(ServiceErrorType.NotFound, result.Error!.Type);
            Assert.Empty(fixture.Storage.Committed.Roots);
            Assert.Empty(fixture.Storage.Committed.Items);
        }
        else
        {
            Assert.Equal(ServiceErrorType.Expired, result.Error!.Type);
            Assert.Single(fixture.Storage.Committed.Roots);
            Assert.Equal(CatalogWriteFixture.NOW.AddMinutes(5), fixture.Storage.Committed.Roots[0].ExpiresAtUtc);
        }
    }

    /// <summary>Новый fence во время второго HTTP отвергает позднего исполнителя без потери ранее выполненного tool.</summary>
    [Fact]
    public async Task FencedRunCannotWriteLateModelResponse()
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync()).Token;
        fixture.Transport.Enqueue([CatalogWriteFixture.Call("call-one")]);
        fixture.Transport.Replies.Enqueue(async ct =>
        {
            DialogSnapshot current = await fixture.Storage.SnapshotAsync(token);
            fixture.Storage.Time.Now = CatalogWriteFixture.NOW.AddMinutes(2);
            ServiceResult<DialogContinuationState> fenced = await fixture.Storage.Lifecycle.FenceAsync(new(
                fixture.Storage.Access(current.Token), current.Token, current.Continuation!.Lease!, Guid.NewGuid()), ct);
            Assert.True(fenced.Success, fenced.Error?.Message);
            return fixture.Transport.Json("""{"status":"completed","output":[{"type":"function_call","call_id":"late","name":"action","arguments":"{}"}]}""");
        });

        // Act
        AgentRunResult result = await fixture.RunAsync(token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.False(result.TerminalSaved);
        Assert.Equal(1, fixture.HandlerCalls);
        Assert.Equal(2, fixture.Transport.Bodies.Count);
        Assert.Single(fixture.Storage.Committed.Steps);
        Assert.Equal("fence", Assert.Single(fixture.Storage.Committed.Operations).Kind);
        Assert.DoesNotContain(fixture.Storage.Committed.Items, item => item.ContentJson.Contains("\"late\"", StringComparison.Ordinal));
    }
}
