using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentBridge.CodexLb.Tests;

/// <summary>Публичная optional регистрация и actual HttpClientLibrary без сети и БД.</summary>
public class AuxiliaryGatewayTests
{
    /// <summary>Три стадии upload, без Bearer на signed URL и без успеха до подтверждения.</summary>
    [Fact]
    public async Task UploadConfirmsSameIdAndDisposesContents()
    {
        List<HttpContent> contents = [];
        int calls = 0;
        using Fixture fixture = new(async (request, ct) =>
        {
            calls++;
            if (calls == 1)
            {
                using JsonDocument json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal("codex", json.RootElement.GetProperty("use_case").GetString());
                Assert.Equal(3, json.RootElement.GetProperty("file_size").GetInt64());
                Assert.Equal("Bearer key", request.Headers.Authorization!.ToString());
                return Json("""{"file_id":"file-1","upload_url":"https://upload.invalid/blob?sig=secret"}""");
            }
            if (calls == 2)
            {
                Assert.Null(request.Headers.Authorization);
                Assert.Equal("BlockBlob", request.Headers.GetValues("x-ms-blob-type").Single());
                Assert.Equal(new byte[] { 1, 2, 3 }, await request.Content!.ReadAsByteArrayAsync(ct));
                contents.Add(request.Content);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("<ignored/>") };
            }

            Assert.EndsWith("/backend-api/files/file-1/uploaded", request.RequestUri!.AbsoluteUri);
            return Json("""{"status":"success","file_id":"file-1"}""");
        });
        ServiceResult<string> result = await fixture.Gateway.UploadFileAsync(
            new() { FileName = "a.pdf", MimeType = "application/pdf", Bytes = [1, 2, 3] }, new("key"), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("file-1", result.Data);
        Assert.Equal(3, calls);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => contents.Single().ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Ошибка загрузки прекращает pipeline, финализация не вызывается.</summary>
    [Fact]
    public async Task UploadFailureDoesNotConfirmOrLeakSignedUrl()
    {
        int calls = 0;
        using Fixture fixture = new((_, _) => Task.FromResult(++calls == 1
            ? Json("""{"file_id":"file-1","upload_url":"https://upload.invalid/blob?sig=secret"}""")
            : Json("""{"error":{"message":"secret","code":"secret"}}""", HttpStatusCode.Forbidden)));
        ServiceResult<string> result = await fixture.Gateway.UploadFileAsync(new() { FileName = "a", Bytes = [1] },
            new("key"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(2, calls);
        Assert.DoesNotContain("secret", result.Error!.Message);
    }

    /// <summary>Чужой ID финализации не возвращается вызывающему.</summary>
    [Fact]
    public async Task UploadRejectsMismatchedConfirmation()
    {
        int calls = 0;
        using Fixture fixture = new((_, _) => Task.FromResult(++calls switch
        {
            1 => Json("""{"file_id":"file-1","upload_url":"https://upload.invalid/blob"}"""),
            2 => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => Json("""{"status":"success","file_id":"other"}""")
        }));

        Assert.False((await fixture.Gateway.UploadFileAsync(new() { FileName = "a", Bytes = [1] }, new("key"),
            TestContext.Current.CancellationToken)).Success);
    }

    /// <summary>JSON generation и multipart edit сохраняют параметры и освобождают fresh content.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImagesPreservePayload(bool edit)
    {
        HttpContent? sent = null;
        using Fixture fixture = new(async (request, ct) =>
        {
            sent = request.Content;
            Assert.Equal("Bearer image-key", request.Headers.Authorization!.ToString());
            string body = await request.Content!.ReadAsStringAsync(ct);
            if (edit)
            {
                Assert.Contains("multipart/form-data", request.Content.Headers.ContentType!.ToString());
                Assert.Contains("gpt-image-2", body);
                Assert.Contains("image/png", body);
                Assert.EndsWith("/v1/images/edits", request.RequestUri!.AbsoluteUri);
            }
            else
            {
                using JsonDocument json = JsonDocument.Parse(body);
                Assert.Equal("1280x720", json.RootElement.GetProperty("size").GetString());
                Assert.False(json.RootElement.GetProperty("stream").GetBoolean());
            }

            return Json("""{"data":[{"b64_json":"AQID"}]}""");
        });
        ServiceResult<ModelAuxiliaryResult> result = edit
            ? await fixture.Gateway.EditImageAsync(new() { Model = "gpt-image-2", Prompt = "test", Images =
                [new() { FileName = "a.png", MimeType = "image/png", Bytes = [1, 2, 3] }] }, new("image-key"), TestContext.Current.CancellationToken)
            : await fixture.Gateway.GenerateImageAsync(new() { Model = "gpt-image-2", Prompt = "test", Size = "1280x720" },
                new("image-key"), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("AQID", result.Data!.Content.GetProperty("data")[0].GetProperty("b64_json").GetString());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sent!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Пустые data, raw errors и malformed JSON не признаются успехом и не повторяются.</summary>
    [Theory]
    [InlineData("""{"data":[]}""")]
    [InlineData("""{"data":[{}]}""")]
    [InlineData("""{"error":{"message":"secret"}}""")]
    [InlineData("invalid")]
    public async Task ImagesRejectInvalidResults(string body)
    {
        int calls = 0;
        using Fixture fixture = new((_, _) => { calls++; return Task.FromResult(Json(body)); });
        ServiceResult<ModelAuxiliaryResult> result = await fixture.Gateway.GenerateImageAsync(
            new() { Model = "image", Prompt = "prompt" }, new("key"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(1, calls);
        Assert.DoesNotContain("secret", result.Error!.Message);
    }

    /// <summary>Usage использует prefix и текущий ключ каждого вызова, без общей Authorization.</summary>
    [Fact]
    public async Task UsagePreservesPerCallAccess()
    {
        List<string> keys = [];
        using Fixture fixture = new((request, _) =>
        {
            Assert.Equal("https://gateway.invalid/prefix/v1/usage", request.RequestUri!.AbsoluteUri);
            keys.Add(request.Headers.Authorization!.Parameter!);
            return Task.FromResult(Json("""{"usage":{"requests":1},"limits":{"remaining":2}}"""));
        });

        Assert.True((await fixture.Gateway.ReadUsageAsync(new("first"), TestContext.Current.CancellationToken)).Success);
        Assert.True((await fixture.Gateway.ReadUsageAsync(new("second"), TestContext.Current.CancellationToken)).Success);
        Assert.Equal(new[] { "first", "second" }, keys);
    }

    /// <summary>Caller cancellation остаётся отменой; локальный deadline становится безопасным Timeout.</summary>
    [Fact]
    public async Task CancellationAndDeadlineAreDistinct()
    {
        using Fixture fixture = new(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json("{}"); }, "00:00:00.02");
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Gateway.ReadUsageAsync(new("key"), canceled.Token));

        ServiceResult<ModelAuxiliaryResult> result = await fixture.Gateway.ReadUsageAsync(new("key"), TestContext.Current.CancellationToken);
        Assert.Equal(ServiceErrorType.Timeout, result.Error!.Type);
    }

    /// <summary>Создаёт локальный JSON response.</summary>
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    /// <summary>Фиксирует реальные public DI и borrowed HttpClient без host.</summary>
    private class Fixture : IDisposable
    {
        private readonly HttpClient http;
        private readonly ServiceProvider root;
        private readonly IServiceScope scope;
        internal Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, string timeout = "00:00:10")
        {
            http = new(new Handler(send));
            ServiceCollection services = new();
            services.AddLogging();
            services.AddCodexLbConfiguration(options =>
            {
                options.BaseAddress = "https://gateway.invalid/prefix"; options.Model = "gpt-5"; options.ReasoningEffort = "medium";
                options.KeySource = ModelKeySourceMode.Shared; options.SharedApiKey = "key";
                options.GenerationTimeout = TimeSpan.FromSeconds(10); options.CompactTimeout = TimeSpan.FromSeconds(10);
            });
            services.AddCodexLbResponses(_ => http);
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetadataTimeout"] = timeout, ["FileCreateTimeout"] = timeout, ["FileUploadTimeout"] = timeout,
                ["FileFinalizeTimeout"] = timeout, ["ImageTimeout"] = timeout
            }).Build();
            services.AddCodexLbAuxiliary(config);
            root = services.BuildServiceProvider();
            scope = root.CreateScope();
        }
        internal IModelAuxiliaryGateway Gateway => scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>();
        public void Dispose() { scope.Dispose(); root.Dispose(); http.Dispose(); }
    }

    /// <summary>Подставляет ответы и локальные потоки вместо сети.</summary>
    private class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}

