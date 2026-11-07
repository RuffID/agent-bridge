using System.Net;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Sub2Api.Configuration;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Options;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.Sub2Api.Models;

/// <inheritdoc/>
internal class Sub2ApiModelCatalog(HttpApiClient http, IOptionsSnapshot<Sub2ApiOptions> options) : IModelCatalog
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelCatalogSnapshot>> ReadAsync(ModelAccess access, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ct.ThrowIfCancellationRequested();

        Sub2ApiOptions configured = options.Value;
        if (!Sub2ApiEndpoints.TryGetModelsUrl(configured.BaseAddress, out string endpoint))
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
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);

        try
        {
            HttpResponseResult<JsonElement> response = await http.SendWithResponseAsync<JsonElement>(request, linked.Token);
            ModelCatalogSnapshot snapshot = Sub2ApiCatalogJsonReader.Read(response.Body);
            ct.ThrowIfCancellationRequested();
            deadline.Token.ThrowIfCancellationRequested();
            return ServiceResult<ModelCatalogSnapshot>.Ok(snapshot);
        }
        catch (HttpRequestFailedException failure) when (failure.ProcessingFailure is null)
        {
            ServiceErrorType type = failure.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ServiceErrorType.Unauthorized,
                HttpStatusCode.Forbidden => ServiceErrorType.Forbidden,
                _ => ServiceErrorType.Rejected
            };

            return Failure(type, "Сервер отклонил чтение каталога моделей.");
        }
        catch (JsonException failure) when (!HasCleanupFailure(failure))
        {
            return Failure(ServiceErrorType.Rejected, "Получен некорректный каталог моделей.");
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure) && ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (OperationCanceledException failure) when (!HasCleanupFailure(failure) && deadline.IsCancellationRequested)
        {
            return Failure(ServiceErrorType.Timeout, "Истёк срок чтения каталога моделей.");
        }
    }

    /// <summary>Сохраняет combined и standalone cleanup failures на границе actual библиотеки.</summary>
    private static bool HasCleanupFailure(Exception failure)
    {
        if (failure is HttpResponseCanceledException canceled && canceled.ProcessingFailure.CleanupExceptions.Count != 0)
        {
            return true;
        }

        return failure.Data["HttpClientLibrary.CleanupExceptions"] is IReadOnlyList<Exception>;
    }

    /// <summary>Возвращает безопасную ошибку без headers, body и серверного текста.</summary>
    private static ServiceResult<ModelCatalogSnapshot> Failure(ServiceErrorType type, string message)
    {
        return ServiceResult<ModelCatalogSnapshot>.Fail(new(type, message));
    }
}
