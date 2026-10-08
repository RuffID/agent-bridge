using EFCoreLibrary.Maintenance.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AgentBridge.Persistence.EfCore.Configuration;

/// <summary>Инфраструктурный модуль явно подключённого провайдера без открытия соединения.</summary>
public interface IAgentBridgeDatabaseProvider
{
    /// <summary>Поддерживаемое значение Database.Provider.</summary>
    DatabaseProvider Provider { get; }

    /// <summary>Настраивает общий контекст, migrations assembly и отдельную history table.</summary>
    void Configure(DbContextOptionsBuilder builder, DatabaseOptions options);

    /// <summary>Распознаёт только подтверждённую PK collision указанной таблицы; другие ошибки остаются исключениями.</summary>
    bool IsPrimaryKeyViolation(DbUpdateException error, string table, string constraint);

    /// <summary>Создаёт библиотечный maintenance provider в текущем scope, не выполняя операции.</summary>
    IDatabaseMaintenanceProvider<AgentBridgeContextKey> CreateMaintenance(IServiceProvider services, DatabaseBackupOptions backup);
}
