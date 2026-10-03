using System.Net;
using System.Text.Json;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Exceptions;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Options;
using LibraryHttpRequestOptions = HttpClientLibrary.Models.HttpRequestOptions;

namespace AgentBridge.CodexLb.Models;

/// <inheritdoc/>
public class CodexLbModelCatalog(HttpApiClient http, IOptionsSnapshot<CodexLbOptions> options) : IModelCatalog
{
    /// <inheritdoc/>
    public async Task<ServiceResult<ModelCatalogSnapshot>> ReadAsync(ModelAccess access, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ct.ThrowIfCancellationRequested();
        string baseAddress = options.Value.BaseAddress
            ?? throw new InvalidOperationException("Адрес codex-lb не настроен.");
        LibraryHttpRequestOptions request = new()
        {
            Method = HttpMethod.Get,
            Url = baseAddress.TrimEnd('/') + "/v1/models",
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + access.RevealApiKey() }
        };
        try
        {
            HttpResponseResult<JsonElement> response = await http.SendWithResponseAsync<JsonElement>(request, ct);
            ct.ThrowIfCancellationRequested();
            return ServiceResult<ModelCatalogSnapshot>.Ok(ModelCatalogJsonReader.Read(response.Body));
        }
        catch (HttpRequestFailedException failure)
        {
            // Raw details/preview не разбираются: даже Complete не гарантирует валидный envelope.
            ServiceErrorType type = failure.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ServiceErrorType.Unauthorized,
                HttpStatusCode.Forbidden => ServiceErrorType.Forbidden,
                _ => ServiceErrorType.Rejected
            };
            return ServiceResult<ModelCatalogSnapshot>.Fail(new(type, "Сервер отклонил чтение каталога моделей."));
        }
        catch (JsonException)
        {
            return ServiceResult<ModelCatalogSnapshot>.Fail(new(ServiceErrorType.Rejected, "Получен некорректный каталог моделей."));
        }
    }
}
