using AgentBridge.Configuration;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Регистрация настроек БД без создания контекста или подключения к БД.</summary>
public static class DatabaseConfigurationExtensions
{
    /// <summary>Привязывает переданный раздел БД и регистрирует локальную валидацию.</summary>
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        Validate(services.AddOptions<DatabaseOptions>().BindSafely(configuration, "Database",
            nameof(DatabaseOptions.Provider), nameof(DatabaseOptions.ConnectionString)));
        return services;
    }

    /// <summary>Задаёт провайдер и подключение программно без файла настроек.</summary>
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, Action<DatabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        Validate(services.AddOptions<DatabaseOptions>().Configure(configure));
        return services;
    }

    /// <summary>Регистрирует проверки обязательных полей и локально поддержанных провайдеров.</summary>
    private static void Validate(OptionsBuilder<DatabaseOptions> builder) => builder
        .Validate(options => options.Provider.HasValue, "Database.Provider обязателен; провайдер выбирает приложение.")
        .Validate(options => !options.Provider.HasValue || Enum.IsDefined(options.Provider.Value),
            "Database.Provider не поддержан локальным контрактом SQLite/PostgreSQL/SQL Server.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Database.ConnectionString обязателен.")
        .Validate(options => string.IsNullOrWhiteSpace(options.ConnectionString) || IsConnectionString(options.ConnectionString),
            "Database.ConnectionString: invalid_format — некорректная форма строки подключения.")
        .ValidateOnStart();

    /// <summary>Проверяет только синтаксис без provider, подключения и вывода секрета; серверные свойства проверяются отдельно.</summary>
    private static bool IsConnectionString(string connection)
    {
        try
        {
            DbConnectionStringBuilder parsed = new() { ConnectionString = connection };
            return parsed.Count > 0;
        }
        catch (ArgumentException) { return false; }
    }
}
