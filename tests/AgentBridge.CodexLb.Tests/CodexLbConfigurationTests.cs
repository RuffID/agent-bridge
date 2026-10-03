using AgentBridge.CodexLb.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.CodexLb.Tests;

/// <summary>Локальные настройки транспорта через DI, без HTTP и каталога сервера.</summary>
public class CodexLbConfigurationTests
{
    /// <summary>Модель приложения сохраняется без подмены, дефолты не обещают поддержку сервером.</summary>
    [Fact]
    public void MinimalConfigurationPreservesModelAndTrialDefaultsWithoutSharedKey()
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
    [InlineData("ReasoningEffort", " ", "пустым")]
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

    /// <summary>Создаёт подставной источник минимальных настроек без файлов и сети.</summary>
    private static IConfigurationRoot BuildConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BaseAddress"] = "https://gateway.example/",
        ["Model"] = "application-selected-model"
    }).Build();
}
