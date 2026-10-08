using Microsoft.Extensions.Options;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Отклоняет отсутствующий либо неоднозначный модуль до операций БД.</summary>
internal class DatabaseProviderOptionsValidator(IEnumerable<IAgentBridgeDatabaseProvider> providers) : IValidateOptions<DatabaseOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (!options.Provider.HasValue || !Enum.IsDefined(options.Provider.Value))
            return ValidateOptionsResult.Success;

        return providers.Count(provider => provider.Provider == options.Provider.Value) == 1
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Database.Provider: требуется ровно один явно подключённый модуль выбранного провайдера.");
    }
}
