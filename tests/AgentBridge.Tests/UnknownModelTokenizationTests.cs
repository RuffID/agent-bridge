using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Tokenization;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Оценка неизвестных кодировок через actual BPE и guard без сети, хоста и БД.</summary>
public class UnknownModelTokenizationTests
{
    /// <summary>Новые ID не требуют точного mapping, но приблизительность разрешена только явной серверной политикой.</summary>
    [Theory]
    [InlineData("gpt-6.1-sol")]
    [InlineData("gpt-6-sol")]
    [InlineData("gpt-6-astra")]
    [InlineData("gpt-6-luna")]
    [InlineData("future-test-model")]
    public async Task UnknownModelEstimateRequiresServerPolicy(string model)
    {
        ContextTokenCounter counter = Counter();
        ModelRequest request = new(model, "medium", "Привет", [], []);
        ContextTokenCount count = (await counter.CountAsync(request, TestContext.Current.CancellationToken)).Data!;
        ModelSettingsSnapshot settings = Settings(model, checked((int)count.EstimatedInputTokens!.Value + 10),
            checked((int)count.EstimatedInputTokens.Value), 10);

        Assert.Equal("o200k_base", count.Encoding);
        Assert.True(count.IsApproximateEncoding);
        Assert.Equal(2, count.KnownTokens);
        Assert.False(count.HasOpaqueContent);
        ServiceResult<ContextBudgetAssessment> strict = await new ContextBudgetGuard(counter).CheckAsync(
            request, settings, TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Unsupported, strict.Error!.Type);

        ServiceResult<ContextBudgetAssessment> allowed = await new ContextBudgetGuard(counter, ContextBudgetPolicy.ServerValidation)
            .CheckAsync(request, settings, TestContext.Current.CancellationToken);
        Assert.True(allowed.Success);
        Assert.True(allowed.Data!.RequiresServerValidation);
        Assert.True(allowed.Data.ThresholdReached);
        Assert.True(allowed.Data.TokenCount.IsApproximateEncoding);

        ServiceResult<ContextBudgetAssessment> over = await new ContextBudgetGuard(counter, ContextBudgetPolicy.ServerValidation)
            .CheckAsync(request, Settings(model, settings.Model.InputContextWindow!.Value, 1, 11), TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Rejected, over.Error!.Type);
    }

    /// <summary>Подтверждённые builtin/configured соответствия имеют приоритет над оценочным словарём.</summary>
    [Theory]
    [InlineData("gpt-5.6-sol", "o200k_base", 2L)]
    [InlineData("gpt-5.6-terra", "o200k_base", 2L)]
    [InlineData("gpt-5.6-luna", "o200k_base", 2L)]
    [InlineData("configured-test-model", "cl100k_base", 3L)]
    public async Task ConfirmedEncodingTakesPriority(string model, string encoding, long tokens)
    {
        ContextTokenCounter counter = new(Options.Create(new TokenizationOptions
        {
            UnknownModelEstimateEncoding = "cl100k_base",
            ModelEncodings = new() { ["configured-test-model"] = "cl100k_base" }
        }));
        ModelRequest request = new(model, "medium", "Привет", [], []);
        ContextTokenCount count = (await counter.CountAsync(request, TestContext.Current.CancellationToken)).Data!;

        Assert.Equal(encoding, count.Encoding);
        Assert.Equal(tokens, count.KnownTokens);
        Assert.False(count.IsApproximateEncoding);
        ContextBudgetAssessment budget = (await new ContextBudgetGuard(counter).CheckAsync(
            request, Settings(model, 1000, 100, 10), TestContext.Current.CancellationToken)).Data!;
        Assert.False(budget.RequiresServerValidation);
    }

    /// <summary>Изображения/continuation сохраняют неизвестный полный размер даже при оценочной кодировке.</summary>
    [Fact]
    public async Task ApproximateEncodingDoesNotInventOpaqueBudget()
    {
        using JsonDocument content = JsonDocument.Parse("""{"role":"user","content":[{"type":"input_text","text":"Привет"},{"type":"input_image","image_url":"private"}]}""");
        ModelRequest request = new("gpt-6-astra", "medium", "", [new(content.RootElement)], []);
        ContextBudgetAssessment budget = (await new ContextBudgetGuard(Counter(), ContextBudgetPolicy.ServerValidation)
            .CheckAsync(request, Settings(request.Model, 1000, 100, 10), TestContext.Current.CancellationToken)).Data!;

        Assert.True(budget.TokenCount.IsApproximateEncoding);
        Assert.True(budget.TokenCount.HasOpaqueContent);
        Assert.Equal(2, budget.TokenCount.KnownTokens);
        Assert.Null(budget.TokenCount.EstimatedInputTokens);
        Assert.True(budget.RequiresServerValidation);
    }

    /// <summary>Public DTO не позволяет выдать приблизительный count за проверенную локальную оценку.</summary>
    [Fact]
    public void AssessmentCannotHideApproximateEncoding()
    {
        ContextTokenCount count = new("o200k_base", 2, 20, false, isApproximateEncoding: true);
        Assert.Throws<ArgumentException>(() => new ContextBudgetAssessment(count, 100, 10, false));
        Assert.True(new ContextBudgetAssessment(count, 100, 10, false, requiresServerValidation: true).RequiresServerValidation);
        Assert.Throws<ArgumentException>(() => new ContextBudgetAssessment(new("o200k_base", 0, null, false, true),
            100, 10, false, requiresServerValidation: true));
    }

    /// <summary>Счётчик фиксирует оценочную настройку при создании, без чтения mutable options на каждом запросе.</summary>
    [Fact]
    public async Task EstimateEncodingIsFrozenUntilCounterIsRecreated()
    {
        TokenizationOptions options = new() { UnknownModelEstimateEncoding = "o200k_base" };
        ContextTokenCounter original = new(Options.Create(options));
        options.UnknownModelEstimateEncoding = "cl100k_base";
        ModelRequest request = new("future-test-model", "medium", "Привет", [], []);

        Assert.Equal(2, (await original.CountAsync(request, TestContext.Current.CancellationToken)).Data!.KnownTokens);
        Assert.Equal(3, (await new ContextTokenCounter(Options.Create(options))
            .CountAsync(request, TestContext.Current.CancellationToken)).Data!.KnownTokens);
    }

    /// <summary>Создаёт actual BPE с явным оценочным словарём без подтверждения модели.</summary>
    private static ContextTokenCounter Counter() => new(Options.Create(new TokenizationOptions
        { UnknownModelEstimateEncoding = "o200k_base" }));

    /// <summary>Создаёт catalog-backed settings для проверки порога, резерва и политики.</summary>
    private static ModelSettingsSnapshot Settings(string model, int window, int threshold, int reserve) => new(
        new(model, true, window, window, 100, ["medium"], "medium", ["text", "image"], true, false, false, false, false),
        "medium", threshold, reserve);
}
