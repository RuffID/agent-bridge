using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Responses;
using AgentBridge.Sub2Api.Configuration;
using AgentBridge.Sub2Api.Responses;
using HttpClientLibrary.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.Sub2Api;

/// <inheritdoc/>
/// <remarks>Canonical гарантия относится к границе sub2api; обработка сервера не считается upstream evidence.</remarks>
internal class Sub2ApiModelGateway(Func<HttpApiClient> httpFactory, IOptionsSnapshot<Sub2ApiOptions> options,
    ILogger<Sub2ApiModelGateway> logger) : IModelGateway
{
    /// <inheritdoc/>
    public bool AutomaticCompactionEnabled => false;

    /// <inheritdoc/>
    public async Task<ServiceResult<ModelResponse>> GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Continuation is not null)
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Unsupported,
                "Продолжение sub2api требует полного рабочего контекста без transport continuation."));
        }

        string key = access.RevealApiKey();
        if (!Sub2ApiCredential.IsValid(key))
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Validation,
                "Заданный ключ доступа имеет недопустимый формат."));
        }

        Sub2ApiOptions configured = options.Value;
        if (!Sub2ApiEndpoints.TryGetModelsUrl(configured.BaseAddress, out string modelsUrl))
        {
            return ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Validation, "Sub2Api.BaseAddress: required_or_invalid."));
        }

        ServiceResult<ResponseRequestBody> body = ResponseRequestWriter.Write(request, null, onUpdate is not null);
        if (!body.Success)
        {
            return ServiceResult<ModelResponse>.Fail(body.Error!);
        }

        LibraryHttpRequestOptions transport = new()
        {
            Method = HttpMethod.Post,
            Url = modelsUrl[..^"models".Length] + "responses",
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + key },
            Body = body.Data!.Content,
            CorrelationId = call.TurnId
        };
        using CancellationTokenSource deadline = new(configured.GenerationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        cancellationToken.ThrowIfCancellationRequested();
        Sub2ApiResponseTransport sender = new(httpFactory());
        try
        {
            ServiceResult<ModelResponse> result = await sender.SendAsync(transport, onUpdate, cancellationToken, deadline.Token, linked.Token);
            string outcome = "NoReport";
            ServiceErrorType? errorType = result.Error?.Type;
            if (result.Success)
            {
                outcome = result.Data!.Status.ToString();
                errorType = result.Data.Error?.Type;
            }

            LogOutcome(call.TurnId, outcome, errorType);
            return result;
        }
        catch
        {
            LogOutcome(call.TurnId, "Exception", null);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<ServiceResult<ModelResponse>> CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(access);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ServiceResult<ModelResponse>.Fail(new(ServiceErrorType.Unsupported,
            "Сжатие sub2api отключено: использование сжатого окна следующей генерацией не подтверждено; сервер может удалить непрочитанный compact. Исходный контекст не изменён.")));
    }

    /// <summary>Итог относится к lifecycle ответа, не upstream identity; logger не меняет результат/primary.</summary>
    private void LogOutcome(Guid turnId, string outcome, ServiceErrorType? errorType)
    {
        try
        {
            logger.LogInformation(new EventId(5200, "Sub2ApiGenerationFinished"),
                "Sub2Api generation finished; CorrelationId={CorrelationId}; Outcome={Outcome}; ErrorType={ErrorType}",
                turnId, outcome, errorType);
        }
        catch (Exception)
        {
            // Диагностика не подменяет результат запроса и не создаёт повторную попытку.
        }
    }
}
