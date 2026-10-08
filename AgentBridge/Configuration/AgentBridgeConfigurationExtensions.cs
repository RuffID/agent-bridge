using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.TryAddScoped(provider => new DialogRetentionPolicy(provider.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value));

        ValidateAgent(services.AddOptions<AgentOptions>().BindSafely(configuration.GetSection("Agent"), "Agent",
            nameof(AgentOptions.MaxToolSteps), nameof(AgentOptions.InstructionsSource)).Configure(options =>
            {
                if (options.InstructionsSource == AgentInstructionsSource.Configuration)
                    SafeOptionsBindingExtensions.RequireValues<AgentOptions>(configuration.GetSection("Agent"), "Agent", Options.DefaultName, nameof(AgentOptions.Instructions));
            }));
        ValidateRetention(services.AddOptions<DialogRetentionOptions>().BindSafely(configuration.GetSection("Retention"), "Retention",
            nameof(DialogRetentionOptions.SoftContentLimitBytes)));
        ValidateCompaction(services.AddOptions<ContextCompactionOptions>().BindSafely(configuration.GetSection("Compaction"), "Compaction",
            nameof(ContextCompactionOptions.TokenThreshold), nameof(ContextCompactionOptions.InputTokenReserve), nameof(ContextCompactionOptions.MaxPasses)));
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
        services.TryAddScoped(provider => new DialogRetentionPolicy(provider.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value));

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
        .Validate(options => options.InstructionsSource.HasValue && Enum.IsDefined(options.InstructionsSource.Value),
            "Agent.InstructionsSource: required_or_invalid — обязателен явный режим Configuration/PerRequest.")
        .Validate(options => options.InstructionsSource != AgentInstructionsSource.Configuration || !string.IsNullOrWhiteSpace(options.Instructions),
            "Agent.Instructions: required — обязательны непустые инструкции в режиме Configuration.")
        .Validate(options => options.MaxToolSteps > 0, "Agent.MaxToolSteps должен быть положительным; required_or_range.")
        .ValidateOnStart();

    /// <summary>Регистрирует проверку периода хранения и мягкого порога содержимого.</summary>
    private static void ValidateRetention(OptionsBuilder<DialogRetentionOptions> builder) => builder
        .Validate(options => options.RetentionPeriod is null || options.RetentionPeriod > TimeSpan.Zero,
            "Retention.RetentionPeriod должен быть null или положительным; invalid_range.")
        .Validate(options => options.SoftContentLimitBytes > 0, "Retention.SoftContentLimitBytes должен быть положительным; required_or_range.")
        .ValidateOnStart();

    /// <summary>Регистрирует проверку локальных лимитов сжатия, без обещания модельного бюджета.</summary>
    private static void ValidateCompaction(OptionsBuilder<ContextCompactionOptions> builder) => builder
        .Validate(options => Enum.IsDefined(options.BudgetPolicy), "Compaction.BudgetPolicy: invalid.")
        .Validate(options => options.TokenThreshold > 0, "Compaction.TokenThreshold должен быть положительным; required_or_range.")
        .Validate(options => options.InputTokenReserve >= 0, "Compaction.InputTokenReserve не может быть отрицательным; required_or_range.")
        .Validate(options => (long)options.TokenThreshold + options.InputTokenReserve <= int.MaxValue,
            "Сумма Compaction.TokenThreshold и InputTokenReserve выходит за локальный диапазон числа токенов; overflow.")
        .Validate(options => options.MaxPasses > 0, "Compaction.MaxPasses должен быть положительным; required_or_range.")
        .ValidateOnStart();
}
