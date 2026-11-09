using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Sub2Api.Auxiliary;
using AgentBridge.Sub2Api.Configuration;
using HttpClientLibrary.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiCatalogFixture;
using static AgentBridge.Sub2Api.Tests.Sub2ApiUsageTests;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Проверки завершённых Images результатов через actual optional gateway и HttpClientLibrary.</summary>
public class Sub2ApiImageTests
{
    internal const string RESULT = "{\"created\":123,\"data\":[{\"b64_json\":\"aW1hZ2U=\",\"revised_prompt\":\"synthetic-private-body-header\"}],\"usage\":{\"images\":1},\"extra\":null}";

    /// <summary>Ошибка диагностики после cleanup не меняет результат и не создаёт повторную платную операцию.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LoggerFailureDoesNotChangeResultOrRetry(bool edit, bool rejected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        string body = RESULT;
        if (rejected)
        {
            body = "{\"error\":{\"message\":\"" + PRIVATE + "\"}}";
        }

        fixture.Handler.Respond = (_, _) => Task.FromResult(Json(body));
        Sub2ApiAuxiliaryGateway gateway = new(() => fixture.Scope.ServiceProvider.GetRequiredService<HttpApiClient>(),
            fixture.Scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Sub2ApiOptions>>(), new FailingLogger());

        // Act
        ServiceResult<ModelAuxiliaryResult> result;
        if (edit)
        {
            result = await gateway.EditImageAsync(new() { Model = "selected-image-model", Prompt = PRIVATE,
                Images = [new() { FileName = "x.png", Bytes = [1] }] }, new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        }
        else
        {
            result = await gateway.GenerateImageAsync(new() { Model = "selected-image-model", Prompt = PRIVATE },
                new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(!rejected, result.Success);
        if (rejected)
        {
            Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        }

        Assert.Equal(1, fixture.Handler.Calls);
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Base64, URL, mixed results и heartbeat сохраняются снимком без дополнительных запросов.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PreservesCompletedResult(bool edit, bool url)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        string body = RESULT;
        if (url)
        {
            body = "{\"data\":[{\"url\":\"https://image.invalid/signed?secret=synthetic-private-body-header\"},{\"b64_json\":\"aW1hZ2U=\"}]}";
        }

        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes(" \n\t\r\n " + body), 1);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await SendAsync(fixture, edit, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(body, result.Data!.Content.GetRawText());
        Assert.True(stream.Disposed);
        Assert.False(fixture.Handler.Disposed);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY], fixture.Handler.Authorizations);
        Assert.Equal(0, fixture.KeySource.Calls);
        AssertSafe(fixture, string.Empty);
        Assert.DoesNotContain("image.invalid", string.Join("", fixture.Logs.Entries));
        Assert.Contains("Outcome=Completed", fixture.Logs.Entries);
    }

    /// <summary>Некорректные формы и error envelope отклоняются при HTTP 200 с heartbeat.</summary>
    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public async Task RejectsInvalidResult(bool edit, string body)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        Sub2ApiTestStream stream = new(Encoding.UTF8.GetBytes(" \n " + body), 2);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await SendAsync(fixture, edit, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.True(stream.Disposed);
        AssertSafe(fixture, result.Error.Message);
        Assert.Contains("Outcome=Rejected", fixture.Logs.Entries);
        Assert.DoesNotContain("Outcome=Completed", fixture.Logs.Entries);
    }

    /// <summary>Повреждённый UTF-8 неизвестного поля не принимается JsonElement как успех.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsInvalidUtf8(bool edit)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        byte[] bytes = [.. Encoding.UTF8.GetBytes("{\"data\":[{\"b64_json\":\"aW1hZ2U=\"}],\"unknown\":\""),
            0xff, .. Encoding.UTF8.GetBytes("\"}")];
        Sub2ApiTestStream stream = new(bytes, 1);
        fixture.Handler.Respond = (_, _) => Task.FromResult(StreamResponse(stream));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await SendAsync(fixture, edit, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceErrorType.Rejected, result.Error!.Type);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.True(stream.Disposed);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Серверные отказы по доступу, платформе, группе, модели и capacity не вызывают retry/fallback.</summary>
    [Theory]
    [MemberData(nameof(ServerRefusals))]
    public async Task ServerRefusalIsSafe(bool edit, int status, ServiceErrorType expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json("{\"error\":{\"message\":\"" + PRIVATE + "\"}}", (HttpStatusCode)status));

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await SendAsync(fixture, edit, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Error!.Type);
        Assert.Null(result.Data);
        Assert.Equal(1, fixture.Handler.Calls);
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY], fixture.Handler.Authorizations);
        Assert.Equal(0, fixture.KeySource.Calls);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Набор невалидных завершённых результатов для обеих операций.</summary>
    public static IEnumerable<object[]> InvalidResponses()
    {
        string[] bodies = ["", " ", "{malformed", "null", "[]", "{}", "{\"data\":null}", "{\"data\":{}}", "{\"data\":[]}",
            "{\"data\":[null]}", "{\"data\":[{}]}", "{\"data\":[{\"url\":\" \"}]}", "{\"data\":[{\"b64_json\":null}]}",
            "{\"data\":[{\"b64_json\":true}]}", "{\"data\":[{\"url\":42}]}", "{\"data\":[{\"url\":\"x\"},{}]}",
            "{\"error\":{\"message\":\"synthetic-private-body-header\"}}",
            "{\"error\":{\"message\":\"synthetic-private-body-header\"},\"data\":[{\"url\":\"x\"}]}",
            "{\"data\":[{\"url\":\"x\"}],\"error\":false}", "{\"data\":[],\"data\":[{\"url\":\"x\"}]}",
            "{\"data\":[{\"url\":\"x\",\"url\":\"y\"}]}", "{\"data\":[{\"url\":\"x\"}]}{}"];
        foreach (bool edit in new[] { false, true })
        {
            foreach (string body in bodies)
            {
                yield return [edit, body];
            }
        }
    }

    /// <summary>Проверяет одинаковое отображение server refusals у generation/edit.</summary>
    public static IEnumerable<object[]> ServerRefusals()
    {
        foreach (bool edit in new[] { false, true })
        {
            foreach (int status in new[] { 302, 400, 401, 403, 404, 413, 429, 500, 502, 503 })
            {
                ServiceErrorType expected = ServiceErrorType.Rejected;
                if (status == 401)
                {
                    expected = ServiceErrorType.Unauthorized;
                }
                else if (status == 403)
                {
                    expected = ServiceErrorType.Forbidden;
                }

                yield return [edit, status, expected];
            }
        }
    }

    /// <summary>Вызывает actual gateway с синтетическими приватными значениями, не подменяя transport.</summary>
    internal static Task<ServiceResult<ModelAuxiliaryResult>> SendAsync(Sub2ApiCatalogFixture fixture, bool edit, CancellationToken ct)
    {
        if (edit)
        {
            return Gateway(fixture).EditImageAsync(new()
            {
                Model = "selected-image-model", Prompt = PRIVATE,
                Images = [new() { FileName = "private.png", MimeType = "image/png", Bytes = Encoding.UTF8.GetBytes(PRIVATE) }]
            }, new(INDIVIDUAL_KEY), ct);
        }

        return Gateway(fixture).GenerateImageAsync(new() { Model = "selected-image-model", Prompt = PRIVATE }, new(INDIVIDUAL_KEY), ct);
    }

    /// <summary>Моделирует отказ только outcome logger без изменения HTTP logging pipeline.</summary>
    private class FailingLogger : ILogger<Sub2ApiAuxiliaryGateway>
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException(PRIVATE);
        }
    }
}
