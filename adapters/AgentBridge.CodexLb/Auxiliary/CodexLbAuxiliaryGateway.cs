using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Responses;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Options;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.CodexLb.Auxiliary;

/// <inheritdoc/>
public class CodexLbAuxiliaryGateway(HttpApiClient http, IOptionsSnapshot<CodexLbOptions> options,
    IOptionsSnapshot<CodexLbAuxiliaryOptions> auxiliary) : IModelAuxiliaryGateway
{
    private const string CLEANUP_DATA_KEY = "HttpClientLibrary.CleanupExceptions";

    /// <inheritdoc/>
    public async Task<ServiceResult<ModelAuxiliaryResult>> ReadUsageAsync(ModelAccess access, CancellationToken cancellationToken = default)
    {
        LibraryHttpRequestOptions request = ApiRequest(HttpMethod.Get, "/v1/usage", access);
        ServiceResult<ModelAuxiliaryResult> result = await SendJsonAsync(request, auxiliary.Value.MetadataTimeout, cancellationToken);

        return result.Success && result.Data!.Content.ValueKind != JsonValueKind.Object
            ? Invalid<ModelAuxiliaryResult>() : result;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<string>> UploadFileAsync(ModelFileUpload file, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(file.FileName) || file.Bytes.Length == 0)
            return ServiceResult<string>.Fail(new(ServiceErrorType.Validation, "Требуются имя и содержимое файла."));

        byte[] bytes = file.Bytes.ToArray();
        string fileName = file.FileName;
        string? mimeType = file.MimeType;
        LibraryHttpRequestOptions registration = ApiRequest(HttpMethod.Post, "/backend-api/files", access,
            new { file_name = fileName, file_size = bytes.LongLength, use_case = "codex" });
        ServiceResult<ModelAuxiliaryResult> created = await SendJsonAsync(registration, auxiliary.Value.FileCreateTimeout, cancellationToken);
        if (!created.Success)
            return ServiceResult<string>.Fail(created.Error!);

        string? fileId = ReadString(created.Data!.Content, "file_id");
        string? uploadUrl = ReadString(created.Data!.Content, "upload_url");
        if (string.IsNullOrWhiteSpace(fileId) || !Uri.TryCreate(uploadUrl, UriKind.Absolute, out Uri? uploadUri)
            || uploadUri.Scheme is not ("https" or "http") || uploadUri.UserInfo.Length != 0 || uploadUri.Fragment.Length != 0)
            return Invalid<string>();

        LibraryHttpRequestOptions upload = new()
        {
            Method = HttpMethod.Put,
            Url = uploadUri.AbsoluteUri,
            Headers = new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob" },
            ContentFactory = () => ByteContent(bytes, mimeType)
        };
        ServiceResult uploaded = await UploadAsync(upload, auxiliary.Value.FileUploadTimeout, cancellationToken);
        if (!uploaded.Success)
            return ServiceResult<string>.Fail(uploaded.Error!);

        LibraryHttpRequestOptions confirmation = ApiRequest(HttpMethod.Post,
            "/backend-api/files/" + Uri.EscapeDataString(fileId) + "/uploaded", access, new { });
        ServiceResult<ModelAuxiliaryResult> confirmed = await SendJsonAsync(confirmation, auxiliary.Value.FileFinalizeTimeout, cancellationToken);
        if (!confirmed.Success)
            return ServiceResult<string>.Fail(confirmed.Error!);

        string? confirmedId = ReadString(confirmed.Data!.Content, "file_id");
        if (!string.Equals(ReadString(confirmed.Data!.Content, "status"), "success", StringComparison.OrdinalIgnoreCase)
            || (confirmedId is not null && !string.Equals(confirmedId, fileId, StringComparison.Ordinal)))
            return Invalid<string>();

        return ServiceResult<string>.Ok(fileId);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<ModelAuxiliaryResult>> GenerateImageAsync(ModelImageRequest request, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ValidImage(request))
            return ServiceResult<ModelAuxiliaryResult>.Fail(new(ServiceErrorType.Validation, "Некорректный запрос изображения."));

        LibraryHttpRequestOptions transport = ApiRequest(HttpMethod.Post, "/v1/images/generations", access,
            new { model = request.Model, prompt = request.Prompt, n = request.Count, size = request.Size,
                quality = request.Quality, background = request.Background, output_format = request.OutputFormat, stream = false });
        ServiceResult<ModelAuxiliaryResult> result = await SendJsonAsync(transport, auxiliary.Value.ImageTimeout, cancellationToken);

        return ValidateImages(result);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<ModelAuxiliaryResult>> EditImageAsync(ModelImageEditRequest request, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ValidImage(request) || request.Images.Count == 0 || request.Images.Any(image => image is null || image.Bytes.Length == 0))
            return ServiceResult<ModelAuxiliaryResult>.Fail(new(ServiceErrorType.Validation, "Требуются параметры и входные изображения."));

        ModelFileUpload[] images = request.Images.Select(image => new ModelFileUpload
        {
            FileName = image.FileName, MimeType = image.MimeType, Bytes = image.Bytes.ToArray()
        }).ToArray();
        // Фабрика фиксирует значения, а не изменяемые пользовательские коллекции.
        string model = request.Model, prompt = request.Prompt, size = request.Size, quality = request.Quality;
        string background = request.Background, format = request.OutputFormat;
        int count = request.Count;
        LibraryHttpRequestOptions baseRequest = ApiRequest(HttpMethod.Post, "/v1/images/edits", access);
        LibraryHttpRequestOptions transport = new()
        {
            Method = baseRequest.Method, Url = baseRequest.Url, Headers = baseRequest.Headers,
            ContentFactory = () => Multipart(model, prompt, count, size, quality, background, format, images)
        };
        ServiceResult<ModelAuxiliaryResult> result = await SendJsonAsync(transport, auxiliary.Value.ImageTimeout, cancellationToken);

        return ValidateImages(result);
    }

    /// <summary>Отправляет JSON с отдельным deadline и безопасной проекцией ожидаемых отказов.</summary>
    private async Task<ServiceResult<ModelAuxiliaryResult>> SendJsonAsync(LibraryHttpRequestOptions request, TimeSpan timeout, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        using CancellationTokenSource deadline = new(timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(caller, deadline.Token);

        try
        {
            HttpResponseResult<JsonElement> result = await http.SendWithResponseAsync<JsonElement>(request, linked.Token);
            caller.ThrowIfCancellationRequested();
            deadline.Token.ThrowIfCancellationRequested();

            if (result.Body.ValueKind != JsonValueKind.Object || result.Body.TryGetProperty("error", out JsonElement error)
                && error.ValueKind != JsonValueKind.Null)
                return Invalid<ModelAuxiliaryResult>();

            return ServiceResult<ModelAuxiliaryResult>.Ok(new(result.Body));
        }
        catch (HttpRequestFailedException failure) when (!HasCleanupFailure(failure))
        {
            return ServiceResult<ModelAuxiliaryResult>.Fail(ResponseErrorReader.Read(failure));
        }
        catch (JsonException failure) when (!HasCleanupFailure(failure))
        {
            return Invalid<ModelAuxiliaryResult>();
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure) && caller.IsCancellationRequested)
        {
            throw new OperationCanceledException(caller);
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure) && deadline.IsCancellationRequested)
        {
            return ServiceResult<ModelAuxiliaryResult>.Fail(new(ServiceErrorType.Timeout, "Истёк срок ожидания дополнительной операции."));
        }
    }

    /// <summary>Принимает upload HTTP-успех без JSON-десериализации пустого или XML тела.</summary>
    private async Task<ServiceResult> UploadAsync(LibraryHttpRequestOptions request, TimeSpan timeout, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        using CancellationTokenSource deadline = new(timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(caller, deadline.Token);

        try
        {
            await using HttpStreamResponseResult response = await http.SendStreamAsync(request, linked.Token);
            caller.ThrowIfCancellationRequested();
            deadline.Token.ThrowIfCancellationRequested();

            return ServiceResult.Ok();
        }
        catch (HttpRequestFailedException failure) when (!HasCleanupFailure(failure))
        {
            // Ошибка стороннего signed URL не является доверенным API envelope.
            return ServiceResult.Fail(new(ServiceErrorType.Rejected, "Сервер отклонил загрузку файла."));
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure) && caller.IsCancellationRequested)
        {
            throw new OperationCanceledException(caller);
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure) && deadline.IsCancellationRequested)
        {
            return ServiceResult.Fail(new(ServiceErrorType.Timeout, "Истёк срок загрузки файла."));
        }
    }

    /// <summary>Формирует только per-request Bearer и сохраняет base-prefix.</summary>
    private LibraryHttpRequestOptions ApiRequest(HttpMethod method, string path, ModelAccess access, object? body = null)
    {
        ArgumentNullException.ThrowIfNull(access);
        string key = access.RevealApiKey();
        if (key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
            throw new ArgumentException("Некорректный формат доступа.", nameof(access));

        string address = options.Value.BaseAddress ?? throw new InvalidOperationException("Адрес codex-lb не настроен.");
        return new LibraryHttpRequestOptions
        {
            Method = method, Url = address.TrimEnd('/') + path,
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + key }, Body = body
        };
    }

    /// <summary>Создаёт fresh multipart; при ошибке построения освобождает уже добавленные части.</summary>
    private static MultipartFormDataContent Multipart(string model, string prompt, int count, string size, string quality,
        string background, string format, IReadOnlyList<ModelFileUpload> images)
    {
        MultipartFormDataContent content = new();
        try
        {
            content.Add(new StringContent(model), "model");
            content.Add(new StringContent(prompt), "prompt");
            content.Add(new StringContent(count.ToString(CultureInfo.InvariantCulture)), "n");
            content.Add(new StringContent(size), "size");
            content.Add(new StringContent(quality), "quality");
            content.Add(new StringContent(background), "background");
            content.Add(new StringContent(format), "output_format");
            content.Add(new StringContent("false"), "stream");
            foreach (ModelFileUpload image in images)
                content.Add(ByteContent(image.Bytes, image.MimeType), "image",
                    string.IsNullOrWhiteSpace(image.FileName) ? "image.png" : image.FileName);

            return content;
        }
        catch
        {
            content.Dispose();
            throw;
        }
    }

    /// <summary>Создаёт новый byte content с явным MIME.</summary>
    private static ByteArrayContent ByteContent(byte[] bytes, string? mimeType)
    {
        ByteArrayContent content = new(bytes);
        try
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType);

            return content;
        }
        catch
        {
            content.Dispose();
            throw;
        }
    }

    /// <summary>Проверяет обязательные параметры DTO, без угадывания серверных enum.</summary>
    private static bool ValidImage(ModelImageRequest request) => !string.IsNullOrWhiteSpace(request.Model)
        && !string.IsNullOrWhiteSpace(request.Prompt) && request.Count > 0;

    /// <summary>Не признаёт пустой или невалидный результат изображения успехом.</summary>
    private static ServiceResult<ModelAuxiliaryResult> ValidateImages(ServiceResult<ModelAuxiliaryResult> result)
    {
        if (!result.Success)
            return result;

        if (!result.Data!.Content.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array
            || data.GetArrayLength() == 0 || data.EnumerateArray().Any(image =>
                string.IsNullOrWhiteSpace(ReadString(image, "b64_json")) && string.IsNullOrWhiteSpace(ReadString(image, "url"))))
            return Invalid<ModelAuxiliaryResult>();

        return result;
    }

    /// <summary>Читает строковое поле только из объекта.</summary>
    private static string? ReadString(JsonElement json, string property) => json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Не подменяет combined cleanup failure обычным expected отказом.</summary>
    private static bool HasCleanupFailure(Exception failure) => failure.Data[CLEANUP_DATA_KEY] is IReadOnlyList<Exception>;

    /// <summary>Возвращает безопасный отказ без исходного ответа.</summary>
    private static ServiceResult<T> Invalid<T>() where T : class => ServiceResult<T>.Fail(new(ServiceErrorType.Rejected,
        "Получен некорректный ответ дополнительного API."));
}

