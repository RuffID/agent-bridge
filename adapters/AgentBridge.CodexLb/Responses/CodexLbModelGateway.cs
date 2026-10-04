using System.Text.Json;
using System.Text;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Responses.Models;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Options;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.CodexLb.Responses;

/// <inheritdoc/>
/// <remarks>Callback выбирает SSE, null сохраняет JSON. Caller cancellation приоритетнее локального deadline.</remarks>
public class CodexLbModelGateway(HttpApiClient http, IOptionsSnapshot<CodexLbOptions> options) : IModelGateway
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request,
        ModelAccess access, Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();
        string key = access.RevealApiKey();
        if (key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Validation, "Заданный ключ доступа имеет недопустимый формат."));
        }
        CodexLbOptions configuration = options.Value;
        string endpoint = (configuration.BaseAddress ?? throw new InvalidOperationException("Адрес codex-lb не настроен."))
            .TrimEnd('/') + "/v1/responses";
        string binding = ResponseContinuationMapper.Binding(call, access, endpoint);
        ServiceResult<ResponseContinuationState> continuation = ResponseContinuationMapper.Read(request.Continuation, binding);
        if (!continuation.Success) { return ServiceResult<ModelResponse>.Fail(continuation.Error!); }
        ServiceResult<ResponseRequestBody> body = ResponseRequestWriter.Write(request, continuation.Data!.PreviousId, onUpdate is not null);
        if (!body.Success) { return ServiceResult<ModelResponse>.Fail(body.Error!); }
        Dictionary<string, string> headers = new() { ["Authorization"] = "Bearer " + key };
        if (continuation.Data.TurnState is not null) { headers["x-codex-turn-state"] = continuation.Data.TurnState; }
        LibraryHttpRequestOptions transport = new()
        {
            Method = HttpMethod.Post, Url = endpoint, Headers = headers, Body = body.Data!.Content, CorrelationId = call.TurnId
        };
        using CancellationTokenSource deadline = new(configuration.GenerationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        if (onUpdate is not null)
        {
            return await GenerateStreamAsync(transport, request.Continuation, binding, onUpdate,
                cancellationToken, deadline.Token, linked.Token);
        }
        try
        {
            HttpResponseResult<JsonElement> response = await http.SendWithResponseAsync<JsonElement>(transport, linked.Token);
            ModelResponse report = ResponseJsonReader.Read(response.Body, response.Headers, request.Continuation, binding);
            // Полученный explicit failure не подменяется поздней отменой; известный output не теряется.
            if (report.Status == ModelResponseStatus.Failed) { return ServiceResult<ModelResponse>.Ok(report); }
            if (cancellationToken.IsCancellationRequested)
            {
                return ServiceResult<ModelResponse>.Ok(ModelResponse.Canceled(report.Output, report.Envelope, report.Continuation));
            }
            deadline.Token.ThrowIfCancellationRequested();
            return ServiceResult<ModelResponse>.Ok(report);
        }
        catch (HttpRequestFailedException failure)
        {
            return ServiceResult<ModelResponse>.Fail(ResponseErrorReader.Read(failure));
        }
        catch (JsonException)
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Rejected, "Получен некорректный JSON-ответ модели."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Timeout, "Истёк срок ожидания модели."));
        }
    }

    /// <summary>Владеет SSE wrapper; ошибки callbacks не нормализуются как ошибки транспорта.</summary>
    private async Task<ServiceResult<ModelResponse>> GenerateStreamAsync(LibraryHttpRequestOptions transport,
        ModelContinuation? previous, string binding, Func<ModelStreamUpdate, CancellationToken, ValueTask> onUpdate,
        CancellationToken caller, CancellationToken deadline, CancellationToken linked)
    {
        ResponseSseState state = new(previous, binding);
        bool callbackFailed = false;
        try
        {
            await using (HttpStreamResponseResult response = await http.SendStreamAsync(transport, linked))
            {
                await foreach ((string? eventName, string data) in SseEventReader.ReadAsync(response.Body, linked))
                {
                    if (data == "[DONE]") { continue; }
                    using JsonDocument document = JsonDocument.Parse(data);
                    ModelStreamUpdate? update = state.Apply(document.RootElement, eventName, response.Headers);
                    if (update is not null)
                    {
                        linked.ThrowIfCancellationRequested();
                        try { await onUpdate(update, linked); }
                        catch { callbackFailed = true; throw; }
                    }
                    if (state.Terminal) { break; }
                    linked.ThrowIfCancellationRequested();
                }
            }
            ModelResponse report = state.Report();
            if (report.Status == ModelResponseStatus.Failed) { return ServiceResult<ModelResponse>.Ok(report); }
            if (caller.IsCancellationRequested) { return ServiceResult<ModelResponse>.Ok(state.Cancel()); }
            deadline.ThrowIfCancellationRequested();
            return ServiceResult<ModelResponse>.Ok(report);
        }
        catch (HttpRequestFailedException failure) when (!callbackFailed)
        {
            return ServiceResult<ModelResponse>.Fail(ResponseErrorReader.Read(failure));
        }
        catch (Exception failure) when (!callbackFailed && failure is JsonException or DecoderFallbackException)
        {
            ServiceError error = new(ServiceErrorType.Rejected, "Получен некорректный SSE-ответ модели.");
            return state.HasData ? ServiceResult<ModelResponse>.Ok(state.Fail(error)) : ServiceResult<ModelResponse>.Fail(error);
        }
        catch (OperationCanceledException) when (!callbackFailed && caller.IsCancellationRequested)
        {
            if (state.HasData) { return ServiceResult<ModelResponse>.Ok(state.Cancel()); }
            throw new OperationCanceledException(caller);
        }
        catch (OperationCanceledException) when (!callbackFailed && deadline.IsCancellationRequested)
        {
            ServiceError error = new(ServiceErrorType.Timeout, "Истёк срок ожидания модели.");
            return state.HasData ? ServiceResult<ModelResponse>.Ok(state.Fail(error)) : ServiceResult<ModelResponse>.Fail(error);
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request,
        ModelAccess access, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();
        string key = access.RevealApiKey();
        if (key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Validation, "Заданный ключ доступа имеет недопустимый формат."));
        }
        ServiceResult<ResponseRequestBody> body = CompactRequestWriter.Write(request);
        if (!body.Success) { return ServiceResult<ModelResponse>.Fail(body.Error!); }
        CodexLbOptions configuration = options.Value;
        string endpoint = (configuration.BaseAddress ?? throw new InvalidOperationException("Адрес codex-lb не настроен."))
            .TrimEnd('/') + "/v1/responses/compact";
        LibraryHttpRequestOptions transport = new()
        {
            Method = HttpMethod.Post, Url = endpoint,
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + key },
            Body = body.Data!.Content, CorrelationId = call.TurnId
        };
        using CancellationTokenSource deadline = new(configuration.CompactTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            HttpResponseResult<JsonElement> response = await http.SendWithResponseAsync<JsonElement>(transport, linked.Token);
            ModelResponse report = CompactJsonReader.Read(response.Body);
            if (report.Status == ModelResponseStatus.Failed) { return ServiceResult<ModelResponse>.Ok(report); }
            if (cancellationToken.IsCancellationRequested)
            {
                return ServiceResult<ModelResponse>.Ok(ModelResponse.Canceled(report.Output, report.Envelope));
            }
            deadline.Token.ThrowIfCancellationRequested();
            return ServiceResult<ModelResponse>.Ok(report);
        }
        catch (HttpRequestFailedException failure)
        {
            return ServiceResult<ModelResponse>.Fail(ResponseErrorReader.Read(failure));
        }
        catch (JsonException)
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Rejected, "Получен некорректный JSON-ответ compact."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Timeout, "Истёк срок ожидания compact."));
        }
    }
}
