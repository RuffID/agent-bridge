using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentBridge.Application.Models;
using AgentBridge.Application;
using AgentBridge.Tokenization;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Models;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using HttpClientLibrary.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.CodexLb.Tests;

/// <summary>Публичный DI-путь каталога через actual HttpClientLibrary и подставной handler, без сети.</summary>
public class ModelCatalogTests
{
    private const string INDIVIDUAL_KEY = "synthetic-individual-key";
    private const string SHARED_KEY = "synthetic-shared-key";
    private const string PRIVATE_PAYLOAD = "synthetic-private-payload";

    /// <summary>Safe settings21 проверяет per-dialog новый выбор actual reader/HttpClientLibrary, не меняя defaults или ключ.</summary>
    [Fact]
    public async Task PublicSettingsServiceValidatesStoredChoiceThroughActualHttpLibrary()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ApplicationCallContext call = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        SelectionStore store = new(new(new(call.DialogId, Guid.NewGuid(), 0), call.OwnerId, now.AddHours(-1), now.AddDays(1), 0, [], null));
        ContextTokenCounter counter = new();
        AgentSettingsService service = new(store, store, fixture.Reader, new(counter, []), counter, TimeProvider.System,
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>(),
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>(),
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<AgentOptions>>());
        AgentSettingsSnapshot initial = (await service.ReadAsync(call)).Data!;
        Assert.Equal("application-model", initial.Model.Model.Id);
        fixture.Handler.Response = (_, _) => Task.FromResult(JsonResponse(Catalog(40_000, "new-effort").Replace("application-model", "selected-model", StringComparison.Ordinal)));
        Assert.True((await service.SelectAsync(call, initial.Token, initial.SelectionVersion, "selected-model", "new-effort")).Success);
        AgentSettingsSnapshot selected = (await service.ReadAsync(call)).Data!;
        Assert.Equal("selected-model", selected.Model.Model.Id);
        Assert.Equal("new-effort", selected.Model.ReasoningEffort);
        Assert.Equal(1, selected.SelectionVersion);
        Assert.Equal("application-model", fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CodexLbOptions>>().Value.Model);
        Assert.Equal(3, fixture.Handler.Calls);
        Assert.All(fixture.Handler.Authorizations, authorization => Assert.Equal("Bearer " + INDIVIDUAL_KEY, authorization));
        AssertSafe(JsonSerializer.Serialize(selected));
        AssertSafe(fixture.Log.Text);
    }

    /// <summary>Run settings проверяются pinned доступом без второго чтения изменившегося источника ключей.</summary>
    [Fact]
    public async Task PinnedAccessSettingsUseActualLibraryWithoutResolvingSourceAgain()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        DialogOwnerId owner = DialogOwnerId.From("owner");
        ModelAccess access = (await fixture.Scope.ServiceProvider.GetRequiredService<IModelAccessResolver>().ResolveAsync(owner)).Data!;
        fixture.Source.Key = "changed-key";
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadWithAccessAsync(owner, access);
        Assert.True(result.Success);
        Assert.Equal(1, fixture.Source.Calls);
        Assert.Equal("Bearer " + INDIVIDUAL_KEY, Assert.Single(fixture.Handler.Authorizations));
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Ключ выбирается один раз, owner сохраняется точно, адрес канонический с префиксом без query.</summary>
    [Theory]
    [InlineData(INDIVIDUAL_KEY, INDIVIDUAL_KEY)]
    [InlineData(null, SHARED_KEY)]
    public async Task KeyPriorityAndCanonicalRoute(string? individual, string expected)
    {
        using Fixture fixture = new(individual);
        IModelSettingsReader reader = fixture.Reader;
        Assert.Equal(0, fixture.Handler.Calls);
        ServiceResult<ModelSettingsSnapshot> result = await reader.ReadAsync(DialogOwnerId.From(" Owner-A "));
        Assert.True(result.Success);
        Assert.Equal(" Owner-A ", fixture.Source.LastOwner);
        Assert.Equal("Bearer " + expected, fixture.Handler.Authorizations.Single());
        Assert.Equal("https://gateway.invalid/prefix/v1/models", fixture.Handler.Urls.Single());
        Assert.Equal("application-model", result.Data!.Model.Id);
        Assert.Equal("custom-effort", result.Data.ReasoningEffort);
    }

    /// <summary>Только null разрешает общий ключ; ошибочный заданный ключ не отправляется и не заменяется.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad key")]
    [InlineData("bad\r\nheader")]
    public async Task MalformedIndividualKeyDoesNotFallBack(string key)
    {
        using Fixture fixture = new(key);
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.DoesNotContain(SHARED_KEY, JsonSerializer.Serialize(result));
    }

    /// <summary>Без обоих ключей возвращается Unauthorized до HTTP.</summary>
    [Fact]
    public async Task MissingBothKeysDoesNotSendHttp()
    {
        using Fixture fixture = new(null, null);
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.Equal(ServiceErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Ошибка источника не разрешает общий ключ и не превращается в ожидаемый отказ.</summary>
    [Fact]
    public async Task SourceFailurePropagatesWithoutFallback()
    {
        using Fixture fixture = new(null);
        InvalidOperationException failure = new("source-private-error");
        fixture.Source.Failure = failure;
        Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reader.ReadAsync(DialogOwnerId.From("owner")));
        Assert.Same(failure, actual);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>HTTP-отказы безопасны и не вызывают повтор с общим ключом, даже при секретах в headers/body/reason.</summary>
    [Theory]
    [InlineData(401, ServiceErrorType.Unauthorized)]
    [InlineData(403, ServiceErrorType.Forbidden)]
    [InlineData(429, ServiceErrorType.Rejected)]
    [InlineData(500, ServiceErrorType.Rejected)]
    public async Task HttpRejectionIsSafeAndNeverRetries(int status, ServiceErrorType type)
    {
        using Fixture fixture = new(INDIVIDUAL_KEY, SHARED_KEY, HttpErrorContentLogMode.JsonStructure);
        fixture.Handler.Response = (_, _) =>
        {
            HttpResponseMessage response = new((HttpStatusCode)status)
            {
                ReasonPhrase = PRIVATE_PAYLOAD,
                Content = new StringContent("{\"" + PRIVATE_PAYLOAD + "\":\"" + INDIVIDUAL_KEY + "\"}")
            };
            response.Headers.Add("X-Private-Response", SHARED_KEY);
            response.Content.Headers.Add("X-Private-Content", PRIVATE_PAYLOAD);
            return Task.FromResult(response);
        };
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.Equal(type, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY], fixture.Handler.Authorizations);
        AssertSafe(JsonSerializer.Serialize(result) + fixture.Log.Text);
        Assert.NotEmpty(fixture.Log.Text);
        Assert.All(fixture.Log.Exceptions, Assert.Null);
    }

    /// <summary>Ответы меняют последующую проверку, прошлый снимок неизменен; ключи разных пользователей не смешиваются.</summary>
    [Fact]
    public async Task CatalogRefreshesCapabilitiesPerCallAndKey()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        ServiceResult<ModelSettingsSnapshot> first = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        fixture.Source.Key = "another-synthetic-key";
        fixture.Handler.Response = (_, _) => Task.FromResult(JsonResponse(Catalog(36_095)));
        ServiceResult<ModelSettingsSnapshot> second = await fixture.Reader.ReadAsync(DialogOwnerId.From("another-owner"));
        Assert.True(first.Success);
        Assert.Equal(ServiceErrorType.Validation, second.Error!.Type);
        Assert.Equal(36_096, first.Data!.Model.InputContextWindow);
        fixture.Handler.Response = (_, _) => Task.FromResult(JsonResponse(Catalog(40_000, "new-effort")));
        ServiceResult<ModelSettingsSnapshot> third = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.Equal(ServiceErrorType.Unsupported, third.Error!.Type);
        ServiceResult<ModelSettingsSnapshot> fourth = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"), effort: "new-effort");
        Assert.True(fourth.Success);
        Assert.Equal("custom-effort", first.Data.ReasoningEffort);
        Assert.Equal("Bearer another-synthetic-key", fixture.Handler.Authorizations[1]);
    }

    /// <summary>Выбор конкретного чтения проверяется без изменения общих options или уже возвращённого снимка.</summary>
    [Fact]
    public async Task OverridesAreValidatedWithoutChangingDefaults()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        ServiceResult<ModelSettingsSnapshot> invalidModel = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"), model: "other-model");
        ServiceResult<ModelSettingsSnapshot> invalidEffort = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"), effort: "medium");
        ServiceResult<ModelSettingsSnapshot> empty = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"), effort: "");
        Assert.Equal(ServiceErrorType.Unsupported, invalidModel.Error!.Type);
        Assert.Equal(ServiceErrorType.Unsupported, invalidEffort.Error!.Type);
        Assert.Equal(ServiceErrorType.Validation, empty.Error!.Type);
        Assert.True((await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"))).Success);
    }

    /// <summary>Пустой каталог успешен как чтение, но недоступная выбранная модель отклоняется.</summary>
    [Fact]
    public async Task EmptyCatalogIsNotStaticFallback()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        fixture.Handler.Response = (_, _) => Task.FromResult(JsonResponse("{\"object\":\"list\",\"data\":[]}"));
        ServiceResult<ModelCatalogSnapshot> catalog = await fixture.Catalog.ReadAsync(new ModelAccess(INDIVIDUAL_KEY));
        Assert.True(catalog.Success);
        Assert.Empty(catalog.Data!.Models);
        Assert.Equal(ServiceErrorType.Unsupported, (await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"))).Error!.Type);
    }

    /// <summary>Отсутствие metadata сохраняется, budget/effort не берутся из другого поля или статического списка.</summary>
    [Theory]
    [InlineData("metadata")]
    [InlineData("input_context_window")]
    [InlineData("supported_in_api")]
    [InlineData("supported_reasoning_levels")]
    public async Task UnknownOrUnavailableCapabilitiesAreExplicit(string field)
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        JsonNode root = JsonNode.Parse(Catalog())!;
        JsonNode item = root["data"]![0]!;
        if (field == "metadata") item.AsObject().Remove(field);
        else if (field == "input_context_window") item["metadata"]!.AsObject().Remove(field);
        else if (field == "supported_in_api") item["metadata"]![field] = false;
        else item["metadata"]![field] = new JsonArray();
        fixture.Handler.Response = (_, _) => Task.FromResult(JsonResponse(root.ToJsonString()));
        Assert.Equal(ServiceErrorType.Unsupported, (await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"))).Error!.Type);
    }

    /// <summary>Повреждённая форма/JSON/пустой успешный ответ не превращаются в пустой каталог или статический fallback.</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"models\":[]}")]
    [InlineData("{\"object\":\"list\",\"data\":null}")]
    [InlineData("{\"object\":\"list\",\"data\":[{\"id\":\"x\",\"object\":\"model\",\"metadata\":{}}]}")]
    [InlineData("{\"object\":\"list\",\"data\":[{\"id\":\"x\",\"object\":\"model\"},{\"id\":\"x\",\"object\":\"model\"}]}")]
    [InlineData("")]
    public async Task InvalidCatalogIsSafeRejection(string json)
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        fixture.Handler.Response = (_, _) => Task.FromResult(JsonResponse(json));
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(JsonSerializer.Serialize(result) + fixture.Log.Text);
    }

    /// <summary>Неверные типы capability-полей не превращаются в schema defaults или неизвестный бюджет.</summary>
    [Theory]
    [InlineData("input_context_window", "\"invalid\"")]
    [InlineData("input_context_window", "-1")]
    [InlineData("context_window", "true")]
    [InlineData("supported_in_api", "\"true\"")]
    [InlineData("supported_reasoning_levels", "[null]")]
    [InlineData("input_modalities", "{}")]
    public async Task MalformedCapabilityFieldsAreRejected(string field, string value)
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        JsonNode root = JsonNode.Parse(Catalog())!;
        root["data"]![0]!["metadata"]![field] = JsonNode.Parse(value);
        fixture.Handler.Response = (_, _) => Task.FromResult(JsonResponse(root.ToJsonString()));
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Null(result.Data);
    }

    /// <summary>Лимит 64 КиБ сохраняется в actual pipeline, preview не становится error envelope.</summary>
    [Fact]
    public async Task OversizedErrorIsBoundedAndStreamDisposed()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        CountingStream stream = new(Encoding.UTF8.GetBytes(new string('x', 100_000)));
        fixture.Handler.Response = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StreamContent(stream)
        });
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.Equal(ServiceErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal(65_537, stream.ReadBytes);
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Успешный wire JSON не сохраняет неизвестные чувствительные поля; capabilities доступны после dispose ответа.</summary>
    [Fact]
    public async Task SuccessfulSnapshotExcludesSecretsAndPreservesCapabilities()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        CountingStream stream = new(Encoding.UTF8.GetBytes(Catalog()));
        fixture.Handler.Response = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
        });
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Reader.ReadAsync(DialogOwnerId.From("owner"));
        Assert.True(result.Success);
        Assert.True(stream.Disposed);
        ModelCapabilities model = result.Data!.Model;
        Assert.Equal(50_000, model.ContextWindow);
        Assert.Equal(36_096, model.InputContextWindow);
        Assert.Equal(20_000, model.MaxOutputTokens);
        Assert.Equal(["text", "image"], model.InputModalities);
        Assert.True(model.SupportsParallelToolCalls);
        Assert.True(model.SupportsReasoningSummaries);
        Assert.True(model.SupportsVerbosity);
        Assert.True(model.PreferWebSockets);
        string serialized = JsonSerializer.Serialize(result);
        AssertSafe(serialized + fixture.Log.Text);
        Assert.DoesNotContain("gateway.invalid", serialized);
        Assert.DoesNotContain("description", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(INDIVIDUAL_KEY, JsonSerializer.Serialize(new ModelAccess(INDIVIDUAL_KEY)));
        Assert.Equal(nameof(ModelAccess), new ModelAccess(INDIVIDUAL_KEY).ToString());
    }

    /// <summary>Caller cancellation проходит без Timeout/Rejected и без повторов.</summary>
    [Fact]
    public async Task CallerCancellationPropagatesThroughHttp()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        using CancellationTokenSource cancellation = new();
        fixture.Handler.Response = (_, ct) =>
        {
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException();
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadAsync(DialogOwnerId.From("owner"), ct: cancellation.Token));
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Предварительная отмена не вызывает источник и HTTP.</summary>
    [Fact]
    public async Task PreCanceledCallDoesNotAccessSource()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadAsync(DialogOwnerId.From("owner"), ct: cancellation.Token));
        Assert.Equal(0, fixture.Source.Calls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Неожиданная транспортная ошибка не маскируется HTTP-отказом.</summary>
    [Fact]
    public async Task UnexpectedTransportFailurePropagates()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        IOException failure = new("transport-private-error");
        fixture.Handler.Response = (_, _) => throw failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Reader.ReadAsync(DialogOwnerId.From("owner"))));
        Assert.Equal(1, fixture.Handler.Calls);
    }

    /// <summary>Поздняя отмена не подменяет уже установленный типизированный отказ каталога.</summary>
    [Theory]
    [InlineData(ServiceErrorType.Unauthorized)]
    [InlineData(ServiceErrorType.Rejected)]
    public async Task TypedCatalogFailureSurvivesLateCallerCancellation(ServiceErrorType type)
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        using CancellationTokenSource cancellation = new();
        ServiceError error = new(type, "Безопасный отказ каталога.");
        CancelingCatalog catalog = new(cancellation, ServiceResult<ModelCatalogSnapshot>.Fail(error));
        IModelSettingsReader reader = CreateReader(fixture, catalog);
        ServiceResult<ModelSettingsSnapshot> result = await reader.ReadAsync(DialogOwnerId.From("owner"), ct: cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Same(error, result.Error);
        Assert.Null(result.Data);
        Assert.Equal(0, fixture.Handler.Calls);
        Assert.Equal(1, fixture.Source.Calls);
    }

    /// <summary>Поздняя отмена после успешного каталога по-прежнему запрещает успешный settings snapshot.</summary>
    [Fact]
    public async Task SuccessfulCatalogDoesNotOverrideLateCallerCancellation()
    {
        using Fixture fixture = new(INDIVIDUAL_KEY);
        using CancellationTokenSource cancellation = new();
        CancelingCatalog catalog = new(cancellation, ServiceResult<ModelCatalogSnapshot>.Ok(new([])));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateReader(fixture, catalog)
            .ReadAsync(DialogOwnerId.From("owner"), ct: cancellation.Token));
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Использует настоящий settings reader и настоящий выбор доступа, заменяя только границу каталога.</summary>
    private static IModelSettingsReader CreateReader(Fixture fixture, IModelCatalog catalog) =>
        new CodexLbModelSettingsReader(fixture.Scope.ServiceProvider.GetRequiredService<IModelAccessResolver>(), catalog,
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CodexLbOptions>>(),
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>());

    /// <inheritdoc/>
    private class CancelingCatalog(CancellationTokenSource cancellation, ServiceResult<ModelCatalogSnapshot> result) : IModelCatalog
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelCatalogSnapshot>> ReadAsync(ModelAccess access, CancellationToken ct = default)
        {
            cancellation.Cancel();
            return Task.FromResult(result);
        }
    }

    /// <summary>Генерирует только синтетический wire ответ; неизвестные поля имитируют конфиденциальные данные.</summary>
    private static string Catalog(int input = 36_096, string effort = "custom-effort") => $$$"""
        {"object":"list","data":[{"id":"application-model","object":"model","created":1,"owned_by":"codex-lb",
        "private_field":"{{{PRIVATE_PAYLOAD}}}","metadata":{"display_name":"{{{PRIVATE_PAYLOAD}}}","description":"{{{INDIVIDUAL_KEY}}}",
        "context_window":50000,"input_context_window":{{{input}}},"max_output_tokens":20000,"input_modalities":["text","image"],
        "supported_reasoning_levels":[{"effort":"{{{effort}}}","description":"{{{PRIVATE_PAYLOAD}}}"}],
        "default_reasoning_level":"custom-effort","supported_in_api":true,"supports_reasoning_summaries":true,
        "supports_parallel_tool_calls":true,"support_verbosity":true,"prefer_websockets":true}}]}
        """;

    /// <summary>Возвращает локальное JSON-содержимое.</summary>
    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    /// <summary>Проверяет отсутствие секретов в снимках и диагностике.</summary>
    private static void AssertSafe(string text)
    {
        Assert.DoesNotContain(INDIVIDUAL_KEY, text);
        Assert.DoesNotContain(SHARED_KEY, text);
        Assert.DoesNotContain(PRIVATE_PAYLOAD, text);
    }

    /// <summary>Владеет только изолированными ресурсами теста и настоящим DI-путём.</summary>
    private class Fixture : IDisposable
    {
        public Fixture(string? individual, string? shared = SHARED_KEY, HttpErrorContentLogMode mode = HttpErrorContentLogMode.None)
        {
            Source = new() { Key = individual };
            Handler = new();
            Http = new(Handler);
            ServiceCollection services = new();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(Log));
            services.AddAgentBridgeConfiguration(_ => { });
            services.AddCodexLbConfiguration(options =>
            {
                options.BaseAddress = "https://gateway.invalid/prefix/";
                options.Model = "application-model";
                options.ReasoningEffort = "custom-effort";
                options.SharedApiKey = shared;
            });
            services.AddSingleton<IIndividualModelKeySource>(Source);
            services.AddCodexLbModelCatalog(_ => Http, new() { ErrorContentMode = mode });
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Scope = Provider.CreateScope();
        }

        public KeySource Source { get; }
        public FakeHandler Handler { get; }
        public CaptureLogger Log { get; } = new();
        public HttpClient Http { get; }
        public ServiceProvider Provider { get; }
        public IServiceScope Scope { get; }
        public IModelSettingsReader Reader => Scope.ServiceProvider.GetRequiredService<IModelSettingsReader>();
        public IModelCatalog Catalog => Scope.ServiceProvider.GetRequiredService<IModelCatalog>();
        public void Dispose() { Scope.Dispose(); Provider.Dispose(); Http.Dispose(); }
    }

    /// <inheritdoc/>
    private class KeySource : IIndividualModelKeySource
    {
        public string? Key { get; set; }
        public string? LastOwner { get; private set; }
        public int Calls { get; private set; }
        public Exception? Failure { get; set; }
        /// <inheritdoc/>
        public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            LastOwner = ownerId.Value;
            if (Failure is not null) throw Failure;
            return Task.FromResult(Key);
        }
    }

    /// <inheritdoc cref="IDialogReader"/>
    private class SelectionStore(DialogSnapshot dialog) : IDialogReader, IDialogSettingsWriter
    {
        private DialogSnapshot _dialog = dialog;
        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<DialogSnapshot>.Ok(_dialog));
        /// <inheritdoc/>
        public Task<ServiceResult<DialogModelSelection>> SaveAsync(DialogAccess access, DialogWriteToken expected, long expectedVersion,
            ModelSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            DialogModelSelection selection = new(expectedVersion + 1, settings.Model.Id, settings.ReasoningEffort);
            _dialog = new(_dialog.Token, _dialog.OwnerId, _dialog.CreatedAtUtc, _dialog.ExpiresAtUtc, _dialog.ContentBytes, [], null, selection);
            return Task.FromResult(ServiceResult<DialogModelSelection>.Ok(selection));
        }
    }

    /// <summary>Подставляет ответы в HttpClient приложения, не выполняя сеть.</summary>
    private class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string> Authorizations { get; } = [];
        public List<string> Urls { get; } = [];
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Response { get; set; } =
            (_, _) => Task.FromResult(JsonResponse(ModelCatalogTests.Catalog()));
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorizations.Add(request.Headers.Authorization!.ToString());
            Urls.Add(request.RequestUri!.AbsoluteUri);
            return Response(request, cancellationToken);
        }
    }

    /// <summary>Локальный поток измеряет реальное ограничение чтения и освобождение.</summary>
    private class CountingStream(byte[] data) : MemoryStream(data)
    {
        public int ReadBytes { get; private set; }
        public bool Disposed { get; private set; }
        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            ReadBytes += count;
            return count;
        }
        /// <inheritdoc/>
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    /// <inheritdoc/>
    private class CaptureLogger : ILoggerProvider, ILogger
    {
        public string Text { get; private set; } = "";
        public List<Exception?> Exceptions { get; } = [];
        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => this;
        /// <inheritdoc/>
        public void Dispose() { }
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Text += formatter(state, exception);
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
                foreach (KeyValuePair<string, object?> property in properties) Text += property.Key + property.Value;
            Exceptions.Add(exception);
        }
    }
}
