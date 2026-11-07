using System.Net;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Actual runner/compactor/Sub2Api с in-memory storage и локальным HTTP handler, без поставки и БД.</summary>
public class Sub2ApiAutomaticCompactionTests
{
    /// <summary>Локальный порог не вызывает compact; полный input доходит до generation, HTTP-отказ не повторяется.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task RunnerPassesFullContextAboveThreshold(bool unknownWindow, bool serverRejects, bool streaming)
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Fixture state = new(http.Call, unknownWindow);
        StoredDialogTurn original = Assert.Single(state.Dialog.Turns);
        JsonElement sent = default;
        http.Handler.Respond = async (request, ct) =>
        {
            sent = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            if (serverRejects)
            {
                return Sub2ApiCatalogFixture.Json("""{"error":{"code":"context_length_exceeded","message":"synthetic-private-size-error"}}""",
                    HttpStatusCode.BadRequest);
            }

            return Sub2ApiCatalogFixture.Json("""{"status":"completed","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"done"}]}]}""");
        };
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using IServiceScope scope = root.CreateScope();
        AgentRunner runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
        CanonicalModelItem current = Item("""{"type":"message","role":"user","content":"current","future":true}""");
        AgentRunRequest run = new(http.Call, [current], [], new(1, 1, 1, TimeSpan.FromMinutes(1)));
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback = null;
        if (streaming)
        {
            callback = (_, _) => ValueTask.CompletedTask;
        }

        // Act
        AgentRunResult result = await runner.RunAsync(run, callback, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(http.Gateway.AutomaticCompactionEnabled);
        Assert.Equal(1, http.Handler.Calls);
        Assert.EndsWith("/v1/responses", Assert.Single(http.Handler.Urls));
        Assert.Equal(0, state.ContextSaves);
        Assert.Null(state.Dialog.ActiveContext);
        Assert.Same(original, state.Dialog.Turns[0]);
        Assert.NotNull(result.LastBudgetAssessment);
        Assert.True(result.LastBudgetAssessment.ThresholdReached);
        Assert.True(result.LastBudgetAssessment.RequiresServerValidation);
        Assert.Equal(state.Settings.Model.InputContextWindow, result.LastBudgetAssessment.DeclaredInputContextWindow);
        Assert.Equal("instructions", sent.GetProperty("instructions").GetString());
        Assert.Equal("medium", sent.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.False(sent.TryGetProperty("previous_response_id", out _));
        Assert.Equal(streaming, sent.GetProperty("stream").GetBoolean());
        JsonElement[] actual = sent.GetProperty("input").EnumerateArray().ToArray();
        CanonicalModelItem[] expected = [.. original.Items, current];
        Assert.Equal(expected.Length, actual.Length);
        foreach ((CanonicalModelItem item, JsonElement value) in expected.Zip(actual))
        {
            Assert.True(JsonElement.DeepEquals(item.Content, value));
        }

        Assert.True(result.TerminalSaved);
        if (serverRejects)
        {
            Assert.Equal(AgentRunStatus.Failed, result.Status);
            Assert.NotNull(result.Error);
            Assert.DoesNotContain("synthetic-private", result.Error.Message);
        }
        else
        {
            Assert.Equal(AgentRunStatus.Completed, result.Status);
        }
    }

    /// <summary>Отключение автоматического compact не обходит известный входной лимит.</summary>
    [Fact]
    public async Task KnownBudgetStillRejectsBeforeHttp()
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Fixture state = new(http.Call, false) { KnownTokens = 2000 };
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AgentRunRequest request = new(http.Call, [Item("""{"type":"message","role":"user","content":"current"}""")], [],
            new(1, 1, 1, TimeSpan.FromMinutes(1)));

        // Act
        AgentRunResult result = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Equal(0, http.FactoryCalls);
        Assert.Equal(0, http.Handler.Calls);
        Assert.Equal(0, state.ContextSaves);
        Assert.Null(state.Dialog.ActiveContext);
    }

    /// <summary>Automatic возвращает Disabled без записи, force получает Unsupported до HTTP; история неизменна.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledAndForcedCompactRemainDistinct(bool force)
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Fixture state = new(http.Call, true);
        DialogSnapshot original = state.Dialog;
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ContextCompactor compactor = new(new([]), state, http.Gateway, state, TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>());

        // Act
        ContextCompactionResult result = (await compactor.CompactAsync(http.Call, original, http.Request, state.Settings,
            http.Access, TestContext.Current.CancellationToken, force)).Data!;

        // Assert
        Assert.Equal(0, http.FactoryCalls);
        Assert.Equal(0, http.Handler.Calls);
        Assert.Equal(0, state.ContextSaves);
        Assert.Same(original, state.Dialog);
        Assert.Same(original.Token, result.Token);
        Assert.Null(result.ActiveContext);
        Assert.Equal(original.Turns[0].Items, result.PreparedRequest.Input);
        if (force)
        {
            Assert.Equal(ContextCompactionStatus.Failed, result.Status);
            Assert.Equal(1, result.Passes);
            Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
            Assert.Contains("сжатого окна", result.Error.Message);
        }
        else
        {
            Assert.Equal(ContextCompactionStatus.Disabled, result.Status);
            Assert.Equal(0, result.Passes);
            Assert.Null(result.Error);
        }
    }

    /// <summary>Отключение не расширяет принятое окно и не теряет непокрытый terminal prefix/хвост.</summary>
    [Fact]
    public async Task DisabledCompactRetainsAcceptedWindowAndUncoveredTail()
    {
        // Arrange
        using Sub2ApiGenerationFixture http = new();
        Fixture state = new(http.Call, true, activeWindow: true);
        DialogSnapshot original = state.Dialog;
        await using ServiceProvider root = state.Services(http.Gateway).BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        ContextCompactor compactor = new(new([]), state, http.Gateway, state, TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>());
        CanonicalModelItem current = Item("""{"type":"message","role":"user","content":"current"}""");
        ModelRequest request = new("public-model", "medium", "instructions", [current], []);

        // Act
        ContextCompactionResult result = (await compactor.CompactAsync(http.Call, original, request, state.Settings,
            http.Access, TestContext.Current.CancellationToken)).Data!;

        // Assert
        Assert.Equal(ContextCompactionStatus.Disabled, result.Status);
        Assert.Same(original.ActiveContext, result.ActiveContext);
        Assert.Equal(1, result.ActiveContext!.ThroughTurnSequence);
        Assert.Equal(original.ActiveContext!.Items.Concat(original.Turns[1].Items).Append(current), result.PreparedRequest.Input);
        Assert.Equal(2, original.Turns.Count);
        Assert.Same(original, state.Dialog);
        Assert.Equal(0, state.ContextSaves);
        Assert.Equal(0, http.FactoryCalls);
        Assert.Equal(0, http.Handler.Calls);
    }

    /// <summary>Канонический item с независимой копией JSON.</summary>
    private static CanonicalModelItem Item(string json)
    {
        return new(JsonSerializer.Deserialize<JsonElement>(json));
    }

    /// <summary>Наблюдает только budget и существующие короткие storage ports; HTTP остаётся actual коннектором.</summary>
    private class Fixture : IDialogReader, IDialogTurnWriter, IDialogContextWriter, IModelAccessResolver,
        IModelSettingsReader, IContextTokenCounter
    {
        internal ModelSettingsSnapshot Settings { get; }
        internal DialogSnapshot Dialog { get; private set; }
        internal int ContextSaves { get; private set; }
        internal long KnownTokens { get; set; } = 200;

        /// <summary>Фиксирует полную завершённую историю с encrypted reasoning и закрытой tool pair.</summary>
        internal Fixture(ApplicationCallContext call, bool unknownWindow, bool activeWindow = false)
        {
            int? window = 1000;
            ContextBudgetPolicy policy = ContextBudgetPolicy.ServerValidation;
            if (unknownWindow)
            {
                window = null;
                policy = ContextBudgetPolicy.ServerValidationWithUnknownWindow;
            }

            Settings = new(new("public-model", true, null, window, null, ["medium"], null, [], true, false, false, false, false),
                "medium", 10, 10, policy);
            CanonicalModelItem[] history =
            [
                Item("""{"type":"message","role":"user","content":"old constraints"}"""),
                Item("""{"type":"reasoning","encrypted_content":"opaque-history","content":[{"type":"reasoning_text","text":"keep"}],"future":19}"""),
                Item("""{"type":"function_call","call_id":"old-call","name":"old-tool","arguments":"{}"}"""),
                Item("""{"type":"function_call_output","call_id":"old-call","output":"confirmed"}""")
            ];
            List<StoredDialogTurn> turns = [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, history, [], TurnModelSettings.From(Settings))];
            StoredDialogContext? context = null;
            if (activeWindow)
            {
                context = new(1, 1, ModelResponse.Completed([Item("""{"type":"compaction","encrypted_content":"accepted-window","future":true}""")]),
                    Settings.Model.Id);
                turns.Add(new(Guid.NewGuid(), 2, DialogTurnStatus.Completed,
                    [Item("""{"type":"message","role":"user","content":"uncovered-tail"}""")], [], TurnModelSettings.From(Settings)));
            }

            Dialog = new(new(call.DialogId, Guid.NewGuid(), 0), call.OwnerId, DateTimeOffset.UtcNow.AddMinutes(-1), null, 0, turns, context);
        }

        /// <summary>Собирает actual core orchestration с переданным actual Sub2Api gateway.</summary>
        internal ServiceCollection Services(IModelGateway gateway)
        {
            ServiceCollection services = new();
            services.AddSingleton<IDialogReader>(this);
            services.AddSingleton<IDialogTurnWriter>(this);
            services.AddSingleton<IDialogContextWriter>(this);
            services.AddSingleton<IModelAccessResolver>(this);
            services.AddSingleton<IModelSettingsReader>(this);
            services.AddSingleton<IContextTokenCounter>(this);
            services.AddSingleton(gateway);
            services.AddScoped<ContextBuilder>(_ => new([]));
            services.Configure<AgentOptions>(options =>
            {
                options.Instructions = "instructions";
                options.InstructionsSource = AgentInstructionsSource.Configuration;
                options.MaxToolSteps = 1;
            });
            services.Configure<ContextCompactionOptions>(options =>
            {
                options.MaxPasses = 1;
                options.BudgetPolicy = Settings.BudgetPolicy;
            });
            services.AddAgentBridgeTools();
            services.AddAgentBridgeRunner();
            return services;
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogSnapshot>> ReadAsync(DialogAccess access, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<DialogSnapshot>.Ok(Dialog));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> BeginWithSettingsAsync(DialogAccess access, DialogWriteToken expected,
            Guid turnId, IReadOnlyList<CanonicalModelItem> input, TurnModelSettings settings, CancellationToken cancellationToken = default)
        {
            return Write(expected, Dialog.Turns.Append(new(turnId, Dialog.Turns.Count + 1, DialogTurnStatus.InProgress, input, [], settings)));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> BeginAsync(DialogAccess access, DialogWriteToken expected, Guid turnId,
            IReadOnlyList<CanonicalModelItem> input, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Runner requires atomic settings.");
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> AppendAsync(DialogAccess access, DialogWriteToken expected, Guid turnId,
            IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> modelSteps, CancellationToken cancellationToken = default)
        {
            return Change(expected, turnId, DialogTurnStatus.InProgress, items, modelSteps);
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> FinishAsync(DialogAccess access, DialogWriteToken expected, Guid turnId,
            DialogTurnStatus status, IReadOnlyList<CanonicalModelItem> newItems, IReadOnlyList<StoredModelStep> modelSteps,
            CancellationToken cancellationToken = default)
        {
            return Change(expected, turnId, status, newItems, modelSteps);
        }

        /// <summary>Append/finish сохраняют исходную историю и настройки без compact.</summary>
        private Task<ServiceResult<DialogWriteToken>> Change(DialogWriteToken expected, Guid id, DialogTurnStatus status,
            IReadOnlyList<CanonicalModelItem> items, IReadOnlyList<StoredModelStep> steps)
        {
            StoredDialogTurn current = Dialog.Turns.Single(turn => turn.Id == id);
            StoredDialogTurn next = new(id, current.Sequence, status, current.Items.Concat(items), current.ModelSteps.Concat(steps), current.Settings);
            return Write(expected, Dialog.Turns.Where(turn => turn.Id != id).Append(next));
        }

        /// <summary>Наблюдает последовательные accepted tokens; не имитирует relational transaction acceptance.</summary>
        private Task<ServiceResult<DialogWriteToken>> Write(DialogWriteToken expected, IEnumerable<StoredDialogTurn> turns)
        {
            Assert.Equal(Dialog.Token.Revision, expected.Revision);
            DialogWriteToken next = new(expected.DialogId, expected.IncarnationId, expected.Revision + 1);
            Dialog = new(next, Dialog.OwnerId, Dialog.CreatedAtUtc, Dialog.ExpiresAtUtc, Dialog.ContentBytes, turns, Dialog.ActiveContext);
            return Task.FromResult(ServiceResult<DialogWriteToken>.Ok(next));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<DialogWriteToken>> SaveAsync(DialogAccess access, DialogWriteToken expected, long throughTurnSequence,
            ModelResponse compaction, CancellationToken cancellationToken = default)
        {
            ContextSaves++;
            throw new InvalidOperationException("Disabled compact must not save a window.");
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ModelAccess>> ResolveAsync(DialogOwnerId ownerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ModelAccess>.Ok(new(Sub2ApiCatalogFixture.INDIVIDUAL_KEY)));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadAsync(DialogOwnerId ownerId, string? model = null,
            string? effort = null, CancellationToken ct = default)
        {
            return Task.FromResult(ServiceResult<ModelSettingsSnapshot>.Ok(Settings));
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ModelSettingsSnapshot>> ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access,
            string? model = null, string? effort = null, CancellationToken ct = default)
        {
            return ReadAsync(ownerId, model, effort, ct);
        }

        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ContextTokenCount>.Ok(new("isolated", KnownTokens, null, true)));
        }
    }
}
