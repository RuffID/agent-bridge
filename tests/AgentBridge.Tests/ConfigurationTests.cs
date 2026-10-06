using AgentBridge.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Изолированные проверки конфигурации через публичную DI-границу ядра.</summary>
public class ConfigurationTests
{
    /// <summary>Пустой раздел отклоняется вместо подстановки прежних пробных лимитов.</summary>
    [Fact]
    public void EmptyCoreSectionRejectsMissingLimits()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<AggregateException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Null(provider.GetService<IConfiguration>());
    }

    /// <summary>Binding и программный override меняют настройки и вычисляемый срок будущего диалога.</summary>
    [Fact]
    public void BindingAndApplicationOverrideDetermineFutureExpiration()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:Instructions"] = "Отвечай по данным приложения.",
            ["Agent:InstructionsSource"] = "Configuration",
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
            agent => { agent.MaxToolSteps = 1; agent.InstructionsSource = AgentInstructionsSource.PerRequest; },
            retention => { retention.RetentionPeriod = TimeSpan.FromHours(36); retention.SoftContentLimitBytes = 100; },
            compaction => { compaction.TokenThreshold = 100; compaction.MaxPasses = 1; compaction.InputTokenReserve = 16; });
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
            ["Retention:RetentionPeriod"] = "14.00:00:00", ["Retention:SoftContentLimitBytes"] = "100"
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
        IConfigurationRoot configuration = ValidConfiguration(); configuration[key] = value;
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
        Assert.Throws<AggregateException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    /// <summary>Вычисление срока не маскирует неправильный UTC или переполнение даты.</summary>
    [Fact]
    public void ExpirationRejectsNonUtcAndUnrepresentableDate()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(ValidConfiguration());
        using ServiceProvider provider = services.BuildServiceProvider();
        DialogRetentionOptions options = provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value;
        Assert.Throws<ArgumentException>(() => options.CalculateExpiresAtUtc(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.FromHours(7))));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.CalculateExpiresAtUtc(DateTimeOffset.MaxValue));
    }

    /// <summary>Отсутствие каждого поля отличается от его явного допустимого значения, включая старый default и ноль.</summary>
    [Theory]
    [InlineData("Agent:MaxToolSteps")]
    [InlineData("Agent:InstructionsSource")]
    [InlineData("Retention:RetentionPeriod")]
    [InlineData("Retention:SoftContentLimitBytes")]
    [InlineData("Compaction:TokenThreshold")]
    [InlineData("Compaction:InputTokenReserve")]
    [InlineData("Compaction:MaxPasses")]
    public void EachMissingFieldFailsWithSafePath(string key)
    {
        IConfigurationRoot config = ValidConfiguration();
        foreach (string? value in new string?[] { null, "", " " })
        {
            config[key] = value;
            ServiceCollection services = new(); services.AddAgentBridgeConfiguration(config);
            using ServiceProvider provider = services.BuildServiceProvider();
            OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
            Assert.Contains(key.Replace(':', '.'), error.ToString());
        }
    }

    /// <summary>Malformed, overflow и неизвестный режим не раскрывают исходное значение или inner exception.</summary>
    [Theory]
    [InlineData("Agent:MaxToolSteps", "synthetic-secret")]
    [InlineData("Retention:RetentionPeriod", "synthetic-secret")]
    [InlineData("Retention:SoftContentLimitBytes", "9223372036854775808")]
    [InlineData("Compaction:TokenThreshold", "2147483648")]
    [InlineData("Compaction:InputTokenReserve", "synthetic-secret")]
    [InlineData("Compaction:MaxPasses", "synthetic-secret")]
    [InlineData("Agent:InstructionsSource", "synthetic-secret")]
    [InlineData("Agent:InstructionsSource", "99")]
    public void MalformedBindingIsSafe(string key, string value)
    {
        IConfigurationRoot config = ValidConfiguration(); config[key] = value;
        ServiceCollection services = new(); services.AddAgentBridgeConfiguration(config);
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(key.Replace(':', '.'), error.ToString());
        Assert.DoesNotContain(value, error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>Явно выбранный provider precedence и PostConfigure проходят тот же стандартный pipeline.</summary>
    [Fact]
    public void MergedSectionAndExplicitOldDefaultsWork()
    {
        IConfigurationRoot valid = ValidConfiguration();
        IConfigurationRoot merged = new ConfigurationBuilder().AddInMemoryCollection(valid.AsEnumerable().Select(pair =>
            new KeyValuePair<string, string?>("Library:" + pair.Key, pair.Value)))
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Library:Compaction:InputTokenReserve"] = "0" }).Build();
        ServiceCollection services = new(); services.AddAgentBridgeConfiguration(merged.GetSection("Library"));
        services.PostConfigure<AgentOptions>(options => options.MaxToolSteps = 9);
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(9, provider.GetRequiredService<IOptions<AgentOptions>>().Value.MaxToolSteps);
        Assert.Equal(0, provider.GetRequiredService<IOptions<ContextCompactionOptions>>().Value.InputTokenReserve);
        Assert.Equal(7, provider.GetRequiredService<IOptions<DialogRetentionOptions>>().Value.RetentionPeriod.TotalDays);
        using IServiceScope scope = provider.CreateScope();
        Assert.Equal(0, scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ContextCompactionOptions>>().Value.InputTokenReserve);
        Assert.Equal(0, provider.GetRequiredService<IOptionsMonitor<ContextCompactionOptions>>().CurrentValue.InputTokenReserve);
    }

    /// <summary>Программное отсутствие каждого поля отклоняется той же validation boundary.</summary>
    [Theory]
    [InlineData("Agent.MaxToolSteps")]
    [InlineData("Agent.InstructionsSource")]
    [InlineData("Retention.RetentionPeriod")]
    [InlineData("Retention.SoftContentLimitBytes")]
    [InlineData("Compaction.TokenThreshold")]
    [InlineData("Compaction.InputTokenReserve")]
    [InlineData("Compaction.MaxPasses")]
    public void EachProgrammaticOmissionFails(string missing)
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(agent =>
        {
            if (missing != "Agent.MaxToolSteps") agent.MaxToolSteps = 8;
            if (missing != "Agent.InstructionsSource") agent.InstructionsSource = AgentInstructionsSource.PerRequest;
        }, retention =>
        {
            if (missing != "Retention.RetentionPeriod") retention.RetentionPeriod = TimeSpan.FromDays(7);
            if (missing != "Retention.SoftContentLimitBytes") retention.SoftContentLimitBytes = 10_485_760;
        }, compaction =>
        {
            if (missing != "Compaction.TokenThreshold") compaction.TokenThreshold = 32_000;
            if (missing != "Compaction.InputTokenReserve") compaction.InputTokenReserve = 0;
            if (missing != "Compaction.MaxPasses") compaction.MaxPasses = 3;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains(missing, Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate()).Message);
    }

    /// <summary>Instructions условно обязательны; режим Configuration отвергает null/blank без fallback.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConfigurationInstructionsMustBeExplicit(string? instructions)
    {
        IConfigurationRoot config = ValidConfiguration(); config["Agent:InstructionsSource"] = "Configuration"; config["Agent:Instructions"] = instructions;
        ServiceCollection services = new(); services.AddAgentBridgeConfiguration(config);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains("Agent.Instructions", Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate()).Message);
    }

    /// <summary>Предшествующий Configure не скрывает missing выбранного configuration key/section; валидные overrides разрешены отдельно.</summary>
    [Theory]
    [InlineData("Agent:MaxToolSteps")]
    [InlineData("Agent:InstructionsSource")]
    [InlineData("Agent:Instructions")]
    [InlineData("Retention:RetentionPeriod")]
    [InlineData("Retention:SoftContentLimitBytes")]
    [InlineData("Compaction:TokenThreshold")]
    [InlineData("Compaction:InputTokenReserve")]
    [InlineData("Compaction:MaxPasses")]
    [InlineData("Retention")]
    [InlineData("Agent")]
    [InlineData("Compaction")]
    public void PriorConfigureCannotHideMissingConfiguration(string key)
    {
        IConfigurationRoot config = ValidConfiguration(); config["Agent:InstructionsSource"] = "Configuration"; config["Agent:Instructions"] = "configured";
        if (!key.Contains(':'))
        {
            foreach (KeyValuePair<string, string?> pair in config.GetSection(key).AsEnumerable().ToArray()) config[pair.Key] = null;
        }
        else config[key] = null;
        ServiceCollection services = new();
        services.Configure<AgentOptions>(options => { options.MaxToolSteps = 8; options.InstructionsSource = AgentInstructionsSource.Configuration; options.Instructions = "prior instructions"; });
        services.Configure<DialogRetentionOptions>(options => { options.RetentionPeriod = TimeSpan.FromDays(7); options.SoftContentLimitBytes = 10_485_760; });
        services.Configure<ContextCompactionOptions>(options => { options.TokenThreshold = 32_000; options.InputTokenReserve = 4_096; options.MaxPasses = 3; });
        services.AddAgentBridgeConfiguration(config);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains(key.Replace(':', '.'), Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate()).Message);
    }

    /// <summary>Создаёт явную полную конфигурацию, в том числе значения прежних defaults.</summary>
    private static IConfigurationRoot ValidConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Agent:InstructionsSource"] = "PerRequest", ["Agent:MaxToolSteps"] = "8",
        ["Retention:RetentionPeriod"] = "7.00:00:00", ["Retention:SoftContentLimitBytes"] = "10485760",
        ["Compaction:TokenThreshold"] = "32000", ["Compaction:InputTokenReserve"] = "4096", ["Compaction:MaxPasses"] = "3"
    }).Build();
}
