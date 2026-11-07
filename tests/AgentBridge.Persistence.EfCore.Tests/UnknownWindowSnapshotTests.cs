using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Configuration;
using AgentBridge.Persistence.EfCore.Mapping;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверяет только чистый JSON mapping бюджета v1/v2, без подключения к EF/provider.</summary>
public class UnknownWindowSnapshotTests
{
    /// <summary>Неизвестное окно остаётся null после mapping, без fabricated лимита и connector provenance.</summary>
    [Fact]
    public void ExplicitUnknownWindowRoundTripsAsVersionTwo()
    {
        // Arrange
        TurnModelSettings original = new("model", "medium", 100, 10, null, ContextBudgetPolicy.ServerValidationWithUnknownWindow);

        // Act
        string json = TurnSettingsMapping.Write(original)!;
        TurnModelSettings restored = TurnSettingsMapping.Read(json)!;

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("settings").GetProperty("InputContextWindow").ValueKind);
        Assert.Null(restored.DeclaredInputContextWindow);
        Assert.Equal(original.BudgetPolicy, restored.BudgetPolicy);
        Assert.Equal(original.Model, restored.Model);
        Assert.Equal(original.Effort, restored.Effort);
        Assert.Equal(original.TokenThreshold, restored.TokenThreshold);
        Assert.Equal(original.InputTokenReserve, restored.InputTokenReserve);
        Assert.Throws<InvalidOperationException>(() => restored.InputContextWindow);
        Assert.DoesNotContain("ApiKey", json);
        Assert.DoesNotContain("Connector", json);
        using JsonDocument safeJson = JsonDocument.Parse(JsonSerializer.Serialize(restored));
        Assert.Equal(JsonValueKind.Null, safeJson.RootElement.GetProperty("InputContextWindow").ValueKind);
        TurnModelSettings direct = JsonSerializer.Deserialize<TurnModelSettings>(safeJson.RootElement.GetRawText())!;
        Assert.Null(direct.DeclaredInputContextWindow);
        Assert.Equal(restored.BudgetPolicy, direct.BudgetPolicy);
    }

    /// <summary>Прежний literal v1 читается независимо от нового writer и остаётся прежним форматом при записи.</summary>
    [Fact]
    public void LiteralVersionOneRemainsReadableAndWritable()
    {
        // Arrange
        const string json = """{"version":1,"settings":{"Model":"old","Effort":"high","TokenThreshold":100,"InputTokenReserve":10,"InputContextWindow":200}}""";

        // Act
        TurnModelSettings restored = TurnSettingsMapping.Read(json)!;
        string written = TurnSettingsMapping.Write(restored)!;

        // Assert
        Assert.Equal(200, restored.InputContextWindow);
        Assert.Equal(200, restored.DeclaredInputContextWindow);
        Assert.Equal(ContextBudgetPolicy.RequireLocalEstimate, restored.BudgetPolicy);
        Assert.Equal(json, written);
    }

    /// <summary>Неизвестное окно нельзя сохранить с прежней политикой либо невалидным числовым значением.</summary>
    [Theory]
    [InlineData(null, ContextBudgetPolicy.RequireLocalEstimate)]
    [InlineData(null, ContextBudgetPolicy.ServerValidation)]
    [InlineData(0, ContextBudgetPolicy.ServerValidationWithUnknownWindow)]
    [InlineData(-1, ContextBudgetPolicy.ServerValidationWithUnknownWindow)]
    public void SnapshotCannotInventUnknownWindowAuthorization(int? window, ContextBudgetPolicy policy)
    {
        // Arrange
        const int threshold = 100;

        // Act
        Exception? error = Record.Exception(() => new TurnModelSettings("model", "medium", threshold, 10, window, policy));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(error);
    }
}
