using System.Runtime.CompilerServices;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Явно включает реальные БД, сохраняя безопасный обычный запуск изолированных тестов.</summary>
public class DatabaseIntegrationTheoryAttribute : TheoryAttribute
{
    /// <summary>Включённый набор не подменяет отсутствующие зависимости заглушками.</summary>
    /// <param name="sourceFilePath">Путь исходного файла теста для runner xUnit v3.</param>
    /// <param name="sourceLineNumber">Номер строки объявления теста.</param>
    public DatabaseIntegrationTheoryAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("AGENTBRIDGE_INTEGRATION") != "1")
        {
            Skip = "Требуется отдельное разрешение и AGENTBRIDGE_INTEGRATION=1.";
        }
    }
}
