using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentBridge.Configuration;

/// <summary>Безопасная граница стандартного binding скалярных options без вывода исходных значений.</summary>
public static class SafeOptionsBindingExtensions
{
    /// <summary>Ограждает стандартный ConfigurationBinder, сохраняя Configure/PostConfigure и reload options.</summary>
    /// <remarks>Тип должен содержать только скалярные публичные настройки. IConfiguration остаётся у стандартной options registration, а не runtime-сервисов.</remarks>
    public static OptionsBuilder<TOptions> BindSafely<TOptions>(this OptionsBuilder<TOptions> builder,
        IConfiguration configuration, string group, params string[] requiredFields) where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        PropertyInfo[] properties = typeof(TOptions).GetProperties().Where(property => property.CanWrite).ToArray();
        builder.Services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(
            new ConfigurationChangeTokenSource<TOptions>(builder.Name, configuration));
        return builder.Configure(options =>
        {
            RequireValues<TOptions>(configuration, group, builder.Name, requiredFields);
            foreach (PropertyInfo property in properties)
            {
                IConfigurationSection section = configuration.GetSection(property.Name);
                try
                {
                    _ = section.Get(property.PropertyType);
                }
                catch (InvalidOperationException)
                {
                    // Binder включает значение и inner exception; наружу передаём только принадлежащий нам путь.
                    throw new OptionsValidationException(builder.Name, typeof(TOptions),
                        [$"{group}.{property.Name}: invalid_type — недопустимая форма настройки."]);
                }
            }
            try
            {
                configuration.Bind(options);
            }
            catch (InvalidOperationException)
            {
                // Источник мог измениться между проверкой скаляров и binding; raw Binder error всё равно запрещён.
                throw new OptionsValidationException(builder.Name, typeof(TOptions),
                    [$"{group}: invalid_type — некорректный раздел настроек."]);
            }
        });
    }

    /// <summary>Проверяет присутствие условных configuration keys независимо от ранее настроенных options.</summary>
    public static void RequireValues<TOptions>(IConfiguration configuration, string group, string optionsName,
        params string[] fields) where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string[] missing = fields.Where(field => string.IsNullOrWhiteSpace(configuration[field]))
            .Select(field => $"{group}.{field} обязателен; required — настройка отсутствует или пуста.").ToArray();
        if (missing.Length > 0)
        {
            throw new OptionsValidationException(optionsName, typeof(TOptions), missing);
        }
    }
}
