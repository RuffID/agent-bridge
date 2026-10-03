using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.Configuration;

/// <summary>Групповая регистрация настроек ядра в composition root приложения.</summary>
public static class AgentBridgeConfigurationExtensions
{
    /// <summary>Привязывает группы Agent, Retention и Compaction из переданного раздела.</summary>
    /// <remarks>Валидация выполняется при получении options и при проверке старта приложением; IConfiguration не регистрируется.</remarks>
    public static IServiceCollection AddAgentBridgeConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        ValidateAgent(services.AddOptions<AgentOptions>().Bind(configuration.GetSection("Agent")));
        ValidateRetention(services.AddOptions<DialogRetentionOptions>().Bind(configuration.GetSection("Retention")));
        ValidateCompaction(services.AddOptions<ContextCompactionOptions>().Bind(configuration.GetSection("Compaction")));
        return services;
    }

    /// <summary>Настраивает группы ядра программно без файла конфигурации и без хоста.</summary>
    public static IServiceCollection AddAgentBridgeConfiguration(
        this IServiceCollection services,
        Action<AgentOptions> configureAgent,
        Action<DialogRetentionOptions>? configureRetention = null,
        Action<ContextCompactionOptions>? configureCompaction = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureAgent);

        ValidateAgent(services.AddOptions<AgentOptions>().Configure(configureAgent));
        OptionsBuilder<DialogRetentionOptions> retention = services.AddOptions<DialogRetentionOptions>();
        OptionsBuilder<ContextCompactionOptions> compaction = services.AddOptions<ContextCompactionOptions>();
        if (configureRetention is not null)
        {
            retention.Configure(configureRetention);
        }

        if (configureCompaction is not null)
        {
            compaction.Configure(configureCompaction);
        }

        ValidateRetention(retention);
        ValidateCompaction(compaction);
        return services;
    }

    /// <summary>Регистрирует проверку локальных лимитов агента.</summary>
    private static void ValidateAgent(OptionsBuilder<AgentOptions> builder) => builder
        .Validate(options => options.MaxToolSteps > 0, "Agent.MaxToolSteps должен быть положительным.")
        .ValidateOnStart();

    /// <summary>Регистрирует проверку периода хранения и мягкого порога содержимого.</summary>
    private static void ValidateRetention(OptionsBuilder<DialogRetentionOptions> builder) => builder
        .Validate(options => options.RetentionPeriod > TimeSpan.Zero, "Retention.RetentionPeriod должен быть положительным.")
        .Validate(options => options.SoftContentLimitBytes > 0, "Retention.SoftContentLimitBytes должен быть положительным.")
        .ValidateOnStart();

    /// <summary>Регистрирует проверку локальных лимитов сжатия, без обещания модельного бюджета.</summary>
    private static void ValidateCompaction(OptionsBuilder<ContextCompactionOptions> builder) => builder
        .Validate(options => options.TokenThreshold > 0, "Compaction.TokenThreshold должен быть положительным.")
        .Validate(options => options.InputTokenReserve >= 0, "Compaction.InputTokenReserve не может быть отрицательным.")
        .Validate(options => (long)options.TokenThreshold + options.InputTokenReserve <= int.MaxValue,
            "Сумма Compaction.TokenThreshold и InputTokenReserve выходит за локальный диапазон числа токенов.")
        .Validate(options => options.MaxPasses > 0, "Compaction.MaxPasses должен быть положительным.")
        .ValidateOnStart();
}
