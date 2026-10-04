using System.Text.Json;
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
/// <remarks>Реализует только JSON генерацию этапа 14. Caller cancellation приоритетнее локального deadline.</remarks>
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
        if (onUpdate is not null) { return Unsupported(); }
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
        ServiceResult<ResponseRequestBody> body = ResponseRequestWriter.Write(request, continuation.Data!.PreviousId);
        if (!body.Success) { return ServiceResult<ModelResponse>.Fail(body.Error!); }
        Dictionary<string, string> headers = new() { ["Authorization"] = "Bearer " + key };
        if (continuation.Data.TurnState is not null) { headers["x-codex-turn-state"] = continuation.Data.TurnState; }
        LibraryHttpRequestOptions transport = new()
        {
            Method = HttpMethod.Post, Url = endpoint, Headers = headers, Body = body.Data!.Content, CorrelationId = call.TurnId
        };
        using CancellationTokenSource deadline = new(configuration.GenerationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
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

    /// <inheritdoc/>
    public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request,
        ModelAccess access, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Unsupported());
    }

    /// <summary>Отказывает в ещё не реализованном transport без вызова callback/HTTP либо скрытой подмены генерацией.</summary>
    private static ServiceResult<ModelResponse> Unsupported() => ServiceResult<ModelResponse>.Fail(
        new(ServiceErrorType.Unsupported, "Запрошенная операция не поддерживается JSON-адаптером."));
}
