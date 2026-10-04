using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Tokenization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Публичная граница guard: actual BPE, input budget, reserve, threshold и ошибки без side effects.</summary>
public class ContextBudgetGuardTests
{
    /// <summary>Проверяет равенство threshold/budget и отказ без integer overflow.</summary>
    [Theory]
    [InlineData(90, 100, 10, 90, true, true)]
    [InlineData(89, 100, 10, 90, true, false)]
    [InlineData(91, 100, 10, 90, false, false)]
    [InlineData(int.MaxValue, int.MaxValue, 0, int.MaxValue, true, true)]
    [InlineData(int.MaxValue, int.MaxValue, 1, 1, false, false)]
    [InlineData(long.MaxValue, int.MaxValue, 0, 1, false, false)]
    public async Task EqualityAndOverflowAreExplicit(long estimate, int window, int reserve, int threshold,
        bool success, bool reached)
    {
        ContextTokenCount count = new("o200k_base", 1, estimate, false);
        ServiceResult<ContextBudgetAssessment> result = await new ContextBudgetGuard(new Counter(count)).CheckAsync(
            Request(), Settings(window, threshold, reserve));
        Assert.Equal(success, result.Success);
        if (success)
        {
            Assert.Same(count, result.Data!.TokenCount);
            Assert.Equal(reached, result.Data.ThresholdReached);
            Assert.Equal(reserve, result.Data.InputTokenReserve);
        }
        else { Assert.Null(result.Data); Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type); }
    }

    /// <summary>Проверяет реальный BPE запрос с instructions/tools на input границе.</summary>
    [Fact]
    public async Task ActualWholeRequestWithToolsHitsBoundaryAndThenReserveRejects()
    {
        using JsonDocument schema = JsonDocument.Parse("""{"type":"object","properties":{"число":{"type":"integer"}}}""");
        ModelRequest request = new("gpt-5", "medium", "Привет", [], [new("name", "Hello World", schema.RootElement, true)]);
        ContextTokenCounter counter = new();
        ContextTokenCount count = (await counter.CountAsync(request)).Data!;
        int estimate = checked((int)count.EstimatedInputTokens!.Value);
        ContextBudgetGuard guard = new(counter);
        ServiceResult<ContextBudgetAssessment> equal = await guard.CheckAsync(request, Settings(estimate + 10, estimate, 10));
        Assert.True(equal.Success);
        Assert.True(equal.Data!.ThresholdReached);
        ServiceResult<ContextBudgetAssessment> over = await guard.CheckAsync(request, Settings(estimate + 10, 1, 11));
        Assert.False(over.Success);
        Assert.Equal(ServiceErrorType.Rejected, over.Error!.Type);
    }

    /// <summary>Большой резерв не подменяет отсутствующую оценку multimodal input.</summary>
    [Fact]
    public async Task OpaqueBudgetRefusedEvenWithLargeReserveAndSmallKnownPart()
    {
        using JsonDocument item = JsonDocument.Parse("""{"role":"user","content":[{"type":"input_text","text":"Привет"},{"type":"input_image","image_url":"secret"}]}""");
        ModelRequest request = new("gpt-5", "medium", "", [new(item.RootElement)], []);
        ContextTokenCounter counter = new();
        ContextTokenCount count = (await counter.CountAsync(request)).Data!;
        Assert.Equal(2, count.KnownTokens);
        Assert.Null(count.EstimatedInputTokens);
        ServiceResult<ContextBudgetAssessment> result = await new ContextBudgetGuard(counter).CheckAsync(request, Settings(100_000, 1000, 10_000));
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.DoesNotContain("secret", result.Error.Message);
    }

    /// <summary>Публичный DTO не обходит catalog validation, ContextWindow не подменяет input limit.</summary>
    [Theory]
    [InlineData(null, 1, 0, ServiceErrorType.Unsupported)]
    [InlineData(0, 1, 0, ServiceErrorType.Unsupported)]
    [InlineData(100, 0, 0, ServiceErrorType.Validation)]
    [InlineData(100, 1, -1, ServiceErrorType.Validation)]
    [InlineData(100, 100, 1, ServiceErrorType.Validation)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, ServiceErrorType.Validation)]
    public async Task PublicUnvalidatedSnapshotCannotBypassCatalogRules(int? window, int threshold, int reserve, ServiceErrorType error)
    {
        Counter counter = new(new("o200k_base", 1, 1, false));
        ServiceResult<ContextBudgetAssessment> result = await new ContextBudgetGuard(counter).CheckAsync(Request(), Settings(window, threshold, reserve));
        Assert.False(result.Success);
        Assert.Equal(error, result.Error!.Type);
        Assert.Equal(0, counter.Calls);
    }

    /// <summary>Проверяет exact model/effort до подсчёта.</summary>
    [Theory]
    [InlineData("GPT-5", "medium")]
    [InlineData("gpt-5", "Medium")]
    [InlineData("gpt-5", null)]
    public async Task ExactRequestMustMatchValidatedSettings(string model, string? effort)
    {
        Counter counter = new(new("o200k_base", 1, 1, false));
        ServiceResult<ContextBudgetAssessment> result = await new ContextBudgetGuard(counter).CheckAsync(new(model, effort, "", [], []), Settings(100, 10, 0));
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Equal(0, counter.Calls);
    }

    /// <summary>Сохраняет тот же typed failure при поздней отмене.</summary>
    [Fact]
    public async Task FailureIdentitySurvivesLateCancellation()
    {
        using CancellationTokenSource caller = new();
        ServiceError error = new(ServiceErrorType.Unsupported, "Безопасный отказ.");
        Counter counter = new(null, error, caller.Cancel);
        ServiceResult<ContextBudgetAssessment> result = await new ContextBudgetGuard(counter).CheckAsync(Request(), Settings(100, 10, 0), caller.Token);
        Assert.Same(error, result.Error);
        Assert.Null(result.Data);
    }

    /// <summary>Успех counter после отмены не разрешает оценку guard.</summary>
    [Fact]
    public async Task SuccessAfterCancellationDoesNotAllowBudget()
    {
        using CancellationTokenSource caller = new();
        Counter counter = new(new("o200k_base", 1, 1, false), beforeReturn: caller.Cancel);
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ContextBudgetGuard(counter).CheckAsync(Request(), Settings(100, 10, 0), caller.Token));
        Assert.Equal(caller.Token, error.CancellationToken);
    }

    /// <summary>Не заменяет явно зарегистрированный counter приложения.</summary>
    [Fact]
    public void PublicRegistrationPreservesApplicationCounter()
    {
        Counter counter = new(new("o200k_base", 1, 1, false));
        ServiceCollection services = new();
        services.AddSingleton<IContextTokenCounter>(counter);
        services.AddAgentBridgeTokenization();
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(counter, provider.GetRequiredService<IContextTokenCounter>());
    }

    /// <summary>Создаёт prepared request, соответствующий exact settings.</summary>
    private static ModelRequest Request() => new("gpt-5", "medium", "", [], []);

    /// <summary>ContextWindow/MaxOutputTokens намеренно велики и не должны заменять InputContextWindow.</summary>
    private static ModelSettingsSnapshot Settings(int? inputWindow, int threshold, int reserve) => new(
        new("gpt-5", true, int.MaxValue, inputWindow, int.MaxValue, ["medium"], "medium", ["text"], true, false, false, false, false),
        "medium", threshold, reserve);

    /// <inheritdoc/>
    private class Counter(ContextTokenCount? count, ServiceError? error = null, Action? beforeReturn = null) : IContextTokenCounter
    {
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            beforeReturn?.Invoke();
            return Task.FromResult(error is null ? ServiceResult<ContextTokenCount>.Ok(count
                ?? throw new InvalidOperationException("Fixture требует count.")) : ServiceResult<ContextTokenCount>.Fail(error));
        }
    }
}
