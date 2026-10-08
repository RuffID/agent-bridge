using AgentBridge.Persistence.PostgreSql;
using AgentBridge.Persistence.Sqlite;
using AgentBridge.Persistence.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.Persistence.EfCore.Tests;

/// <summary>Явная композиция трёх модулей только для общей матрицы тестов; не открывает БД.</summary>
public static class TestDatabaseProviders
{
    /// <summary>Подключает проверяемые провайдеры; DatabaseOptions по-прежнему выбирает один модуль.</summary>
    public static IServiceCollection AddTestDatabaseProviders(this IServiceCollection services) =>
        services.AddAgentBridgeSqlite().AddAgentBridgePostgreSql().AddAgentBridgeSqlServer();
}
