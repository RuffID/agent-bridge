using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Sub2Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiCatalogFixture;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Usage проходит actual HttpClientLibrary с синтетическими ответами без сети.</summary>
public class Sub2ApiUsageTests
{
    /// <summary>Режим, вложенные данные, null и неизвестные поля сохраняются без wire-проекции.</summary>
    [Theory]
    [InlineData("""{"mode":"quota_limited","isValid":true,"status":"quota_exhausted","quota":{"limit":10,"used":10,"remaining":0,"unit":"USD"},"remaining":0,"rate_limits":[{"window":"5h","limit":2,"used":1,"remaining":1,"window_start":null}],"usage":{"today":{"requests":3}},"daily_usage":[{"date":"2026-11-08","cost":0.25}],"model_stats":[{"model":"synthetic-model","cost":0.25}],"future":{"value":null}}""")]
    [InlineData("""{"mode":"unrestricted","isValid":true,"planName":"wallet","unit":"USD","balance":1.25,"remaining":1.25,"usage":{"total":{"input_tokens":12}},"daily_usage":[],"model_stats":[]}""")]
    [InlineData("""{"mode":"unrestricted","isValid":true,"planName":"subscription","unit":"USD","subscription":{"daily_usage_usd":1,"daily_limit_usd":null,"weekly_window_start":"2026-11-08T00:00:00Z"},"remaining":-1}""")]
    public async Task PreservesDirectJsonSnapshot(string body)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes(body), 7);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);
        fixture.Scope.Dispose();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(body, result.Data!.Content.GetRawText());
        Assert.True(stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        Assert.Equal(["https://gateway.invalid/prefix/v1/usage"], fixture.Handler.Urls);
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY], fixture.Handler.Authorizations);
        AssertSafe(fixture, result.Error?.Message ?? string.Empty);
    }

    /// <summary>Недоступная дополнительная статистика не заменяется default или unlimited.</summary>
    [Theory]
    [InlineData("""{"mode":"quota_limited","isValid":true,"status":"expired"}""")]
    [InlineData("""{"mode":"unrestricted","isValid":true,"planName":"subscription","unit":"USD"}""")]
    public async Task MissingOptionalFieldsRemainAbsent(string body)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json(body));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        foreach (string field in new[] { "quota", "rate_limits", "subscription", "balance", "remaining", "usage", "daily_usage", "model_stats" })
        {
            Assert.False(result.Data!.Content.TryGetProperty(field, out _));
        }

        Assert.Equal(body, result.Data!.Content.GetRawText());
    }

    /// <summary>Malformed/error/non-usage JSON не объявляется успешным usage.</summary>
    [Theory]
    [InlineData("{malformed")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"mode\":1}")]
    [InlineData("{\"mode\":\"future\"}")]
    [InlineData("{\"data\":{\"mode\":\"unrestricted\"}}")]
    [InlineData("{\"mode\":\"unrestricted\",\"error\":{\"message\":\"synthetic-private-body-header\"}}")]
    [InlineData("{\"mode\":\"unrestricted\",\"mode\":\"quota_limited\"}")]
    [InlineData("")]
    public async Task InvalidJsonIsSafelyRejected(string body)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes(body), 7);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.True(stream.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Повреждённый UTF-8 даже неизвестного поля отклоняется после освобождения body.</summary>
    [Fact]
    public async Task InvalidUtf8InUnknownFieldIsRejected()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        byte[] bytes = Encoding.UTF8.GetBytes("{\"mode\":\"unrestricted\",\"future\":\"X\"}");
        bytes[Array.IndexOf(bytes, (byte)'X')] = 0xFF;
        Sub2ApiTestStream stream = new(bytes, 7);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.True(stream.Disposed);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Отказы HTTP не повторяются и не запускают shared fallback; body не попадает в ошибку или журнал.</summary>
    [Theory]
    [InlineData(302, ServiceErrorType.Rejected)]
    [InlineData(401, ServiceErrorType.Unauthorized)]
    [InlineData(403, ServiceErrorType.Forbidden)]
    [InlineData(404, ServiceErrorType.Rejected)]
    [InlineData(429, ServiceErrorType.Rejected)]
    [InlineData(500, ServiceErrorType.Rejected)]
    [InlineData(502, ServiceErrorType.Rejected)]
    public async Task HttpFailureIsSafeAndDoesNotRetry(int status, ServiceErrorType expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes("{\"error\":\"" + PRIVATE + "\"}"), 11);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream, (HttpStatusCode)status));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY], fixture.Handler.Authorizations);
        Assert.Equal(0, fixture.KeySource.Calls);
        Assert.True(stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Каждый вызов использует только переданный доступ и не изменяет default headers borrowed client.</summary>
    [Fact]
    public async Task BearerIsPerRequestAndUsesSelectedAccess()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        fixture.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "app-default");
        fixture.Handler.Respond = (request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Null(request.Content);
            return Task.FromResult(Json("{\"mode\":\"unrestricted\"}"));
        };
        IModelAuxiliaryGateway gateway = Gateway(fixture);

        // Act
        await gateway.ReadUsageAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        await gateway.ReadUsageAsync(new(SHARED_KEY), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY, "Bearer " + SHARED_KEY], fixture.Handler.Authorizations);
        Assert.Equal("app-default", fixture.Http.DefaultRequestHeaders.Authorization.Parameter);
        Assert.Equal(0, fixture.KeySource.Calls);
    }

    /// <summary>Root, prefix и один конечный /v1 дают один /v1/usage в actual запросе.</summary>
    [Theory]
    [InlineData("https://gateway.invalid", "https://gateway.invalid/v1/usage")]
    [InlineData("https://gateway.invalid/", "https://gateway.invalid/v1/usage")]
    [InlineData("https://gateway.invalid/v1/", "https://gateway.invalid/v1/usage")]
    [InlineData("https://gateway.invalid/a", "https://gateway.invalid/a/v1/usage")]
    [InlineData("https://gateway.invalid/a/v1", "https://gateway.invalid/a/v1/usage")]
    [InlineData("http://gateway.invalid:8080/a%20b/v1/", "http://gateway.invalid:8080/a%20b/v1/usage")]
    public async Task NormalizesUrl(string address, string expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(address, auxiliary: true);
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json("{\"mode\":\"unrestricted\"}"));

        // Act
        await Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([expected], fixture.Handler.Urls);
    }

    /// <summary>Неоднозначные адреса отклоняются общей нормализацией до HTTP.</summary>
    [Theory]
    [InlineData("https://gateway.invalid/v1/usage")]
    [InlineData("https://gateway.invalid/usage")]
    [InlineData("https://gateway.invalid/v1/v1")]
    [InlineData("https://gateway.invalid/v1/prefix")]
    [InlineData("https://gateway.invalid/%76%31/")]
    [InlineData("https://gateway.invalid/a%2fb")]
    [InlineData("https://gateway.invalid/?key=secret")]
    [InlineData("https://secret@gateway.invalid")]
    public async Task RejectsAmbiguousUrlBeforeFactory(string address)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(address, auxiliary: true);

        // Act
        Exception? failure = await Record.ExceptionAsync(() => Gateway(fixture).ReadUsageAsync(new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<Microsoft.Extensions.Options.OptionsValidationException>(failure);
        Assert.DoesNotContain(address, failure.Message);
        Assert.False(Sub2ApiEndpoints.TryGetUsageUrl(address, out _));
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Получает actual optional scoped gateway.</summary>
    internal static IModelAuxiliaryGateway Gateway(Sub2ApiCatalogFixture fixture)
    {
        return fixture.Scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>();
    }

    /// <summary>Создаёт response без внешних ресурсов; transport владеет stream.</summary>
    internal static HttpResponseMessage StreamResponse(Stream stream, HttpStatusCode status = HttpStatusCode.OK)
    {
        HttpResponseMessage response = new(status) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = new("application/json");
        response.Headers.Add("x-private-header", PRIVATE);

        return response;
    }

    /// <summary>Проверяет публичную ошибку и все каналы actual HTTP logging.</summary>
    internal static void AssertSafe(Sub2ApiCatalogFixture fixture, string error)
    {
        string text = error + string.Join("", fixture.Logs.Entries);
        Assert.DoesNotContain(PRIVATE, text);
        Assert.DoesNotContain(INDIVIDUAL_KEY, text);
        Assert.DoesNotContain(SHARED_KEY, text);
        Assert.DoesNotContain("gateway.invalid", text);
    }
}
