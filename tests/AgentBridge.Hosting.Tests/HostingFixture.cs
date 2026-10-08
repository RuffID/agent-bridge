using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Integration;
using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Models;
using AgentBridge.Persistence.EfCore.Tests;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentBridge.Hosting.Tests;

/// <summary>Композиция тестового приложения: настоящий Host/facade, borrowed HTTP и disconnected base read boundaries.</summary>
internal class HostingFixture
{
    internal static readonly TimeSpan BUDGET = TimeSpan.FromSeconds(15);
    internal readonly CancellationTokenSource Abort = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    internal readonly TaskCompletionSource KeyDisposeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource KeyDisposeRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly List<HostingKeySource> Keys = [];
    internal readonly List<RootRepository> Roots = [];
    internal readonly HostingHandler Handler = new();
    internal readonly HostingLogger Logger = new();
    internal readonly HttpClient Client;
    internal readonly IHost Host;
    internal readonly IHostApplicationLifetime Lifetime;
    internal readonly ApplicationCallContext Call = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("hosting-owner"), Guid.NewGuid(), "hosting-agent");
    internal HostingWorker? Worker;
    internal int HttpFactories;
    internal bool HoldKeyDisposal;
    internal Exception? KeyDisposalFailure;
    internal Exception? ReadFailure;
    internal Exception? ReadDisposalFailure;

    /// <summary>Создаёт хост без среды/файлов/сети и включает реальные проверки scoped-графа.</summary>
    internal HostingFixture(Dictionary<string, string?>? changes = null, bool worker = false, bool source = true)
    {
        Client = new(Handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        Dictionary<string, string?> values = new()
        {
            ["AgentBridge:Agent:MaxToolSteps"] = "5", ["AgentBridge:Agent:InstructionsSource"] = "Configuration",
            ["AgentBridge:Agent:Instructions"] = "hosting instructions",
            ["AgentBridge:Retention:RetentionPeriod"] = "14.00:00:00", ["AgentBridge:Retention:SoftContentLimitBytes"] = "10485760",
            ["AgentBridge:Compaction:TokenThreshold"] = "24000", ["AgentBridge:Compaction:InputTokenReserve"] = "0",
            ["AgentBridge:Compaction:MaxPasses"] = "3", ["CodexLb:BaseAddress"] = "https://example.invalid/prefix",
            ["CodexLb:Model"] = "gpt-4.1", ["CodexLb:ReasoningEffort"] = "medium", ["CodexLb:KeySource"] = "Shared",
            ["CodexLb:SharedApiKey"] = "synthetic-shared-key", ["CodexLb:GenerationTimeout"] = "00:03:00",
            ["CodexLb:CompactTimeout"] = "00:03:00", ["Database:Provider"] = "SqlServer",
            ["Database:ConnectionString"] = "Server=example.invalid;Database=synthetic;User Id=synthetic;Password=synthetic-password;Encrypt=True"
        };
        if (changes is not null)
            foreach (KeyValuePair<string, string?> pair in changes) values[pair.Key] = pair.Value;

        HostBuilder builder = new();
        builder.UseDefaultServiceProvider(options => { options.ValidateScopes = true; options.ValidateOnBuild = true; });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILoggerFactory>(Logger);
            if (source) services.AddScoped<IIndividualModelKeySource>(_ =>
            {
                HostingKeySource key = new(this);
                Keys.Add(key);
                return key;
            });
            RegisterReadBoundaries(services);
            services.AddAgentBridge(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), _ =>
            {
                HttpFactories++;
                return Client;
            });
            services.AddDbContext<AgentBridgeDbContext>(options => options.AddInterceptors(new NoDatabaseInterceptor()));
            services.Configure<HostOptions>(options => options.ShutdownTimeout = BUDGET);
            if (worker)
            {
                services.AddSingleton(this);
                services.AddSingleton<HostingWorker>();
                services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostingWorker>());
            }
        });
        Host = builder.Build();
        Lifetime = Host.Services.GetRequiredService<IHostApplicationLifetime>();
        if (worker) Worker = Host.Services.GetRequiredService<HostingWorker>();
    }

    /// <summary>Подключает existing fake base repositories; реальные reader/query/gate/UoW остаются из facade.</summary>
    private void RegisterReadBoundaries(IServiceCollection services)
    {
        services.AddScoped<IContextGetItemByIdRepository<DialogRecord, Guid, AgentBridgeContextKey>>(_ =>
        {
            RootRepository root = new(this);
            Roots.Add(root);
            return root;
        });
        Register<DialogRecord>(services);
        Register<DialogTurnRecord>(services);
        Register<CanonicalItemRecord>(services);
        Register<ModelStepRecord>(services);
        Register<DialogContextRecord>(services);
        services.AddScoped<IContextGetItemByIdRepository<DialogSettingsRecord, Guid, AgentBridgeContextKey>>(_ => new FakeSettingsByIdRepository(new()));
    }

    /// <summary>Переиспользует точный общий base read double без копии UoW или reader.</summary>
    private static void Register<T>(IServiceCollection services) where T : class =>
        services.AddScoped<IContextGetItemByPredicateRepository<T, AgentBridgeContextKey>>(_ => new FakeBaseRepository<T>());

    /// <summary>Ожидает cleanup отдельно от отмены runner с собственным конечным бюджетом.</summary>
    internal static Task AwaitCleanupAsync(Task task) => task.WaitAsync(BUDGET);

    /// <summary>Освобождает gates, отменяет/ожидает работу и всегда пытается остановить и async-dispose хост.</summary>
    internal async Task CleanupAsync(Exception? expected = null)
    {
        List<Exception> errors = [];
        Handler.Release.TrySetResult();
        KeyDisposeRelease.TrySetResult();
        Abort.Cancel();
        Lifetime.StopApplication();
        using CancellationTokenSource cleanup = new(BUDGET);
        try { await Host.StopAsync(cleanup.Token).WaitAsync(cleanup.Token); }
        catch (Exception error) { if (!ReferenceEquals(error, expected)) errors.Add(error); }
        if (Worker?.ExecuteTask is { } execution)
        {
            try { await execution.WaitAsync(cleanup.Token); }
            catch (Exception error) { if (!ReferenceEquals(error, expected)) errors.Add(error); }
        }
        try { await ((IAsyncDisposable)Host).DisposeAsync().AsTask().WaitAsync(cleanup.Token); }
        catch (Exception error) { errors.Add(error); }
        try { Client.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        try { Handler.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        try { Logger.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        try { Abort.Dispose(); }
        catch (Exception error) { errors.Add(error); }

        if (errors.Count > 0) throw new AggregateException("Ошибка bounded cleanup hosting-теста.", errors);

        Assert.True(Handler.Disposed);
        Assert.True(Logger.Disposed);
        Assert.Throws<ObjectDisposedException>(() => Client.CancelPendingRequests());
        Assert.All(Keys, key => Assert.True(key.Disposed));
        Assert.All(Roots, root => Assert.True(root.Disposed));
        Assert.All(Handler.Bodies, body => Assert.True(body.Disposed));
    }

    /// <summary>Реальный reader получает синтетический root и его настоящие scoped disposal errors.</summary>
    internal class RootRepository : IContextGetItemByIdRepository<DialogRecord, Guid, AgentBridgeContextKey>, IAsyncDisposable
    {
        private readonly HostingFixture fixture;
        internal int Reads;
        internal bool Disposed;

        /// <summary>Создаёт read boundary одного scope.</summary>
        internal RootRepository(HostingFixture fixture) => this.fixture = fixture;

        /// <inheritdoc/>
        public Task<DialogRecord?> GetItemByIdAsync(Guid id, bool asNoTracking = false,
            Func<IQueryable<DialogRecord>, IQueryable<DialogRecord>>? include = null, CancellationToken ct = default)
        {
            Reads++;
            ct.ThrowIfCancellationRequested();
            if (fixture.ReadFailure is { } error) throw error;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return Task.FromResult<DialogRecord?>(new()
            {
                Id = id, OwnerId = fixture.Call.OwnerId.Value, IncarnationId = fixture.Call.DialogId.Value,
                CreatedAtUtc = now.AddHours(-1), ExpiresAtUtc = now.AddDays(1), LastChangedAtUtc = now.AddHours(-1)
            });
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return fixture.ReadDisposalFailure is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
        }
    }
}
