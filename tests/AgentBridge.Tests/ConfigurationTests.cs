using AgentBridge.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Изолированные проверки конфигурации через публичную DI-границу ядра.</summary>
public class ConfigurationTests
{
    /// <summary>Пустой раздел ядра сохраняет все согласованные пробные лимиты.</summary>
    [Fact]
    public void EmptyCoreSectionUsesTrialDefaults()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();

        AgentOptions agent = provider.GetRequiredService<IOptions<AgentOptions>>().Value;
        DialogRetentionOptions retention = provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value;
        ContextCompactionOptions compaction = provider.GetRequiredService<IOptions<ContextCompactionOptions>>().Value;
        Assert.Equal(8, agent.MaxToolSteps);
        Assert.Equal(10_485_760, retention.SoftContentLimitBytes);
        Assert.Equal(32_000, compaction.TokenThreshold);
        Assert.Equal(4_096, compaction.InputTokenReserve);
        Assert.Equal(3, compaction.MaxPasses);
        DateTimeOffset created = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(created.AddDays(7), retention.CalculateExpiresAtUtc(created));
        Assert.Null(provider.GetService<IConfiguration>());
    }

    /// <summary>Binding и программный override меняют настройки и вычисляемый срок будущего диалога.</summary>
    [Fact]
    public void BindingAndApplicationOverrideDetermineFutureExpiration()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:Instructions"] = "Отвечай по данным приложения.",
            ["Agent:MaxToolSteps"] = "5",
            ["Retention:RetentionPeriod"] = "14.00:00:00",
            ["Retention:SoftContentLimitBytes"] = "123456",
            ["Compaction:TokenThreshold"] = "10000",
            ["Compaction:InputTokenReserve"] = "0",
            ["Compaction:MaxPasses"] = "2"
        }).Build();
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(configuration);
        services.Configure<ContextCompactionOptions>(options => options.TokenThreshold = 12_000);
        using ServiceProvider provider = services.BuildServiceProvider();
        DialogRetentionOptions retention = provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value;
        ContextCompactionOptions compaction = provider.GetRequiredService<IOptions<ContextCompactionOptions>>().Value;
        AgentOptions agent = provider.GetRequiredService<IOptions<AgentOptions>>().Value;

        DateTimeOffset created = new(2026, 10, 3, 7, 10, 0, TimeSpan.Zero);
        Assert.Equal(created.AddDays(14), retention.CalculateExpiresAtUtc(created));
        Assert.Equal(123_456, retention.SoftContentLimitBytes);
        Assert.Equal("Отвечай по данным приложения.", agent.Instructions);
        Assert.Equal(5, agent.MaxToolSteps);
        Assert.Equal(12_000, compaction.TokenThreshold);
        Assert.Equal(0, compaction.InputTokenReserve);
        Assert.Equal(2, compaction.MaxPasses);
    }

    /// <summary>Программная конфигурация работает без IConfiguration и хоста.</summary>
    [Fact]
    public void ProgrammaticConfigurationNeedsNoHost()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(
            agent => agent.MaxToolSteps = 1,
            retention => retention.RetentionPeriod = TimeSpan.FromHours(36),
            compaction => compaction.InputTokenReserve = 16);
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
        DateTimeOffset created = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(created.AddHours(36), provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value.CalculateExpiresAtUtc(created));
        Assert.Equal(1, provider.GetRequiredService<IOptions<AgentOptions>>().Value.MaxToolSteps);
        Assert.Equal(16, provider.GetRequiredService<IOptions<ContextCompactionOptions>>().Value.InputTokenReserve);
    }

    /// <summary>Изменение конфигурации обновляет monitor и новые scope, сохраняя уже вычисленную дату.</summary>
    [Fact]
    public void ReloadChangesFuturePeriodWithoutChangingExistingSnapshot()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Retention:RetentionPeriod"] = "14.00:00:00"
        }).Build();
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(configuration);
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using IServiceScope oldScope = provider.CreateScope();
        DialogRetentionOptions oldOptions = oldScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value;
        DateTimeOffset created = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset existingExpiration = oldOptions.CalculateExpiresAtUtc(created);
        IOptionsMonitor<DialogRetentionOptions> monitor = provider.GetRequiredService<IOptionsMonitor<DialogRetentionOptions>>();
        Assert.Equal(TimeSpan.FromDays(14), monitor.CurrentValue.RetentionPeriod);

        configuration["Retention:RetentionPeriod"] = "21.00:00:00";
        configuration.Reload();
        using IServiceScope newScope = provider.CreateScope();
        DialogRetentionOptions newOptions = newScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value;
        Assert.Equal(created.AddDays(21), newOptions.CalculateExpiresAtUtc(created));
        Assert.Equal(TimeSpan.FromDays(21), monitor.CurrentValue.RetentionPeriod);
        Assert.Equal(TimeSpan.FromDays(14), oldOptions.RetentionPeriod);
        Assert.Equal(created.AddDays(14), existingExpiration);
    }

    /// <summary>Недопустимые локальные диапазоны явно отклоняются при получении options.</summary>
    [Theory]
    [InlineData("Agent:MaxToolSteps", "0")]
    [InlineData("Agent:MaxToolSteps", "-1")]
    [InlineData("Retention:RetentionPeriod", "00:00:00")]
    [InlineData("Retention:RetentionPeriod", "-1.00:00:00")]
    [InlineData("Retention:SoftContentLimitBytes", "0")]
    [InlineData("Retention:SoftContentLimitBytes", "-1")]
    [InlineData("Compaction:TokenThreshold", "0")]
    [InlineData("Compaction:TokenThreshold", "-1")]
    [InlineData("Compaction:TokenThreshold", "2147483647")]
    [InlineData("Compaction:InputTokenReserve", "-1")]
    [InlineData("Compaction:MaxPasses", "0")]
    [InlineData("Compaction:MaxPasses", "-1")]
    public void InvalidRangeFailsAtPublicOptionsBoundary(string key, string value)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() =>
        {
            if (key.StartsWith("Agent:", StringComparison.Ordinal))
            {
                _ = provider.GetRequiredService<IOptions<AgentOptions>>().Value;
            }
            else if (key.StartsWith("Retention:", StringComparison.Ordinal))
            {
                _ = provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value;
            }
            else
            {
                _ = provider.GetRequiredService<IOptions<ContextCompactionOptions>>().Value;
            }
        });
        Assert.Contains(key.Replace(':', '.'), error.Message);
    }

    /// <summary>Проверка старта доступна приложению явно, без запуска hosting.</summary>
    [Fact]
    public void ExplicitStartupValidationRejectsInvalidProgrammaticOptions()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(_ => { }, retention => retention.RetentionPeriod = TimeSpan.Zero);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    /// <summary>Вычисление срока не маскирует неправильный UTC или переполнение даты.</summary>
    [Fact]
    public void ExpirationRejectsNonUtcAndUnrepresentableDate()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(_ => { });
        using ServiceProvider provider = services.BuildServiceProvider();
        DialogRetentionOptions options = provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value;
        Assert.Throws<ArgumentException>(() => options.CalculateExpiresAtUtc(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.FromHours(7))));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.CalculateExpiresAtUtc(DateTimeOffset.MaxValue));
    }
}
