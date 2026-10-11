using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual AgentRunner/ContextBuilder/ToolExecutor и persistence public API, doubles только I/O/base storage.</summary>
internal class CatalogRunnerFixture : IAsyncDisposable
{
    internal static readonly ModelToolDefinition TOOL = new("action", "test action",
        JsonSerializer.SerializeToElement(new { type = "object" }), true);
    public CatalogWriteFixture Storage { get; }
    public ServiceProvider Services { get; }
    public Queue<ModelResponse> Responses { get; } = new();
    public List<ModelRequest> Requests { get; } = [];
    public int HandlerCalls { get; private set; }
    public int ProviderCalls { get; private set; }
    public Func<ToolInvocation, CancellationToken, Task<ServiceResult<ToolOutput>>> HandlerAction { get; set; } =
        (_, _) => Task.FromResult(ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { confirmed = "UNCHANGED" }))));
    public Func<CancellationToken, Task<ServiceResult<ModelResponse>>>? Generate { get; set; }
    public string PageSnapshot { get; set; } = "page-v1";

    public CatalogRunnerFixture(CatalogWriteFixture? storage = null)
    {
        Storage = storage ?? new();
        ServiceCollection services = new();
        services.AddSingleton(this);
        services.AddSingleton<TimeProvider>(Storage.Time);
        services.AddSingleton<IDialogReader, Reader>();
        services.AddSingleton(Storage.Writer);
        services.AddSingleton(Storage.Attempts);
        services.AddSingleton(Storage.ContextWriter);
        services.AddSingleton(Storage.Lifecycle);
        services.AddSingleton<IModelSettingsReader, Settings>();
        services.AddSingleton<IModelAccessResolver, Access>();
        services.AddSingleton<IModelGateway, Gateway>();
        services.AddSingleton<IContextTokenCounter, Counter>();
        services.AddSingleton<IContextProvider, Provider>();
        services.AddScoped<ContextBuilder>(provider => new([provider.GetRequiredService<IContextProvider>()]));
        services.Configure<AgentOptions>(options =>
        {
            options.InstructionsSource = AgentInstructionsSource.Configuration;
            options.Instructions = "instructions";
            options.MaxToolSteps = 8;
        });
        services.Configure<ContextCompactionOptions>(options => options.MaxPasses = 2);
        services.AddAgentBridgeTool<Handler, Validator>(TOOL);
        services.AddAgentBridgeRunner();
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public async Task<AgentRunResult> RunAsync(DialogWriteToken token, Guid turnId, CancellationToken ct)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(new(
            new(token.DialogId, CatalogWriteFixture.SCOPE.OwnerId, turnId, "agent"),
            [CatalogWriteFixture.Question("Новый вопрос")], [TOOL.Name], new(8, 8, 1, TimeSpan.FromSeconds(2)),
            new DialogRunOptions(CatalogWriteFixture.SCOPE, CatalogWriteFixture.PROFILE, TimeSpan.FromMinutes(1))),
            cancellationToken: ct);
    }

    public ValueTask DisposeAsync() => Services.DisposeAsync();

    /// <inheritdoc/>
    private class Reader(CatalogRunnerFixture fixture) : IDialogReader
    {
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
        {
            fixture.Storage.Reload();
            return fixture.Storage.Reader.ReadAsync(access, cancellationToken);
        }

        public Task<ServiceResult<DialogSnapshot>> ReadCatalogAsync(DialogCatalogAccess access, CancellationToken cancellationToken = default)
        {
            fixture.Storage.Reload();
            return fixture.Storage.Reader.ReadCatalogAsync(access, cancellationToken);
        }
    }

    /// <inheritdoc/>
    private class Settings : IModelSettingsReader
    {
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null,
            string? effort = null, CancellationToken ct = default) => throw new InvalidOperationException("Unpinned access.");
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access,
            string? model = null, string? effort = null, CancellationToken ct = default) => Task.FromResult(ServiceResult<ModelSettingsSnapshot>.Ok(
                new(new("gpt-5", true, 1000, 1000, 100, ["high"], "high", ["text"], true, true, true, true, false), "high", 900, 10)));
    }

    /// <inheritdoc/>
    private class Access : IModelAccessResolver
    {
        public Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ModelAccess>.Ok(new("isolated-key")));
    }

    /// <inheritdoc/>
    private class Counter : IContextTokenCounter
    {
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ContextTokenCount>.Ok(new("isolated", request.Input.Count, request.Input.Count, false)));
    }

    /// <inheritdoc/>
    private class Provider(CatalogRunnerFixture fixture) : IContextProvider
    {
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default)
        {
            fixture.ProviderCalls++;
            return Task.FromResult(ServiceResult<ContextContribution>.Ok(new([
                new(JsonSerializer.SerializeToElement(new { type = "message", role = "developer", content = fixture.PageSnapshot }))
            ])));
        }
    }

    /// <inheritdoc/>
    private class Gateway(CatalogRunnerFixture fixture) : IModelGateway
    {
        public Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
        {
            fixture.Requests.Add(request);
            return fixture.Generate?.Invoke(cancellationToken) ??
                Task.FromResult(ServiceResult<ModelResponse>.Ok(fixture.Responses.Dequeue()));
        }

        public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected compact in bounded test.");
    }

    /// <inheritdoc/>
    private class Handler(CatalogRunnerFixture fixture) : IToolHandler
    {
        public ModelToolDefinition Definition => TOOL;
        public Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            fixture.HandlerCalls++;
            Assert.Contains(fixture.Storage.Committed.Steps, step =>
            {
                using JsonDocument journal = JsonDocument.Parse(step.ToolAttemptsJson!);
                return journal.RootElement.GetProperty("attempts").EnumerateArray().Any(item => item.GetProperty("state").GetInt32() == 0);
            });
            return fixture.HandlerAction(invocation, cancellationToken);
        }
    }

    /// <inheritdoc/>
    private class Validator : IToolInvocationValidator
    {
        public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult.Ok());
    }
}
