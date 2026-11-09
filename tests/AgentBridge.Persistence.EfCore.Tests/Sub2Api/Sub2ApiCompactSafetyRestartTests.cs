using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Sub2Api;

/// <summary>Actual persistence roundtrip не превращает сохранённый compact output в разрешённое окно.</summary>
public class Sub2ApiCompactSafetyRestartTests
{
    /// <summary>Canonical/provenance сохраняются, но новый DI отклоняет compact input до generation HTTP.</summary>
    [Theory]
    [InlineData("compaction")]
    [InlineData("compaction_summary")]
    [InlineData("compaction_trigger")]
    public async Task RestoredCompactItemCannotReachGenerationRecovery(string type)
    {
        // Arrange
        CatalogWriteFixture restarted;
        DialogWriteToken savedToken;
        string savedHistory;
        DateTimeOffset created;
        DateTimeOffset? expiry;
        await using (Sub2ApiRunFixture first = new())
        {
            DialogWriteToken token = (await first.Storage.CreateAsync()).Token;
            CanonicalModelItem item = new(JsonSerializer.SerializeToElement(new
            {
                type, encrypted_content = "synthetic-covered-prefix", future = new[] { 1, 2 }
            }));
            first.Transport.Enqueue([item, CatalogWriteFixture.Assistant("Первый ответ")]);
            AgentRunResult saved = await first.RunAsync(token, Guid.NewGuid(), "Полная исходная история",
                TestContext.Current.CancellationToken);
            Assert.Equal(AgentRunStatus.Completed, saved.Status);
            savedToken = saved.Token!;
            DialogSnapshot before = await first.Storage.SnapshotAsync(savedToken);
            Assert.Null(before.ActiveContext);
            Assert.Empty(first.Storage.Committed.Contexts);
            savedHistory = JsonSerializer.Serialize(before.Turns[0]);
            created = before.CreatedAtUtc;
            expiry = before.ExpiresAtUtc;
            restarted = first.RestartStorage();
        }

        await using Sub2ApiRunFixture next = new(restarted);

        // Act
        AgentRunResult result = await next.RunAsync(savedToken, Guid.NewGuid(), "Непокрытый хвост",
            TestContext.Current.CancellationToken);
        DialogSnapshot after = await restarted.SnapshotAsync(result.Token!);

        // Assert
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Empty(next.Transport.Bodies);
        Assert.Equal(0, next.HandlerCalls);
        Assert.Empty(restarted.Committed.Contexts);
        Assert.Null(after.ActiveContext);
        Assert.Equal(created, after.CreatedAtUtc);
        Assert.Equal(expiry, after.ExpiresAtUtc);
        Assert.Equal(savedHistory, JsonSerializer.Serialize(after.Turns[0]));
        ModelResponse restored = after.Turns[0].ModelSteps[0].Response;
        Assert.Equal("sub2api", restored.ConnectorProvenance!.ConnectorId);
        Assert.Equal(type, restored.Output[0].Content.GetProperty("type").GetString());
        Assert.Equal("synthetic-covered-prefix", restored.Output[0].Content.GetProperty("encrypted_content").GetString());
    }
}
