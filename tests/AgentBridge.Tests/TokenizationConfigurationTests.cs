using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Tokenization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Tests;

/// <summary>Public options/DI → actual offline BPE, без сети, host и БД.</summary>
public class TokenizationConfigurationTests
{
    /// <summary>Дополнительный exact ID из конфигурации работает с обеими встроенными кодировками.</summary>
    [Theory]
    [InlineData("o200k_base", 2L)]
    [InlineData("cl100k_base", 3L)]
    public async Task ConfigurationAddsExactModelWithoutRebuildingCounter(string encoding, long tokens)
    {
        IConfigurationRoot configuration = Configuration(new()
        {
            ["Tokenization:ModelEncodings:application-model"] = encoding
        });
        using ServiceProvider provider = Provider(configuration);
        provider.GetRequiredService<IStartupValidator>().Validate();
        IContextTokenCounter counter = provider.GetRequiredService<IContextTokenCounter>();

        ContextTokenCount count = Assert.IsType<ContextTokenCount>((await Count(counter, "application-model")).Data);
        Assert.Equal(encoding, count.Encoding);
        Assert.Equal(tokens, count.KnownTokens);
        Assert.NotNull(count.EstimatedInputTokens);
        Assert.Equal("o200k_base", (await Count(counter, "gpt-5.6-sol")).Data!.Encoding);
        Assert.Equal(ServiceErrorType.Unsupported, (await Count(counter, "APPLICATION-MODEL")).Error!.Type);
        Assert.Equal(ServiceErrorType.Unsupported, (await Count(counter, "application-model-suffix")).Error!.Type);
    }

    /// <summary>Программная регистрация сохраняет пользовательский counter и принимает явные соответствия.</summary>
    [Fact]
    public async Task ProgrammaticRegistrationPreservesCustomCounter()
    {
        ContextTokenCounter custom = new(Options.Create(new TokenizationOptions
        {
            ModelEncodings = new(StringComparer.Ordinal) { ["application-model"] = "cl100k_base" }
        }));
        ServiceCollection services = new();
        services.AddSingleton<IContextTokenCounter>(custom);
        services.AddAgentBridgeTokenization(options => options.ModelEncodings.Add("other-model", "o200k_base"));
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        provider.GetRequiredService<IStartupValidator>().Validate();

        Assert.Same(custom, provider.GetRequiredService<IContextTokenCounter>());
        Assert.Equal(3, (await Count(custom, "application-model")).Data!.KnownTokens);
        Assert.Equal(ServiceErrorType.Unsupported, (await Count(custom, "other-model")).Error!.Type);
    }

    /// <summary>Без раздела Tokenization встроенные ID работают, неизвестный ID остаётся Unsupported.</summary>
    [Fact]
    public async Task MissingSectionPreservesBuiltinModels()
    {
        using ServiceProvider provider = Provider(Configuration(new()));
        provider.GetRequiredService<IStartupValidator>().Validate();
        IContextTokenCounter counter = provider.GetRequiredService<IContextTokenCounter>();

        Assert.Empty(provider.GetRequiredService<IOptions<TokenizationOptions>>().Value.ModelEncodings);
        Assert.Equal("o200k_base", (await Count(counter, "gpt-5.5")).Data!.Encoding);
        Assert.Equal("cl100k_base", (await Count(counter, "gpt-4")).Data!.Encoding);
        Assert.Equal(ServiceErrorType.Unsupported, (await Count(counter, "application-model")).Error!.Type);
    }

    /// <summary>Ошибки формы/значений fail-fast на старте без раскрытия исходной настройки.</summary>
    [Theory]
    [InlineData("Tokenization", "private-configuration")]
    [InlineData("Tokenization:ModelEncodings", "private-configuration")]
    [InlineData("Tokenization:ModelEncodings:private-model", "private-configuration")]
    [InlineData("Tokenization:ModelEncodings:private-model", "O200K_BASE")]
    [InlineData("Tokenization:ModelEncodings:private-model", "")]
    [InlineData("Tokenization:ModelEncodings:private-model", null)]
    [InlineData("Tokenization:ModelEncodings: private-model ", "o200k_base")]
    [InlineData("Tokenization:ModelEncodings:gpt-5", "cl100k_base")]
    [InlineData("Tokenization:ModelEncodings:private-model:child", "private-configuration")]
    public void InvalidConfigurationFailsSafelyAtStartup(string path, string? value)
    {
        using ServiceProvider provider = Provider(Configuration(new() { [path] = value }));
        OptionsValidationException failure = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("Tokenization.ModelEncodings", failure.Message);
        Assert.DoesNotContain("private-", failure.ToString());
        Assert.Null(failure.InnerException);
    }

    /// <summary>Options.Create не обходит проверки при прямом создании counter.</summary>
    [Fact]
    public void DirectConstructionRejectsInvalidEncoding()
    {
        OptionsValidationException failure = Assert.Throws<OptionsValidationException>(() => new ContextTokenCounter(
            Options.Create(new TokenizationOptions { ModelEncodings = new() { ["private-model"] = "private-encoding" } })));

        Assert.Contains("unsupported_encoding", failure.Message);
        Assert.DoesNotContain("private-", failure.ToString());
    }

    /// <summary>Изменение options/config не меняет словарь работающего singleton; новый контейнер принимает новый конфиг.</summary>
    [Fact]
    public async Task ExistingCounterKeepsMappingUntilContainerIsRecreated()
    {
        IConfigurationRoot configuration = Configuration(new() { ["Tokenization:ModelEncodings:application-model"] = "o200k_base" });
        using ServiceProvider original = Provider(configuration);
        IContextTokenCounter counter = original.GetRequiredService<IContextTokenCounter>();
        original.GetRequiredService<IOptions<TokenizationOptions>>().Value.ModelEncodings["application-model"] = "cl100k_base";
        configuration["Tokenization:ModelEncodings:application-model"] = "cl100k_base";
        configuration.Reload();

        Assert.Equal(2, (await Count(counter, "application-model")).Data!.KnownTokens);
        using ServiceProvider restarted = Provider(configuration);
        restarted.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(3, (await Count(restarted.GetRequiredService<IContextTokenCounter>(), "application-model")).Data!.KnownTokens);
    }

    /// <summary>Одноимённое подтверждённое соответствие встроенной модели допустимо.</summary>
    [Fact]
    public async Task MatchingBuiltinEncodingIsAccepted()
    {
        ServiceCollection services = new();
        services.AddAgentBridgeTokenization(options => options.ModelEncodings["gpt-5.6-sol"] = "o200k_base");
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        provider.GetRequiredService<IStartupValidator>().Validate();

        Assert.Equal(2, (await Count(provider.GetRequiredService<IContextTokenCounter>(), "gpt-5.6-sol")).Data!.KnownTokens);
    }

    /// <summary>Оценочный словарь проходит configuration/options/startup pipeline без утверждения кодировки новой модели.</summary>
    [Theory]
    [InlineData("o200k_base", 2L)]
    [InlineData("cl100k_base", 3L)]
    public async Task ConfigurationEnablesApproximateEncoding(string encoding, long tokens)
    {
        using ServiceProvider provider = Provider(Configuration(new()
            { ["Tokenization:UnknownModelEstimateEncoding"] = encoding }));
        provider.GetRequiredService<IStartupValidator>().Validate();

        ContextTokenCount count = (await Count(provider.GetRequiredService<IContextTokenCounter>(), "future-test-model")).Data!;
        Assert.Equal(encoding, count.Encoding);
        Assert.Equal(tokens, count.KnownTokens);
        Assert.True(count.IsApproximateEncoding);
    }

    /// <summary>Недопустимые encoding/shape отклоняются на старте без раскрытия исходных значений.</summary>
    [Theory]
    [InlineData("Tokenization:UnknownModelEstimateEncoding", "private-encoding")]
    [InlineData("Tokenization:UnknownModelEstimateEncoding", "")]
    [InlineData("Tokenization:UnknownModelEstimateEncoding", "O200K_BASE")]
    [InlineData("Tokenization:UnknownModelEstimateEncoding:child", "private-value")]
    public void InvalidEstimateEncodingFailsSafely(string path, string value)
    {
        using ServiceProvider provider = Provider(Configuration(new() { [path] = value }));
        OptionsValidationException failure = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("Tokenization.UnknownModelEstimateEncoding", failure.Message);
        Assert.DoesNotContain("private-", failure.ToString());
        Assert.Null(failure.InnerException);
    }

    /// <summary>Создаёт валидные обязательные настройки ядра и test-only дополнения.</summary>
    private static IConfigurationRoot Configuration(Dictionary<string, string?> additional)
    {
        Dictionary<string, string?> values = new()
        {
            ["Agent:InstructionsSource"] = "PerRequest", ["Agent:MaxToolSteps"] = "1",
            ["Retention:SoftContentLimitBytes"] = "1000", ["Compaction:TokenThreshold"] = "1000",
            ["Compaction:InputTokenReserve"] = "0", ["Compaction:MaxPasses"] = "1"
        };
        foreach (KeyValuePair<string, string?> entry in additional)
            values.Add(entry.Key, entry.Value);

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>Регистрирует actual configuration/tokenizer без приложения и I/O.</summary>
    private static ServiceProvider Provider(IConfiguration configuration)
    {
        ServiceCollection services = new();
        services.AddAgentBridgeConfiguration(configuration).AddAgentBridgeTokenization();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Считает фиксированный BPE vector через public counter.</summary>
    private static Task<ServiceResult<ContextTokenCount>> Count(IContextTokenCounter counter, string model) =>
        counter.CountAsync(new(model, "medium", "Привет", [], []), TestContext.Current.CancellationToken);
}
