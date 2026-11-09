using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using Xunit;
using static AgentBridge.Sub2Api.Tests.Sub2ApiCatalogFixture;
using static AgentBridge.Sub2Api.Tests.Sub2ApiUsageTests;
using static AgentBridge.Sub2Api.Tests.Sub2ApiImageTests;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Wire-параметры, snapshot входных файлов, local guards и per-call credentials.</summary>
public class Sub2ApiImageRequestTests
{
    /// <summary>Generation отправляет точные параметры и явный non-stream JSON без дополнительных controls.</summary>
    [Fact]
    public async Task GenerationPreservesAllParameters()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        JsonElement sent = default;
        HttpContent? content = null;
        fixture.Handler.Respond = async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            content = request.Content;
            Assert.Equal("application/json", content!.Headers.ContentType!.MediaType);
            using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync(ct));
            sent = document.RootElement.Clone();

            return Json(RESULT);
        };
        ModelImageRequest input = new()
        {
            Model = "custom-model-exact", Prompt = "  " + PRIVATE + "  ", Count = 3, Size = "1536x1024",
            Quality = "high", Background = "transparent", OutputFormat = "webp"
        };

        // Act
        ServiceResult<ModelAuxiliaryResult> result = await Gateway(fixture).GenerateImageAsync(input, new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(8, sent.EnumerateObject().Count());
        Assert.Equal(input.Model, sent.GetProperty("model").GetString());
        Assert.Equal(input.Prompt, sent.GetProperty("prompt").GetString());
        Assert.Equal(input.Count, sent.GetProperty("n").GetInt32());
        Assert.Equal(input.Size, sent.GetProperty("size").GetString());
        Assert.Equal(input.Quality, sent.GetProperty("quality").GetString());
        Assert.Equal(input.Background, sent.GetProperty("background").GetString());
        Assert.Equal(input.OutputFormat, sent.GetProperty("output_format").GetString());
        Assert.False(sent.GetProperty("stream").GetBoolean());
        Assert.Equal(["https://gateway.invalid/prefix/v1/images/generations"], fixture.Handler.Urls);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => content!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Multipart фиксирует коллекцию и bytes до await; два вызова создают разный content и per-request Bearer.</summary>
    [Fact]
    public async Task EditsSnapshotFilesBeforeAwaitAndUseFreshMultipart()
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);
        fixture.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "app-default");
        byte[] bytes = Encoding.UTF8.GetBytes(PRIVATE);
        byte[] expected = bytes.ToArray();
        List<ModelFileUpload> inputs = [new() { FileName = "фото.png", MimeType = "image/png", Bytes = bytes },
            new() { FileName = "other.bin", Bytes = [0, 1, 255] }];
        ModelImageEditRequest input = new()
        {
            Model = "custom-exact-model", Prompt = PRIVATE, Count = 2, Size = "auto", Quality = "high",
            Background = "transparent", OutputFormat = "jpeg", Images = inputs
        };
        List<MultipartFormDataContent> contents = [];
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Respond = async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            MultipartFormDataContent multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            contents.Add(multipart);
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            Dictionary<string, string> fields = new(StringComparer.Ordinal);
            List<HttpContent> images = [];
            foreach (HttpContent part in multipart)
            {
                string name = part.Headers.ContentDisposition!.Name!.Trim('"');
                if (name == "image[]")
                {
                    images.Add(part);
                }
                else
                {
                    fields.Add(name, await part.ReadAsStringAsync(ct));
                }
            }

            Assert.Equal(8, fields.Count);
            Assert.Equal(input.Model, fields["model"]);
            Assert.Equal(input.Prompt, fields["prompt"]);
            Assert.Equal("2", fields["n"]);
            Assert.Equal(input.Size, fields["size"]);
            Assert.Equal(input.Quality, fields["quality"]);
            Assert.Equal(input.Background, fields["background"]);
            Assert.Equal(input.OutputFormat, fields["output_format"]);
            Assert.Equal("false", fields["stream"]);
            Assert.Equal(2, images.Count);
            Assert.Equal("фото.png", images[0].Headers.ContentDisposition!.FileNameStar);
            Assert.Equal("image/png", images[0].Headers.ContentType!.MediaType);
            Assert.Equal("application/octet-stream", images[1].Headers.ContentType!.MediaType);
            Assert.Equal(expected, await images[0].ReadAsByteArrayAsync(ct));
            Assert.Equal(new byte[] { 0, 1, 255 }, await images[1].ReadAsByteArrayAsync(ct));

            return Json(RESULT);
        };

        // Act
        Task<ServiceResult<ModelAuxiliaryResult>> pending = Gateway(fixture).EditImageAsync(input, new(INDIVIDUAL_KEY),
            TestContext.Current.CancellationToken);
        ServiceResult<ModelAuxiliaryResult>? first = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Array.Fill(bytes, (byte)0);
            inputs.Clear();
            release.TrySetResult();
            first = await pending;
        }
        finally
        {
            release.TrySetResult();
            await Record.ExceptionAsync(() => pending);
        }

        inputs.Add(new() { FileName = "фото.png", MimeType = "image/png", Bytes = expected });
        inputs.Add(new() { FileName = "other.bin", Bytes = [0, 1, 255] });
        ServiceResult<ModelAuxiliaryResult> second = await Gateway(fixture).EditImageAsync(input, new(SHARED_KEY),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first!.Success);
        Assert.True(second.Success);
        Assert.NotSame(contents[0], contents[1]);
        Assert.Equal(["Bearer " + INDIVIDUAL_KEY, "Bearer " + SHARED_KEY], fixture.Handler.Authorizations);
        Assert.Equal("app-default", fixture.Http.DefaultRequestHeaders.Authorization.Parameter);
        Assert.All(fixture.Handler.Urls, url => Assert.Equal("https://gateway.invalid/prefix/v1/images/edits", url));
        Assert.Equal(2, fixture.Handler.Calls);
        foreach (HttpContent content in contents)
        {
            await Assert.ThrowsAsync<ObjectDisposedException>(() => content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }

        AssertSafe(fixture, string.Empty);
    }

    /// <summary>Root, prefix и завершающий /v1 нормализуются одинаково у обоих endpoints.</summary>
    [Theory]
    [InlineData("https://gateway.invalid", "https://gateway.invalid/v1/images/")]
    [InlineData("https://gateway.invalid/v1/", "https://gateway.invalid/v1/images/")]
    [InlineData("https://gateway.invalid/a", "https://gateway.invalid/a/v1/images/")]
    [InlineData("https://gateway.invalid/a/v1", "https://gateway.invalid/a/v1/images/")]
    public async Task NormalizesImagesUrl(string address, string expected)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(address, auxiliary: true);
        fixture.Handler.Respond = (_, _) => Task.FromResult(Json(RESULT));

        // Act
        await SendAsync(fixture, false, TestContext.Current.CancellationToken);
        await SendAsync(fixture, true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([expected + "generations", expected + "edits"], fixture.Handler.Urls);
    }

    /// <summary>Полные Images endpoints и encoded route отклоняются до HTTP factory.</summary>
    [Theory]
    [InlineData("https://gateway.invalid/v1/images/generations")]
    [InlineData("https://gateway.invalid/images/edits")]
    [InlineData("https://gateway.invalid/%69mages/edits")]
    public async Task RejectsEndpointAsBaseAddress(string address)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(address, auxiliary: true);

        // Act
        Exception? failure = await Record.ExceptionAsync(() => SendAsync(fixture, true, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<Microsoft.Extensions.Options.OptionsValidationException>(failure);
        Assert.DoesNotContain(address, failure.Message);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
    }

    /// <summary>Невалидные параметры и файлы не создают HTTP-клиент.</summary>
    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task LocalValidationBeforeFactory(ModelImageRequest input, bool edit)
    {
        // Arrange
        using Sub2ApiCatalogFixture fixture = new(auxiliary: true);

        // Act
        ServiceResult<ModelAuxiliaryResult> result;
        if (edit)
        {
            result = await Gateway(fixture).EditImageAsync((ModelImageEditRequest)input, new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        }
        else
        {
            result = await Gateway(fixture).GenerateImageAsync(input, new(INDIVIDUAL_KEY), TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.Error!.Type);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Handler.Calls);
        AssertSafe(fixture, result.Error.Message);
    }

    /// <summary>Подготовка ошибочных DTO без исполнения gateway.</summary>
    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [new ModelImageRequest { Prompt = PRIVATE }, false];
        yield return [new ModelImageRequest { Model = "model" }, false];
        yield return [new ModelImageRequest { Model = "model", Prompt = PRIVATE, Count = 0 }, false];
        yield return [new ModelImageRequest { Model = "model", Prompt = PRIVATE, Size = "" }, false];
        yield return [new ModelImageRequest { Model = "model", Prompt = PRIVATE, Quality = "" }, false];
        yield return [new ModelImageRequest { Model = "model", Prompt = PRIVATE, Background = "" }, false];
        yield return [new ModelImageRequest { Model = "model", Prompt = PRIVATE, OutputFormat = "" }, false];
        yield return [new ModelImageEditRequest { Model = "model", Prompt = PRIVATE }, true];
        yield return [new ModelImageEditRequest { Model = "model", Prompt = PRIVATE, Images = null! }, true];
        ModelFileUpload[] invalidFiles = [null!, new() { FileName = "x", Bytes = [] }, new() { FileName = "x", Bytes = null! },
            new() { Bytes = [1] }, new() { FileName = "../x", Bytes = [1] }, new() { FileName = "x\r\nheader", Bytes = [1] },
            new() { FileName = "x", Bytes = [1], MimeType = "image/png\r\nheader" },
            new() { FileName = "x", Bytes = [1], MimeType = "invalid mime" }];
        foreach (ModelFileUpload file in invalidFiles)
        {
            yield return [new ModelImageEditRequest { Model = "model", Prompt = PRIVATE, Images = [file] }, true];
        }
    }
}
