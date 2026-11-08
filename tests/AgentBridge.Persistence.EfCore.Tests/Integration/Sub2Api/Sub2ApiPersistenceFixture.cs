using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Integration;
using AgentBridge.Persistence.EfCore.Repositories;
using AgentBridge.Persistence.EfCore.Tests.Sub2Api;
using AgentBridge.Persistence.EfCore.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration.Sub2Api;

/// <summary>Actual facade и persistence над собственной PostgreSQL; подменяет только HTTP и инструмент приложения.</summary>
internal class Sub2ApiPersistenceFixture : IAsyncDisposable
{
    internal static readonly DateTimeOffset NOW = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
    internal static readonly DialogCatalogScope SCOPE = new(DialogOwnerId.From("restart-owner"), "restart-site", "agent");
    internal static readonly CatalogAccessProfile PROFILE = new("restart-profile", 1,
        JsonSerializer.SerializeToElement(new { permission = "integration-test" }));
    internal static readonly DialogProjectionPolicy POLICY = new("restart-text", 1, 80, 80, 16384);
    internal static readonly ModelToolDefinition TOOL = new("action", "Тестовое действие",
        JsonSerializer.SerializeToElement(new { type = "object" }), true);

    private readonly HttpClient client;
    private readonly IConfigurationRoot configuration;

    /// <summary>Создаёт новый DI root без чтения рабочей конфигурации и без подключения при регистрации.</summary>
    internal Sub2ApiPersistenceFixture(IntegrationDatabase database, ToolAttemptState? failCommitWhen = null)
    {
        FailCommitWhen = failCommitWhen;
        client = new HttpClient(Transport) { Timeout = Timeout.InfiniteTimeSpan };
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "8",
            ["AgentBridge:Agent:InstructionsSource"] = "Configuration",
            ["AgentBridge:Agent:Instructions"] = "fixed integration instructions",
            ["AgentBridge:Retention:RetentionPeriod"] = "1.12:00:00",
            ["AgentBridge:Retention:SoftContentLimitBytes"] = "1048576",
            ["AgentBridge:Compaction:TokenThreshold"] = "1",
            ["AgentBridge:Compaction:InputTokenReserve"] = "0",
            ["AgentBridge:Compaction:MaxPasses"] = "2",
            ["AgentBridge:Compaction:BudgetPolicy"] = "ServerValidationWithUnknownWindow",
            ["Sub2Api:BaseAddress"] = "https://gateway.invalid/prefix/v1/",
            ["Sub2Api:Model"] = "gpt-5",
            ["Sub2Api:ReasoningEffort"] = "medium",
            ["Sub2Api:KeySource"] = "Shared",
            ["Sub2Api:SharedApiKey"] = "synthetic-restart-key",
            ["Sub2Api:GenerationTimeout"] = "00:00:10",
            ["Sub2Api:CompactTimeout"] = "00:00:10",
            ["Database:Provider"] = "PostgreSql",
            ["Database:ConnectionString"] = database.ConnectionString
        }).Build();
        Services = database.BuildRoot(customize: services =>
        {
            services.AddSingleton(this);
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IDialogCatalogProjector, Projector>();
            services.AddAgentBridgeTool<Handler, Validator>(TOOL);
            services.AddAgentBridgeSub2ApiConnector();
            services.AddAgentBridge(configuration, _ => client);
            if (FailCommitWhen.HasValue)
            {
                services.AddScoped<IUnitOfWorkSession, Sub2ApiCommitAcknowledgementSession>();
            }
        });
        Services.GetRequiredService<IStartupValidator>().Validate();
    }

    internal ServiceProvider Services { get; }
    internal Clock Time { get; } = new();
    internal Sub2ApiRunTransport Transport { get; } = new();
    internal ToolAttemptState? FailCommitWhen { get; }
    internal IOException CommitFailure { get; } = new("Injected lost acknowledgement after actual PostgreSQL commit.");
    internal int CommitFailures { get; set; }
    internal int HandlerCalls { get; private set; }
    internal Guid? ExecutionId { get; private set; }
    internal Func<CancellationToken, Task<ServiceResult<ToolOutput>>>? Action { get; set; }

    /// <summary>Создаёт registered dialog через настоящий публичный creator.</summary>
    internal async Task<DialogWriteToken> CreateAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        ServiceResult<DialogCatalogState> result = await scope.ServiceProvider.GetRequiredService<IDialogCatalogCreator>()
            .CreateAsync(new(DialogId.From(Guid.NewGuid()), SCOPE, Time.Now, Time.Now.AddHours(36), PROFILE, POLICY),
                TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Error?.Message);
        return result.Data!.Token;
    }

    /// <summary>Запускает тот же public runner, что и консоль; registered lease и input проходят настоящие guards.</summary>
    internal async Task<AgentRunResult> RunAsync(DialogWriteToken token, Guid turnId, string question = "Новый вопрос",
        bool stream = false, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AgentRunRequest request = new(new(token.DialogId, SCOPE.OwnerId, turnId, SCOPE.AgentId),
            [Message(question)], [TOOL.Name], new(8, 8, 1, TimeSpan.FromSeconds(10)),
            new DialogRunOptions(SCOPE, PROFILE, TimeSpan.FromMinutes(1)));
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? update = null;
        if (stream)
        {
            update = (_, _) => ValueTask.CompletedTask;
        }

        return await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(request, update, cancellationToken);
    }

    /// <summary>Каждое чтение создаёт новый реальный DbContext.</summary>
    internal async Task<DialogSnapshot> ReadAsync(DialogWriteToken token)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        ServiceResult<DialogSnapshot> result = await scope.ServiceProvider.GetRequiredService<IDialogReader>()
            .ReadCatalogAsync(new(Access(token), SCOPE, PROFILE), TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Error?.Message);
        return result.Data!;
    }

    /// <summary>Формирует доступ на управляемом server UTC.</summary>
    internal DialogAccess Access(DialogWriteToken token)
    {
        return new(token.DialogId, SCOPE.OwnerId, Time.Now);
    }

    /// <summary>Создаёт canonical сообщение без транспортной metadata.</summary>
    internal static CanonicalModelItem Message(string text, string role = "user")
    {
        string type = "input_text";
        if (role == "assistant")
        {
            type = "output_text";
        }

        return new(JsonSerializer.SerializeToElement(new
        {
            type = "message", role, content = new[] { new { type, text } }
        }));
    }

    /// <summary>Создаёт opaque элемент с неизвестным полем для полной проверки сохранности.</summary>
    internal static CanonicalModelItem Opaque()
    {
        return new(JsonSerializer.SerializeToElement(new
        {
            type = "reasoning", encrypted_content = "synthetic-opaque-restart", summary = Array.Empty<object>(),
            future = new { text = "Сохранённое поле", values = new[] { 2, 1 } }
        }));
    }

    /// <summary>Создаёт один явный вызов подставного business handler.</summary>
    internal static CanonicalModelItem Call()
    {
        return new(JsonSerializer.SerializeToElement(new
        {
            type = "function_call", call_id = "restart-call", name = TOOL.Name, arguments = "{}"
        }));
    }

    /// <summary>Фиксирует original SQL rows отдельно от изменяемых root/runtime/recovery records.</summary>
    internal static async Task<string> OriginalRowsAsync(IntegrationDatabase database, Guid turnId)
    {
        string id = turnId.ToString("D");
        return JsonSerializer.Serialize(new
        {
            Turns = await database.QueryAsync("SELECT * FROM \"DialogTurns\" WHERE \"Id\"='" + id + "'"),
            Items = await database.QueryAsync("SELECT * FROM \"CanonicalItems\" WHERE \"TurnId\"='" + id + "' ORDER BY \"Sequence\""),
            Steps = await database.QueryAsync("SELECT * FROM \"ModelSteps\" WHERE \"TurnId\"='" + id + "' ORDER BY \"Sequence\"")
        });
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Services.DisposeAsync();
        }
        finally
        {
            client.Dispose();
            ((IDisposable)configuration).Dispose();
        }
    }

    /// <summary>Управляет только серверным UTC, не таймерами runner или PostgreSQL.</summary>
    internal class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = NOW;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    /// <inheritdoc/>
    private class Handler(Sub2ApiPersistenceFixture fixture, AgentBridgeDbContext handlerContext) : IToolHandler
    {
        public ModelToolDefinition Definition => TOOL;

        /// <inheritdoc/>
        public async Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            Assert.Null(handlerContext.Database.CurrentTransaction);
            Assert.Empty(handlerContext.ChangeTracker.Entries());
            await using AsyncServiceScope observer = fixture.Services.CreateAsyncScope();
            string journal = (await observer.ServiceProvider.GetRequiredService<ModelStepRecordQueries>()
                .ReadTurnAsync(invocation.Call.DialogId.Value, invocation.Call.TurnId)).Last().ToolAttemptsJson!;
            using JsonDocument saved = JsonDocument.Parse(journal);
            Assert.Contains(saved.RootElement.GetProperty("attempts").EnumerateArray(),
                attempt => attempt.GetProperty("state").GetInt32() == (int)ToolAttemptState.Started);
            fixture.HandlerCalls++;
            fixture.ExecutionId = Guid.NewGuid();
            if (fixture.Action is not null)
            {
                return await fixture.Action(cancellationToken);
            }

            return ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { executionId = fixture.ExecutionId })));
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

    /// <inheritdoc/>
    private class Projector : IDialogCatalogProjector
    {
        public string Key => POLICY.Key;
        public int Version => POLICY.Version;

        /// <inheritdoc/>
        public ServiceResult<DialogCatalogTextUpdate> Project(SavedDialogMessage message, DialogCatalogTextState current,
            DialogProjectionPolicy policy)
        {
            string text = message.Message.Content.GetProperty("content")[0].GetProperty("text").GetString()!;
            string title = current.Title;
            bool hasQuestion = current.HasSavedQuestion;
            if (message.Role == "user" && !hasQuestion)
            {
                title = text;
                hasQuestion = true;
            }

            return ServiceResult<DialogCatalogTextUpdate>.Ok(new(new(title, title.ToUpperInvariant(), text, hasQuestion)));
        }
    }
}
