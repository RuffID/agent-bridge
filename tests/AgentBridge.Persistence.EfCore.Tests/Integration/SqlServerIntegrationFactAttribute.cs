using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Отдельно включает MSSQL cases; legacy SQLite/PostgreSQL opt-in их не запускает.</summary>
public class SqlServerIntegrationFactAttribute : FactAttribute
{
    /// <summary>Отсутствие opt-in пропускает C; включённая неверная конфигурация должна завершить тест ошибкой.</summary>
    public SqlServerIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AGENTBRIDGE_SQLSERVER_INTEGRATION") != "1")
            Skip = "Требуются согласованные MSSQL ресурсы и AGENTBRIDGE_SQLSERVER_INTEGRATION=1.";
    }
}
