using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Проверка выбора по динамическому снимку без HTTP или tokenizer.</summary>
public class ModelSelectionTests
{
    /// <summary>Граница относится к input_context_window; output не вычитается повторно.</summary>
    [Theory]
    [InlineData(36_096, true)]
    [InlineData(36_095, false)]
    public void InputBudgetBoundaryIsExact(int inputBudget, bool success)
    {
        ServiceResult<ModelSettingsSnapshot> result = ModelSelectionValidator.Validate(
            Catalog(inputBudget), "model-A", "custom-effort", 32_000, 4_096);
        Assert.Equal(success, result.Success);
        if (!success)
        {
            Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
            Assert.Null(result.Data);
        }
    }

    /// <summary>Выбор сравнивается точно, без alias, trim, casefold или замены effort на default.</summary>
    [Theory]
    [InlineData("model-a", "custom-effort")]
    [InlineData("model-A ", "custom-effort")]
    [InlineData("model-A", "CUSTOM-EFFORT")]
    [InlineData("model-A", "medium")]
    public void SelectionDoesNotSubstituteValues(string model, string effort)
    {
        ServiceResult<ModelSettingsSnapshot> result = ModelSelectionValidator.Validate(Catalog(100), model, effort, 1, 0);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Null(result.Data);
    }

    /// <summary>Некорректные локальные значения отклоняются, переполнение не превращает бюджет в отрицательный.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, -1)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void InvalidAndOverflowingBudgetsFail(int threshold, int reserve)
    {
        ServiceResult<ModelSettingsSnapshot> result = ModelSelectionValidator.Validate(Catalog(int.MaxValue),
            "model-A", "custom-effort", threshold, reserve);
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
    }

    /// <summary>Неизвестный input budget не подменяется большим context_window.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void UnknownInputBudgetIsUnsupported(int? budget)
    {
        ServiceResult<ModelSettingsSnapshot> result = ModelSelectionValidator.Validate(Catalog(budget),
            "model-A", "custom-effort", 1, 0);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
    }

    /// <summary>Отсутствие metadata или supported_in_api не разрешает выбор.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void UnknownOrUnavailableCapabilitiesFail(bool metadata, bool supported)
    {
        ModelCapabilities capability = new("model-A", metadata, 100, 100, null,
            ["custom-effort"], null, ["text"], supported, false, false, false, false);
        ServiceResult<ModelSettingsSnapshot> result = ModelSelectionValidator.Validate(new([capability]),
            "model-A", "custom-effort", 1, 0);
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
    }

    /// <summary>Коллекции остаются независимыми от исходных массивов и защищены от изменения.</summary>
    [Fact]
    public void SnapshotsCopyCapabilitiesAndCatalogCollections()
    {
        string[] efforts = ["custom-effort"];
        string[] modalities = ["text"];
        ModelCapabilities capability = new("model-A", true, 100, 100, null, efforts, null, modalities,
            true, false, false, false, false);
        ModelCapabilities[] models = [capability];
        ModelCatalogSnapshot catalog = new(models);
        efforts[0] = "mutated";
        modalities[0] = "mutated";
        models[0] = Catalog(200).Models[0];
        Assert.Equal("custom-effort", catalog.Models[0].ReasoningEfforts[0]);
        Assert.Equal("text", catalog.Models[0].InputModalities[0]);
        Assert.Equal(100, catalog.Models[0].InputContextWindow);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)capability.ReasoningEfforts)[0] = "mutated");
        Assert.Throws<NotSupportedException>(() => ((IList<ModelCapabilities>)catalog.Models).Clear());
    }

    /// <summary>Создаёт динамический снимок с различными общим, входным и выходным бюджетами.</summary>
    private static ModelCatalogSnapshot Catalog(int? budget) => new([
        new ModelCapabilities("model-A", true, int.MaxValue, budget, 50_000,
            ["custom-effort"], "unused-default", ["text"], true, false, false, false, false)]);
}
