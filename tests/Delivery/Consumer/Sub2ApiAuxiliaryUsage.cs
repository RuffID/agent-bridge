using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Пример optional usage и изображений sub2api; методы не исполняются при compile-check.</summary>
public static class Sub2ApiAuxiliaryUsage
{
    /// <summary>После app logging/provider подключает выбранный bundle и optional auxiliary без новых настроек.</summary>
    public static IServiceCollection Register(IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, HttpClient> httpClientFactory)
    {
        services.AddAgentBridgeSub2ApiConnector();
        services.AddAgentBridge(configuration, httpClientFactory);

        return services.AddAgentBridgeSub2ApiAuxiliary();
    }

    /// <summary>После авторизации приложения читает usage уже выбранного доступа в собственном scope.</summary>
    /// <remarks>selectedAccess получают через IModelAccessResolver; ошибку resolver возвращают без смены ключа.
    /// JSON содержит чувствительные данные sub2api, не предназначен для диагностического журнала и не доказывает quota ChatGPT.</remarks>
    public static async Task<ServiceResult<ModelAuxiliaryResult>> ReadUsageAsync(IServiceProvider root,
        ModelAccess selectedAccess, CancellationToken cancellationToken)
    {
        using IServiceScope scope = root.CreateScope();
        IModelAuxiliaryGateway gateway = scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>();

        return await gateway.ReadUsageAsync(selectedAccess, cancellationToken);
    }

    /// <summary>После авторизации приложения создаёт изображение с явно выбранными параметрами без повторов.</summary>
    public static async Task<ServiceResult<ModelAuxiliaryResult>> GenerateImageAsync(IServiceProvider root,
        ModelAccess selectedAccess, ModelImageRequest request, CancellationToken cancellationToken)
    {
        using IServiceScope scope = root.CreateScope();
        IModelAuxiliaryGateway gateway = scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>();

        return await gateway.GenerateImageAsync(request, selectedAccess, cancellationToken);
    }

    /// <summary>Отдельно от AgentRunner отправляет входные bytes через multipart без предварительного upload.</summary>
    /// <remarks>Результат содержит data[].b64_json либо data[].url; URL не скачивается автоматически.
    /// Приложение обрабатывает ServiceResult и не логирует prompt, файлы, JSON или URL.</remarks>
    public static async Task<ServiceResult<ModelAuxiliaryResult>> EditImageAsync(IServiceProvider root,
        ModelAccess selectedAccess, ModelImageEditRequest request, CancellationToken cancellationToken)
    {
        using IServiceScope scope = root.CreateScope();
        IModelAuxiliaryGateway gateway = scope.ServiceProvider.GetRequiredService<IModelAuxiliaryGateway>();

        return await gateway.EditImageAsync(request, selectedAccess, cancellationToken);
    }
}
