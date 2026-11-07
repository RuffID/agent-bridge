using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Проверяет явный неизвестный лимит без server I/O и подстановки локального порога.</summary>
public class UnknownWindowBudgetTests
{
    /// <summary>Обе прежние политики отклоняют неизвестное окно до counter, новый режим требует согласованных settings.</summary>
    [Theory]
    [InlineData(ContextBudgetPolicy.RequireLocalEstimate, ContextBudgetPolicy.ServerValidationWithUnknownWindow, false)]
    [InlineData(ContextBudgetPolicy.ServerValidation, ContextBudgetPolicy.ServerValidationWithUnknownWindow, false)]
    [InlineData(ContextBudgetPolicy.ServerValidationWithUnknownWindow, ContextBudgetPolicy.RequireLocalEstimate, false)]
    [InlineData(ContextBudgetPolicy.ServerValidationWithUnknownWindow, ContextBudgetPolicy.ServerValidationWithUnknownWindow, true)]
    public async Task UnknownWindowRequiresExplicitSettingsAndGuard(ContextBudgetPolicy guardPolicy,
        ContextBudgetPolicy settingsPolicy, bool allowed)
    {
        // Arrange
        Counter counter = new(new("o200k_base", 10, 10, false));
        ModelSettingsSnapshot settings = new(Capability(), "medium", 100, int.MaxValue, settingsPolicy);

        // Act
        ServiceResult<ContextBudgetAssessment> result = await new ContextBudgetGuard(counter, guardPolicy)
            .CheckAsync(new("model", "medium", "instructions", [], []), settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(allowed, result.Success);
        if (allowed)
        {
            Assert.Null(result.Data!.DeclaredInputContextWindow);
            Assert.True(result.Data.RequiresServerValidation);
            Assert.False(result.Data.ThresholdReached);
            Assert.Throws<InvalidOperationException>(() => result.Data.InputContextWindow);
            using JsonDocument safeJson = JsonDocument.Parse(JsonSerializer.Serialize(result.Data));
            Assert.Equal(JsonValueKind.Null, safeJson.RootElement.GetProperty("InputContextWindow").ValueKind);
            Assert.Equal(1, counter.Calls);
        }
        else
        {
            Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
            Assert.Equal(0, counter.Calls);
        }
    }

    /// <summary>Threshold сравнивается с оценкой/известной частью, не становится budget; opaque null сохраняется.</summary>
    [Theory]
    [InlineData(99L, 99L, false, false)]
    [InlineData(100L, 100L, false, true)]
    [InlineData(long.MaxValue, long.MaxValue, false, true)]
    [InlineData(100L, null, true, true)]
    public async Task ThresholdNeverBecomesModelBudget(long known, long? estimate, bool opaque, bool reached)
    {
        // Arrange
        Counter counter = new(new("o200k_base", known, estimate, opaque));
        ContextBudgetPolicy policy = ContextBudgetPolicy.ServerValidationWithUnknownWindow;
        ModelSettingsSnapshot settings = ModelSelectionValidator.Validate(new([Capability()]), "model", "medium",
            100, int.MaxValue, policy).Data!;

        // Act
        ContextBudgetAssessment result = (await new ContextBudgetGuard(counter, policy)
            .CheckAsync(new("model", "medium", "", [], []), settings, TestContext.Current.CancellationToken)).Data!;

        // Assert
        Assert.Null(result.DeclaredInputContextWindow);
        Assert.Equal(estimate, result.TokenCount.EstimatedInputTokens);
        Assert.Equal(reached, result.ThresholdReached);
        Assert.True(result.RequiresServerValidation);
    }

    /// <summary>Новая политика не разрешает неизвестный effort, недоступную модель или невалидное числовое окно.</summary>
    [Theory]
    [InlineData("Medium", true, null)]
    [InlineData("medium", false, null)]
    [InlineData("medium", true, 0)]
    [InlineData("medium", true, -1)]
    public void UnknownPolicyStillValidatesDeclarations(string effort, bool available, int? window)
    {
        // Arrange
        ModelCapabilities model = new("model", true, int.MaxValue, window, null, ["medium"], null, [],
            available, false, false, false, false);

        // Act
        ServiceResult<ModelSettingsSnapshot> result = ModelSelectionValidator.Validate(new([model]), "model", effort,
            100, 0, ContextBudgetPolicy.ServerValidationWithUnknownWindow);

        // Assert
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
    }

    /// <summary>Создаёт объявленную модель с неизвестным входным окном.</summary>
    private static ModelCapabilities Capability()
    {
        return new("model", true, int.MaxValue, null, null, ["medium"], null, [], true, false, false, false, false);
    }

    /// <inheritdoc/>
    private class Counter(ContextTokenCount count) : IContextTokenCounter
    {
        internal int Calls { get; private set; }

        /// <inheritdoc/>
        public Task<ServiceResult<ContextTokenCount>> CountAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(ServiceResult<ContextTokenCount>.Ok(count));
        }
    }
}
