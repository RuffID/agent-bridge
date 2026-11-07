using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Нет обхода обязательных T05 guards через прямой gateway или выданные приложением settings.</summary>
public class Sub2ApiGatewayBoundaryTests
{
    /// <summary>JSON/SSE и compact отклоняются без HTTP/callback даже с opaque input и legacy continuation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockedGatewayCannotDispatchOrMutateCanonicalInput(bool streaming)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        using JsonDocument opaque = JsonDocument.Parse("""{"type":"reasoning","encrypted_content":"secret-opaque","future":true}""");
        using JsonDocument legacy = JsonDocument.Parse("""{"previous_response_id":"resp_legacy","turn_state":"secret-turn-state"}""");
        ModelRequest request = new("public-model", "medium", "instructions", [new(opaque.RootElement)], [], new(legacy.RootElement));
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), fixture.Owner, Guid.NewGuid(), "agent");
        IModelGateway gateway = fixture.Scope.ServiceProvider.GetRequiredService<IModelGateway>();
        int callbacks = 0;
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null;
        if (streaming)
        {
            callback = (_, _) =>
            {
                callbacks++;
                throw new IOException("callback must not run");
            };
        }

        // Act
        ServiceResult<ModelResponse> generation = await gateway.GenerateAsync(call, request, new("key"), callback,
            TestContext.Current.CancellationToken);
        ServiceResult<ModelResponse> compact = await gateway.CompactAsync(call, request, new("key"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Unsupported, generation.Error!.Type);
        Assert.Equal(ServiceErrorType.Unsupported, compact.Error!.Type);
        Assert.Null(generation.Data);
        Assert.Null(compact.Data);
        Assert.Equal(0, callbacks);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.True(JsonElement.DeepEquals(opaque.RootElement, Assert.Single(request.Input).Content));
        Assert.True(JsonElement.DeepEquals(legacy.RootElement, request.Continuation!.Content));
        Assert.DoesNotContain("secret-", generation.Error.Message + compact.Error.Message);
    }

    /// <summary>Отмена имеет приоритет перед Unsupported и не запускает transport.</summary>
    [Fact]
    public async Task BlockedGatewayPreservesCallerCancellation()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        using CancellationTokenSource caller = new();
        caller.Cancel();
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), fixture.Owner, Guid.NewGuid(), "agent");
        ModelRequest request = new("public-model", "medium", "instructions", [], []);
        IModelGateway gateway = fixture.Scope.ServiceProvider.GetRequiredService<IModelGateway>();

        // Act
        OperationCanceledException generation = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.GenerateAsync(call, request, new("key"), cancellationToken: caller.Token));
        OperationCanceledException compact = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.CompactAsync(call, request, new("key"), caller.Token));

        // Assert
        Assert.Equal(caller.Token, generation.CancellationToken);
        Assert.Equal(caller.Token, compact.CancellationToken);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
    }
}
