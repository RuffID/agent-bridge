using System.Runtime.CompilerServices;
using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Обозначает автоматический MSSQL case без переключателя окружения и условного пропуска.</summary>
public class SqlServerIntegrationFactAttribute : FactAttribute
{
    /// <summary>Сохраняет источник теста; ошибки Docker и подготовки должны приводить к падению.</summary>
    /// <param name="sourceFilePath">Путь исходного файла теста для runner xUnit v3.</param>
    /// <param name="sourceLineNumber">Номер строки объявления теста.</param>
    public SqlServerIntegrationFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
    }
}
