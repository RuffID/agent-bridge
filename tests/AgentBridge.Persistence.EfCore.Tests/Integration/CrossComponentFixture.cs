using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Persistence.EfCore.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Соединяет actual runner/transport/BPE/persistence; подставляет только HTTP и прикладной инструмент.</summary>
public class CrossComponentFixture : IAsyncDisposable
{
    /// <summary>Фиксированное начало тестового диалога.</summary>
    public static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    /// <summary>Синтетический индивидуальный ключ, не внешний credential.</summary>
    public const string INDIVIDUAL = "stage23-individual-synthetic";
    /// <summary>Синтетический общий ключ.</summary>
    public const string SHARED = "stage23-shared-synthetic";
    /// <summary>Полная схема инструмента приложения.</summary>
    public static readonly ModelToolDefinition TOOL = new("action", "Тестовое действие",
        JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, additionalProperties = false }), true);

    private readonly HttpClient _http;
    private readonly List<ServiceProvider> _roots = [];
    /// <summary>Создаёт локальный handler, без подключения к БД или сети.</summary>
    public CrossComponentFixture(IntegrationDatabase database)
    {
        Database = database;
        Handler = new();
        _http = new(Handler);
    }

    /// <summary>Собственная реальная тестовая БД.</summary>
    public IntegrationDatabase Database { get; }
    /// <summary>Управляемое время expiry equality.</summary>
    public Clock Time { get; } = new();
    /// <summary>Изменяемый источник только синтетических ключей.</summary>
    public KeySource Keys { get; } = new();
    /// <summary>HTTP boundary с настоящим HttpClientLibrary над ним.</summary>
    public ScriptHandler Handler { get; }
    /// <summary>Параметры текущего тестового run.</summary>
    public ApplicationCallContext Call { get; } = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("stage23-owner"), Guid.NewGuid(), "agent");
    /// <summary>Собственный root для наблюдения handler.</summary>
    public ServiceProvider Root { get; private set; } = null!;
    /// <summary>Считает фактические вызовы прикладного действия.</summary>
    public int Actions;
    /// <summary>Считает вызовы provider, который runner должен фиксировать один раз.</summary>
    public int ProviderCalls;
    /// <summary>Действие внутри scoped handler; транзакция runner уже завершилась.</summary>
    public Func<ToolInvocation, CancellationToken, Task<ServiceResult<ToolOutput>>> Action { get; set; } =
        (_, _) => Task.FromResult(ServiceResult<ToolOutput>.Ok(new(JsonSerializer.SerializeToElement(new { confirmed = true }))));

    /// <summary>Создаёт новый DI root над той же БД и actual adapter; настройка не исполняет I/O.</summary>
    public ServiceProvider BuildRoot(int threshold = 8000, Action<ServiceCollection>? customize = null, string? connection = null)
    {
        ServiceProvider root = Database.BuildRoot(connection: connection, customize: services =>
        {
            services.AddSingleton(this);
            services.AddSingleton<TimeProvider>(Time);
            services.AddAgentBridgeConfiguration(agent => { agent.Instructions = "fixed instructions"; agent.InstructionsSource = AgentInstructionsSource.Configuration; agent.MaxToolSteps = 8; },
                retention => { retention.RetentionPeriod = TimeSpan.FromHours(37); retention.SoftContentLimitBytes = 10_485_760; },
                compact => { compact.TokenThreshold = threshold; compact.InputTokenReserve = 10; compact.MaxPasses = 2; });
            services.AddCodexLbConfiguration(options =>
            {
                options.BaseAddress = "https://stage23.invalid/prefix/";
                options.Model = "gpt-5";
                options.ReasoningEffort = "medium";
                options.SharedApiKey = SHARED;
                options.KeySource = ModelKeySourceMode.Shared;
                options.GenerationTimeout = TimeSpan.FromSeconds(180);
                options.CompactTimeout = TimeSpan.FromSeconds(180);
            });
            services.AddSingleton<IIndividualModelKeySource>(Keys);
            services.AddCodexLbResponses(_ => _http);
            services.AddAgentBridgeTokenization();
            services.AddAgentBridgeSettings();
            services.AddAgentBridgeDialogCleanup();
            services.AddAgentBridgeTool<ActionHandler, Validator>(TOOL);
            services.AddScoped<ContextBuilder>(_ => new([new Provider(this)]));
            services.AddAgentBridgeRunner();
            customize?.Invoke(services);
        });
        _roots.Add(root);
        Root = root;
        return root;
    }

    /// <summary>Создаёт actual dialog по configured retention, сохраняя даты один раз.</summary>
    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        BuildRoot();
        await using AsyncServiceScope scope = Root.CreateAsyncScope();
        DateTimeOffset expiry = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value.CalculateExpiresAtUtc(NOW);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IDialogCreator>().CreateAsync(Call.DialogId, Call.OwnerId, NOW, expiry)).Success);
    }

    /// <summary>Вызывает public runner с полным production composition.</summary>
    public async Task<AgentRunResult> RunAsync(bool stream = false, ApplicationCallContext? call = null,
        string? effort = null, CancellationToken cancellationToken = default, CanonicalModelItem[]? input = null)
    {
        await using AsyncServiceScope scope = Root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AgentRunner>().RunAsync(
            new(call ?? Call, input ?? [Message("new input")], [TOOL.Name], new(6, 4, 1, TimeSpan.FromMinutes(1)), effort: effort),
            stream ? (_, _) => ValueTask.CompletedTask : null, cancellationToken);
    }

    /// <summary>Читает через новый короткий scope, включая metadata после expiry.</summary>
    public async Task<DialogSnapshot> ReadAsync(ServiceProvider? root = null)
    {
        await using AsyncServiceScope scope = (root ?? Root).CreateAsyncScope();
        ServiceResult<DialogSnapshot> read = await scope.ServiceProvider.GetRequiredService<IDialogReader>().ReadAsync(new(Call.DialogId, Call.OwnerId, Time.Now));
        Assert.True(read.Success, read.Error?.Message);
        return read.Data!;
    }

    /// <summary>Сохраняет exact selection через реальный каталог и settings UoW.</summary>
    public async Task SelectAsync(string model, string effort)
    {
        DialogSnapshot before = await ReadAsync();
        await using AsyncServiceScope scope = Root.CreateAsyncScope();
        ServiceResult<DialogModelSelection> selected = await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(Call, before.Token, before.Selection?.Version ?? 0, model, effort);
        Assert.True(selected.Success, selected.Error?.Message);
        DialogSnapshot after = await ReadAsync();
        Assert.Equal(before.Token.Revision, after.Token.Revision);
        Assert.Equal(before.ContentBytes, after.ContentBytes);
        Assert.Equal(before.ExpiresAtUtc, after.ExpiresAtUtc);
    }

    /// <summary>Создаёт обычный canonical текст с исходной ролью.</summary>
    public static CanonicalModelItem Message(string text, string role = "user") => new(JsonSerializer.SerializeToElement(new
    { type = "message", role, content = new[] { new { type = role == "assistant" ? "output_text" : "input_text", text } } }));
    /// <summary>Создаёт известный вызов, пригодный для actual BPE и проверки пары.</summary>
    public static CanonicalModelItem Function(string id = "same") => new(JsonSerializer.SerializeToElement(new
    { type = "function_call", id = "fc-" + id, call_id = id, name = "action", arguments = "{}" }));
    /// <summary>Возвращает discriminator canonical item.</summary>
    public static string? Type(CanonicalModelItem item) => item.Content.GetProperty("type").GetString();
    /// <summary>Создаёт complete envelope с неизвестной metadata, отдельно от canonical input.</summary>
    public static string Response(params CanonicalModelItem[] output) => JsonSerializer.Serialize(new
    { id = "response-stage23", @object = "response", status = "completed", model = "actual-server-model", future = new { nested = new[] { 1, 2 } }, output = output.Select(item => item.Content) });
    /// <summary>Оборачивает local JSON в terminal SSE frame без HTTP server.</summary>
    public static string Sse(string response) => "data: {\"type\":\"response.completed\",\"response\":" + response + "}\n\n";

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (ServiceProvider root in Enumerable.Reverse(_roots)) await root.DisposeAsync();
        _http.Dispose();
    }

    /// <summary>Часы не зависят от времени реального сервера.</summary>
    public class Clock : TimeProvider
    {
        /// <summary>UTC для deterministic interleaving.</summary>
        public DateTimeOffset Now = NOW;
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => Now;
    }
    /// <inheritdoc/>
    public class KeySource : IIndividualModelKeySource
    {
        /// <summary>Null означает отсутствие индивидуального ключа.</summary>
        public string? Key = INDIVIDUAL;
        /// <summary>Управляемая ошибка источника.</summary>
        public Exception? Failure;
        /// <summary>Количество обращений, включая settings calls.</summary>
        public int Calls;
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
        { Calls++; return Failure is null ? Task.FromResult(Key) : Task.FromException<string?>(Failure); }
    }
    /// <summary>Записывает outgoing wire JSON и возвращает только заранее заданные локальные ответы.</summary>
    public class ScriptHandler : HttpMessageHandler
    {
        /// <summary>Ожидаемые path/body/media responses; лишний запрос немедленно падает.</summary>
        public Queue<(string Path, string Body, bool Stream)> Responses { get; } = new();
        /// <summary>Наблюдённые canonical тела и synthetic keys, только внутри теста.</summary>
        public List<(string Path, string? Key, JsonElement? Body)> Requests { get; } = [];
        /// <summary>Фактический входной лимит каталога.</summary>
        public int? InputWindow = 10000;
        /// <summary>Отказ каталога для no-fallback regression.</summary>
        public HttpStatusCode CatalogStatus = HttpStatusCode.OK;
        /// <summary>Выполняется после фиксации outgoing request.</summary>
        public Action? AfterCatalog;
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            using JsonDocument? parsed = request.Content is null ? null : JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            JsonElement? body = parsed?.RootElement.Clone();
            Requests.Add((path, request.Headers.Authorization?.Parameter, body));
            string response;
            bool stream = false;
            HttpStatusCode status = HttpStatusCode.OK;
            if (path == "/prefix/v1/models")
            {
                status = CatalogStatus;
                response = status == HttpStatusCode.OK ? Catalog() : "{\"error\":{\"message\":\"stage23-private-payload\"}}";
                AfterCatalog?.Invoke();
            }
            else
            {
                Assert.NotEmpty(Responses);
                (string expected, string content, bool sse) = Responses.Dequeue();
                Assert.Equal(expected, path);
                response = content;
                stream = sse;
                Assert.Equal(stream, body!.Value.TryGetProperty("stream", out JsonElement mode) && mode.GetBoolean());
                Assert.False(body.Value.TryGetProperty("previous_response_id", out _));
            }
            return new(status) { Content = new StringContent(response, Encoding.UTF8, stream ? "text/event-stream" : "application/json") };
        }
        /// <summary>Dynamic catalog сохраняет различие общего и входного окна.</summary>
        private string Catalog() => JsonSerializer.Serialize(new { @object = "list", data = new[] { "gpt-5", "gpt-4.1" }.Select(id => new
        {
            id, @object = "model", metadata = new
            {
                context_window = 1000000, input_context_window = InputWindow, max_output_tokens = 50000,
                supported_reasoning_levels = new[] { new { effort = "low" }, new { effort = "medium" }, new { effort = "high" } },
                default_reasoning_level = "low", supported_in_api = true, input_modalities = new[] { "text" }, supports_parallel_tool_calls = true
            }
        }) });
    }
    /// <inheritdoc/>
    private class Provider(CrossComponentFixture fixture) : IContextProvider
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default)
        { fixture.ProviderCalls++; return Task.FromResult(ServiceResult<ContextContribution>.Ok(new([Message("provider text", "developer")]))); }
    }
    /// <inheritdoc/>
    private class Validator : IToolInvocationValidator
    {
        /// <inheritdoc/>
        public Task<ServiceResult> ValidateAsync(ModelToolDefinition definition, ToolInvocation invocation, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult.Ok());
    }
    /// <inheritdoc/>
    private class ActionHandler(CrossComponentFixture fixture, AgentBridgeDbContext context) : IToolHandler
    {
        /// <inheritdoc/>
        public ModelToolDefinition Definition => TOOL;
        /// <inheritdoc/>
        public async Task<ServiceResult<ToolOutput>> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
        {
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Empty(context.ChangeTracker.Entries());
            await using AsyncServiceScope observation = fixture.Root.CreateAsyncScope();
            // Один parent-aware query видит committed Started; multi-read root не используется при concurrent writes.
            string journal = (await observation.ServiceProvider.GetRequiredService<ModelStepRecordQueries>()
                .ReadTurnAsync(invocation.Call.DialogId.Value, invocation.Call.TurnId)).Last().ToolAttemptsJson!;
            using JsonDocument document = JsonDocument.Parse(journal);
            Assert.Contains(document.RootElement.GetProperty("attempts").EnumerateArray(),
                item => item.GetProperty("state").GetInt32() == (int)ToolAttemptState.Started);
            fixture.Actions++;
            return await fixture.Action(invocation, cancellationToken);
        }
    }
}
