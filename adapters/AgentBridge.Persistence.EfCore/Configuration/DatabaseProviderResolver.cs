using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Выбирает только зарегистрированный модуль без reflection, загрузки DLL или fallback.</summary>
internal static class DatabaseProviderResolver
{
    /// <summary>Возвращает единственный модуль для проверенных options.</summary>
    internal static IAgentBridgeDatabaseProvider Resolve(IServiceProvider services, DatabaseOptions options)
    {
        IAgentBridgeDatabaseProvider[] matches = services.GetServices<IAgentBridgeDatabaseProvider>()
            .Where(provider => provider.Provider == options.Provider).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Database.Provider: требуется ровно один явно подключённый модуль выбранного провайдера.");

        return matches[0];
    }
}
