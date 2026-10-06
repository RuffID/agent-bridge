using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Проверки выбора БД через публичную DI-границу без провайдера и подключения.</summary>
public class DatabaseConfigurationTests
{
    /// <summary>Оба провайдера выбираются явно, строка подключения только сохраняется в options.</summary>
    [Theory]
    [InlineData(DatabaseProvider.SQLite)]
    [InlineData(DatabaseProvider.PostgreSql)]
    public void ProviderIsExplicitAndCanBeBoundWithoutDatabase(DatabaseProvider selected)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Provider"] = selected.ToString(),
            ["ConnectionString"] = "Password=synthetic-connection-secret"
        }).Build();
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
        DatabaseOptions options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        Assert.Equal(selected, options.Provider);
        Assert.Equal("Password=synthetic-connection-secret", options.ConnectionString);
        Assert.Null(provider.GetService<IConfiguration>());
    }

    /// <summary>Отсутствие раздела не выбирает SQLite по умолчанию и сообщает обе обязательные настройки.</summary>
    [Fact]
    public void MissingSectionFailsInsteadOfSelectingSqlite()
    {
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("Database.Provider обязателен", error.Message);
        Assert.Contains("Database.ConnectionString обязателен", error.Message);
        Assert.DoesNotContain("не поддержан", error.Message);
    }

    /// <summary>Неизвестное числовое значение отличается от отсутствия провайдера, секрет не попадает в ошибку.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void UnsupportedLocalProviderIsRejectedWithoutConnectionSecret(int selected)
    {
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = (DatabaseProvider)selected;
            options.ConnectionString = "Password=synthetic-connection-secret";
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);
        Assert.Contains("не поддержан локальным контрактом", error.Message);
        Assert.DoesNotContain("обязателен", error.Message);
        Assert.DoesNotContain("synthetic-connection-secret", error.ToString());
    }

    /// <summary>Неизвестное имя провайдера даёт явную ошибку binding, без обращения к БД.</summary>
    [Fact]
    public void UnknownProviderNameFailsBinding()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Provider"] = "UnsupportedProvider",
            ["ConnectionString"] = "Password=synthetic-connection-secret"
        }).Build();
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);
        Assert.Contains("Provider", error.Message);
        Assert.DoesNotContain("synthetic-connection-secret", error.ToString());
    }

    /// <summary>Пустое подключение явно отклоняется при любом выбранном провайдере.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingConnectionFailsLocalValidation(string? connection)
    {
        ServiceCollection services = new();
        services.AddDatabaseConfiguration(options =>
        {
            options.Provider = DatabaseProvider.PostgreSql;
            options.ConnectionString = connection;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);
        Assert.Contains("Database.ConnectionString обязателен", error.Message);
    }

    /// <summary>Missing/blank и malformed provider сохраняют safe путь, не секрет или Binder inner exception.</summary>
    [Theory]
    [InlineData("Provider", null)]
    [InlineData("Provider", " ")]
    [InlineData("Provider", "synthetic-secret")]
    [InlineData("Provider", "2147483648")]
    [InlineData("Provider", "99")]
    [InlineData("ConnectionString", null)]
    [InlineData("ConnectionString", " ")]
    [InlineData("ConnectionString", "Password=\"synthetic-secret")]
    public void InvalidConfigurationIsSafeInAllOptionsViews(string field, string? value)
    {
        IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Provider"] = "SqlServer", ["ConnectionString"] = "Password=synthetic-secret", [field] = value }).Build();
        ServiceCollection services = new();
        services.Configure<DatabaseOptions>(options => { options.Provider = DatabaseProvider.SqlServer; options.ConnectionString = "Password=prior-secret"; });
        services.AddDatabaseConfiguration(config);
        using ServiceProvider provider = services.BuildServiceProvider(); using IServiceScope scope = provider.CreateScope();
        Action[] reads = [() => { _ = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value; },
            () => { _ = provider.GetRequiredService<IOptionsMonitor<DatabaseOptions>>().CurrentValue; },
            () => { _ = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>().Value; },
            () => provider.GetRequiredService<IStartupValidator>().Validate()];
        foreach (Action read in reads)
        {
            OptionsValidationException error = Assert.Throws<OptionsValidationException>(read);
            Assert.Contains("Database." + field, error.ToString()); Assert.DoesNotContain("synthetic-secret", error.ToString()); Assert.Null(error.InnerException);
        }
    }
}
