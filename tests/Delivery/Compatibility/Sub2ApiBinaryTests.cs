using AgentBridge.Persistence.PostgreSql;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.BinaryCompatibility.Tests;

/// <summary>Новый consumer использует actual Sub2Api DLL и public facade без сети, host либо БД.</summary>
public class Sub2ApiBinaryTests
{
    /// <summary>Два offline BPE словаря доступны через actual facade и package closure поставки.</summary>
    [Theory]
    [InlineData("gpt-4.1", "o200k_base")]
    [InlineData("gpt-4", "cl100k_base")]
    public async Task EmbeddedBpeLoadsFromDeliveredAssemblies(string model, string encoding)
    {
        // Arrange
        using LocalHandler handler = new(false);
        using HttpClient client = new(handler);
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAgentBridgePostgreSql();
        services.AddAgentBridgeSub2ApiConnector();
        services.AddAgentBridge(Configuration(), _ => client);
        using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using IServiceScope scope = root.CreateScope();

        // Act
        ServiceResult<ContextTokenCount> result = await scope.ServiceProvider.GetRequiredService<IContextTokenCounter>()
            .CountAsync(new ModelRequest(model, null, "Hello world", [], []), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(encoding, result.Data!.Encoding);
        Assert.Equal(2, result.Data.KnownTokens);
        Assert.NotNull(result.Data.EstimatedInputTokens);
        Assert.False(result.Data.HasOpaqueContent);
        Assert.Empty(handler.Paths);
    }

    /// <summary>Binary graph выбирает manifest и JSON/SSE generation с полным tool pair без anchors и compact.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitConnectorUsesManifestAndFullContext(bool sse)
    {
        // Arrange
        using LocalHandler handler = new(sse);
        using HttpClient client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAgentBridgePostgreSql();
        services.AddAgentBridgeSub2ApiConnector();
        int factories = 0;
        IConfiguration configuration = Configuration();
        services.AddAgentBridge(configuration, _ =>
        {
            factories++;
            return client;
        });
        using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        root.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(0, factories);
        Assert.False(configuration.GetSection("CodexLb").Exists());
        using IServiceScope scope = root.CreateScope();
        DialogOwnerId owner = DialogOwnerId.From("binary-owner");
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), owner, Guid.NewGuid(), "binary-agent");
        ModelAccess access = new("synthetic-binary-key");

        // Act
        ServiceResult<ModelSettingsSnapshot> settings = await scope.ServiceProvider.GetRequiredService<IModelSettingsReader>()
            .ReadWithAccessAsync(owner, access, ct: TestContext.Current.CancellationToken);
        IModelGateway gateway = scope.ServiceProvider.GetRequiredService<IModelGateway>();
        ModelRequest first = new("gpt-4.1", "medium", "instructions", [Item("""{"type":"message","role":"user","content":"first"}""")], []);
        ServiceResult<ModelResponse> initial = await gateway.GenerateAsync(call, first, access, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(settings.Success, settings.Error?.Message);
        Assert.Null(settings.Data!.Model.InputContextWindow);
        Assert.True(initial.Success, initial.Error?.Message);
        Assert.Null(initial.Data!.Continuation);
        Assert.NotNull(initial.Data.ConnectorProvenance);
        Assert.False(gateway.AutomaticCompactionEnabled);
        Assert.Throws<InvalidOperationException>(() => root.GetRequiredService<IModelGateway>());

        // Arrange
        CanonicalModelItem toolOutput = Item("""{"type":"function_call_output","call_id":"call-1","output":"saved-result"}""");
        ModelRequest next = new(first.Model, first.ReasoningEffort, first.Instructions,
            [..first.Input, ..initial.Data.Output, toolOutput, Item("""{"type":"message","role":"user","content":"next"}""")], []);
        next = next.WithConnectorProvenance(initial.Data.ConnectorProvenance!);

        // Act
        ServiceResult<ModelResponse> continued = await gateway.GenerateAsync(call, next, access, cancellationToken: TestContext.Current.CancellationToken);
        ServiceResult<ModelResponse> compact = await gateway.CompactAsync(call, next, access, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(continued.Success, continued.Error?.Message);
        Assert.Equal(ModelResponseStatus.Completed, continued.Data!.Status);
        Assert.False(compact.Success);
        Assert.Equal(ServiceErrorType.Unsupported, compact.Error!.Type);
        Assert.Equal(1, factories);
        Assert.Equal(new[] { "/prefix/backend-api/codex/models", "/prefix/v1/responses", "/prefix/v1/responses" }, handler.Paths);
        JsonElement sent = handler.Bodies[1];
        Assert.False(sent.TryGetProperty("previous_response_id", out _));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(next.Input.Select(item => item.Content)), sent.GetProperty("input")));
    }

    /// <summary>Содержит только синтетические in-memory значения и explicit unknown-window policy.</summary>
    private static IConfiguration Configuration()
    {
        Dictionary<string, string?> values = new()
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "5", ["AgentBridge:Agent:InstructionsSource"] = "Configuration",
            ["AgentBridge:Agent:Instructions"] = "instructions", ["AgentBridge:Retention:SoftContentLimitBytes"] = "10485760",
            ["AgentBridge:Compaction:TokenThreshold"] = "24000", ["AgentBridge:Compaction:InputTokenReserve"] = "0",
            ["AgentBridge:Compaction:MaxPasses"] = "3", ["AgentBridge:Compaction:BudgetPolicy"] = "ServerValidationWithUnknownWindow",
            ["Sub2Api:BaseAddress"] = "https://binary.invalid/prefix/v1/", ["Sub2Api:Model"] = "gpt-4.1",
            ["Sub2Api:ReasoningEffort"] = "medium", ["Sub2Api:KeySource"] = "Shared",
            ["Sub2Api:SharedApiKey"] = "synthetic-binary-key", ["Sub2Api:GenerationTimeout"] = "00:00:30",
            ["Sub2Api:CompactTimeout"] = "00:00:30", ["Database:Provider"] = "PostgreSql",
            ["Database:ConnectionString"] = "Server=never-open.invalid;Database=synthetic;Integrated Security=true;ConnectRetryCount=0"
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>Clone не сохраняет ссылку на disposed JsonDocument.</summary>
    private static CanonicalModelItem Item(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new(document.RootElement);
    }

    /// <summary>Отвечает локально; запрет неизвестного URL исключает скрытый fallback.</summary>
    private class LocalHandler(bool sse) : HttpMessageHandler
    {
        internal List<string> Paths { get; } = [];
        internal List<JsonElement> Bodies { get; } = [];

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer synthetic-binary-key", request.Headers.Authorization?.ToString());
            Assert.Empty(request.RequestUri!.Query);
            string path = request.RequestUri.AbsolutePath;
            Paths.Add(path);
            if (path == "/prefix/backend-api/codex/models")
            {
                return Json("""{"models":[{"slug":"gpt-4.1","supported_in_api":true,"supported_reasoning_levels":[{"effort":"medium"}]}]}""");
            }

            Assert.Equal("/prefix/v1/responses", path);
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Bodies.Add(body.RootElement.Clone());
            const string RESPONSE = """{"id":"echo","status":"completed","output":[{"type":"reasoning","encrypted_content":"synthetic-opaque","future":true},{"type":"function_call","call_id":"call-1","name":"probe","arguments":"{}"}]}""";
            if (sse)
            {
                return new(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("data: {\"type\":\"response.completed\",\"response\":" + RESPONSE + "}\n\n", Encoding.UTF8, "text/event-stream")
                };
            }

            return Json(RESPONSE);
        }

        /// <summary>Не использует сетевой handler и возвращает новый response с отдельным content.</summary>
        private static HttpResponseMessage Json(string value)
        {
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
        }
    }
}
