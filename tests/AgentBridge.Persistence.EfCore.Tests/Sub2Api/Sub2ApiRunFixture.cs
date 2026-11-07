using System.Text.Json;
using System.Text.Json.Serialization;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Integration;
using AgentBridge.Persistence.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Sub2Api;

/// <summary>Настоящий facade/runner/transport/storage с локальными HTTP и base repository границами.</summary>
internal class Sub2ApiRunFixture : IAsyncDisposable
{
    internal static readonly ModelToolDefinition TOOL = new("action", "test action",
        JsonSerializer.SerializeToElement(new { type = "object" }), true);
    internal CatalogWriteFixture Storage { get; }
    internal ServiceProvider Services { get; }
    internal IConfigurationRoot Configuration { get; }
    internal Sub2ApiRunTransport Transport { get; } = new();
    internal HttpClient Client { get; }
    internal string Key { get; set; } = "synthetic-pinned-key";
    internal int KeyCalls { get; private set; }
    internal int ProviderCalls { get; private set; }
    internal int HandlerCalls { get; private set; }
    internal int FactoryCalls { get; private set; }
    internal Func<CancellationToken, Task<ServiceResult<ToolOutput>>>? Handle { get; set; }

    /// <summary>Создаёт независимый контейнер без host, соединения и подмены model gateway.</summary>
    internal Sub2ApiRunFixture(CatalogWriteFixture? storage = null)
    {
        Storage = storage ?? new(loadLimits: new(256, 1048576, 128));
        Client = new(Transport) { Timeout = Timeout.InfiniteTimeSpan };
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "8",
            ["AgentBridge:Agent:InstructionsSource"] = "Configuration",
            ["AgentBridge:Agent:Instructions"] = "isolated instructions",
            ["AgentBridge:Retention:RetentionPeriod"] = "1.00:00:00",
            ["AgentBridge:Retention:SoftContentLimitBytes"] = "1048576",
            ["AgentBridge:Compaction:TokenThreshold"] = "1",
            ["AgentBridge:Compaction:InputTokenReserve"] = "0",
            ["AgentBridge:Compaction:MaxPasses"] = "2",
            ["AgentBridge:Compaction:BudgetPolicy"] = "ServerValidationWithUnknownWindow",
            ["Sub2Api:BaseAddress"] = "https://gateway.invalid/prefix/v1/",
            ["Sub2Api:Model"] = "gpt-5",
            ["Sub2Api:ReasoningEffort"] = "medium",
            ["Sub2Api:KeySource"] = "Individual",
            ["Sub2Api:GenerationTimeout"] = "00:00:05",
            ["Sub2Api:CompactTimeout"] = "00:00:05",
            ["Database:Provider"] = "SqlServer",
            ["Database:ConnectionString"] = "Server=never-open.invalid;Database=synthetic;Integrated Security=True;Encrypt=True"
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(this);
        services.AddSingleton<TimeProvider>(Storage.Time);
        services.AddSingleton<IDialogReader, Reader>();
        services.AddSingleton<IBoundedDialogReader>(provider => (IBoundedDialogReader)provider.GetRequiredService<IDialogReader>());
        services.AddSingleton(Storage.Writer);
        services.AddSingleton(Storage.Attempts);
        services.AddSingleton(Storage.ContextWriter);
        services.AddSingleton(Storage.Lifecycle);
        services.AddScoped<IIndividualModelKeySource, KeySource>();
        services.AddScoped<IContextProvider, Provider>();
        services.AddAgentBridgeTool<Handler, Validator>(TOOL);
        services.AddAgentBridgeSqlServer();
        services.AddAgentBridgeSub2ApiConnector();
        services.AddAgentBridge(Configuration, _ =>
        {
            FactoryCalls++;
            return Client;
        });
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Services.GetRequiredService<IStartupValidator>().Validate();
    }

    /// <summary>Запускает registered bounded turn через публичный AgentRunner.</summary>
    internal async Task<AgentRunResult> RunAsync(DialogWriteToken token, Guid turnId, string question = "Вопрос",
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AgentRunRequest request = new(
            new(token.DialogId, CatalogWriteFixture.SCOPE.OwnerId, turnId, "agent"),
            [CatalogWriteFixture.Question(question)], [TOOL.Name], new(8, 8, 1, TimeSpan.FromSeconds(2)),
            new DialogRunOptions(CatalogWriteFixture.SCOPE, CatalogWriteFixture.PROFILE, TimeSpan.FromMinutes(1)));
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunBoundedAsync(request,
            cancellationToken: cancellationToken);
    }

    /// <summary>Сериализует committed данные и восстанавливает новые списки для следующего экземпляра.</summary>
    internal CatalogWriteFixture RestartStorage()
    {
        string json = JsonSerializer.Serialize(Storage.Committed);
        CatalogWriteFixture.Database restored = JsonSerializer.Deserialize<CatalogWriteFixture.Database>(json,
            new JsonSerializerOptions { PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate })!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.NotSame(Storage.Committed.Roots, restored.Roots);
        Assert.NotSame(Storage.Committed.Roots[0], restored.Roots[0]);
        return new(restored, loadLimits: new(256, 1048576, 128));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        Client.Dispose();
    }

    /// <inheritdoc/>
    private class Reader(Sub2ApiRunFixture fixture) : IBoundedDialogReader
    {
        public DialogHistoryLoadLimits LoadLimits => ((IBoundedDialogReader)fixture.Storage.Reader).LoadLimits;

        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
        {
            fixture.Storage.Reload();
            return fixture.Storage.Reader.ReadAsync(access, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadCatalogAsync(DialogCatalogAccess access, CancellationToken cancellationToken = default)
        {
            fixture.Storage.Reload();
            return fixture.Storage.Reader.ReadCatalogAsync(access, cancellationToken);
        }
    }

    /// <inheritdoc/>
    private class KeySource(Sub2ApiRunFixture fixture) : IIndividualModelKeySource
    {
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            fixture.KeyCalls++;
            return Task.FromResult<string?>(fixture.Key);
        }
    }

    /// <inheritdoc/>
    private class Provider(Sub2ApiRunFixture fixture) : IContextProvider
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default)
        {
            fixture.ProviderCalls++;
            return Task.FromResult(ServiceResult<ContextContribution>.Ok(new([
                new(JsonSerializer.SerializeToElement(new { type = "message", role = "developer", content = "page-snapshot" }))
            ])));
        }
    }

    /// <inheritdoc/>
    private class Handler(Sub2ApiRunFixture fixture) : IToolHandler
    {
        public ModelToolDefinition Definition => TOOL;

        /// <inheritdoc/>
        public Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            fixture.HandlerCalls++;
            ModelStepRecordCheckpoint(fixture);
            if (fixture.Handle is not null)
            {
                return fixture.Handle(cancellationToken);
            }

            return Task.FromResult(ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { receipt = "business-result" }))));
        }

        /// <summary>Проверяет сохранённую Started-позицию до выполнения бизнес-действия.</summary>
        private static void ModelStepRecordCheckpoint(Sub2ApiRunFixture fixture)
        {
            Assert.Contains(fixture.Storage.Committed.Steps, step =>
            {
                using JsonDocument journal = JsonDocument.Parse(step.ToolAttemptsJson!);
                return journal.RootElement.GetProperty("attempts").EnumerateArray()
                    .Any(item => item.GetProperty("state").GetInt32() == 0);
            });
        }
    }

    /// <inheritdoc/>
    private class Validator : IToolInvocationValidator
    {
        /// <inheritdoc/>
        public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult.Ok());
        }
    }
}
