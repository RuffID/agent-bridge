using System.Net;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Sub2Api.Configuration;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Options;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.Sub2Api.Auxiliary;

/// <summary>Однократная non-stream Images операция через существующую отложенную HTTP factory.</summary>
internal class Sub2ApiImageTransport(Func<HttpApiClient> httpFactory, IOptionsSnapshot<Sub2ApiOptions> options)
{
    /// <summary>Фиксирует вход до await; сохраняет общий deadline, caller token и cleanup failures.</summary>
    internal async Task<ServiceResult<ModelAuxiliaryResult>> SendAsync(ModelImageRequest request, ModelAccess access,
        bool edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();

        Sub2ApiOptions configured = options.Value;
        if (!Sub2ApiEndpoints.TryGetImagesUrl(configured.BaseAddress, edit, out string endpoint))
        {
            return Failure(ServiceErrorType.Validation, "Sub2Api.BaseAddress: required_or_invalid.");
        }

        string key = access.RevealApiKey();
        if (!Sub2ApiCredential.IsValid(key))
        {
            return Failure(ServiceErrorType.Validation, "Заданный ключ доступа имеет недопустимый формат.");
        }

        LibraryHttpRequestOptions? transport = Sub2ApiImageRequestFactory.Create(request, endpoint, key, edit);
        if (transport is null)
        {
            return Failure(ServiceErrorType.Validation, "Некорректные параметры или входные файлы изображения.");
        }

        using CancellationTokenSource deadline = new(configured.GenerationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        try
        {
            HttpResponseResult<JsonElement> response = await httpFactory().SendWithResponseAsync<JsonElement>(transport, linked.Token);
            cancellationToken.ThrowIfCancellationRequested();
            deadline.Token.ThrowIfCancellationRequested();
            Sub2ApiImageResultReader.Validate(response.Body);

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

            return Failure(type, "Сервер отклонил операцию изображения sub2api.");
        }
        catch (JsonException failure) when (!HasCleanupFailure(failure))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested)
            {
                return TimeoutFailure();
            }

            return Failure(ServiceErrorType.Rejected, "Получен некорректный результат изображения sub2api.");
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

            return Failure(ServiceErrorType.Rejected, "Не удалось выполнить операцию изображения sub2api.");
        }
    }

    /// <summary>Не превращает combined cleanup failure в обычный typed отказ.</summary>
    private static bool HasCleanupFailure(Exception failure)
    {
        if (failure is HttpResponseCanceledException canceled && canceled.ProcessingFailure.CleanupExceptions.Count != 0)
        {
            return true;
        }

        return failure.Data["HttpClientLibrary.CleanupExceptions"] is IReadOnlyList<Exception>;
    }

    /// <summary>Не раскрывает raw server/transport данные в ожидаемом отказе.</summary>
    private static ServiceResult<ModelAuxiliaryResult> Failure(ServiceErrorType type, string message)
    {
        return ServiceResult<ModelAuxiliaryResult>.Fail(new(type, message));
    }

    /// <summary>Возвращает безопасный timeout без endpoint и исходного исключения.</summary>
    private static ServiceResult<ModelAuxiliaryResult> TimeoutFailure()
    {
        return Failure(ServiceErrorType.Timeout, "Истёк срок операции изображения sub2api.");
    }
}
