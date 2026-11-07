using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using AgentBridge.Sub2Api.Configuration;
using AgentBridge.Sub2Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Проверяет orchestration settings; synthetic capabilities не являются доказательством возможностей sub2api.</summary>
public class Sub2ApiSettingsTests
{
    /// <summary>Точный выбор и pinned доступ проходят прежний validator, включая equality и overflow.</summary>
    [Theory]
    [InlineData("public-model", "medium", 100, 10, 110, null)]
    [InlineData("PUBLIC-MODEL", "medium", 100, 10, 110, ServiceErrorType.Unsupported)]
    [InlineData("public-model", "Medium", 100, 10, 110, ServiceErrorType.Unsupported)]
    [InlineData("public-model", "medium", 100, 10, null, ServiceErrorType.Unsupported)]
    [InlineData("public-model", "medium", 100, 10, 109, ServiceErrorType.Validation)]
    [InlineData("public-model", "medium", int.MaxValue, int.MaxValue, int.MaxValue, ServiceErrorType.Validation)]
    public async Task SelectionKeepsExactValuesAndBudgetChecks(string model, string effort, int threshold, int reserve,
        int? inputBudget, ServiceErrorType? expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        ContextCompactionOptions context = fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>().Value;
        context.TokenThreshold = threshold;
        context.InputTokenReserve = reserve;
        ModelCapabilities synthetic = new("public-model", true, 1000, inputBudget, null, ["medium"], null, [],
            true, false, false, false, false);
        CatalogDouble catalog = new(ServiceResult<ModelCatalogSnapshot>.Ok(new([synthetic])));
        IModelSettingsReader reader = Reader(fixture, catalog);
        ModelAccess pinned = new("pinned-key");

        // Act
        ServiceResult<ModelSettingsSnapshot> result = await reader.ReadWithAccessAsync(fixture.Owner, pinned, model, effort,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(pinned, catalog.Access);
        Assert.Equal(0, fixture.KeySource.Calls);
        Assert.Equal(0, fixture.Handler.Calls);
        if (expected is null)
        {
            Assert.True(result.Success);
            Assert.Equal(model, result.Data!.Model.Id);
            Assert.Equal(effort, result.Data.ReasoningEffort);
        }
        else
        {
            Assert.Equal(expected, result.Error!.Type);
            Assert.Null(result.Data);
        }
    }

    /// <summary>ReadAsync сохраняет primitives до ожидания источника; option mutation не меняет текущий выбор.</summary>
    [Fact]
    public async Task SettingsSnapshotIsCapturedBeforeSourceIo()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        Sub2ApiOptions configured = fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Sub2ApiOptions>>().Value;
        ModelCapabilities synthetic = new("public-model", true, 1000, 1000, null, ["medium"], null, [], true, false, false, false, false);
        CatalogDouble catalog = new(ServiceResult<ModelCatalogSnapshot>.Ok(new([synthetic])));
        IModelSettingsReader reader = Reader(fixture, catalog);
        fixture.KeySource.BeforeReturn = () =>
        {
            configured.Model = "changed-model";
            configured.ReasoningEffort = "changed-effort";
            return Task.CompletedTask;
        };

        // Act
        ServiceResult<ModelSettingsSnapshot> result = await reader.ReadAsync(fixture.Owner, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("public-model", result.Data!.Model.Id);
        Assert.Equal("medium", result.Data.ReasoningEffort);
        Assert.Equal(Sub2ApiCatalogFixture.INDIVIDUAL_KEY, catalog.Access!.RevealApiKey());
        Assert.Equal(1, fixture.KeySource.Calls);
    }

    /// <summary>Typed failure сохраняется при поздней отмене; успешный каталог не обходит отмену.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCancellationPreservesEstablishedTypedFailure(bool success)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ServiceError error = new(ServiceErrorType.Forbidden, "Безопасный отказ.");
        ServiceResult<ModelCatalogSnapshot> response = ServiceResult<ModelCatalogSnapshot>.Fail(error);
        if (success)
        {
            response = ServiceResult<ModelCatalogSnapshot>.Ok(new([]));
        }

        CatalogDouble catalog = new(response) { BeforeReturn = caller.Cancel };
        IModelSettingsReader reader = Reader(fixture, catalog);
        ServiceResult<ModelSettingsSnapshot>? result = null;

        // Act
        Exception? observed = await Record.ExceptionAsync(async () =>
        {
            result = await reader.ReadWithAccessAsync(fixture.Owner, new("key"), ct: caller.Token);
        });

        // Assert
        if (success)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(observed);
            Assert.Null(result);
        }
        else
        {
            Assert.Null(observed);
            Assert.Same(error, result!.Error);
        }
    }

    /// <summary>Заменяет только catalog I/O, сохраняя actual resolver/settings и Options pipeline.</summary>
    private static IModelSettingsReader Reader(Sub2ApiCatalogFixture fixture, IModelCatalog catalog)
    {
        return new Sub2ApiModelSettingsReader(fixture.Resolver, catalog,
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Sub2ApiOptions>>(),
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>());
    }

    /// <inheritdoc/>
    private class CatalogDouble(ServiceResult<ModelCatalogSnapshot> result) : IModelCatalog
    {
        internal ModelAccess? Access { get; private set; }
        internal Action? BeforeReturn { get; init; }

        /// <inheritdoc/>
        public Task<ServiceResult<ModelCatalogSnapshot>> ReadAsync(ModelAccess access, CancellationToken ct = default)
        {
            Access = access;
            BeforeReturn?.Invoke();
            return Task.FromResult(result);
        }
    }
}
