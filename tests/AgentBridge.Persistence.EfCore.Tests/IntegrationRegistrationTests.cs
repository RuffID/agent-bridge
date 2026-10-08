using System.Net;
using System.Reflection;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Integration;
using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using HttpClientLibrary.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Actual public facade/DI и библиотечный pipeline без host, сети, БД, native или процессов.</summary>
public class IntegrationRegistrationTests
{
    /// <summary>Базовый Shared режим не требует пустых business классов и не выполняет I/O при composition.</summary>
    [Fact]
    public async Task SharedCompositionResolvesFullGraphWithoutBusinessClassesOrIo()
    {
        ServiceCollection services = Services();
        IConfiguration configuration = Config();
        int factories = 0;
        using Handler handler = new();
        using HttpClient client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        services.AddAgentBridge(configuration, _ => { factories++; return client; });
        Assert.Equal(0, factories);
        await using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(0, factories);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IServiceProvider scoped = scope.ServiceProvider;
        Assert.NotNull(scoped.GetRequiredService<AgentRunner>());
        Assert.NotNull(scoped.GetRequiredService<AgentSettingsService>());
        Assert.NotNull(scoped.GetRequiredService<ContextCompactor>());
        Assert.NotNull(scoped.GetRequiredService<ExpiredDialogCleanup>());
        Assert.NotNull(scoped.GetRequiredService<IDialogCreator>());
        Assert.NotNull(scoped.GetRequiredService<IDialogReader>());
        Assert.NotNull(scoped.GetRequiredService<IDialogContextWriter>());
        Assert.NotNull(scoped.GetRequiredService<IDialogToolAttemptWriter>());
        Assert.Empty(scoped.GetRequiredService<IToolRegistry>().Definitions);
        Assert.Empty(scoped.GetServices<IContextProvider>());
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", scoped.GetRequiredService<AgentBridgeDbContext>().Database.ProviderName);
        Assert.Null(scoped.GetService<IConfiguration>());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name.Contains("MaintenanceCoordinator", StringComparison.Ordinal));
        Assert.Equal(0, handler.Calls);
        ServiceResult<ModelAccess> access = await scoped.GetRequiredService<IModelAccessResolver>().ResolveAsync(DialogOwnerId.From("owner"), ct: TestContext.Current.CancellationToken);
        Assert.True(access.Success);
        Assert.Equal("synthetic-shared-key", access.Data!.RevealApiKey());
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>Scoped key source не разрешается из startup/root; actual HTTP library получает individual key.</summary>
    [Theory]
    [InlineData("Shared", "individual-key", true)]
    [InlineData("Individual", "individual-key", true)]
    [InlineData("Shared", null, true)]
    [InlineData("Individual", null, false)]
    [InlineData("Shared", " ", false)]
    [InlineData("Individual", " ", false)]
    public async Task ScopedSourceAndActualCatalogRespectKeyModes(string mode, string? key, bool success)
    {
        ServiceCollection services = Services();
        int created = 0;
        KeySource? source = null;
        services.AddScoped<IIndividualModelKeySource>(_ => { created++; return source = new(key); });
        using Handler handler = new();
        using HttpClient client = new(handler);
        services.AddAgentBridge(Config(new() { ["CodexLb:KeySource"] = mode }), _ => client);
        await using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(0, created);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            ServiceResult<ModelSettingsSnapshot> result = await scope.ServiceProvider.GetRequiredService<IModelSettingsReader>()
                .ReadAsync(DialogOwnerId.From("owner"), ct: TestContext.Current.CancellationToken);
            Assert.Equal(success, result.Success);
            Assert.Equal(1, created);
            Assert.Equal(success ? 1 : 0, handler.Calls);
            if (success) Assert.Equal("Bearer " + (key ?? "synthetic-shared-key"), handler.Authorization);
            Assert.False(source!.Disposed);
        }
        Assert.True(source!.Disposed);
    }

    /// <summary>Отсутствующий source в Individual даёт безопасную ошибку до фабрики/HTTP; Shared — positive control.</summary>
    [Fact]
    public void IndividualWithoutAppSourceFailsAtOptionsValidation()
    {
        ServiceCollection services = Services();
        int calls = 0;
        services.AddAgentBridge(Config(new() { ["CodexLb:KeySource"] = "Individual" }), _ => { calls++; throw new InvalidOperationException(); });
        using ServiceProvider provider = Build(services);
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("IIndividualModelKeySource", error.Message);
        Assert.DoesNotContain("synthetic-shared-key", error.ToString());
        Assert.Equal(0, calls);
    }

    /// <summary>Ошибка scoped источника сохраняет identity без shared fallback или HTTP.</summary>
    [Fact]
    public async Task SourceFailureDoesNotFallBack()
    {
        ServiceCollection services = Services();
        Exception primary = new InvalidOperationException("source-failure");
        services.AddScoped<IIndividualModelKeySource>(_ => new KeySource(null, primary));
        using Handler handler = new();
        using HttpClient client = new(handler);
        services.AddAgentBridge(Config(), _ => client);
        await using ServiceProvider provider = Build(services);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Exception error = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IModelSettingsReader>().ReadAsync(DialogOwnerId.From("owner"), ct: TestContext.Current.CancellationToken));
        Assert.Same(primary, error);
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>Каждый обязательный key13 реально отклоняется через public facade без I/O.</summary>
    [Theory]
    [InlineData("AgentBridge:Agent:MaxToolSteps")]
    [InlineData("AgentBridge:Agent:InstructionsSource")]
    [InlineData("AgentBridge:Agent:Instructions")]
    [InlineData("AgentBridge:Retention:SoftContentLimitBytes")]
    [InlineData("AgentBridge:Compaction:TokenThreshold")]
    [InlineData("AgentBridge:Compaction:InputTokenReserve")]
    [InlineData("AgentBridge:Compaction:MaxPasses")]
    [InlineData("CodexLb:BaseAddress")]
    [InlineData("CodexLb:Model")]
    [InlineData("CodexLb:ReasoningEffort")]
    [InlineData("CodexLb:KeySource")]
    [InlineData("CodexLb:SharedApiKey")]
    [InlineData("CodexLb:GenerationTimeout")]
    [InlineData("CodexLb:CompactTimeout")]
    [InlineData("Database:Provider")]
    [InlineData("Database:ConnectionString")]
    public void RequiredKeysAreValidated(string path)
    {
        ServiceCollection services = Services();
        services.AddAgentBridge(Config(new() { [path] = null }), _ => throw new InvalidOperationException("factory-must-not-run"));
        using ServiceProvider provider = Build(services);
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(path.Split(':').Last(), error.Message);
        Assert.DoesNotContain("synthetic-shared-key", error.ToString());
        Assert.DoesNotContain("Password=", error.ToString());
    }

    /// <summary>Malformed/range options не скрываются фасадом, секреты Binder не выходят наружу.</summary>
    [Theory]
    [InlineData("AgentBridge:Agent:MaxToolSteps", "0")]
    [InlineData("AgentBridge:Agent:InstructionsSource", "synthetic-secret")]
    [InlineData("AgentBridge:Retention:RetentionPeriod", "00:00:00")]
    [InlineData("AgentBridge:Retention:SoftContentLimitBytes", "-1")]
    [InlineData("AgentBridge:Compaction:TokenThreshold", "0")]
    [InlineData("AgentBridge:Compaction:InputTokenReserve", "-1")]
    [InlineData("AgentBridge:Compaction:MaxPasses", "2147483648")]
    [InlineData("CodexLb:BaseAddress", "https://user:synthetic-secret@example.invalid")]
    [InlineData("CodexLb:KeySource", "synthetic-secret")]
    [InlineData("CodexLb:SharedApiKey", "synthetic-secret bad")]
    [InlineData("CodexLb:GenerationTimeout", "00:00:00")]
    [InlineData("CodexLb:CompactTimeout", "synthetic-secret")]
    [InlineData("Database:Provider", "synthetic-secret")]
    [InlineData("Database:ConnectionString", "Password=\"synthetic-secret")]
    public void InvalidSettingsRemainSafe(string path, string value)
    {
        ServiceCollection services = Services();
        services.AddAgentBridge(Config(new() { [path] = value }), _ => throw new InvalidOperationException("factory-must-not-run"));
        using ServiceProvider provider = Build(services);
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(path.Split(':').Last(), error.Message);
        Assert.DoesNotContain("synthetic-secret", error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>Individual/PerRequest явно допускают отсутствие общего ключа/инструкций без hidden defaults.</summary>
    [Fact]
    public async Task ExplicitConditionalModesDoNotRequireUnusedConfiguration()
    {
        ServiceCollection services = Services();
        services.AddScoped<IIndividualModelKeySource>(_ => new KeySource("individual-key"));
        services.AddAgentBridge(Config(new()
        {
            ["CodexLb:KeySource"] = "Individual", ["CodexLb:SharedApiKey"] = null,
            ["AgentBridge:Agent:InstructionsSource"] = "PerRequest", ["AgentBridge:Agent:Instructions"] = null
        }), _ => throw new InvalidOperationException("factory-must-not-run"));
        await using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Assert.Equal("individual-key", (await scope.ServiceProvider.GetRequiredService<IModelAccessResolver>().ResolveAsync(DialogOwnerId.From("owner"), ct: TestContext.Current.CancellationToken)).Data!.RevealApiKey());
        Assert.Null(provider.GetRequiredService<IOptions<AgentOptions>>().Value.Instructions);
    }

    /// <summary>Logger обязателен до регистрации; нестандартная app factory сохраняется без file path.</summary>
    [Fact]
    public void RequiresAppLoggerWithoutCreatingFallback()
    {
        ServiceCollection services = new();
        Assert.Throws<InvalidOperationException>(() => services.AddAgentBridge(Config(), _ => throw new InvalidOperationException()));
        Assert.Empty(services);
        using LoggerFactory logger = new();
        services.AddTestDatabaseProviders();
        services.AddSingleton<ILoggerFactory>(logger);
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException());
        using ServiceProvider provider = Build(services);
        Assert.Same(logger, provider.GetRequiredService<ILoggerFactory>());
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    /// <summary>Повтор не удваивает modules/options/providers; новые arguments явно отклоняются.</summary>
    [Fact]
    public void RepeatRegistrationIsIdempotentAndRejectsDifferentArguments()
    {
        ServiceCollection services = Services();
        IConfiguration configuration = Config();
        Func<IServiceProvider, HttpClient> factory = _ => throw new InvalidOperationException();
        services.AddAgentBridge(configuration, factory);
        ServiceDescriptor[] before = services.ToArray();
        services.AddAgentBridge(configuration, factory);
        Assert.Equal(before, services.ToArray());
        Assert.Throws<InvalidOperationException>(() => services.AddAgentBridge(Config(), factory));
        Assert.Throws<InvalidOperationException>(() => services.AddAgentBridge(configuration, _ => throw new InvalidOperationException()));
    }

    /// <summary>Собственные ports реально разрешаются до/после facade; default registrations не затеняют их.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomContractsBeforeOrAfterFacadeRemainSelected(bool before)
    {
        Type[] types = [typeof(IContextTokenCounter), typeof(IContextContentInspector), typeof(IModelGateway),
            typeof(IModelAccessResolver), typeof(IModelCatalog), typeof(IModelSettingsReader), typeof(IDialogReader),
            typeof(IDialogSettingsWriter), typeof(IContextModelCompatibility), typeof(IToolExecutor), typeof(IToolRegistry)];
        Dictionary<Type, object> custom = types.ToDictionary(type => type, type => DispatchProxy.Create(type, typeof(ContractProxy)));
        ServiceCollection services = Services();
        if (before) foreach (KeyValuePair<Type, object> pair in custom) services.AddSingleton(pair.Key, pair.Value);
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException("custom graph must not request HTTP"));
        if (!before) foreach (KeyValuePair<Type, object> pair in custom) services.AddSingleton(pair.Key, pair.Value);
        await using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        foreach (KeyValuePair<Type, object> pair in custom) Assert.Same(pair.Value, scope.ServiceProvider.GetRequiredService(pair.Key));
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AgentRunner>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AgentSettingsService>());
    }

    /// <summary>Существующий actual HttpApiClient сохраняется, callback стандартного pipeline не выполняется.</summary>
    [Fact]
    public async Task ExistingHttpPipelineIsPreserved()
    {
        using Handler handler = new();
        using HttpClient client = new(handler);
        HttpApiClient http = new(client, NullLogger<HttpApiClient>.Instance);
        ServiceCollection services = Services();
        services.AddScoped(_ => http);
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException("factory-must-not-run"));
        await using ServiceProvider provider = Build(services);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Assert.Same(http, scope.ServiceProvider.GetRequiredService<HttpApiClient>());
        Assert.True((await scope.ServiceProvider.GetRequiredService<IModelSettingsReader>().ReadAsync(DialogOwnerId.From("owner"), ct: TestContext.Current.CancellationToken)).Success);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>HTTP client принадлежит app scoped registration; actual pipeline не освобождает его раньше scope.</summary>
    [Fact]
    public async Task AppScopedHttpClientIsDisposedByAppContainer()
    {
        ServiceCollection services = Services();
        AppHttpClient? client = null;
        services.AddScoped<HttpClient>(_ => client = new(new Handler()));
        services.AddAgentBridge(Config(), provider => provider.GetRequiredService<HttpClient>());
        await using ServiceProvider provider = Build(services);
        Assert.Null(client);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IModelSettingsReader>().ReadAsync(DialogOwnerId.From("owner"), ct: TestContext.Current.CancellationToken)).Success);
            Assert.False(client!.Disposed);
        }
        Assert.True(client!.Disposed);
    }

    /// <summary>Ordered scoped providers до/после facade и обычная история остаются в правильном input.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProvidersPreserveOrderAndHistoryAndAwaitAsyncDisposal(bool before)
    {
        ServiceCollection services = Services();
        List<ContextProvider> created = [];
        Action register = () =>
        {
            services.AddScoped<IContextProvider>(_ => { ContextProvider value = new("first"); created.Add(value); return value; });
            services.AddScoped<IContextProvider>(_ => { ContextProvider value = new("second"); created.Add(value); return value; });
        };
        if (before) register();
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException());
        if (!before) register();
        await using ServiceProvider provider = Build(services);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ContextBuilder>());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DialogId dialog = DialogId.From(Guid.NewGuid());
        DialogOwnerId owner = DialogOwnerId.From("owner");
        CanonicalModelItem history = Item("history");
        DialogSnapshot snapshot = new(new(dialog, Guid.NewGuid(), 0), owner, now.AddDays(-1), now.AddDays(1), 100,
            [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [history], [])], null);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            ServiceResult<ModelRequest> result = await scope.ServiceProvider.GetRequiredService<ContextBuilder>().BuildAsync(
                new(dialog, owner, Guid.NewGuid(), "agent"), snapshot, new("gpt-4.1", "medium", "instructions", [Item("new")], []), now, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Success);
            Assert.Equal(new[] { "first", "second", "history", "new" }, result.Data!.Input.Select(item => item.Content.GetProperty("content").GetString()));
            Assert.Same(history, result.Data.Input[2]);
            Assert.All(created, value => Assert.False(value.Disposed));
        }
        Assert.All(created, value => Assert.True(value.Disposed));
        Assert.Same(history, snapshot.Turns[0].Items[0]);
    }

    /// <summary>Стандартные PostConfigure приложения сохраняются в реальном options pipeline.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigurationOverridesKeepStandardOptionsPipeline(bool before)
    {
        ServiceCollection services = Services();
        if (before) services.PostConfigure<AgentOptions>(options => options.MaxToolSteps = 11);
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException());
        if (!before) services.PostConfigure<AgentOptions>(options => options.MaxToolSteps = 11);
        using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(11, provider.GetRequiredService<IOptions<AgentOptions>>().Value.MaxToolSteps);
    }

    /// <summary>Пустой business набор включает обычную историю, не удаляет snapshot и не требует provider-заглушки.</summary>
    [Fact]
    public async Task EmptyProvidersPreserveOrdinaryHistory()
    {
        ServiceCollection services = Services();
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException());
        await using ServiceProvider provider = Build(services);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DialogId dialog = DialogId.From(Guid.NewGuid());
        DialogOwnerId owner = DialogOwnerId.From("owner");
        CanonicalModelItem history = Item("history");
        DialogSnapshot snapshot = new(new(dialog, Guid.NewGuid(), 0), owner, now.AddDays(-1), now.AddDays(1), 100,
            [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [history], [])], null);
        ServiceResult<ModelRequest> result = await scope.ServiceProvider.GetRequiredService<ContextBuilder>().BuildAsync(
            new(dialog, owner, Guid.NewGuid(), "agent"), snapshot, new("gpt-4.1", "medium", "instructions", [Item("new")], []), now, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(new[] { "history", "new" }, result.Data!.Input.Select(item => item.Content.GetProperty("content").GetString()));
        Assert.Same(history, snapshot.Turns[0].Items[0]);
        Assert.Same(history, result.Data.Input[0]);
    }

    /// <summary>Custom builder явно сохраняется до/после facade.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CustomBuilderIsPreserved(bool before)
    {
        ServiceCollection services = Services();
        ContextBuilder builder = new([]);
        if (before) services.AddSingleton(builder);
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException());
        if (!before) services.AddSingleton(builder);
        using ServiceProvider provider = Build(services);
        using IServiceScope scope = provider.CreateScope();
        Assert.Same(builder, scope.ServiceProvider.GetRequiredService<ContextBuilder>());
    }

    /// <summary>Actual singleton registry открывает отдельные async invocation scopes для scoped app tools.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopedToolsRemainIndependentBeforeOrAfterFacade(bool before)
    {
        ServiceCollection services = Services();
        services.AddScoped<BusinessState>();
        if (before) services.AddAgentBridgeTool<BusinessTool, BusinessValidator>(BusinessTool.TOOL_DEFINITION);
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException());
        if (!before) services.AddAgentBridgeTool<BusinessTool, BusinessValidator>(BusinessTool.TOOL_DEFINITION);
        await using ServiceProvider provider = Build(services);
        provider.GetRequiredService<IStartupValidator>().Validate();
        IToolRegistry registry = provider.GetRequiredService<IToolRegistry>();
        Assert.Single(registry.Definitions);
        BusinessState firstState;
        BusinessState secondState;
        await using (IToolHandlerScope first = registry.OpenScope("business")!)
        await using (IToolHandlerScope second = registry.OpenScope("business")!)
        {
            firstState = Assert.IsType<BusinessTool>(first.Handler).State;
            secondState = Assert.IsType<BusinessTool>(second.Handler).State;
            Assert.Same(firstState, Assert.IsType<BusinessValidator>(first.Validator).State);
            Assert.Same(secondState, Assert.IsType<BusinessValidator>(second.Validator).State);
            Assert.NotSame(firstState, secondState);
            Assert.False(firstState.Disposed);
        }
        Assert.True(firstState.Disposed);
        Assert.True(secondState.Disposed);
    }

    /// <summary>ValidateOnBuild не отключается и отклоняет captive scoped business state в singleton.</summary>
    [Fact]
    public void CaptiveAppDependencyIsRejected()
    {
        ServiceCollection services = Services();
        services.AddScoped<BusinessState>();
        services.AddSingleton<BusinessTool>();
        services.AddAgentBridge(Config(), _ => throw new InvalidOperationException());
        Assert.Throws<AggregateException>(() => Build(services));
    }

    /// <summary>Создаёт app-owned logging без sink/files и host.</summary>
    private static ServiceCollection Services() { ServiceCollection services = new(); services.AddTestDatabaseProviders(); services.AddLogging(); return services; }

    /// <summary>Включает обе стандартные проверки scope/graph.</summary>
    private static ServiceProvider Build(IServiceCollection services) => services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    /// <summary>Полный explicit configuration13 только с синтетическими значениями.</summary>
    private static IConfiguration Config(Dictionary<string, string?>? changes = null)
    {
        Dictionary<string, string?> values = new()
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "5", ["AgentBridge:Agent:InstructionsSource"] = "Configuration", ["AgentBridge:Agent:Instructions"] = "instructions",
            ["AgentBridge:Retention:RetentionPeriod"] = "14.00:00:00", ["AgentBridge:Retention:SoftContentLimitBytes"] = "10485760",
            ["AgentBridge:Compaction:TokenThreshold"] = "24000", ["AgentBridge:Compaction:InputTokenReserve"] = "0", ["AgentBridge:Compaction:MaxPasses"] = "3",
            ["CodexLb:BaseAddress"] = "https://example.invalid/prefix", ["CodexLb:Model"] = "gpt-4.1", ["CodexLb:ReasoningEffort"] = "medium",
            ["CodexLb:KeySource"] = "Shared", ["CodexLb:SharedApiKey"] = "synthetic-shared-key", ["CodexLb:GenerationTimeout"] = "00:03:00", ["CodexLb:CompactTimeout"] = "00:03:00",
            ["Database:Provider"] = "SqlServer", ["Database:ConnectionString"] = "Server=example.invalid;Database=synthetic;User Id=synthetic;Password=synthetic-password;Encrypt=True"
        };
        if (changes is not null) foreach (KeyValuePair<string, string?> pair in changes) values[pair.Key] = pair.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>Создаёт canonical сообщение без хранения JsonDocument.</summary>
    private static CanonicalModelItem Item(string text)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new { type = "message", role = "user", content = text }));
        return new(document.RootElement);
    }

    /// <summary>Proxy служит только различимой identity custom contract; его методы не заменяют business успех.</summary>
    public class ContractProxy : DispatchProxy
    {
        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("custom contract method must not run in DI-only test");
    }

    /// <summary>Наблюдаемое scoped состояние приложения без внешних операций.</summary>
    private class BusinessState : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        /// <inheritdoc/>
        public async ValueTask DisposeAsync() { await Task.Yield(); Disposed = true; }
    }

    /// <inheritdoc/>
    private class BusinessTool(BusinessState state) : IToolHandler
    {
        public static readonly ModelToolDefinition TOOL_DEFINITION = new("business", "Тестовый scoped инструмент.", JsonSerializer.SerializeToElement(new { type = "object" }), strict: false);
        public BusinessState State => state;
        /// <inheritdoc/>
        public ModelToolDefinition Definition => TOOL_DEFINITION;
        /// <inheritdoc/>
        public Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default) => throw new InvalidOperationException("DI test does not execute business action");
    }

    /// <inheritdoc/>
    private class BusinessValidator(BusinessState state) : IToolInvocationValidator
    {
        public BusinessState State => state;
        /// <inheritdoc/>
        public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation, CancellationToken cancellationToken = default) => throw new InvalidOperationException("DI test does not authorize business action");
    }

    /// <summary>Наблюдаемый app-owned client с local handler, без сети.</summary>
    private class AppHttpClient(HttpMessageHandler handler) : HttpClient(handler)
    {
        public bool Disposed { get; private set; }
        /// <inheritdoc/>
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }

    /// <inheritdoc/>
    private class KeySource(string? key, Exception? error = null) : IIndividualModelKeySource, IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default) => error is null ? Task.FromResult(key) : Task.FromException<string?>(error);
        /// <inheritdoc/>
        public async ValueTask DisposeAsync() { await Task.Yield(); Disposed = true; }
    }

    /// <inheritdoc/>
    private class ContextProvider(string text) : IContextProvider, IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ContextContribution>.Ok(new([Item(text)])));
        /// <inheritdoc/>
        public async ValueTask DisposeAsync() { await Task.Yield(); Disposed = true; }
    }

    /// <summary>Local fake handler actual библиотечного pipeline без сетевых запросов.</summary>
    private class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Authorization { get; private set; }
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorization = request.Headers.Authorization?.ToString();
            Assert.Equal("https://example.invalid/prefix/v1/models", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"object":"list","data":[{"id":"gpt-4.1","object":"model","created":1,"owned_by":"codex-lb","metadata":{"context_window":50000,"input_context_window":40000,"input_modalities":["text"],"supported_in_api":true,"supported_reasoning_levels":[{"effort":"medium"}]}}]}""")
            });
        }
    }
}
