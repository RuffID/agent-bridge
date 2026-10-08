using AgentBridge.Persistence.EfCore.Configuration;
using AgentBridge.Persistence.SqlServer;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBridge.BinaryConsumer;

/// <summary>Compile-only пример явного подключения SQL Server; не запускает maintenance или приложение.</summary>
public static class SqlServerRegistration
{
    /// <summary>Читает выбранные приложением разделы Database/Backup; provider=SqlServer задаётся явно в IConfiguration.</summary>
    /// <remarks>Приложение задаёт secrets/TLS/auth, серверный каталог backup, retention и остановку writes/DDL/всех экземпляров.
    /// Метод компилируется на этапе12 вместе с isolated persistence проектом, но не исполняется; DLL kits проверяются отдельно16.</remarks>
    public static IServiceCollection AddSqlServerStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddDatabaseConfiguration(configuration.GetSection("Database"));
        services.AddAgentBridgeSqlServer();
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDatabaseMaintenance(configuration.GetSection("Backup"), MaintenanceExecutionMode.SingleInitializer);
        return services;
    }
}
