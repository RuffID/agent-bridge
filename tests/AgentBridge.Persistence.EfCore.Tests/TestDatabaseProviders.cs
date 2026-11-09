using AgentBridge.Persistence.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Явная регистрация PostgreSQL для тестов; не открывает БД.</summary>
public static class TestDatabaseProviders
{
    /// <summary>Подключает проверяемые провайдеры; DatabaseOptions по-прежнему выбирает один модуль.</summary>
    public static IServiceCollection AddTestDatabaseProviders(this IServiceCollection services)
    {
        return services.AddAgentBridgePostgreSql();
    }
}
