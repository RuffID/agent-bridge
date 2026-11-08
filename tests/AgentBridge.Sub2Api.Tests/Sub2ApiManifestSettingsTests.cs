using System.Net;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Actual bundle/HttpClientLibrary manifest/settings с локальным handler; fixtures не подтверждают runtime pool.</summary>
public class Sub2ApiManifestSettingsTests
{
    private const string MANIFEST = """{"models":[{"slug":"public-model","supported_in_api":true,"supported_reasoning_levels":[{"effort":"medium","description":"declared"}],"context_window":272000,"effective_context_window_percent":95,"input_context_window":258400}]}""";

    /// <summary>Явный режим выбирает прямой manifest без client_version, сохраняет prefix, exact effort и pinned ключ.</summary>
    [Theory]
    [InlineData("https://gateway.invalid", "https://gateway.invalid/backend-api/codex/models")]
    [InlineData("https://gateway.invalid/", "https://gateway.invalid/backend-api/codex/models")]
    [InlineData("https://gateway.invalid/v1", "https://gateway.invalid/backend-api/codex/models")]
    [InlineData("https://gateway.invalid/v1/", "https://gateway.invalid/backend-api/codex/models")]
    [InlineData("https://gateway.invalid/prefix", "https://gateway.invalid/prefix/backend-api/codex/models")]
    [InlineData("https://gateway.invalid/prefix/", "https://gateway.invalid/prefix/backend-api/codex/models")]
    [InlineData("https://gateway.invalid/prefix/v1", "https://gateway.invalid/prefix/backend-api/codex/models")]
    [InlineData("https://gateway.invalid/prefix/v1/", "https://gateway.invalid/prefix/backend-api/codex/models")]
    public async Task DeclaredManifestEnablesSettingsWithoutInventedWindow(string address, string expectedUrl)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(address);
        fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>().Value.BudgetPolicy =
            ContextBudgetPolicy.ServerValidationWithUnknownWindow;
        fixture.Handler.Respond = (_, _) => Task.FromResult(Sub2ApiCatalogFixture.Json(MANIFEST));

        // Act
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Settings.ReadWithAccessAsync(fixture.Owner, new("pinned-key"),
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("public-model", result.Data!.Model.Id);
        Assert.Equal("medium", result.Data.ReasoningEffort);
        Assert.True(result.Data.Model.SupportedInApi);
        Assert.Null(result.Data.Model.InputContextWindow);
        Assert.Null(result.Data.Model.ContextWindow);
        Assert.Equal(ContextBudgetPolicy.ServerValidationWithUnknownWindow, result.Data.BudgetPolicy);
        Assert.Equal("Bearer pinned-key", Assert.Single(fixture.Handler.Authorizations));
        string url = Assert.Single(fixture.Handler.Urls);
        Assert.Equal(expectedUrl, url);
        Assert.DoesNotContain("client_version", url);
        Assert.Equal(0, fixture.KeySource.Calls);
        Assert.Null(TurnModelSettings.From(result.Data).DeclaredInputContextWindow);
    }

    /// <summary>Прежний режим не включает manifest и сохраняет Unsupported неизвестного окна.</summary>
    [Theory]
    [InlineData(ContextBudgetPolicy.RequireLocalEstimate)]
    [InlineData(ContextBudgetPolicy.ServerValidation)]
    public async Task PreviousPoliciesKeepOrdinaryCatalog(ContextBudgetPolicy policy)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>().Value.BudgetPolicy = policy;

        // Act
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Settings.ReadAsync(fixture.Owner,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Unsupported, result.Error!.Type);
        Assert.Equal("https://gateway.invalid/prefix/v1/models", Assert.Single(fixture.Handler.Urls));
    }

    /// <summary>Неполное/повреждённое объявление и HTTP отказ не запускают второй каталог либо guessed defaults.</summary>
    [Theory]
    [InlineData("{\"models\":[{\"slug\":\"public-model\"}]}", 200, ServiceErrorType.Unsupported)]
    [InlineData("{\"models\":[{\"slug\":\"public-model\",\"supported_in_api\":true}]}", 200, ServiceErrorType.Unsupported)]
    [InlineData("{\"models\":[{\"slug\":12}]}", 200, ServiceErrorType.Rejected)]
    [InlineData("{\"models\":[{}]}", 200, ServiceErrorType.Rejected)]
    [InlineData("{\"models\":[{\"slug\":\"public-model\",\"slug\":\"other\"}]}", 200, ServiceErrorType.Rejected)]
    [InlineData("{\"models\":[{\"slug\":\"public-model\"},{\"slug\":\"public-model\"}]}", 200, ServiceErrorType.Rejected)]
    [InlineData("{}", 403, ServiceErrorType.Forbidden)]
    [InlineData("{}", 429, ServiceErrorType.Rejected)]
    [InlineData("{}", 502, ServiceErrorType.Rejected)]
    public async Task MissingDeclarationsAndFailuresDoNotFallback(string body, int status, ServiceErrorType expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>().Value.BudgetPolicy =
            ContextBudgetPolicy.ServerValidationWithUnknownWindow;
        fixture.Handler.Respond = (_, _) => Task.FromResult(Sub2ApiCatalogFixture.Json(body, (HttpStatusCode)status));

        // Act
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Settings.ReadAsync(fixture.Owner,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal("https://gateway.invalid/prefix/backend-api/codex/models", Assert.Single(fixture.Handler.Urls));
        Assert.Null(result.Data);
    }
}
