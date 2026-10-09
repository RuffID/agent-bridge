using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Compile-only пример полной явной конфигурации; методы не запускают host, HTTP или БД.</summary>
public static class StrictConfigurationRegistration
{
    /// <summary>Добавляет точный ID с заранее подтверждённой приложением встроенной кодировкой без изменения DLL.</summary>
    /// <param name="services">Контейнер приложения.</param>
    /// <param name="model">Точный ID доступной модели; live каталог проверяется при выборе/обращении.</param>
    /// <param name="confirmedEncoding">Подтверждённая приложением o200k_base либо cl100k_base.</param>
    public static IServiceCollection AddConfirmedModelEncoding(this IServiceCollection services,
        string model, string confirmedEncoding) =>
        services.AddAgentBridgeTokenization(options => options.ModelEncodings.Add(model, confirmedEncoding));

    /// <summary>Регистрирует явно выбранные лимиты и shared mode; секреты предоставляет приложение.</summary>
    public static IServiceCollection AddExplicitConfiguration(this IServiceCollection services,
        string serverAddress, string model, string effort, string sharedKey, string connectionString)
    {
        services.AddAgentBridgeConfiguration(
            agent => { agent.InstructionsSource = AgentInstructionsSource.PerRequest; agent.MaxToolSteps = 5; },
            retention => { retention.RetentionPeriod = TimeSpan.FromDays(14); retention.SoftContentLimitBytes = 10_485_760; },
            compaction => { compaction.TokenThreshold = 24_000; compaction.InputTokenReserve = 0; compaction.MaxPasses = 3; });
        services.AddCodexLbConfiguration(options =>
        {
            options.BaseAddress = serverAddress; options.Model = model; options.ReasoningEffort = effort;
            options.KeySource = ModelKeySourceMode.Shared; options.SharedApiKey = sharedKey;
            options.GenerationTimeout = TimeSpan.FromSeconds(180); options.CompactTimeout = TimeSpan.FromSeconds(180);
        });
        return services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.SqlServer; options.ConnectionString = connectionString;
        });
    }
}
