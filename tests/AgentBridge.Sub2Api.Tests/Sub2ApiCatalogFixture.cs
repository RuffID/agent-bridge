using System.Net;
using System.Text;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Sub2Api.Configuration;
using AgentBridge.Tokenization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Собирает actual bundle, HttpClientLibrary и scoped source без host, EF, сети или профиля пользователя.</summary>
internal class Sub2ApiCatalogFixture : IDisposable
{
    internal const string INDIVIDUAL_KEY = "synthetic-individual-secret";
    internal const string SHARED_KEY = "synthetic-shared-secret";
    internal const string PRIVATE = "synthetic-private-body-header";
    internal const string CATALOG = """{"object":"list","data":[{"object":"model","id":"public-model"}]}""";

    /// <summary>Фабрика вызывается только при разрешении HTTP-потребителя, клиент остаётся у fixture.</summary>
    internal Sub2ApiCatalogFixture(string address = "https://gateway.invalid/prefix/v1/", TimeSpan? timeout = null)
    {
        Handler = new();
        Http = new(Handler);
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        services.AddScoped<IIndividualModelKeySource, Source>();
        services.AddSingleton<IContextContentInspector, ContextTokenCounter>();
        services.Configure<ContextCompactionOptions>(options =>
        {
            options.TokenThreshold = 100;
            options.InputTokenReserve = 10;
            options.MaxPasses = 2;
        });
        IConfiguration section = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseAddress"] = address, ["Model"] = "public-model", ["ReasoningEffort"] = "medium",
            ["KeySource"] = "Shared", ["SharedApiKey"] = SHARED_KEY,
            ["GenerationTimeout"] = (timeout ?? TimeSpan.FromSeconds(30)).ToString(), ["CompactTimeout"] = "00:00:30"
        }).Build();
        Sub2ApiComposition.AddConfiguration(services, section, true);
        Sub2ApiComposition.AddPorts(services, _ =>
        {
            FactoryCalls++;
            return Http;
        });
        Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Scope = Provider.CreateScope();
    }

    internal DialogOwnerId Owner { get; } = DialogOwnerId.From("synthetic-owner");
    internal ServiceProvider Provider { get; }
    internal IServiceScope Scope { get; }
    internal HandlerDouble Handler { get; }
    internal HttpClient Http { get; }
    internal LogCapture Logs { get; } = new();
    internal int FactoryCalls { get; private set; }
    internal Source KeySource => (Source)Scope.ServiceProvider.GetRequiredService<IIndividualModelKeySource>();
    internal IModelAccessResolver Resolver => Scope.ServiceProvider.GetRequiredService<IModelAccessResolver>();
    internal IModelCatalog Catalog => Scope.ServiceProvider.GetRequiredService<IModelCatalog>();
    internal IModelSettingsReader Settings => Scope.ServiceProvider.GetRequiredService<IModelSettingsReader>();

    /// <summary>Создаёт локальный HTTP-ответ с синтетическими приватными полями.</summary>
    internal static HttpResponseMessage Json(string body = CATALOG, HttpStatusCode status = HttpStatusCode.OK)
    {
        HttpResponseMessage response = new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.Add("x-private-header", PRIVATE);
        return response;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Scope.Dispose();
        Provider.Dispose();
        Http.Dispose();
    }

    /// <inheritdoc/>
    internal class Source : IIndividualModelKeySource
    {
        internal string? Key { get; set; } = INDIVIDUAL_KEY;
        internal int Calls { get; private set; }
        internal Exception? Failure { get; set; }
        internal Func<Task>? BeforeReturn { get; set; }

        /// <inheritdoc/>
        public async Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            if (Failure is not null)
            {
                throw Failure;
            }

            if (BeforeReturn is not null)
            {
                await BeforeReturn();
            }

            return Key;
        }
    }

    /// <summary>Наблюдает только локальные запросы; ни один путь не делегирует сетевому handler.</summary>
    internal class HandlerDouble : HttpMessageHandler
    {
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (_, _) => Task.FromResult(Json());
        internal int Calls { get; private set; }
        internal bool Disposed { get; private set; }
        internal List<string> Authorizations { get; } = [];
        internal List<string> Urls { get; } = [];

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            Urls.Add(request.RequestUri!.AbsoluteUri);
            return Respond(request, cancellationToken);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>Сохраняет formatted, structured и exception каналы actual ILogger pipeline.</summary>
    internal class LogCapture : ILoggerProvider, ILogger
    {
        internal List<string> Entries { get; } = [];

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception) + exception);
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                foreach (KeyValuePair<string, object?> pair in properties)
                {
                    Entries.Add(pair.Key + "=" + pair.Value);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
