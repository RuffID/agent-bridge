using AgentBridge.Sub2Api.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Новая нормализация принадлежит только sub2api и не изменяет legacy URL codex-lb.</summary>
public class Sub2ApiEndpointTests
{
    /// <summary>Root, prefix и один /v1 сохраняют исходный proxy prefix.</summary>
    [Theory]
    [InlineData("https://gateway.invalid", "https://gateway.invalid/v1/models")]
    [InlineData("https://gateway.invalid/", "https://gateway.invalid/v1/models")]
    [InlineData("https://gateway.invalid/v1/", "https://gateway.invalid/v1/models")]
    [InlineData("https://gateway.invalid/bridge", "https://gateway.invalid/bridge/v1/models")]
    [InlineData("https://gateway.invalid/bridge/v1/", "https://gateway.invalid/bridge/v1/models")]
    [InlineData("http://gateway.invalid:8080/a%20b/v1", "http://gateway.invalid:8080/a%20b/v1/models")]
    public async Task PrefixNormalizationUsesActualHttpRequest(string address, string expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(address);

        // Act
        await fixture.Catalog.ReadAsync(new(Sub2ApiCatalogFixture.INDIVIDUAL_KEY), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([expected], fixture.Handler.Urls);
    }

    /// <summary>Неоднозначные адреса отклоняются безопасной Options validation до HTTP.</summary>
    [Theory]
    [InlineData("https://secret@gateway.invalid")]
    [InlineData("https://gateway.invalid/?key=secret")]
    [InlineData("https://gateway.invalid/#secret")]
    [InlineData("https://gateway.invalid/v1/v1/")]
    [InlineData("https://gateway.invalid/v1/prefix")]
    [InlineData("https://gateway.invalid/v1/models")]
    [InlineData("https://gateway.invalid/responses/compact")]
    [InlineData("https://gateway.invalid/backend-api/codex")]
    [InlineData("https://gateway.invalid/prefix%2fv1")]
    [InlineData("https://gateway.invalid/prefix%5cv1")]
    [InlineData("https://gateway.invalid/%76%31/")]
    [InlineData("https://gateway.invalid/a//b")]
    [InlineData("file:///secret")]
    public async Task AmbiguousAddressFailsBeforeFactory(string address)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(address);

        // Act
        Exception? observed = await Record.ExceptionAsync(() => fixture.Resolver.ResolveAsync(fixture.Owner, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<OptionsValidationException>(observed);
        Assert.DoesNotContain(address, observed.Message);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.False(Sub2ApiEndpoints.TryGetModelsUrl(address, out _));
    }
}
