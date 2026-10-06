using System.Runtime.CompilerServices;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Отдельно включает MSSQL cases; legacy SQLite/PostgreSQL opt-in их не запускает.</summary>
public class SqlServerIntegrationFactAttribute : FactAttribute
{
    /// <summary>Отсутствие opt-in пропускает C; включённая неверная конфигурация должна завершить тест ошибкой.</summary>
    /// <param name="sourceFilePath">Путь исходного файла теста для runner xUnit v3.</param>
    /// <param name="sourceLineNumber">Номер строки объявления теста.</param>
    public SqlServerIntegrationFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("AGENTBRIDGE_SQLSERVER_INTEGRATION") != "1")
            Skip = "Требуются согласованные MSSQL ресурсы и AGENTBRIDGE_SQLSERVER_INTEGRATION=1.";
    }
}
