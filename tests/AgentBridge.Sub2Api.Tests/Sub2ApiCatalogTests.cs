using System.Net;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Sub2Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiCatalogFixture;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Подтверждённая граница ordinary models, ключей и fail-closed settings через actual HTTP library.</summary>
public class Sub2ApiCatalogTests
{
    /// <summary>Индивидуальный ключ приоритетен; только null разрешает shared, без повторного source I/O.</summary>
    [Theory]
    [InlineData(INDIVIDUAL_KEY, INDIVIDUAL_KEY)]
    [InlineData(null, SHARED_KEY)]
    public async Task AccessIsPinnedAndUsedAsBearer(string? individual, string expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        fixture.KeySource.Key = individual;

        // Act
        ServiceResult<ModelAccess> access = await fixture.Resolver.ResolveAsync(fixture.Owner, TestContext.Current.CancellationToken);
        fixture.KeySource.Key = "changed-key";
        ServiceResult<ModelSettingsSnapshot> settings = await fixture.Settings.ReadWithAccessAsync(fixture.Owner, access.Data!,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(access.Success);
        Assert.Equal(expected, access.Data!.RevealApiKey());
        Assert.Equal(1, fixture.KeySource.Calls);
        Assert.Equal(["Bearer " + expected], fixture.Handler.Authorizations);
        Assert.Equal(["https://gateway.invalid/prefix/v1/models"], fixture.Handler.Urls);
        Assert.Null(fixture.Http.DefaultRequestHeaders.Authorization);
        Assert.Equal(ServiceErrorType.Unsupported, settings.Error!.Type);
        Assert.Null(settings.Data);
    }

    /// <summary>Непригодный individual не запускает каталог и не переключается на shared.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad key")]
    [InlineData("bad\r\nkey")]
    public async Task InvalidIndividualNeverFallsBack(string individual)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        fixture.KeySource.Key = individual;

        // Act
        ServiceResult<ModelSettingsSnapshot> result = await fixture.Settings.ReadAsync(fixture.Owner,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Equal(1, fixture.KeySource.Calls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Source failure сохраняет identity и не делает HTTP; Individual без ключа даёт Unauthorized.</summary>
    [Fact]
    public async Task SourceFailureAndAbsentIndividualDoNotUseShared()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        IOException failure = new("private-source-exception");
        fixture.KeySource.Failure = failure;

        // Act
        Exception? observed = await Record.ExceptionAsync(() => fixture.Settings.ReadAsync(fixture.Owner, ct: TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(failure, observed);
        Assert.Equal(0, fixture.Handler.Calls);

        // Arrange
        fixture.KeySource.Failure = null;
        fixture.KeySource.Key = null;
        fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Sub2ApiOptions>>().Value.KeySource = Sub2ApiKeySourceMode.Individual;

        // Act
        ServiceResult<ModelSettingsSnapshot> absent = await fixture.Settings.ReadAsync(fixture.Owner, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Unauthorized, absent.Error!.Type);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Один source на scope; регистрация и blocked gateway не вызывают app factory.</summary>
    [Fact]
    public async Task ScopesAndBorrowedResourcesRemainAppOwned()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        using IServiceScope other = fixture.Provider.CreateScope();

        // Act
        IModelAccessResolver first = fixture.Resolver;
        IModelAccessResolver second = other.ServiceProvider.GetRequiredService<IModelAccessResolver>();

        // Assert
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Same(first, fixture.Resolver);
        Assert.NotSame(first, second);
        Assert.NotSame(fixture.KeySource, other.ServiceProvider.GetRequiredService<IIndividualModelKeySource>());

        // Act
        await fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        fixture.Scope.Dispose();

        // Assert
        Assert.False(fixture.Handler.Disposed);
        using HttpResponseMessage response = await fixture.Http.GetAsync("https://gateway.invalid/probe", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Каталог не превращает произвольные numeric/effort/boolean metadata в подтверждённые capabilities.</summary>
    [Fact]
    public async Task UnknownMetadataNeverBecomesBudgetOrEffort()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json("""
            {"object":"list","data":[{"id":"exact/alias","object":"model","metadata":{
            "context_window":272000,"max_context_window":1000000,"effective_context_window_percent":95,
            "input_context_window":258400,"max_output_tokens":999999999999999999999999,
            "supported_reasoning_levels":[{"effort":"invented"}],"supported_in_api":true,
            "supports_parallel_tool_calls":true,"description":"synthetic-private-body-header"}}]}
            """));

        // Act
        ServiceResult<ModelCatalogSnapshot> result = await fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        ServiceResult<ModelSettingsSnapshot> settings = await fixture.Settings.ReadWithAccessAsync(fixture.Owner,
            new(INDIVIDUAL_KEY), "exact/alias", "invented", TestContext.Current.CancellationToken);

        // Assert
        ModelCapabilities model = Assert.Single(result.Data!.Models);
        Assert.Equal("exact/alias", model.Id);
        Assert.False(model.HasMetadata);
        Assert.Null(model.ContextWindow);
        Assert.Null(model.InputContextWindow);
        Assert.Null(model.MaxOutputTokens);
        Assert.Empty(model.ReasoningEfforts);
        Assert.Empty(model.InputModalities);
        Assert.Null(model.DefaultReasoningEffort);
        Assert.False(model.SupportedInApi);
        Assert.False(model.SupportsParallelToolCalls);
        Assert.Equal(ServiceErrorType.Unsupported, settings.Error!.Type);
        AssertSafe(JsonSerializer.Serialize(result) + string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Чтения не кешируются между ключами и запросами, exact ID не нормализуется.</summary>
    [Fact]
    public async Task SequentialCatalogsRetainExactIdsWithoutCache()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();

        // Act
        ServiceResult<ModelCatalogSnapshot> first = await fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json("""{"object":"list","data":[{"object":"model","id":"Case"},{"object":"model","id":"case"}]}"""));
        ServiceResult<ModelCatalogSnapshot> second = await fixture.Catalog.ReadAsync(new(SHARED_KEY), TestContext.Current.CancellationToken);
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json("""{"object":"list","data":[]}"""));
        ServiceResult<ModelCatalogSnapshot> empty = await fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("public-model", Assert.Single(first.Data!.Models).Id);
        Assert.Equal(["Case", "case"], second.Data!.Models.Select(model => model.Id));
        Assert.Empty(empty.Data!.Models);
        Assert.Equal(3, fixture.Handler.Calls);
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY, "Bearer " + SHARED_KEY, "Bearer " + INDIVIDUAL_KEY], fixture.Handler.Authorizations);
        Assert.All(fixture.Handler.Urls, url => Assert.DoesNotContain("client_version", url));
    }

    /// <summary>Нарушенный envelope, duplicate ID и неподтверждённый manifest дают безопасный отказ.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"object\":\"list\",\"data\":{}}")]
    [InlineData("{\"object\":\"list\",\"data\":[{}]}")]
    [InlineData("{\"object\":\"list\",\"data\":[],\"data\":[]}")]
    [InlineData("{\"object\":\"list\",\"data\":[{\"object\":\"model\",\"id\":\"x\",\"id\":\"y\"}]}")]
    [InlineData("{\"object\":\"list\",\"data\":[{\"object\":\"model\",\"id\":\" \"}]}")]
    [InlineData("{\"object\":\"list\",\"data\":[{\"object\":\"model\",\"id\":\"x\"},{\"object\":\"model\",\"id\":\"x\"}]}")]
    [InlineData("{\"models\":[{\"slug\":\"x\",\"context_window\":272000}]}")]
    [InlineData("{\"private\":\"synthetic-private-body-header\"")]
    public async Task MalformedCatalogIsRejected(string json)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json(json));

        // Act
        ServiceResult<ModelCatalogSnapshot> result = await fixture.Catalog.ReadAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(JsonSerializer.Serialize(result) + string.Join("", fixture.Logs.Entries));
    }

    /// <summary>Оба error envelope, truncated или oversized body не раскрывают секреты и не вызывают retry.</summary>
    [Theory]
    [InlineData(401, ServiceErrorType.Unauthorized)]
    [InlineData(403, ServiceErrorType.Forbidden)]
    [InlineData(429, ServiceErrorType.Rejected)]
    [InlineData(500, ServiceErrorType.Rejected)]
    [InlineData(502, ServiceErrorType.Rejected)]
    [InlineData(503, ServiceErrorType.Rejected)]
    public async Task HttpErrorsAreSafeAndNeverRetried(int status, ServiceErrorType expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new();
        string[] bodies = ["{\"code\":\"INVALID_API_KEY\",\"message\":\"" + PRIVATE + "\"}",
            "{\"error\":{\"type\":\"compact_not_supported\",\"message\":\"" + INDIVIDUAL_KEY + "\"}}",
            "{" + PRIVATE, new string('x', 70_000) + SHARED_KEY];

        foreach (string body in bodies)
        {
            fixture.Handler.Respond = (_, _) => Task.FromResult(Json(body, (HttpStatusCode)status));

            // Act
            ServiceResult<ModelSettingsSnapshot> result = await fixture.Settings.ReadAsync(fixture.Owner, ct: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expected, result.Error!.Type);
            Assert.Null(result.Data);
            AssertSafe(JsonSerializer.Serialize(result) + string.Join("", fixture.Logs.Entries));
        }

        Assert.Equal(bodies.Length, fixture.Handler.Calls);
        Assert.NotEmpty(fixture.Logs.Entries);
    }

    /// <summary>Ни один из диагностических каналов не содержит синтетические секреты и raw headers.</summary>
    internal static void AssertSafe(string text)
    {
        Assert.DoesNotContain(INDIVIDUAL_KEY, text);
        Assert.DoesNotContain(SHARED_KEY, text);
        Assert.DoesNotContain(PRIVATE, text);
        Assert.DoesNotContain("Authorization", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("x-private-header", text, StringComparison.OrdinalIgnoreCase);
    }
}
