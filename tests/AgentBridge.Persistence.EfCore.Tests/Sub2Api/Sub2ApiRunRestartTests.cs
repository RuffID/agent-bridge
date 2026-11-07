using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Sub2Api;

/// <summary>Сериализованный restart с новым DI и actual reader, без shared mutable store и реальной БД.</summary>
public class Sub2ApiRunRestartTests
{
    /// <summary>Родное opaque состояние, отчёты и tool output читаются новым экземпляром и уходят следующей generation.</summary>
    [Fact]
    public async Task SerializedHistoryContinuesThroughNewContainerWithoutToolReplay()
    {
        // Arrange
        CatalogWriteFixture restarted;
        DialogWriteToken savedToken;
        string originalMetadata;
        await using (Sub2ApiRunFixture first = new())
        {
            DialogWriteToken token = (await first.Storage.CreateAsync()).Token;
            first.Transport.Enqueue([
                new(JsonSerializer.SerializeToElement(new { type = "reasoning", encrypted_content = "synthetic-opaque", summary = Array.Empty<object>() })),
                CatalogWriteFixture.Call("call-one")
            ]);
            first.Transport.Enqueue([CatalogWriteFixture.Assistant("Первый ответ")]);
            AgentRunResult result = await first.RunAsync(token, Guid.NewGuid(), "Первый вопрос", TestContext.Current.CancellationToken);
            Assert.True(result.Status == AgentRunStatus.Completed, result.Error?.Message);
            Assert.Equal(1, first.HandlerCalls);
            savedToken = result.Token!;
            originalMetadata = first.Storage.Committed.Steps[0].Response.EnvelopeJson!;
            restarted = first.RestartStorage();
        }

        await using Sub2ApiRunFixture next = new(restarted);
        next.Transport.Enqueue([CatalogWriteFixture.Assistant("После restart")]);

        // Act
        AgentRunResult continued = await next.RunAsync(savedToken, Guid.NewGuid(), "Следующий вопрос", TestContext.Current.CancellationToken);
        DialogSnapshot snapshot = await restarted.SnapshotAsync(continued.Token!);

        // Assert
        Assert.Equal(AgentRunStatus.Completed, continued.Status);
        Assert.True(continued.TerminalSaved);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Equal(1, next.KeyCalls);
        Assert.Equal(2, snapshot.Turns.Count);
        Sub2ApiRunFlowTests.AssertWire(Assert.Single(next.Transport.Bodies));
        Assert.Equal(originalMetadata, restarted.Committed.Steps[0].Response.EnvelopeJson);
        Assert.Equal("sub2api", snapshot.Turns[0].ModelSteps[0].Response.ConnectorProvenance!.ConnectorId);
        Assert.Null(snapshot.Turns[0].ModelSteps[0].Response.Continuation);
    }

    /// <summary>Новый контейнер с чужим доступом не принимает opaque историю и не начинает turn.</summary>
    [Fact]
    public async Task ChangedAccessAfterRestartRejectsOpaqueBeforeBeginAndGeneration()
    {
        // Arrange
        await using Sub2ApiRunFixture first = new();
        DialogWriteToken token = (await first.Storage.CreateAsync()).Token;
        first.Transport.Enqueue([new(JsonSerializer.SerializeToElement(new
        {
            type = "reasoning", encrypted_content = "synthetic-opaque", summary = Array.Empty<object>()
        })), CatalogWriteFixture.Assistant("Ответ")]);
        AgentRunResult saved = await first.RunAsync(token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(saved.Status == AgentRunStatus.Completed, saved.Error?.Message);
        CatalogWriteFixture restarted = first.RestartStorage();
        string before = JsonSerializer.Serialize(restarted.Committed);
        await using Sub2ApiRunFixture next = new(restarted) { Key = "synthetic-other-key" };

        // Act
        AgentRunResult refused = await next.RunAsync(saved.Token!, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Failed, refused.Status);
        Assert.Equal(ServiceErrorType.Conflict, refused.Error!.Type);
        Assert.False(refused.TerminalSaved);
        Assert.Empty(next.Transport.Bodies);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Equal(0, next.ProviderCalls);
        Assert.Equal(before, JsonSerializer.Serialize(restarted.Committed));
    }
}
