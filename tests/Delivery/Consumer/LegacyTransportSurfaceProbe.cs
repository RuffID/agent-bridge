using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Auxiliary;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.CodexLb.Models;
using AgentBridge.CodexLb.Responses;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using HttpClientLibrary.Clients;
using HttpClientLibrary.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.BinaryConsumer;

/// <summary>Compile-only ссылки на прежние конструкторы и прямое подключение codex-lb; методы не исполняются.</summary>
public static class LegacyTransportSurfaceProbe
{
    /// <summary>Проверяет обе перегрузки options и отдельные регистрации, включая optional auxiliary API.</summary>
    public static IServiceCollection RegisterDirect(IServiceCollection services, IConfiguration section,
        Action<CodexLbOptions> configure, Func<IServiceProvider, HttpClient> factory,
        HttpClientLoggingOptions loggingOptions, IConfiguration auxiliarySection)
    {
        services.AddCodexLbConfiguration(section);
        services.AddCodexLbConfiguration(configure);
        services.AddCodexLbModelCatalog(factory);
        services.AddCodexLbModelCatalog(factory, loggingOptions);
        services.AddCodexLbResponses(factory);
        services.AddCodexLbResponses(factory, loggingOptions);

        return services.AddCodexLbAuxiliary(auxiliarySection);
    }

    /// <summary>Фиксирует публичные конструкторы; создание этих объектов не выполняется при compile-check.</summary>
    public static object[] Constructors(HttpApiClient http, IOptionsSnapshot<CodexLbOptions> options,
        IOptionsSnapshot<CodexLbAuxiliaryOptions> auxiliary, IIndividualModelKeySource keys,
        IModelAccessResolver resolver, IModelCatalog catalog, IOptionsSnapshot<ContextCompactionOptions> context)
    {
        return
        [
            new CodexLbOptions(),
            new CodexLbAuxiliaryOptions(),
            new CodexLbModelAccessResolver(keys, options),
            new CodexLbModelCatalog(http, options),
            new CodexLbModelSettingsReader(resolver, catalog, options, context),
            new CodexLbModelGateway(http, options),
            new CodexLbAuxiliaryGateway(http, options, auxiliary)
        ];
    }

    /// <summary>Сохраняет статические ссылки на методы JSON/SSE, compact и settings с прежними optional аргументами.</summary>
    public static object[] Methods(CodexLbModelGateway gateway, CodexLbModelAccessResolver resolver,
        CodexLbModelCatalog catalog, CodexLbModelSettingsReader settings, ApplicationCallContext call,
        ModelRequest request, ModelAccess access, DialogOwnerId owner,
        Func<ModelStreamUpdate, CancellationToken, ValueTask> callback, CancellationToken cancellationToken)
    {
        Task<ServiceResult<ModelResponse>> json = gateway.GenerateAsync(call, request, access);
        Task<ServiceResult<ModelResponse>> stream = gateway.GenerateAsync(call, request, access, callback, cancellationToken);
        Task<ServiceResult<ModelResponse>> compact = gateway.CompactAsync(call, request, access, cancellationToken);
        Task<ServiceResult<ModelAccess>> key = resolver.ResolveAsync(owner, cancellationToken);
        Task<ServiceResult<ModelCatalogSnapshot>> models = catalog.ReadAsync(access, cancellationToken);
        Task<ServiceResult<ModelSettingsSnapshot>> selection = settings.ReadAsync(owner);
        Task<ServiceResult<ModelSettingsSnapshot>> pinned = settings.ReadWithAccessAsync(owner, access);

        return [json, stream, compact, key, models, selection, pinned];
    }

    /// <summary>Фиксирует методы auxiliary и доступные только для чтения безопасные поля ошибки.</summary>
    public static object?[] Auxiliary(CodexLbAuxiliaryGateway gateway, ModelAccess access, ModelFileUpload file,
        ModelImageRequest image, ModelImageEditRequest edit, CodexLbServiceError error, CancellationToken cancellationToken)
    {
        return
        [
            gateway.ReadUsageAsync(access, cancellationToken),
            gateway.UploadFileAsync(file, access, cancellationToken),
            gateway.GenerateImageAsync(image, access, cancellationToken),
            gateway.EditImageAsync(edit, access, cancellationToken),
            error.HttpStatus,
            error.ApiType,
            error.Code,
            error.Param
        ];
    }
}
