using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests.CompactSafety;

/// <summary>Actual core и gateway сохраняют полный контекст при отключённом compact и отказах.</summary>
public class Sub2ApiFullContextSafetyTests
{
    /// <summary>Повторный force не принимает окно, даже когда terminal prefix и хвост уже существуют.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RepeatedCompactionPreservesSnapshot(bool activeWindow, bool force)
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Sub2ApiAutomaticCompactionTests.Fixture state = new(http.Call, true, activeWindow);
        DialogSnapshot source = state.Dialog;
        state.Dialog = new(source.Token, source.OwnerId, source.CreatedAtUtc, DateTimeOffset.UtcNow.AddHours(1),
            1234, source.Turns, source.ActiveContext);
        DialogSnapshot original = state.Dialog;
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ContextCompactor compactor = new(new([]), state, http.Gateway, state, TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>());

        for (int attempt = 0; attempt < 2; attempt++)
        {
            // Act
            ServiceResult<ContextCompactionResult> result = await compactor.CompactAsync(http.Call, state.Dialog,
                http.Request, state.Settings, http.Access, TestContext.Current.CancellationToken, force);

            // Assert
            Assert.True(result.Success);
            Assert.Same(original.Token, result.Data!.Token);
            Assert.Same(original.ActiveContext, result.Data.ActiveContext);
            Assert.Same(original, state.Dialog);
            Assert.Equal(1234, state.Dialog.ContentBytes);
            Assert.Equal(original.ExpiresAtUtc, state.Dialog.ExpiresAtUtc);
            Assert.Equal(0, state.ContextSaves);
            Assert.Equal(0, http.FactoryCalls);
            Assert.Equal(0, http.Handler.Calls);
            if (force)
            {
                Assert.Equal(ContextCompactionStatus.Failed, result.Data.Status);
                Assert.Equal(ServiceErrorType.Unsupported, result.Data.Error!.Type);
            }
            else
            {
                Assert.Equal(ContextCompactionStatus.Disabled, result.Data.Status);
            }
        }
    }

    /// <summary>Родное сохранённое окно не попадает в server recovery и не заменяется одним хвостом.</summary>
    [Fact]
    public async Task RunnerRejectsOwnWindowBeforeEncryptedRecoveryCanDropCoveredContext()
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Sub2ApiAutomaticCompactionTests.Fixture state = new(http.Call, true, activeWindow: true);
        DialogSnapshot original = state.Dialog;
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AgentRunRequest request = new(http.Call, [], [], new(1, 1, 1, TimeSpan.FromMinutes(1)));

        // Act
        AgentRunResult result = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(0, http.FactoryCalls);
        Assert.Equal(0, http.Handler.Calls);
        Assert.Equal(0, state.ContextSaves);
        Assert.Same(original.ActiveContext, state.Dialog.ActiveContext);
        Assert.Equal(1, state.Dialog.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(original.CreatedAtUtc, state.Dialog.CreatedAtUtc);
        Assert.Equal(original.ExpiresAtUtc, state.Dialog.ExpiresAtUtc);
        Assert.Equal(original.Turns, state.Dialog.Turns.Take(original.Turns.Count));
    }

    /// <summary>HTTP200, malformed и EOF не разрешают compact, retry либо удаление истории.</summary>
    [Theory]
    [InlineData("completed", AgentRunStatus.Completed)]
    [InlineData("failed", AgentRunStatus.Failed)]
    [InlineData("malformed", AgentRunStatus.Failed)]
    [InlineData("eof", AgentRunStatus.Incomplete)]
    [InlineData("encrypted_error", AgentRunStatus.Failed)]
    public async Task FullInputSurvivesTransportOutcome(string outcome, AgentRunStatus expected)
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Sub2ApiAutomaticCompactionTests.Fixture state = new(http.Call, true);
        DialogSnapshot source = state.Dialog;
        state.Dialog = new(source.Token, source.OwnerId, source.CreatedAtUtc, DateTimeOffset.UtcNow.AddHours(1),
            1234, source.Turns, null);
        DialogSnapshot original = state.Dialog;
        string wire = Sub2ApiGenerationFixture.Frame("""{"type":"response.completed","response":{"status":"completed","output":[]}}""");
        if (outcome == "failed")
        {
            wire = Sub2ApiGenerationFixture.Frame("""{"type":"response.failed","response":{"status":"failed","output":[],"error":{"code":"server_error","message":"private-error"}}}""");
        }
        else if (outcome == "malformed")
        {
            wire = Sub2ApiGenerationFixture.Partial() + "data: {broken}\n\n";
        }
        else if (outcome == "eof")
        {
            wire = Sub2ApiGenerationFixture.Partial() + "data: [DONE]\n\n";
        }

        Sub2ApiTestStream stream = http.Reply(wire);
        if (outcome == "encrypted_error")
        {
            http.Handler.Respond = (_, _) => Task.FromResult(Sub2ApiCatalogFixture.Json(
                """{"error":{"code":"invalid_encrypted_content","message":"private-error"}}""", System.Net.HttpStatusCode.BadRequest));
        }

        JsonElement sent = default;
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply = http.Handler.Respond;
        http.Handler.Respond = async (request, ct) =>
        {
            sent = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            return await reply(request, ct);
        };
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        CanonicalModelItem current = new(JsonSerializer.Deserialize<JsonElement>("""{"type":"message","role":"user","content":"current"}"""));
        AgentRunRequest run = new(http.Call, [current], [], new(1, 1, 1, TimeSpan.FromMinutes(1)));

        // Act
        AgentRunResult result = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(run, (_, _) => ValueTask.CompletedTask, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Status);
        Assert.True(result.TerminalSaved);
        Assert.Equal(1, http.Handler.Calls);
        Assert.EndsWith("/v1/responses", Assert.Single(http.Handler.Urls));
        Assert.True(sent.GetProperty("stream").GetBoolean());
        JsonElement expectedInput = JsonSerializer.SerializeToElement(original.Turns[0].Items.Append(current).Select(item => item.Content));
        Assert.True(JsonElement.DeepEquals(expectedInput, sent.GetProperty("input")));
        Assert.Equal("function_call", sent.GetProperty("input")[2].GetProperty("type").GetString());
        Assert.Equal("function_call_output", sent.GetProperty("input")[3].GetProperty("type").GetString());
        Assert.Null(result.LastBudgetAssessment!.DeclaredInputContextWindow);
        Assert.True(result.LastBudgetAssessment.RequiresServerValidation);
        Assert.Null(state.Dialog.ActiveContext);
        Assert.Equal(0, state.ContextSaves);
        Assert.Same(original.Turns[0], state.Dialog.Turns[0]);
        Assert.Equal(original.CreatedAtUtc, state.Dialog.CreatedAtUtc);
        Assert.Equal(original.ExpiresAtUtc, state.Dialog.ExpiresAtUtc);
        Assert.DoesNotContain("private-error", result.Error?.Message ?? "");
        if (outcome != "encrypted_error")
        {
            Assert.True(stream.Disposed);
        }
    }

    /// <summary>Истёкший диалог отказывает до compact/generation и не продлевает retention.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredDialogNeverDispatchesOrSaves(bool force)
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Sub2ApiAutomaticCompactionTests.Fixture state = new(http.Call, true);
        DialogSnapshot source = state.Dialog;
        state.Dialog = new(source.Token, source.OwnerId, source.CreatedAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1),
            source.ContentBytes, source.Turns, null);
        DialogSnapshot original = state.Dialog;
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ContextCompactor compactor = new(new([]), state, http.Gateway, state, TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>());

        // Act
        ServiceResult<ContextCompactionResult> result = await compactor.CompactAsync(http.Call, state.Dialog,
            http.Request, state.Settings, http.Access, TestContext.Current.CancellationToken, force);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Expired, result.Error!.Type);
        Assert.Same(original, state.Dialog);
        Assert.Equal(0, state.ContextSaves);
        Assert.Equal(0, http.FactoryCalls);
        Assert.Equal(0, http.Handler.Calls);
    }
}
