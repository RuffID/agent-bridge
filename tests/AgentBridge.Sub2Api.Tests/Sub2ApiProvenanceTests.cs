using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Actual gateway и runner отклоняют чужой контекст до HTTP и принятия нового turn.</summary>
public class Sub2ApiProvenanceTests
{
    /// <summary>Unknown/foreign/binding mismatch не создают HTTP-клиент и не вызывают callback.</summary>
    [Theory]
    [InlineData("unknown", ServiceErrorType.Unsupported)]
    [InlineData("foreign", ServiceErrorType.Unsupported)]
    [InlineData("binding", ServiceErrorType.Conflict)]
    public async Task DirectGatewayRejectsBeforeFactory(string source, ServiceErrorType expected)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        ModelConnectorProvenance own = ((IModelContextGateway)fixture.Gateway).GetContextProvenance(fixture.Call, fixture.Access);
        ModelRequest request = new("public-model", "medium", "instructions", [Opaque()], []);
        if (source == "foreign")
        {
            request = request.WithConnectorProvenance(new("codex-lb", own.Binding));
        }
        else if (source == "binding")
        {
            request = request.WithConnectorProvenance(new("sub2api", new string('0', 64)));
        }

        int updates = 0;

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, request, fixture.Access,
            (_, _) => { updates++; return ValueTask.CompletedTask; }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Error!.Type);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.Equal(0, updates);
        Assert.DoesNotContain(own.Binding, string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Родной synthetic marker разрешает JSON/SSE, оставаясь вне полного wire body.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnProvenanceStaysOutsideWire(bool streaming)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        ModelConnectorProvenance own = ((IModelContextGateway)fixture.Gateway).GetContextProvenance(fixture.Call, fixture.Access);
        ModelRequest request = new ModelRequest("public-model", "medium", "instructions", [Opaque()], [])
            .WithConnectorProvenance(own);
        string? sent = null;
        fixture.Handler.Respond = async (http, ct) =>
        {
            sent = await http.Content!.ReadAsStringAsync(ct);
            return Sub2ApiCatalogFixture.Json(Sub2ApiGenerationFixture.COMPLETED);
        };
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null;
        if (streaming)
        {
            fixture.Reply(Sub2ApiGenerationFixture.Terminal());
            callback = (_, _) => ValueTask.CompletedTask;
        }

        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply = fixture.Handler.Respond!;
        fixture.Handler.Respond = async (http, ct) =>
        {
            sent = await http.Content!.ReadAsStringAsync(ct);
            return await reply(http, ct);
        };

        // Act
        ServiceResult<ModelResponse> result = await fixture.Gateway.GenerateAsync(fixture.Call, request, fixture.Access,
            callback, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ModelResponseStatus.Completed, result.Data!.Status);
        Assert.Equal(own.Binding, result.Data.ConnectorProvenance!.Binding);
        Assert.Null(result.Data.Continuation);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.NotNull(sent);
        Assert.DoesNotContain(own.Binding, sent);
        Assert.DoesNotContain("ConnectorProvenance", sent);
        Assert.DoesNotContain("\"connector\"", sent);
    }

    /// <summary>Родное окно не разрешает чужой tail даже при одинаковой selected модели.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunnerRejectsForeignOrUnknownHistoryBeforeBegin(bool unknown)
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiAutomaticCompactionTests.Fixture state = new(fixture.Call, true, activeWindow: true);
        DialogSnapshot original = state.Dialog;
        ModelResponse response = ModelResponse.Completed([Opaque()]);
        if (!unknown)
        {
            response = response.WithConnectorProvenance(new("codex-lb", new string('A', 64)));
        }

        StoredDialogTurn foreign = new(Guid.NewGuid(), 2, DialogTurnStatus.Completed,
            response.Output, [new(Guid.NewGuid(), response)], TurnModelSettings.From(state.Settings));
        state.Dialog = new(original.Token, original.OwnerId, original.CreatedAtUtc, original.ExpiresAtUtc, 0,
            [original.Turns[0], foreign], original.ActiveContext);
        DialogSnapshot before = state.Dialog;
        await using ServiceProvider root = state.Services(fixture.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AgentRunRequest run = new(fixture.Call, [], [], new(1, 1, 1, TimeSpan.FromMinutes(1)));

        // Act
        AgentRunResult result = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(run, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(0, state.Begins);
        Assert.Equal(0, state.ContextSaves);
        Assert.Same(before, state.Dialog);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Opaque новый input отказывает до Begin и не получает метку родной истории.</summary>
    [Fact]
    public async Task RunnerRejectsNewOpaqueInputBeforeBegin()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiAutomaticCompactionTests.Fixture state = new(fixture.Call, true);
        DialogSnapshot original = state.Dialog;
        await using ServiceProvider root = state.Services(fixture.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AgentRunRequest run = new(fixture.Call, [Opaque()], [], new(1, 1, 1, TimeSpan.FromMinutes(1)));

        // Act
        AgentRunResult result = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(run, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(0, state.Begins);
        Assert.Same(original, state.Dialog);
        Assert.Equal(0, fixture.FactoryCalls);
    }

    /// <summary>Standalone compact проверяет историю до маркировки, даже если request уже имеет родной marker.</summary>
    [Fact]
    public async Task StandaloneCompactorDoesNotTrustForeignHistory()
    {
        // Arrange
        using Sub2ApiGenerationFixture fixture = new();
        Sub2ApiAutomaticCompactionTests.Fixture state = new(fixture.Call, true);
        DialogSnapshot original = state.Dialog;
        ModelResponse foreign = ModelResponse.Completed([Opaque()]).WithConnectorProvenance(new("codex-lb", new string('A', 64)));
        state.Dialog = new(original.Token, original.OwnerId, original.CreatedAtUtc, null, 0, [], new(1, 0, foreign, "public-model"));
        await using ServiceProvider root = state.Services(fixture.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ContextCompactor compactor = new(new([]), state, fixture.Gateway, state, TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>());
        ModelRequest request = fixture.Request.WithConnectorProvenance(
            ((IModelContextGateway)fixture.Gateway).GetContextProvenance(fixture.Call, fixture.Access));

        // Act
        ServiceResult<ContextCompactionResult> result = await compactor.CompactAsync(fixture.Call, state.Dialog, request,
            state.Settings, fixture.Access, TestContext.Current.CancellationToken, force: true);

        // Assert
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, state.ContextSaves);
        Assert.Same(foreign, state.Dialog.ActiveContext!.Compaction);
    }

    /// <summary>Синтетический opaque item без wire преобразований.</summary>
    private static CanonicalModelItem Opaque()
    {
        return new(JsonSerializer.Deserialize<JsonElement>("""{"type":"reasoning","encrypted_content":"opaque"}"""));
    }
}
