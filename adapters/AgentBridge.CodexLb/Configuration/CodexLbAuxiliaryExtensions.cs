using AgentBridge.Application.Ports;
using AgentBridge.Configuration;
using AgentBridge.CodexLb.Auxiliary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.CodexLb.Configuration;

/// <summary>Явное optional подключение дополнительных API после AddAgentBridge/AddCodexLbResponses.</summary>
public static class CodexLbAuxiliaryExtensions
{
    /// <summary>Привязывает обязательные сроки из секции CodexLb:Auxiliary, без сети при регистрации.</summary>
    public static IServiceCollection AddCodexLbAuxiliary(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<CodexLbAuxiliaryOptions>().BindSafely(configuration, "CodexLb.Auxiliary",
                nameof(CodexLbAuxiliaryOptions.MetadataTimeout), nameof(CodexLbAuxiliaryOptions.FileCreateTimeout),
                nameof(CodexLbAuxiliaryOptions.FileUploadTimeout), nameof(CodexLbAuxiliaryOptions.FileFinalizeTimeout),
                nameof(CodexLbAuxiliaryOptions.ImageTimeout))
            .Validate(options => Valid(options.MetadataTimeout) && Valid(options.FileCreateTimeout)
                && Valid(options.FileUploadTimeout) && Valid(options.FileFinalizeTimeout) && Valid(options.ImageTimeout),
                "CodexLb.Auxiliary: сроки должны быть положительными и конечными.")
            .ValidateOnStart();
        services.AddScoped<IModelAuxiliaryGateway, CodexLbAuxiliaryGateway>();

        return services;
    }

    /// <summary>Проверяет диапазон CancellationTokenSource без переполнения.</summary>
    private static bool Valid(TimeSpan value) => value > TimeSpan.Zero && value.TotalMilliseconds <= uint.MaxValue - 1;
}

