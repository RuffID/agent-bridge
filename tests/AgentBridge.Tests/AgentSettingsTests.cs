using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Tokenization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Public settings/status21 с actual builder/BPE/shape inspector и isolated ports, без БД/сети.</summary>
public class AgentSettingsTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Новый DI scope видит изменённые limits; предыдущий snapshot остаётся прежним, invalid config отвергается локально.</summary>
    [Fact]
    public async Task ConfigurationChangesAreValidatedAndDoNotMutateSnapshots()
    {
        Probe probe = new();
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:InstructionsSource"] = "PerRequest", ["Agent:MaxToolSteps"] = "8",
            ["Retention:RetentionPeriod"] = "7.00:00:00", ["Retention:SoftContentLimitBytes"] = "128",
            ["Compaction:MaxPasses"] = "3", ["Compaction:TokenThreshold"] = "20", ["Compaction:InputTokenReserve"] = "1"
        }).Build();
        ServiceCollection services = Services(probe);
        services.AddAgentBridgeConfiguration(configuration);
        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope first = root.CreateScope();
        AgentSettingsSnapshot before = Success(await first.ServiceProvider.GetRequiredService<AgentSettingsService>().ReadAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        configuration["Retention:SoftContentLimitBytes"] = "256";
        configuration["Compaction:TokenThreshold"] = "40";
        configuration.Reload();
        using IServiceScope second = root.CreateScope();
        AgentSettingsSnapshot after = Success(await second.ServiceProvider.GetRequiredService<AgentSettingsService>().ReadAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(128, before.SoftContentLimitBytes);
        Assert.Equal(20, before.Model.TokenThreshold);
        Assert.Equal(256, after.SoftContentLimitBytes);
        Assert.Equal(40, after.Model.TokenThreshold);
        Assert.Equal(128, Success(await first.ServiceProvider.GetRequiredService<AgentSettingsService>().ReadAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken)).SoftContentLimitBytes);
        configuration["Compaction:TokenThreshold"] = "-1";
        configuration.Reload();
        using IServiceScope invalid = root.CreateScope();
        await Assert.ThrowsAsync<OptionsValidationException>(() => invalid.ServiceProvider.GetRequiredService<AgentSettingsService>().ReadAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, probe.Writes);
    }

    /// <summary>Повторные opaque outputs сохраняют отдельные occurrences; input не теряется при provenance разборе.</summary>
    [Fact]
    public async Task RepeatedOpaqueOutputsAndInputKeepEveryOccurrence()
    {
        Probe probe = new();
        CanonicalModelItem item = Opaque();
        probe.Dialog = Copy(probe.Dialog, turns: [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [item, item, item],
            [new(Guid.NewGuid(), ModelResponse.Completed([item], new(JsonSerializer.SerializeToElement(new { model = "server-one" })))),
             new(Guid.NewGuid(), ModelResponse.Completed([item], new(JsonSerializer.SerializeToElement(new { model = "server-two" }))))],
             TurnModelSettings.From(Models.Settings("gpt-5", "high")))]);
        List<string?> names = [];
        Compatibility validator = new(source => { names.Add(source.ServerModel); Assert.Single(source.Items); return ServiceResult.Ok(); });
        ServiceCollection services = Services(probe);
        services.AddSingleton<IContextModelCompatibility>(validator);
        await using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().SelectAsync(probe.Call,
            probe.Dialog.Token, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Equal(new string?[] { "server-one", "server-two", null }, names);
        Assert.Equal(3, probe.Dialog.Turns[0].Items.Count);
    }

    /// <summary>Отказ/неожиданная ошибка compatibility портa не вызывает запись или скрытый повтор.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibilityFailureDoesNotWrite(bool unexpected)
    {
        Probe probe = new();
        probe.Dialog = Copy(probe.Dialog, active: new(1, 0, ModelResponse.Completed([Opaque()]), "gpt-5"));
        IOException primary = new("synthetic-secret");
        ServiceError error = new(ServiceErrorType.Unsupported, "Совместимость не подтверждена.");
        Compatibility validator = new(_ => unexpected ? throw primary : ServiceResult.Fail(error));
        ServiceCollection services = Services(probe);
        services.AddSingleton<IContextModelCompatibility>(validator);
        await using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        Task<ServiceResult<DialogModelSelection>> operation = scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken);
        if (unexpected) Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => operation));
        else Assert.Same(error, (await operation).Error);
        Assert.Equal(1, validator.Calls);
        Assert.Equal(0, probe.Writes);
    }

    /// <summary>Сохранённый per-dialog выбор и безопасные лимиты не раскрывают ключи/instructions/raw metadata.</summary>
    [Fact]
    public async Task ReadsStoredSelectionAndSafeLimits()
    {
        Probe probe = new();
        probe.Dialog = Copy(probe.Dialog, selection: new(4, "gpt-4", "low"));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AgentSettingsSnapshot settings = Success(await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().ReadAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("gpt-4", settings.Model.Model.Id);
        Assert.Equal("low", settings.Model.ReasoningEffort);
        Assert.Equal(4, settings.SelectionVersion);
        Assert.Equal(TimeSpan.FromDays(7), settings.RetentionPeriod);
        Assert.Equal(10_485_760, settings.SoftContentLimitBytes);
        Assert.Equal(3, settings.MaxCompactionPasses);
        Assert.Equal(8, settings.MaxToolSteps);
        Assert.DoesNotContain("synthetic-secret", JsonSerializer.Serialize(settings));
    }

    /// <summary>Разные экземпляры DialogId одного GUID допустимы; settings version не меняет root token/expiry.</summary>
    [Fact]
    public async Task SelectionUsesValueIdentityAndIndependentVersion()
    {
        Probe probe = new();
        DialogWriteToken expected = new(DialogId.From(probe.Call.DialogId.Value), probe.Dialog.Token.IncarnationId, 0);
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AgentSettingsService service = scope.ServiceProvider.GetRequiredService<AgentSettingsService>();
        DialogModelSelection result = Success(await service.SelectAsync(probe.Call, expected, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, result.Version);
        Assert.Equal(0, probe.Dialog.Token.Revision);
        Assert.Equal(NOW.AddDays(1), probe.Dialog.ExpiresAtUtc);
        ServiceResult<DialogModelSelection> stale = await service.SelectAsync(probe.Call, expected, 0, "gpt-5", "high", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, stale.Error!.Type);
        Assert.Equal("gpt-4", probe.Dialog.Selection!.Model);
        Assert.Equal(1, probe.Writes);
    }

    /// <summary>Проверка exact каталога отвергает неизвестную модель/effort без записи.</summary>
    [Theory]
    [InlineData("GPT-5", "high")]
    [InlineData("gpt-5", "HIGH")]
    [InlineData("gpt-5", "")]
    public async Task InvalidSelectionDoesNotWrite(string model, string effort)
    {
        Probe probe = new();
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, model, effort, cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Equal(0, probe.Writes);
    }

    /// <summary>Только текст совместим независимо от неизвестного tokenizer mapping выбранной модели.</summary>
    [Fact]
    public async Task TextSelectionDoesNotRequireTokenizerOrCompatibilityPort()
    {
        Probe probe = new();
        probe.Dialog = Copy(probe.Dialog, turns: [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message()], [])]);
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, "gpt-6", "high", cancellationToken: TestContext.Current.CancellationToken)).Success);
        DialogStatus status = Success(await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().GetStatusAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("gpt-6", status.Settings!.Model.Model.Id);
        Assert.Null(status.ContextSize);
        Assert.Equal(ServiceErrorType.Unsupported, status.ContextError!.Type);
    }

    /// <summary>Неподтверждённое opaque переключение не сохраняет выбор и не очищает исходную историю.</summary>
    [Fact]
    public async Task OpaqueChangeWithoutProofIsUnsupported()
    {
        Probe probe = new();
        CanonicalModelItem item = Opaque();
        probe.Dialog = Copy(probe.Dialog, active: new(1, 0, ModelResponse.Completed([item]), "gpt-5"));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ServiceResult<DialogModelSelection> result = await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal(item.Content.GetRawText(), probe.Dialog.ActiveContext!.Items[0].Content.GetRawText());
        Assert.Equal(0, probe.Writes);
    }

    /// <summary>Приложение получает actual server model вместе с исходным selected; opaque output не дублируется как input.</summary>
    [Fact]
    public async Task ValidatorGetsActualServerProvenanceExactlyOnce()
    {
        Probe probe = new();
        CanonicalModelItem output = Opaque();
        ModelResponse response = ModelResponse.Completed([output], new(JsonSerializer.SerializeToElement(new { model = "server-routed" })));
        TurnModelSettings pinned = TurnModelSettings.From(Models.Settings("gpt-5", "high"));
        probe.Dialog = Copy(probe.Dialog, turns: [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message(), output],
            [new(Guid.NewGuid(), response)], pinned)]);
        Compatibility validator = new(source =>
        {
            Assert.Equal("gpt-5", source.SelectedModel);
            Assert.Equal("server-routed", source.ServerModel);
            Assert.Single(source.Items);
            return ServiceResult.Ok();
        });
        ServiceCollection services = Services(probe);
        services.AddSingleton<IContextModelCompatibility>(validator);
        await using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Equal(1, validator.Calls);
    }

    /// <summary>Равные server/target имена не заменяют отсутствие selected provenance historical окна.</summary>
    [Fact]
    public async Task ServerNameDoesNotProveSelectedCompatibility()
    {
        Probe probe = new();
        probe.Dialog = Copy(probe.Dialog, active: new(1, 0, ModelResponse.Completed([Opaque()],
            new(JsonSerializer.SerializeToElement(new { model = "gpt-4" })))));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        Assert.Equal(ServiceErrorType.Unsupported, (await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken)).Error!.Type);
    }

    /// <summary>Actual BPE показывает unknown estimate opaque, точное expiry, компакт count и мягкий порог без удаления.</summary>
    [Fact]
    public async Task StatusRetainsExpiredMetadataAndUnknownBudget()
    {
        Probe probe = new();
        probe.Dialog = Copy(probe.Dialog, active: new(3, 0, ModelResponse.Completed([Opaque()]), "gpt-5"), bytes: 10_485_760);
        probe.Clock.Now = probe.Dialog.ExpiresAtUtc!.Value;
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        DialogStatus status = Success(await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().GetStatusAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(status.IsExpired);
        Assert.False(status.CanContinue);
        Assert.True(status.SoftContentLimitReached);
        Assert.Equal(3, status.CompactionCount);
        Assert.NotNull(status.ContextSize);
        Assert.True(status.ContextSize.HasOpaqueContent);
        Assert.Null(status.ContextSize.EstimatedInputTokens);
        Assert.Null(status.CompactionThresholdReached);
        Assert.Equal(ServiceErrorType.Unsupported, status.ContextError!.Type);
        Assert.Equal(0, probe.Writes);
    }

    /// <summary>Последний report без model не подменяется старым server именем; selected всегда отдельно.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SelectedAndLastServerModelRemainDistinct(bool metadata)
    {
        Probe probe = new();
        ModelResponse response = ModelResponse.Completed([Message()], metadata
            ? new(JsonSerializer.SerializeToElement(new { model = "actual-server", secret = "synthetic-secret" })) : null);
        probe.Dialog = Copy(probe.Dialog, turns: [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message()], [new(Guid.NewGuid(), response)])]);
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        DialogStatus status = Success(await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().GetStatusAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("gpt-5", status.Settings!.Model.Model.Id);
        Assert.Equal(metadata ? "actual-server" : null, status.ServerModel);
        Assert.NotNull(status.ContextSize!.EstimatedInputTokens);
        Assert.True(status.CanContinue);
        Assert.DoesNotContain("synthetic-secret", JsonSerializer.Serialize(status));
    }

    /// <summary>Catalog refusal сохраняет срок/байты в статусе, но не разрешает продолжение.</summary>
    [Fact]
    public async Task CatalogFailureKeepsDialogMetadata()
    {
        Probe probe = new() { CatalogError = new(ServiceErrorType.Unauthorized, "Доступ отклонён.") };
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        DialogStatus status = Success(await scope.ServiceProvider.GetRequiredService<AgentSettingsService>().GetStatusAsync(probe.Call, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(probe.Dialog.ExpiresAtUtc, status.ExpiresAtUtc);
        Assert.Same(probe.CatalogError, status.ContextError);
        Assert.Null(status.Settings);
        Assert.False(status.CanContinue);
    }

    /// <summary>Fresh UTC после проверки каталога запрещает late запись; token не обновляется.</summary>
    [Fact]
    public async Task ExpiryDuringValidationRefusesWrite()
    {
        Probe probe = new();
        probe.AfterCatalog = () => probe.Clock.Now = probe.Dialog.ExpiresAtUtc!.Value;
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ServiceResult<DialogModelSelection> result = await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Expired, result.Error!.Type);
        Assert.Null(probe.Dialog.Selection);
    }

    /// <summary>Отказ конкурирующей записи возвращается без refresh/retry.</summary>
    [Fact]
    public async Task ConcurrentChangeAfterValidationReturnsConflictWithoutRetry()
    {
        Probe probe = new();
        probe.AfterCatalog = () => probe.Dialog = Copy(probe.Dialog, selection: new(1, "gpt-5", "high"));
        await using ServiceProvider root = Services(probe).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ServiceResult<DialogModelSelection> result = await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .SelectAsync(probe.Call, probe.Dialog.Token, 0, "gpt-4", "low", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Conflict, result.Error!.Type);
        Assert.Equal(1, probe.Writes);
        Assert.Equal("gpt-5", probe.Dialog.Selection!.Model);
    }

    /// <summary>Read-only status сохраняет происхождение приблизительного размера и учитывает строгую политику.</summary>
    [Theory]
    [InlineData(ContextBudgetPolicy.RequireLocalEstimate, false)]
    [InlineData(ContextBudgetPolicy.ServerValidation, true)]
    public async Task ApproximateStatusHonorsBudgetPolicy(ContextBudgetPolicy policy, bool canContinue)
    {
        Probe probe = new();
        probe.Dialog = Copy(probe.Dialog, selection: new(1, "gpt-6", "high"));
        ServiceCollection services = Services(probe);
        services.AddAgentBridgeTokenization(options => options.UnknownModelEstimateEncoding = "o200k_base");
        services.Configure<ContextCompactionOptions>(options => options.BudgetPolicy = policy);
        await using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();

        DialogStatus status = Success(await scope.ServiceProvider.GetRequiredService<AgentSettingsService>()
            .GetStatusAsync(probe.Call, TestContext.Current.CancellationToken));

        Assert.True(status.ContextSize!.IsApproximateEncoding);
        Assert.NotNull(status.ContextSize.EstimatedInputTokens);
        Assert.Equal(canContinue, status.CanContinue);
        if (canContinue)
            Assert.Null(status.ContextError);
        else
            Assert.Equal(ServiceErrorType.Unsupported, status.ContextError!.Type);
    }

    private static ServiceCollection Services(Probe probe)
    {
        ServiceCollection services = new();
        services.AddSingleton(probe);
        services.AddSingleton<TimeProvider>(probe.Clock);
        services.AddSingleton<IDialogReader, Store>();
        services.AddSingleton<IDialogSettingsWriter, Store>();
        services.AddScoped<IModelSettingsReader, Models>();
        services.AddSingleton<IContextTokenCounter, ContextTokenCounter>();
        services.AddAgentBridgeConfiguration(options => { options.Instructions = "synthetic-secret"; options.InstructionsSource = AgentInstructionsSource.Configuration; options.MaxToolSteps = 8; },
            options => { options.RetentionPeriod = TimeSpan.FromDays(7); options.SoftContentLimitBytes = 10_485_760; },
            options => { options.TokenThreshold = 32_000; options.InputTokenReserve = 4_096; options.MaxPasses = 3; });
        services.AddAgentBridgeSettings();
        return services;
    }
    private static T Success<T>(ServiceResult<T> result) where T : class { Assert.True(result.Success, result.Error?.Message); return result.Data!; }
    private static CanonicalModelItem Message() => new(JsonSerializer.SerializeToElement(new { type = "message", role = "assistant", content = "Привет" }));
    private static CanonicalModelItem Opaque() => new(JsonSerializer.SerializeToElement(new { type = "reasoning", encrypted_content = "synthetic-secret", summary = Array.Empty<object>() }));
    private static DialogSnapshot Copy(DialogSnapshot dialog, IEnumerable<StoredDialogTurn>? turns = null, StoredDialogContext? active = null,
        long? bytes = null, DialogModelSelection? selection = null) => new(dialog.Token, dialog.OwnerId, dialog.CreatedAtUtc, dialog.ExpiresAtUtc,
            bytes ?? dialog.ContentBytes, turns ?? dialog.Turns, active ?? dialog.ActiveContext, selection ?? dialog.Selection);

    private class Probe
    {
        public ApplicationCallContext Call { get; } = new(DialogId.From(Guid.NewGuid()), DialogOwnerId.From("owner"), Guid.NewGuid(), "agent");
        public Clock Clock { get; } = new();
        public DialogSnapshot Dialog;
        public int Writes;
        public Action? AfterCatalog;
        public ServiceError? CatalogError;
        public Probe() => Dialog = new(new(Call.DialogId, Guid.NewGuid(), 0), Call.OwnerId, NOW, NOW.AddDays(1), 0, [], null);
    }
    private class Clock : TimeProvider
    {
        public DateTimeOffset Now = NOW;
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => Now;
    }
    /// <inheritdoc cref="IDialogReader"/>
    private class Store(Probe probe) : IDialogReader, IDialogSettingsWriter
    {
        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<DialogSnapshot>.Ok(probe.Dialog));
        /// <inheritdoc/>
        public Task<ServiceResult<DialogModelSelection>> SaveAsync(DialogAccess access, DialogWriteToken expected, long expectedVersion,
            ModelSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            probe.Writes++;
            if (access.NowUtc >= probe.Dialog.ExpiresAtUtc) return Task.FromResult(ServiceResult<DialogModelSelection>.Fail(new(ServiceErrorType.Expired, "Срок истёк.")));
            if ((probe.Dialog.Selection?.Version ?? 0) != expectedVersion) return Task.FromResult(ServiceResult<DialogModelSelection>.Fail(new(ServiceErrorType.Conflict, "Выбор устарел.")));
            DialogModelSelection saved = new(expectedVersion + 1, settings.Model.Id, settings.ReasoningEffort);
            probe.Dialog = Copy(probe.Dialog, selection: saved);
            return Task.FromResult(ServiceResult<DialogModelSelection>.Ok(saved));
        }
    }
    /// <inheritdoc/>
    private class Models(Probe probe, IOptionsSnapshot<ContextCompactionOptions> context) : IModelSettingsReader
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null, string? effort = null, CancellationToken ct = default)
        {
            int threshold = context.Value.TokenThreshold;
            int reserve = context.Value.InputTokenReserve;
            probe.AfterCatalog?.Invoke();
            return Task.FromResult(probe.CatalogError is not null ? ServiceResult<ModelSettingsSnapshot>.Fail(probe.CatalogError) :
                ModelSelectionValidator.Validate(new([Capability("gpt-5"), Capability("gpt-4"), Capability("gpt-6")]), model ?? "gpt-5", effort ?? "high", threshold, reserve));
        }
        internal static ModelSettingsSnapshot Settings(string model, string effort) => new(Capability(model), effort, 32_000, 4096);
        private static ModelCapabilities Capability(string model) => new(model, true, 200_000, 200_000, 4096, ["high", "low"], "high", ["text"], true, true, true, true, false);
    }
    /// <inheritdoc/>
    private class Compatibility(Func<ContextModelSource, ServiceResult> check) : IContextModelCompatibility
    {
        public int Calls;
        /// <inheritdoc/>
        public Task<ServiceResult> CheckAsync(ApplicationCallContext call, ContextModelSource source, ModelSettingsSnapshot target, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(check(source));
        }
    }
}
