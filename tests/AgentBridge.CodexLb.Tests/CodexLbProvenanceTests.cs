using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Responses;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.CodexLb.Tests;

/// <summary>Прямые вызовы gateway проверяют происхождение вне protocol fixture маркировки.</summary>
public class CodexLbProvenanceTests
{
    /// <summary>Generation и compact отказывают до HTTP для unknown/foreign/binding mismatch.</summary>
    [Theory]
    [InlineData(false, "unknown", ServiceErrorType.Unsupported)]
    [InlineData(true, "unknown", ServiceErrorType.Unsupported)]
    [InlineData(false, "foreign", ServiceErrorType.Unsupported)]
    [InlineData(true, "foreign", ServiceErrorType.Unsupported)]
    [InlineData(false, "binding", ServiceErrorType.Conflict)]
    [InlineData(true, "binding", ServiceErrorType.Conflict)]
    [InlineData(false, "own", null)]
    [InlineData(true, "own", null)]
    public async Task DirectGatewayChecksProvenance(bool compact, string source, ServiceErrorType? expected)
    {
        // Arrange
        Handler handler = new();
        using HttpClient http = new(handler);
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCodexLbConfiguration(options =>
        {
            options.KeySource = ModelKeySourceMode.Shared;
            options.SharedApiKey = "synthetic-key";
            options.BaseAddress = "https://gateway.invalid";
            options.Model = "same-model";
            options.ReasoningEffort = "medium";
            options.GenerationTimeout = TimeSpan.FromSeconds(5);
            options.CompactTimeout = TimeSpan.FromSeconds(5);
        });
        services.AddCodexLbResponses(_ => http);
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        IModelGateway gateway = scope.ServiceProvider.GetRequiredService<IModelGateway>();
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        ModelAccess access = new("synthetic-key");
        ModelConnectorProvenance own = ((IModelContextGateway)gateway).GetContextProvenance(call, access);
        CanonicalModelItem item = new(JsonSerializer.Deserialize<JsonElement>("""{"type":"reasoning","encrypted_content":"opaque"}"""));
        ModelRequest request = new("same-model", "medium", "instructions", [item], []);
        if (source == "own")
        {
            request = request.WithConnectorProvenance(own);
        }
        else if (source == "foreign")
        {
            request = request.WithConnectorProvenance(new("sub2api", own.Binding));
        }
        else if (source == "binding")
        {
            request = request.WithConnectorProvenance(new("codex-lb", new string('0', 64)));
        }

        handler.Compact = compact;

        // Act
        ServiceResult<ModelResponse> result;
        if (compact)
        {
            result = await gateway.CompactAsync(call, request, access, TestContext.Current.CancellationToken);
        }
        else
        {
            result = await gateway.GenerateAsync(call, request, access, cancellationToken: TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(expected, result.Error?.Type);
        if (expected is not null)
        {
            Assert.False(result.Success);
            Assert.Equal(0, handler.Calls);
        }
        else
        {
            Assert.True(result.Success);
            Assert.Equal(1, handler.Calls);
            Assert.Equal(own.Binding, result.Data!.ConnectorProvenance!.Binding);
            Assert.DoesNotContain(own.Binding, handler.Body!);
            Assert.DoesNotContain("ConnectorProvenance", handler.Body!);
        }
    }

    /// <summary>Изолированный HTTP transport без сети.</summary>
    private class Handler : HttpMessageHandler
    {
        internal bool Compact;
        internal int Calls;
        internal string? Body;

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            string json = """{"id":"response","status":"completed","output":[]}""";
            if (Compact)
            {
                json = """{"object":"response.compaction","output":[{"type":"compaction","encrypted_content":"compact"}]}""";
            }

            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
