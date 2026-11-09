# Провайдер PostgreSQL

Действующий контракт: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). PostgreSQL — единственная поддерживаемая БД AgentBridge. Подключение приложения:

```csharp
using AgentBridge.Persistence.PostgreSql;
using AgentBridge.Persistence.EfCore.Configuration;

services.AddAgentBridgePostgreSql();
services.AddDatabaseConfiguration(options =>
{
    options.Provider = DatabaseProvider.PostgreSql;
    options.ConnectionString = applicationConnectionString;
});
services.AddAgentBridgePersistence();
```

Общий EF-адаптер и Integration facade не зависят от драйвера. Приложение поставляет PostgreSQL-модуль, Npgsql и PostgreSQL migrations assembly. Provider identity остаётся PostgreSql=1; отсутствие настройки и удалённые имена/значения отклоняются до I/O.

Payload хранится в text без JSON-нормализации, OwnerId использует collation C, время — bigint UTC ticks. Составные ключи, каскады, optimistic tokens и история миграций сохраняются. [Миграции и структура папок](11-provider-migrations.md).

Maintenance подключается отдельно. Приложение задаёт абсолютные BackupDirectory и PostgreSqlDumpExecutablePath, major сервера, положительные PostgreSqlCleanupTimeout и BackupRetentionPeriod. Единственного исполнителя, остановку writes/DDL и retention обеспечивает приложение. Регистрация не запускает backup или migrations; [порядок обслуживания](06-database-maintenance.md).

Опциональная ограниченная загрузка: AddAgentBridgePostgreSqlBoundedHistory(limits); [гарантии и оценка payload](31-bounded-history.md). Compile-check не является проверкой PostgreSQL, backup/restore или runtime бинарного комплекта.
