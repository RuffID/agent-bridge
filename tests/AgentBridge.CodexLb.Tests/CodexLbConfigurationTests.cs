using AgentBridge.CodexLb.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.CodexLb.Tests;

/// <summary>Локальные настройки транспорта через DI, без HTTP и каталога сервера.</summary>
public class CodexLbConfigurationTests
{
    /// <summary>Явные значения прежних defaults принимаются без обещания поддержки модели сервером.</summary>
    [Fact]
    public void ExplicitConfigurationPreservesModelWithoutSharedKey()
    {
        ServiceCollection services = new();
        services.AddCodexLbConfiguration(BuildConfiguration());
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
        CodexLbOptions options = provider.GetRequiredService<IOptions<CodexLbOptions>>().Value;
        Assert.Equal("application-selected-model", options.Model);
        Assert.Equal("medium", options.ReasoningEffort);
        Assert.Equal(TimeSpan.FromSeconds(180), options.GenerationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(180), options.CompactTimeout);
        Assert.Null(options.SharedApiKey);
        Assert.Null(provider.GetService<IConfiguration>());
    }

    /// <summary>Приложение может задать все значения без файла настроек; локальный валидатор не имитирует каталог.</summary>
    [Fact]
    public void ProgrammaticOverridesDoNotPretendToValidateServerCapabilities()
    {
        ServiceCollection services = new();
        services.AddCodexLbConfiguration(options =>
        {
            options.BaseAddress = "https://gateway.example/proxy/";
            options.Model = "future-catalog-model";
            options.ReasoningEffort = "future-catalog-effort";
            options.SharedApiKey = "synthetic-secret";
            options.KeySource = ModelKeySourceMode.Shared;
            options.GenerationTimeout = TimeSpan.FromSeconds(42);
            options.CompactTimeout = TimeSpan.FromSeconds(24);
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        CodexLbOptions options = provider.GetRequiredService<IOptions<CodexLbOptions>>().Value;
        Assert.Equal("https://gateway.example/proxy/", options.BaseAddress);
        Assert.Equal("future-catalog-model", options.Model);
        Assert.Equal("future-catalog-effort", options.ReasoningEffort);
        Assert.Equal(TimeSpan.FromSeconds(42), options.GenerationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(24), options.CompactTimeout);
        Assert.Equal("synthetic-secret", options.SharedApiKey);
    }

    /// <summary>Обязательные значения, адреса и конечные deadline проверяются без отправки запросов.</summary>
    [Theory]
    [InlineData("BaseAddress", null, "обязателен")]
    [InlineData("BaseAddress", " ", "обязателен")]
    [InlineData("BaseAddress", "/relative", "HTTP(S)")]
    [InlineData("BaseAddress", "ftp://gateway.example", "HTTP(S)")]
    [InlineData("BaseAddress", "https://user:synthetic-secret@gateway.example", "HTTP(S)")]
    [InlineData("BaseAddress", "https://gateway.example?key=synthetic-secret", "HTTP(S)")]
    [InlineData("BaseAddress", "https://gateway.example#synthetic-secret", "HTTP(S)")]
    [InlineData("Model", null, "обязателен")]
    [InlineData("Model", " ", "обязателен")]
    [InlineData("ReasoningEffort", " ", "required")]
    [InlineData("SharedApiKey", " ", "непустой")]
    [InlineData("GenerationTimeout", "00:00:00", "таймера")]
    [InlineData("GenerationTimeout", "-00:00:01", "таймера")]
    [InlineData("GenerationTimeout", "50.00:00:00", "таймера")]
    [InlineData("CompactTimeout", "00:00:00", "таймера")]
    [InlineData("CompactTimeout", "-00:00:01", "таймера")]
    [InlineData("CompactTimeout", "50.00:00:00", "таймера")]
    public void InvalidLocalOptionsFailWithoutRevealingValues(string key, string? value, string expected)
    {
        IConfigurationRoot configuration = BuildConfiguration();
        configuration[key] = value;
        configuration["SharedApiKey"] = key == "SharedApiKey" ? value : "synthetic-secret";
        ServiceCollection services = new();
        services.AddCodexLbConfiguration(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CodexLbOptions>>().Value);
        Assert.Contains("CodexLb." + key, error.Message);
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain("synthetic-secret", error.ToString());
    }

    /// <summary>Отсутствие всего раздела не создаёт фиктивных адреса и модели.</summary>
    [Fact]
    public void MissingSectionReportsBothRequiredValues()
    {
        ServiceCollection services = new();
        services.AddCodexLbConfiguration(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("CodexLb.BaseAddress обязателен", error.Message);
        Assert.Contains("CodexLb.Model обязателен", error.Message);
    }

    /// <summary>Каждый обязательный скаляр отвергает отсутствие и пустую строку независимо от старого default.</summary>
    [Theory]
    [InlineData("BaseAddress")]
    [InlineData("Model")]
    [InlineData("ReasoningEffort")]
    [InlineData("KeySource")]
    [InlineData("GenerationTimeout")]
    [InlineData("CompactTimeout")]
    public void RequiredFieldsCannotUseDefaults(string field)
    {
        foreach (string? value in new string?[] { null, "", " " })
        {
            IConfigurationRoot config = BuildConfiguration(); config[field] = value;
            ServiceCollection services = new(); services.AddCodexLbConfiguration(config);
            using ServiceProvider provider = services.BuildServiceProvider();
            OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
            Assert.Contains("CodexLb." + field, error.ToString());
        }
    }

    /// <summary>Malformed и overflow преобразуются в безопасные исключения без исходного значения.</summary>
    [Theory]
    [InlineData("KeySource", "synthetic-secret")]
    [InlineData("KeySource", "99")]
    [InlineData("GenerationTimeout", "synthetic-secret")]
    [InlineData("CompactTimeout", "999999999999999999999999")]
    public void BinderErrorsNeverExposeRawValues(string field, string value)
    {
        IConfigurationRoot config = BuildConfiguration(); config[field] = value;
        ServiceCollection services = new(); services.AddCodexLbConfiguration(config);
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CodexLbOptions>>().Value);
        Assert.Contains("CodexLb." + field, error.ToString()); Assert.DoesNotContain(value, error.ToString()); Assert.Null(error.InnerException);
    }

    /// <summary>Shared требует ключ, Individual может не иметь его; пустой callback не получает лимиты.</summary>
    [Fact]
    public void ConditionalSharedKeyAndEmptyCallbackAreExplicit()
    {
        IConfigurationRoot config = BuildConfiguration(); config["KeySource"] = "Shared";
        ServiceCollection services = new(); services.AddCodexLbConfiguration(config);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains("CodexLb.SharedApiKey", Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate()).Message);
        ServiceCollection empty = new(); empty.AddCodexLbConfiguration(_ => { });
        using ServiceProvider emptyProvider = empty.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => emptyProvider.GetRequiredService<IStartupValidator>().Validate());
    }

    /// <summary>Предшествующий Configure с полными options не скрывает missing ключи выбранного configuration.</summary>
    [Theory]
    [InlineData("BaseAddress")]
    [InlineData("Model")]
    [InlineData("ReasoningEffort")]
    [InlineData("KeySource")]
    [InlineData("GenerationTimeout")]
    [InlineData("CompactTimeout")]
    [InlineData("SharedApiKey")]
    public void PriorConfigureCannotHideMissingKey(string key)
    {
        IConfigurationRoot config = BuildConfiguration(); config["KeySource"] = "Shared"; config["SharedApiKey"] = "configured-key"; config[key] = null;
        ServiceCollection services = new();
        services.Configure<CodexLbOptions>(options =>
        {
            options.BaseAddress = "https://prior.invalid"; options.Model = "model"; options.ReasoningEffort = "medium";
            options.KeySource = ModelKeySourceMode.Shared; options.SharedApiKey = "synthetic-secret";
            options.GenerationTimeout = TimeSpan.FromSeconds(180); options.CompactTimeout = TimeSpan.FromSeconds(180);
        });
        services.AddCodexLbConfiguration(config); using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("CodexLb." + key, error.Message); Assert.DoesNotContain("synthetic-secret", error.ToString());
    }

    /// <summary>Programmatic callback должен явно передать каждый обязательный scalar и условный SharedApiKey.</summary>
    [Theory]
    [InlineData("BaseAddress")]
    [InlineData("Model")]
    [InlineData("ReasoningEffort")]
    [InlineData("KeySource")]
    [InlineData("GenerationTimeout")]
    [InlineData("CompactTimeout")]
    [InlineData("SharedApiKey")]
    public void ProgrammaticFieldsCannotUseDefaults(string missing)
    {
        ServiceCollection services = new(); services.AddCodexLbConfiguration(options =>
        {
            if (missing != "BaseAddress") options.BaseAddress = "https://gateway.invalid";
            if (missing != "Model") options.Model = "model";
            if (missing != "ReasoningEffort") options.ReasoningEffort = "medium";
            if (missing != "KeySource") options.KeySource = ModelKeySourceMode.Shared;
            if (missing != "GenerationTimeout") options.GenerationTimeout = TimeSpan.FromSeconds(180);
            if (missing != "CompactTimeout") options.CompactTimeout = TimeSpan.FromSeconds(180);
            if (missing != "SharedApiKey") options.SharedApiKey = "synthetic-secret";
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("CodexLb." + missing, error.Message); Assert.DoesNotContain("synthetic-secret", error.ToString());
    }

    /// <summary>Memory doubles environment/secret providers сохраняют обычную precedence section и standard PostConfigure.</summary>
    [Fact]
    public void MergedProviderOverridesAndSectionWorkWithoutSecretIo()
    {
        Dictionary<string, string?> baseValues = BuildConfiguration().AsEnumerable().ToDictionary(pair => "Library:CodexLb:" + pair.Key, pair => pair.Value);
        baseValues["Library:CodexLb:GenerationTimeout"] = "old-malformed-secret";
        IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(baseValues)
            .AddInMemoryCollection(new Dictionary<string, string?> // Double нормализованных environment keys, без чтения окружения.
            { ["Library:CodexLb:GenerationTimeout"] = "00:03:00", ["Library:CodexLb:KeySource"] = "Shared" })
            .AddInMemoryCollection(new Dictionary<string, string?> // Double secret provider, без secret store/files.
            { ["Library:CodexLb:SharedApiKey"] = "synthetic-secret", ["Library:CodexLb:Model"] = "selected-model" }).Build();
        ServiceCollection services = new(); services.AddCodexLbConfiguration(config.GetSection("Library:CodexLb"));
        services.PostConfigure<CodexLbOptions>(options => options.ReasoningEffort = "postconfigured-effort");
        using ServiceProvider provider = services.BuildServiceProvider(); provider.GetRequiredService<IStartupValidator>().Validate();
        using IServiceScope scope = provider.CreateScope();
        CodexLbOptions[] views = [provider.GetRequiredService<IOptions<CodexLbOptions>>().Value,
            provider.GetRequiredService<IOptionsMonitor<CodexLbOptions>>().CurrentValue,
            scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CodexLbOptions>>().Value];
        Assert.All(views, options =>
        {
            Assert.Equal("selected-model", options.Model); Assert.Equal("postconfigured-effort", options.ReasoningEffort);
            Assert.Equal("synthetic-secret", options.SharedApiKey); Assert.Equal(TimeSpan.FromSeconds(180), options.GenerationTimeout);
        });
        Assert.Null(provider.GetService<IConfiguration>());
    }

    /// <summary>Создаёт подставной источник минимальных настроек без файлов и сети.</summary>
    private static IConfigurationRoot BuildConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BaseAddress"] = "https://gateway.example/",
        ["Model"] = "application-selected-model", ["KeySource"] = "Individual", ["ReasoningEffort"] = "medium",
        ["GenerationTimeout"] = "00:03:00", ["CompactTimeout"] = "00:03:00"
    }).Build();
}
