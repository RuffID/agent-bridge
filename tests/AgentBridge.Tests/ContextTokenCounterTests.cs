using System.IO.Compression;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Tokenization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Публичный offline BPE counter: известные vectors, whole composition и неизвестный полный бюджет.</summary>
public class ContextTokenCounterTests
{
    /// <summary>Проверяет фиксированные BPE vectors обеих кодировок и ordinary special markers.</summary>
    [Theory]
    [InlineData("gpt-4", "Hello World", 2, "cl100k_base")]
    [InlineData("gpt-5", "Hello World", 2, "o200k_base")]
    [InlineData("gpt-5.5", "Hello World", 2, "o200k_base")]
    [InlineData("gpt-5.6-sol", "Hello World", 2, "o200k_base")]
    [InlineData("gpt-4", "Привет", 3, "cl100k_base")]
    [InlineData("gpt-5", "Привет", 2, "o200k_base")]
    [InlineData("gpt-5.5", "Привет", 2, "o200k_base")]
    [InlineData("gpt-5.6-sol", "Привет", 2, "o200k_base")]
    [InlineData("gpt-4", "я", 1, "cl100k_base")]
    [InlineData("gpt-5", "я", 1, "o200k_base")]
    [InlineData("gpt-4", "2 + 2 = 4", 7, "cl100k_base")]
    [InlineData("gpt-5", "2 + 2 = 4", 7, "o200k_base")]
    [InlineData("gpt-4", "{\"a\":1}", 5, "cl100k_base")]
    [InlineData("gpt-5", "{\"a\":1}", 5, "o200k_base")]
    [InlineData("gpt-4", "<|endoftext|>", 7, "cl100k_base")]
    [InlineData("gpt-5", "<|endoftext|>", 7, "o200k_base")]
    [InlineData("gpt-4", "", 0, "cl100k_base")]
    [InlineData("gpt-5", "", 0, "o200k_base")]
    public async Task KnownVectorsUseBpe(string model, string text, long expected, string encoding)
    {
        ContextTokenCount count = await Count(new(model, "medium", text, [], []));
        Assert.Equal(encoding, count.Encoding);
        Assert.Equal(expected, count.KnownTokens);
        Assert.False(count.HasOpaqueContent);
        Assert.True(count.EstimatedInputTokens >= count.KnownTokens);
    }

    /// <summary>Проверяет весь конечный подтверждённый mapping.</summary>
    [Theory]
    [InlineData("gpt-5", "o200k_base", 2L)]
    [InlineData("gpt-5.5", "o200k_base", 2L)]
    [InlineData("gpt-5.6-sol", "o200k_base", 2L)]
    [InlineData("gpt-4.1", "o200k_base", 2L)]
    [InlineData("gpt-4o", "o200k_base", 2L)]
    [InlineData("o1", "o200k_base", 2L)]
    [InlineData("o3", "o200k_base", 2L)]
    [InlineData("o4-mini", "o200k_base", 2L)]
    [InlineData("gpt-4", "cl100k_base", 3L)]
    [InlineData("gpt-3.5-turbo", "cl100k_base", 3L)]
    public async Task ExactConfirmedAliasesSupported(string model, string encoding, long tokens)
    {
        ServiceResult<ContextTokenCount> result = await new ContextTokenCounter().CountAsync(
            new(model, null, "Привет", [], []), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        ContextTokenCount count = Assert.IsType<ContextTokenCount>(result.Data);
        Assert.Equal(encoding, count.Encoding);
        Assert.Equal(tokens, count.KnownTokens);
        Assert.False(count.HasOpaqueContent);
        Assert.True(count.EstimatedInputTokens >= tokens);
    }

    /// <summary>Запрещает case folding, prefix fallback и утечку исходных значений.</summary>
    [Theory]
    [InlineData("GPT-5")]
    [InlineData("gpt-5-FAKE")]
    [InlineData("gpt-5.5-FAKE")]
    [InlineData("gpt-5.6-sol-FAKE")]
    [InlineData("GPT-5.6-SOL")]
    [InlineData("gpt-5.4")]
    [InlineData("gpt-6")]
    [InlineData("o1-arbitrary")]
    [InlineData("unknown-secret-model")]
    public async Task UnconfirmedExactIdIsSafeUnsupported(string model)
    {
        ServiceResult<ContextTokenCount> result = await new ContextTokenCounter().CountAsync(new(model, null, "secret", [], []), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.DoesNotContain(model, result.Error.Message);
        Assert.DoesNotContain("secret", result.Error.Message);
    }

    /// <summary>Проходит actual builder→counter, проверяя всю композицию и неизменность истории.</summary>
    [Fact]
    public async Task ActualBuilderCountsProvidersHistoryNewInputInstructionsToolsAndResults()
    {
        DateTimeOffset now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        DialogId id = DialogId.From(Guid.NewGuid());
        DialogOwnerId owner = DialogOwnerId.From("owner");
        DialogSnapshot snapshot = new(new(id, Guid.NewGuid(), 1), owner, now.AddDays(-1), now.AddDays(1), 0,
            [new(Guid.NewGuid(), 1, DialogTurnStatus.Completed, [Message("123")], [])], null);
        CanonicalModelItem call = Item("""{"type":"function_call","call_id":"c","name":"name","arguments":"{\"a\":1}"}""");
        CanonicalModelItem output = Item("""{"type":"function_call_output","call_id":"c","output":"Hello World"}""");
        ModelToolDefinition tool = new("name", "Hello World", Json("""{"a":1}"""), true);
        ModelRequest fresh = new("gpt-5", "medium", "Hello World", [Message("Привет"), call, output], [tool]);
        ServiceResult<ModelRequest> built = await new ContextBuilder([new Provider()]).BuildAsync(
            new(id, owner, Guid.NewGuid(), "agent"), snapshot, fresh, now, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(built.Success);
        ModelRequest prepared = built.Data!;
        string original = JsonSerializer.Serialize(prepared.Input.Select(item => item.Content));
        ContextTokenCount count = await Count(prepared);
        // instructions2 + provider1 + history1 + new2 + call name1/arguments5 + output2 + tool name1/description2/schema5.
        Assert.Equal(22, count.KnownTokens);
        Assert.False(count.HasOpaqueContent);
        Assert.NotNull(count.EstimatedInputTokens);
        Assert.Equal(original, JsonSerializer.Serialize(prepared.Input.Select(item => item.Content)));
        Assert.Single(snapshot.Turns);
        Assert.Equal(now.AddDays(1), snapshot.ExpiresAtUtc);
        ContextTokenCount newestOnly = await Count(new("gpt-5", "medium", "", [Message("Привет")], []));
        Assert.True(count.EstimatedInputTokens > newestOnly.EstimatedInputTokens);
    }

    /// <summary>Отличает input constraints от управляющих transport metadata.</summary>
    [Fact]
    public async Task FormatSchemaAndToolChoiceContributeButTransportControlsDoNot()
    {
        ContextTokenCount baseline = await Count(Request([Message("Привет")]));
        string controls = """{"parallel_tool_calls":true,"include":["reasoning.encrypted_content"],"service_tier":"auto","truncation":"disabled","prompt_cache_key":"secret","reasoning":{"summary":"auto"}}""";
        ContextTokenCount transport = await Count(Request([Message("Привет")], new(Json(controls))));
        Assert.Equal(baseline.KnownTokens, transport.KnownTokens);
        Assert.Equal(baseline.EstimatedInputTokens, transport.EstimatedInputTokens);
        string constraints = """{"tool_choice":{"type":"function","name":"name"},"text":{"format":{"type":"json_schema","name":"answer","schema":{"type":"object","properties":{"число":{"type":"number","description":"Привет","default":123}}},"strict":true}}}""";
        ContextTokenCount input = await Count(Request([Message("Привет")], new(Json(constraints))));
        Assert.True(input.KnownTokens > transport.KnownTokens);
        Assert.True(input.EstimatedInputTokens > transport.EstimatedInputTokens);
        Assert.False(input.HasOpaqueContent);
    }

    /// <summary>Сохраняет известный текст отдельно от multimodal/opaque или unknown fields.</summary>
    [Theory]
    [InlineData("""{"type":"message","role":"user","content":[{"type":"input_text","text":"Привет"},{"type":"input_image","image_url":"data:image/png;base64,secret"}]}""")]
    [InlineData("""{"type":"message","role":"user","content":[{"type":"input_text","text":"Привет"},{"type":"input_file","file_id":"secret"}]}""")]
    [InlineData("""{"type":"message","role":"user","content":"Привет","future":{"payload":"secret"}}""")]
    [InlineData("""{"type":"message","role":"user","content":[{"type":"output_text","text":"Привет","annotations":[{"future":"secret"}]}]}""")]
    [InlineData("""{"type":"reasoning","encrypted_content":"secret","summary":[{"type":"summary_text","text":"Привет"}]}""")]
    public async Task OpaqueRetainsKnownTextAndNullEstimate(string json)
    {
        CanonicalModelItem item = Item(json);
        string unchanged = item.Content.GetRawText();
        ContextTokenCount count = await Count(Request([item]));
        Assert.Equal(2, count.KnownTokens);
        Assert.True(count.HasOpaqueContent);
        Assert.Null(count.EstimatedInputTokens);
        Assert.Equal(unchanged, item.Content.GetRawText());
    }

    /// <summary>Не выдаёт неизвестную форму за известный нулевой бюджет.</summary>
    [Theory]
    [InlineData("""{"type":"compaction","encrypted_content":"secret"}""")]
    [InlineData("""{"type":"future","text":"secret"}""")]
    [InlineData("""{"type":"message","role":"user","content":[{"type":"future_text","text":"secret"}]}""")]
    [InlineData("""{"type":"message","role":"user","content":null}""")]
    [InlineData("""{"type":"message","role":"user","content":"","content":"secret"}""")]
    public async Task UnknownPayloadIsNeverZeroFullBudget(string json)
    {
        ContextTokenCount count = await Count(Request([Item(json)]));
        Assert.True(count.HasOpaqueContent);
        Assert.Null(count.EstimatedInputTokens);
    }

    /// <summary>Не игнорирует новые prompt-affecting controls.</summary>
    [Theory]
    [InlineData("""{"future":{"text":"secret"}}""")]
    [InlineData("""{"text":{"future":{"payload":"secret"}}}""")]
    [InlineData("""{"text":{"format":{"type":"future"}}}""")]
    [InlineData("""{"reasoning":{"future":"secret"}}""")]
    [InlineData("""{"tool_choice":{"type":"future","payload":"secret"}}""")]
    public async Task UnknownControlsInvalidateFullEstimate(string json)
    {
        ContextTokenCount count = await Count(Request([Message("Привет")], new(Json(json))));
        Assert.True(count.KnownTokens >= 2);
        Assert.True(count.HasOpaqueContent);
        Assert.Null(count.EstimatedInputTokens);
    }

    /// <summary>Не токенизирует continuation IDs и не угадывает скрытый серверный input.</summary>
    [Fact]
    public async Task ContinuationMetadataIsNotInputAndHiddenStatePreventsFullEstimate()
    {
        ContextTokenCount count = await Count(new("gpt-5", "medium", "", [Message("Привет")], [],
            new(Json("""{"previous_response_id":"secret","opaque":{"unknown":"huge"}}"""))));
        Assert.Equal(2, count.KnownTokens);
        Assert.True(count.HasOpaqueContent);
        Assert.Null(count.EstimatedInputTokens);
    }

    /// <summary>Соблюдает исходный caller token.</summary>
    [Fact]
    public async Task CallerCancellationKeepsOriginalToken()
    {
        using CancellationTokenSource caller = new();
        caller.Cancel();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ContextTokenCounter().CountAsync(Request([]), caller.Token));
        Assert.Equal(caller.Token, error.CancellationToken);
    }

    /// <summary>Проверяет public DI путь без hosting/network.</summary>
    [Fact]
    public async Task PublicRegistrationIsIdempotentAndResolvesActualCounterAndGuard()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeTokenization().AddAgentBridgeTokenization();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.Single(services, item => item.ServiceType == typeof(IContextTokenCounter));
        Assert.Single(services, item => item.ServiceType == typeof(ContextBudgetGuard));
        IContextTokenCounter counter = provider.GetRequiredService<IContextTokenCounter>();
        Assert.IsType<ContextTokenCounter>(counter);
        Assert.NotNull(provider.GetRequiredService<ContextBudgetGuard>());
        Assert.Equal(2, (await counter.CountAsync(Request([Message("Привет")]), cancellationToken: TestContext.Current.CancellationToken)).Data!.KnownTokens);
    }

    /// <summary>Восстанавливает ranks, удалённые package build, и проверяет canonical OpenAI vocabulary hashes.</summary>
    [Theory]
    [InlineData("Cl100kBase", "cl100k_base", "223921b76ee99bde995b7ff738513eef100fb51d18c93597a113bcffe865b2a7")]
    [InlineData("O200kBase", "o200k_base", "446a9538cb6c348e3516120d7c08b09f57c36495e2acfffe59a5bf8b0cfb1a2d")]
    public void EmbeddedVocabularyMatchesOpenAi(string suffix, string encoding, string expectedHash)
    {
        Assembly data = Assembly.Load($"Microsoft.ML.Tokenizers.Data.{suffix}");
        using Stream resource = data.GetManifestResourceStream($"{encoding}.tiktoken.deflate")
            ?? throw new InvalidOperationException("Fixture требует embedded resource.");
        using DeflateStream vocabulary = new(resource, CompressionMode.Decompress);
        using StreamReader reader = new(vocabulary, Encoding.UTF8);
        Assert.StartsWith("Capacity: ", reader.ReadLine());
        StringBuilder canonical = new();
        int rank = 0;
        while (reader.ReadLine() is string token)
        {
            canonical.Append(token).Append(' ').Append(rank.ToString(CultureInfo.InvariantCulture)).Append('\n');
            rank++;
        }
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant());
    }

    /// <summary>Проверяет singleton tokenizer/cache actual конкурентными calls с изолированным per-call state.</summary>
    [Fact]
    public async Task SharedCounterKeepsConcurrentCallsIndependent()
    {
        ContextTokenCounter counter = new();
        Task[] calls = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            string model = index % 2 == 0 ? "gpt-5" : "gpt-4";
            long expected = index % 2 == 0 ? 2 : 3;
            foreach (int iteration in Enumerable.Range(0, 40))
            {
                ModelRequest request = new(model, null, "Привет", [], []);
                ContextTokenCount count = (await counter.CountAsync(request)).Data!;
                Assert.Equal(expected, count.KnownTokens);
                Assert.False(count.HasOpaqueContent);
            }
        })).ToArray();
        await Task.WhenAll(calls);
    }

    /// <summary>Считает actual public реализацией, без HTTP или заглушки tokenizer.</summary>
    private static async Task<ContextTokenCount> Count(ModelRequest request)
    {
        ServiceResult<ContextTokenCount> result = await new ContextTokenCounter().CountAsync(request);
        Assert.True(result.Success);
        return result.Data!;
    }

    /// <summary>Создаёт prepared request без дополнительных инструкций.</summary>
    private static ModelRequest Request(IEnumerable<CanonicalModelItem> input, ModelRequestParameters? parameters = null) =>
        new("gpt-5", "medium", "", input, [], parameters: parameters);

    /// <summary>Фиксирует canonical item.</summary>
    private static CanonicalModelItem Item(string json) => new(Json(json));

    /// <summary>Создаёт message с обычным строковым content.</summary>
    private static CanonicalModelItem Message(string text) => Item(JsonSerializer.Serialize(new { role = "user", content = text }));

    /// <summary>Клонирует тестовый JSON.</summary>
    private static JsonElement Json(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <inheritdoc/>
    private class Provider : IContextProvider
    {
        /// <inheritdoc/>
        public Task<ServiceResult<ContextContribution>> GetContextAsync(ContextRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ContextContribution>.Ok(new([Message("я")])));
    }
}
