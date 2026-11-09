using System.Net;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Sub2Api.Configuration;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.Sub2Api.Auxiliary;

/// <inheritdoc/>
internal class Sub2ApiAuxiliaryGateway(Func<HttpApiClient> httpFactory,
    IOptionsSnapshot<Sub2ApiOptions> options, ILogger<Sub2ApiAuxiliaryGateway> logger) : IModelAuxiliaryGateway
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelAuxiliaryResult>> ReadUsageAsync(ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();

        Sub2ApiOptions configured = options.Value;
        if (!Sub2ApiEndpoints.TryGetUsageUrl(configured.BaseAddress, out string endpoint))
        {
            return Failure(ServiceErrorType.Validation, "Sub2Api.BaseAddress: required_or_invalid.");
        }

        string key = access.RevealApiKey();
        if (!Sub2ApiCredential.IsValid(key))
        {
            return Failure(ServiceErrorType.Validation, "Заданный ключ доступа имеет недопустимый формат.");
        }

        LibraryHttpRequestOptions request = new()
        {
            Method = HttpMethod.Get,
            Url = endpoint,
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + key }
        };
        using CancellationTokenSource deadline = new(configured.GenerationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        try
        {
            HttpResponseResult<JsonElement> response = await httpFactory().SendWithResponseAsync<JsonElement>(request, linked.Token);
            cancellationToken.ThrowIfCancellationRequested();
            deadline.Token.ThrowIfCancellationRequested();
            ValidateUsage(response.Body);

            return ServiceResult<ModelAuxiliaryResult>.Ok(new(response.Body));
        }
        catch (HttpRequestFailedException failure) when (failure.ProcessingFailure is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested)
            {
                return TimeoutFailure();
            }

            ServiceErrorType type = failure.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ServiceErrorType.Unauthorized,
                HttpStatusCode.Forbidden => ServiceErrorType.Forbidden,
                _ => ServiceErrorType.Rejected
            };

            return Failure(type, "Сервер отклонил чтение usage sub2api.");
        }
        catch (JsonException failure) when (!HasCleanupFailure(failure))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested)
            {
                return TimeoutFailure();
            }

            return Failure(ServiceErrorType.Rejected, "Получен некорректный ответ usage sub2api.");
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure) && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure)
            && (deadline.IsCancellationRequested || failure.InnerException is TimeoutException))
        {
            return TimeoutFailure();
        }
        catch (HttpRequestException failure) when (!HasCleanupFailure(failure))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested)
            {
                return TimeoutFailure();
            }

            return Failure(ServiceErrorType.Rejected, "Не удалось прочитать usage sub2api.");
        }
    }

    /// <inheritdoc/>
    public Task<ServiceResult<string>> UploadFileAsync(ModelFileUpload file, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<string>.Fail(new(ServiceErrorType.Unsupported,
            "Загрузка файлов sub2api не поддерживается.")));
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelAuxiliaryResult>> GenerateImageAsync(ModelImageRequest request, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        return SendImageAsync(request, access, false, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelAuxiliaryResult>> EditImageAsync(ModelImageEditRequest request, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        return SendImageAsync(request, access, true, cancellationToken);
    }

    /// <summary>Пишет безопасный итог после проверки JSON и очистки, не изменяя transport exception.</summary>
    private async Task<ServiceResult<ModelAuxiliaryResult>> SendImageAsync(ModelImageRequest request, ModelAccess access,
        bool edit, CancellationToken cancellationToken)
    {
        try
        {
            ServiceResult<ModelAuxiliaryResult> result = await new Sub2ApiImageTransport(httpFactory, options)
                .SendAsync(request, access, edit, cancellationToken);
            string outcome = "Rejected";
            if (result.Success)
            {
                outcome = "Completed";
            }

            LogImageOutcome(edit, outcome, result.Error?.Type);

            return result;
        }
        catch (OperationCanceledException)
        {
            LogImageOutcome(edit, "Canceled", null);
            throw;
        }
        catch (Exception)
        {
            LogImageOutcome(edit, "Exception", null);
            throw;
        }
    }

    /// <summary>Использует только фиксированные operation/outcome и тип ошибки, без input/result/exception.</summary>
    private void LogImageOutcome(bool edit, string outcome, ServiceErrorType? errorType)
    {
        string operation = "Generate";
        if (edit)
        {
            operation = "Edit";
        }

        try
        {
            logger.LogInformation(new EventId(5201, "Sub2ApiImageFinished"),
                "Sub2Api image finished; Operation={Operation}; Outcome={Outcome}; ErrorType={ErrorType}",
                operation, outcome, errorType);
        }
        catch (Exception)
        {
            // Диагностика не подменяет результат/primary и не создаёт повторную платную операцию.
        }
    }

    /// <summary>Проверяет discriminator и error envelope без перепроецирования серверного JSON.</summary>
    private static void ValidateUsage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        try
        {
            // JsonElement откладывает декодирование: проверяем также неизвестные поля на повреждённый UTF-8.
            _ = root.GetRawText();
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException();
                }
            }

            if (root.TryGetProperty("error", out JsonElement error) && error.ValueKind != JsonValueKind.Null)
            {
                throw new JsonException();
            }

            if (!root.TryGetProperty("mode", out JsonElement mode) || mode.ValueKind != JsonValueKind.String)
            {
                throw new JsonException();
            }

            if (mode.GetString() is not ("quota_limited" or "unrestricted"))
            {
                throw new JsonException();
            }
        }
        catch (InvalidOperationException failure) when (failure.InnerException is DecoderFallbackException)
        {
            throw new JsonException();
        }
    }

    /// <summary>Не скрывает combined cleanup failure обычным typed отказом.</summary>
    private static bool HasCleanupFailure(Exception failure)
    {
        if (failure is HttpResponseCanceledException canceled && canceled.ProcessingFailure.CleanupExceptions.Count != 0)
        {
            return true;
        }

        return failure.Data["HttpClientLibrary.CleanupExceptions"] is IReadOnlyList<Exception>;
    }

    /// <summary>Создаёт безопасный отказ без transport body, URL и секретов.</summary>
    private static ServiceResult<ModelAuxiliaryResult> Failure(ServiceErrorType type, string message)
    {
        return ServiceResult<ModelAuxiliaryResult>.Fail(new(type, message));
    }

    /// <summary>Не включает транспортное исключение или endpoint в сообщение timeout.</summary>
    private static ServiceResult<ModelAuxiliaryResult> TimeoutFailure()
    {
        return Failure(ServiceErrorType.Timeout, "Истёк срок чтения usage sub2api.");
    }
}
