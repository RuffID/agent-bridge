using Xunit;

namespace AgentBridge.Persistence.EfCore.Tests.Integration;

/// <summary>Последовательно выполняет реальные проверки SQLite/PostgreSQL/MSSQL с ленивыми контейнерами.</summary>
[CollectionDefinition("DatabaseIntegration", DisableParallelization = true)]
public class DatabaseIntegrationCollection : ICollectionFixture<DatabaseIntegrationFixture>
{
}
