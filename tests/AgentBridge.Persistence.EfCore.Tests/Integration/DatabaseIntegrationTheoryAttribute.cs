using System.Runtime.CompilerServices;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Обозначает реальные SQLite/PostgreSQL проверки, запускаемые автоматически без переключателя окружения.</summary>
public class DatabaseIntegrationTheoryAttribute : TheoryAttribute
{
    /// <summary>Передаёт xUnit источник теста; отсутствие Docker или утилит не превращает проверку в skip.</summary>
    /// <param name="sourceFilePath">Путь исходного файла теста для runner xUnit v3.</param>
    /// <param name="sourceLineNumber">Номер строки объявления теста.</param>
    public DatabaseIntegrationTheoryAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
    }
}
