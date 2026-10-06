# Microsoft SQL Server

[Навигатор](README.md) · [Модель и миграции](11-provider-migrations.md) · [Maintenance](06-database-maintenance.md).

## Текущий API

Audit Remediation12 добавляет явный `DatabaseProvider.SqlServer = 2`; прежние SQLite=0/PostgreSql=1 сохранены. Основной сценарий внедрения — MSSQL, но default или fallback отсутствует. `AddDatabaseConfiguration` получает IConfiguration приложения; `AddAgentBridgePersistence` выбирает `UseSqlServer`, EF SQL Server10.0.11, scoped общий AgentBridgeDbContext и существующие base CRUD/scenario UoW EFCoreLibrary0.0.5. Ядро не зависит от provider. Регистрация и resolution не открывают соединение и не запускают операции.

Отдельный library target=startup `AgentBridge.Persistence.Migrations.SqlServer` с factory `SqlServerAgentBridgeDbContextFactory` использует Design10.0.11/PrivateAssets=all/runtimeconfig. Runtime/factory выбирают `AgentBridgeMigrationsAssemblies.SQLSERVER` и `__AgentBridgeMigrationsHistory`; общий адаптер не ссылается на migrations проект. Factory принимает только пустые args, использует synthetic endpoint, не читает secrets/host. Generated initial schema создаётся штатным dotnet-ef10.0.11; exact разрешение и результаты находятся в [отчёте12](<../Plans/AgentBridge Audit Remediation/12-sql-server-provider.md#результаты>). SQLite/PostgreSQL migration history сохранена.

## Модель и границы доказательства

Общие шесть таблиц: Dialogs, DialogTurns, CanonicalItems, ModelSteps, DialogContexts, DialogSettings. Parent-local composite PK/FK и порядок, nullable journal/settings/provenance, root immutable/concurrency и independent settings Version остаются прежними. UTC хранится bigint ticks. К каждой дочерней таблице существует один cascade path от Dialogs; metadata не доказывает исполнение каскадов SQL Server.

Строковые JSON/output/envelope/continuation/journal/metadata используют `nvarchar(max)` без JSON-нормализации и ограничений длины. SQL Server constraints используют bracket identifiers и `DATALENGTH(...) > 0` вместо SQLite/PostgreSQL length(); завершающие пробелы учитываются. Nullable terminal/error semantics сохранены. ContentBytes продолжает считать UTF-8 прикладного содержимого, а не physical database size.

OwnerId отдельно хранится `varbinary(max)` — обратимые little-endian UTF-16 code units. Binary token сохраняет case, trailing spaces, NUL и непарные surrogate без trim/normalization/replacement/MaxLength. Это устраняет зависимость ordinal guard от SQL Server string padding. Actual converter, parameter mapping и перевод EF predicate проверяются без открытия БД; server equality/CAS ещё должны проверяться17. Update concurrency exceptions сохраняют существующий guarded Conflict-контракт; SQL Server PK collision/locking/serialization driver outcomes не классифицируются по raw message и не обещаются как typed Conflict до actual17.

## Явный maintenance

`AddAgentBridgeDatabaseMaintenance` подключает существующий `SqlServerMaintenanceProvider<AgentBridgeContextKey>` и общий coordinator/gate EFCoreLibrary. App явно задаёт SingleInitializer и до операций останавливает writes, DDL и другие экземпляры. Нет собственной SQL orchestration, automatic startup/backup/retry/fallback/recovery.

Для MSSQL обязательны `Backup.SqlServerBackupDirectory` и положительный `Backup.BackupRetentionPeriod`; `BackupDirectory` локального app host не заменяет серверный путь и для MSSQL не требуется. Server directory принимает Unix absolute, Windows drive или UNC независимо от ОС приложения; доступ service account, место, cleanup/retention обеспечивает оператор. SQLServer module требует `ConnectRetryCount=0`, прямой стабильный endpoint без failover/ReadOnly routing/AttachDBFilename. TLS/auth/credentials задаёт приложение.

Existing module поддерживает EngineEdition2/3/4 (обычный Standard/Enterprise/Express). Backup — native full COPY_ONLY/CHECKSUM и единственный подтверждённый HEADERONLY set + VERIFYONLY с FILE position/CHECKSUM; receipt содержит server locator/set GUID/position, не локальный файл/hash. Receipt не доказывает restore. Azure SQL Database/MI/Synapse и SQL Server Engine ARM64 не объявляются поддержанными.

## Подключение и дальнейшая проверка

Передайте выбранные sections Database/Backup в [compile-only пример](../../tests/Delivery/Consumer/SqlServerRegistration.cs), задав `Database.Provider=SqlServer`. ConnectionString приходит из secrets приложения. Пример API компилируется с текущими исходниками и не исполняется. Полные MSSQL DLL kits win-x64/linux-x64/linux-arm64 и external binary consumer проверяются15/16; Linux ARM64 означает клиент к отдельно поддержанному серверу.

Actual17 требует отдельного согласования версии/редакции SQL Server, endpoint, TLS/auth, isolated test database, прав CREATE/metadata/backup/restore, серверного backup directory и cleanup. Эти ресурсы пока не заданы. Изолированные DI/factory/EF metadata/generated consistency/converter tests не подтверждают real CRUD/CAS/journal rollback/backup/restore, Ubuntu или ARM64 runtime.
