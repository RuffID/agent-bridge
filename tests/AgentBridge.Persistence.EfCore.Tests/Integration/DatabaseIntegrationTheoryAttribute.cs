using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Явно включает реальные БД, сохраняя безопасный обычный запуск изолированных тестов.</summary>
public class DatabaseIntegrationTheoryAttribute : TheoryAttribute
{
    /// <summary>Включённый набор не подменяет отсутствующие зависимости заглушками.</summary>
    public DatabaseIntegrationTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("AGENTBRIDGE_INTEGRATION") != "1")
        {
            Skip = "Требуется отдельное разрешение и AGENTBRIDGE_INTEGRATION=1.";
        }
    }
}
