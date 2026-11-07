using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Models;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Sub2Api;

/// <summary>Полный public AgentRunner путь с actual Sub2Api и durable checkpoint над isolated storage.</summary>
public class Sub2ApiRunFlowTests
{
    /// <summary>Несколько turns сохраняют opaque/history/tools без compact и continuation, JSON и SSE проходят один pipeline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullContextAndBusinessCheckpointSurviveSeveralTurns(bool sse)
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync()).Token;
        CanonicalModelItem opaque = new(JsonSerializer.SerializeToElement(new
        {
            type = "reasoning", encrypted_content = "synthetic-opaque", summary = Array.Empty<object>()
        }));
        fixture.Transport.Enqueue([opaque, CatalogWriteFixture.Call("call-one")], sse: sse);
        fixture.Transport.Enqueue([CatalogWriteFixture.Assistant("Первый ответ")], sse: sse);
        fixture.Transport.Enqueue([CatalogWriteFixture.Assistant("Второй ответ")], sse: sse);

        // Act
        AgentRunResult first = await fixture.RunAsync(token, Guid.NewGuid(), "Первый вопрос", TestContext.Current.CancellationToken);
        AgentRunResult second = await fixture.RunAsync(first.Token!, Guid.NewGuid(), "Второй вопрос", TestContext.Current.CancellationToken);
        DialogSnapshot saved = await fixture.Storage.SnapshotAsync(second.Token!);

        // Assert
        Assert.True(first.Status == AgentRunStatus.Completed, first.Error?.Message);
        Assert.True(second.Status == AgentRunStatus.Completed, second.Error?.Message);
        Assert.True(first.TerminalSaved);
        Assert.True(second.TerminalSaved);
        Assert.Equal(1, fixture.HandlerCalls);
        Assert.Equal(2, fixture.KeyCalls);
        Assert.Equal(2, fixture.ProviderCalls);
        Assert.Equal(2, fixture.Transport.CatalogCalls);
        Assert.Equal(3, fixture.Transport.Bodies.Count);
        Assert.Null(saved.ActiveContext);
        Assert.Empty(fixture.Storage.Committed.Contexts);
        Assert.Equal(DialogReadiness.Ready, saved.Continuation!.Readiness);
        Assert.Equal(2, saved.Turns.Count);
        Assert.Equal(ToolAttemptState.Succeeded, Assert.Single(saved.Turns[0].ModelSteps[0].ToolAttempts).State);
        Assert.All(saved.Turns.SelectMany(turn => turn.ModelSteps), step =>
        {
            Assert.Equal("sub2api", step.Response.ConnectorProvenance!.ConnectorId);
            Assert.Null(step.Response.Continuation);
        });
        Assert.Equal(ContextBudgetPolicy.ServerValidationWithUnknownWindow, saved.Turns[0].Settings!.BudgetPolicy);
        Assert.Null(saved.Turns[0].Settings!.DeclaredInputContextWindow);
        Assert.True(first.LastBudgetAssessment!.ThresholdReached);
        Assert.True(first.LastBudgetAssessment.RequiresServerValidation);
        Assert.Null(first.LastBudgetAssessment.DeclaredInputContextWindow);
        AssertWire(fixture.Transport.Bodies[1]);
        AssertWire(fixture.Transport.Bodies[2]);
        string[] questions = fixture.Transport.Bodies[2].GetProperty("input").EnumerateArray()
            .Where(item => item.TryGetProperty("role", out JsonElement role) && role.GetString() == "user")
            .Select(item => JsonSerializer.Deserialize<JsonElement>(item.GetProperty("content").GetString()!)
                .GetProperty("question").GetString()!).ToArray();
        Assert.Equal(["Первый вопрос", "Второй вопрос"], questions);
        Assert.All(fixture.Transport.Responses, body => Assert.True(body.Disposed));
    }

    /// <summary>Reload конфигурации и ключа в handler не меняет atomic настройки текущего run.</summary>
    [Fact]
    public async Task AccessModelEffortAndEndpointStayPinnedInsideRun()
    {
        // Arrange
        await using Sub2ApiRunFixture fixture = new();
        DialogWriteToken token = (await fixture.Storage.CreateAsync()).Token;
        fixture.Storage.Committed.Settings.Add(new DialogSettingsRecord
        {
            Id = token.DialogId.Value, Version = 1, Model = "gpt-5", Effort = "medium"
        });
        fixture.Transport.Enqueue([CatalogWriteFixture.Call("call-one")]);
        fixture.Transport.Enqueue([CatalogWriteFixture.Assistant("Готово")]);
        fixture.Handle = _ =>
        {
            fixture.Key = "synthetic-reloaded-key";
            fixture.Configuration["Sub2Api:Model"] = "reloaded-model";
            fixture.Configuration["Sub2Api:ReasoningEffort"] = "high";
            fixture.Configuration["Sub2Api:BaseAddress"] = "https://other.invalid/";
            fixture.Configuration.Reload();
            // Независимая запись приложения имитируется на committed границе, без изменения revision активного run.
            DialogSettingsRecord selected = fixture.Storage.Committed.Settings[0];
            selected.Version = 2;
            selected.Model = "gpt-4.1";
            selected.Effort = "high";
            return Task.FromResult(ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { receipt = "business-result" }))));
        };

        // Act
        AgentRunResult result = await fixture.RunAsync(token, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Status == AgentRunStatus.Completed, result.Error?.Message);
        Assert.Equal(1, fixture.KeyCalls);
        Assert.Equal(1, fixture.ProviderCalls);
        Assert.Equal(1, fixture.HandlerCalls);
        Assert.Equal(1, fixture.Transport.CatalogCalls);
        Assert.Equal(2, fixture.Transport.Bodies.Count);
        Assert.All(fixture.Transport.Authorizations, value => Assert.Equal("Bearer synthetic-pinned-key", value));
        Assert.All(fixture.Transport.Urls, value => Assert.StartsWith("https://gateway.invalid/prefix/v1/", value));
        Assert.All(fixture.Transport.Bodies, body =>
        {
            Assert.Equal("gpt-5", body.GetProperty("model").GetString());
            Assert.Equal("medium", body.GetProperty("reasoning").GetProperty("effort").GetString());
        });
        DialogSnapshot saved = await fixture.Storage.SnapshotAsync(result.Token!);
        Assert.Equal("gpt-5", saved.Turns[0].Settings!.Model);
        Assert.Equal("medium", saved.Turns[0].Settings!.Effort);
        Assert.Equal("gpt-4.1", saved.Selection!.Model);
        Assert.Equal("high", saved.Selection.Effort);
        Assert.Equal(2, saved.Selection.Version);
    }

    /// <summary>Сверяет canonical opaque и tool pair в полном wire input без локального marker.</summary>
    internal static void AssertWire(JsonElement body)
    {
        Assert.False(body.TryGetProperty("previous_response_id", out _));
        Assert.DoesNotContain("binding", body.GetRawText());
        Assert.DoesNotContain("connector", body.GetRawText());
        JsonElement[] input = body.GetProperty("input").EnumerateArray().ToArray();
        Assert.Contains(input, item => item.TryGetProperty("encrypted_content", out JsonElement content)
            && content.GetString() == "synthetic-opaque");
        Assert.Single(input, item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "function_call");
        JsonElement output = Assert.Single(input, item => item.TryGetProperty("type", out JsonElement type)
            && type.GetString() == "function_call_output");
        Assert.Equal("call-one", output.GetProperty("call_id").GetString());
        Assert.Contains("business-result", output.GetRawText());
    }
}
