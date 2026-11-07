using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.Responses;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.Sub2Api.Responses;

/// <summary>Читает один HTTP-ответ как JSON/SSE без повторной отправки и server continuation.</summary>
internal class Sub2ApiResponseTransport(HttpApiClient http)
{
    private const string CLEANUP_DATA_KEY = "HttpClientLibrary.CleanupExceptions";

    /// <summary>Сохраняет lifecycle/partial output; callback и cleanup exceptions не нормализует.</summary>
    internal async Task<ServiceResult<ModelResponse>> SendAsync(LibraryHttpRequestOptions request,
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? callback,
        CancellationToken caller, CancellationToken deadline, CancellationToken linked)
    {
        ResponseSseAccumulator state = new((_, _) => null, _ => ModelError());
        bool callbackFailed = false;
        ModelResponse? report = null;
        try
        {
            HttpStreamResponseResult response = await http.SendStreamAsync(request, linked);
            Exception? primary = null;
            ExceptionDispatchInfo? primaryDispatch = null;
            try
            {
                string? mediaType = MediaType(response.Headers);
                if (mediaType == "text/event-stream")
                {
                    await foreach ((string? eventName, string data) in SseEventReader.ReadAsync(response.Body, linked))
                    {
                        if (data == "[DONE]")
                        {
                            continue;
                        }

                        using JsonDocument document = JsonDocument.Parse(data);
                        ModelStreamUpdate? update = state.Apply(document.RootElement, eventName, response.Headers);
                        if (update is not null && callback is not null)
                        {
                            linked.ThrowIfCancellationRequested();
                            try
                            {
                                await callback(update, linked);
                            }
                            catch
                            {
                                callbackFailed = true;
                                throw;
                            }
                        }

                        if (state.Terminal)
                        {
                            break;
                        }

                        linked.ThrowIfCancellationRequested();
                    }

                    report = state.Report();
                }
                else if (mediaType == "application/json" || mediaType?.EndsWith("+json", StringComparison.Ordinal) == true)
                {
                    using StreamReader reader = new(response.Body, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
                    string json = await reader.ReadToEndAsync(linked);
                    using JsonDocument document = JsonDocument.Parse(json);
                    report = ReadJson(document.RootElement);
                    if (callback is not null)
                    {
                        foreach (CanonicalModelItem item in report.Output)
                        {
                            linked.ThrowIfCancellationRequested();
                            try
                            {
                                await callback(new(null, item), linked);
                            }
                            catch
                            {
                                callbackFailed = true;
                                throw;
                            }
                        }
                    }
                }
                else
                {
                    throw new JsonException();
                }
            }
            catch (Exception failure)
            {
                primary = failure;
                primaryDispatch = ExceptionDispatchInfo.Capture(failure);
                throw;
            }
            finally
            {
                try
                {
                    await response.DisposeAsync();
                }
                catch (Exception cleanup) when (primary is not null)
                {
                    AttachCleanup(primary, cleanup);
                }

                primaryDispatch?.Throw();
            }

            if (report.Status == ModelResponseStatus.Failed)
            {
                return ServiceResult<ModelResponse>.Ok(report);
            }

            if (caller.IsCancellationRequested)
            {
                if (report.Envelope is null && !state.HasData)
                {
                    caller.ThrowIfCancellationRequested();
                }

                return ServiceResult<ModelResponse>.Ok(ModelResponse.Canceled(report.Output, report.Envelope));
            }

            if (deadline.IsCancellationRequested)
            {
                return Fail(TimeoutError(), report, state);
            }

            return ServiceResult<ModelResponse>.Ok(report);
        }
        catch (HttpRequestFailedException failure) when (!callbackFailed && !HasProcessingFailure(failure))
        {
            ServiceErrorType type = failure.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ServiceErrorType.Validation,
                HttpStatusCode.Unauthorized => ServiceErrorType.Unauthorized,
                HttpStatusCode.Forbidden => ServiceErrorType.Forbidden,
                HttpStatusCode.NotFound => ServiceErrorType.NotFound,
                HttpStatusCode.Conflict => ServiceErrorType.Conflict,
                HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ServiceErrorType.Timeout,
                _ => ServiceErrorType.Rejected
            };
            return ServiceResult<ModelResponse>.Fail(new(type, "Сервер отклонил запрос модели."));
        }
        catch (Exception failure) when (!callbackFailed && !HasProcessingFailure(failure)
            && failure is JsonException or DecoderFallbackException)
        {
            return Fail(new(ServiceErrorType.Rejected, "Получен некорректный ответ модели."), report, state);
        }
        catch (OperationCanceledException failure) when (!callbackFailed && !HasProcessingFailure(failure) && caller.IsCancellationRequested)
        {
            if (report is not null)
            {
                if (report.Status == ModelResponseStatus.Failed)
                {
                    return ServiceResult<ModelResponse>.Ok(report);
                }

                return ServiceResult<ModelResponse>.Ok(ModelResponse.Canceled(report.Output, report.Envelope));
            }

            if (state.HasData)
            {
                return ServiceResult<ModelResponse>.Ok(state.Cancel());
            }

            throw new OperationCanceledException(caller);
        }
        catch (OperationCanceledException failure) when (!callbackFailed && !HasProcessingFailure(failure) && deadline.IsCancellationRequested)
        {
            return Fail(TimeoutError(), report, state);
        }
    }

    /// <summary>Completed требует explicit terminal status и output array; id не создаёт continuation.</summary>
    private static ModelResponse ReadJson(JsonElement body)
    {
        List<CanonicalModelItem> output = CanonicalOutputReader.Read(body, out bool hasOutput);
        CanonicalModelEnvelope envelope = new(body);
        string? status = null;
        if (body.TryGetProperty("status", out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            status = value.GetString();
        }

        if (status == "failed" || body.TryGetProperty("error", out JsonElement error) && error.ValueKind != JsonValueKind.Null)
        {
            return ModelResponse.Failed(output, ModelError(), envelope);
        }

        if (status == "completed" && hasOutput)
        {
            return ModelResponse.Completed(output, envelope);
        }

        return ModelResponse.Incomplete(output, envelope);
    }

    /// <summary>Сохраняет только известную полную media type, без публикации raw headers.</summary>
    private static string? MediaType(IReadOnlyDictionary<string, string[]> headers)
    {
        if (headers.TryGetValue("Content-Type", out string[]? values) && values.Length == 1)
        {
            return values[0].Split(';', 2)[0].Trim().ToLowerInvariant();
        }

        return null;
    }

    /// <summary>Ошибка модели не раскрывает message/type/code из неконтролируемого ответа.</summary>
    private static ServiceError ModelError()
    {
        return new(ServiceErrorType.Rejected, "Модель сообщила об отказе выполнения.");
    }

    /// <summary>Стабильный локальный timeout без upstream текста.</summary>
    private static ServiceError TimeoutError()
    {
        return new(ServiceErrorType.Timeout, "Истёк срок ожидания модели.");
    }

    /// <summary>Отделяет отсутствие отчёта от отказа с известным canonical output.</summary>
    private static ServiceResult<ModelResponse> Fail(ServiceError error, ModelResponse? report, ResponseSseAccumulator state)
    {
        if (report is not null)
        {
            if (report.Status == ModelResponseStatus.Failed)
            {
                return ServiceResult<ModelResponse>.Ok(report);
            }

            return ServiceResult<ModelResponse>.Ok(ModelResponse.Failed(report.Output, error, report.Envelope));
        }

        if (state.HasData)
        {
            return ServiceResult<ModelResponse>.Ok(state.Fail(error));
        }

        return ServiceResult<ModelResponse>.Fail(error);
    }

    /// <summary>Не теряет read/cleanup failures actual HttpClientLibrary.</summary>
    private static bool HasProcessingFailure(Exception failure)
    {
        if (failure is HttpRequestFailedException { ProcessingFailure: not null })
        {
            return true;
        }

        if (failure is HttpResponseCanceledException canceled && canceled.ProcessingFailure.CleanupExceptions.Count != 0)
        {
            return true;
        }

        return failure.Data[CLEANUP_DATA_KEY] is IReadOnlyList<Exception>;
    }

    /// <summary>Сохраняет primary identity и все secondary cleanup exceptions по порядку.</summary>
    private static void AttachCleanup(Exception primary, Exception cleanup)
    {
        List<Exception> errors = new();
        if (primary.Data[CLEANUP_DATA_KEY] is IReadOnlyList<Exception> previous)
        {
            errors.AddRange(previous);
        }

        if (!ReferenceEquals(primary, cleanup))
        {
            errors.Add(cleanup);
            if (cleanup.Data[CLEANUP_DATA_KEY] is IReadOnlyList<Exception> nested)
            {
                errors.AddRange(nested);
            }
        }

        primary.Data[CLEANUP_DATA_KEY] = errors.AsReadOnly();
    }
}
