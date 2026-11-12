# Microsoft SQL Server

[Навигатор](README.md) · [Требования](../../openspec/specs/agent-runtime/spec.md) · [Модель и миграции](11-provider-migrations.md) · [Maintenance](06-database-maintenance.md).

## Текущий API

`DatabaseProvider.SqlServer = 2` восстановлен вместе с отдельным `AgentBridge.Persistence.SqlServer` и `AddAgentBridgeSqlServer()`. PostgreSql=1 сохранён; SQLite=0 остаётся неподдерживаемым. Default и fallback отсутствуют. Модуль регистрируется до `AddAgentBridgePersistence` или Integration facade; EF SQL Server 10.0.14 использует общий scoped AgentBridgeDbContext и существующие base CRUD/scenario UoW EFCoreLibrary. Ядро и общий EF adapter не зависят от конкретного provider. Регистрация и resolution не открывают соединение и не запускают операции.

Отдельный library target=startup `AgentBridge.Persistence.Migrations.SqlServer` с factory `SqlServerAgentBridgeDbContextFactory` использует Design 10.0.14/PrivateAssets=all/runtimeconfig. Runtime/factory выбирают `AgentBridgeMigrationsAssemblies.SQLSERVER` и `__AgentBridgeMigrationsHistory`; общий адаптер не ссылается на migrations проект. Factory принимает только пустые args, использует synthetic endpoint, не читает secrets/host. Generated initial и AddCatalogAndDurableRecovery восстановлены из родителя cbc6a46 без изменения содержимого/ID; snapshot размещён непосредственно в Migrations. Новая migration для восстановления поддержки не нужна. PostgreSQL migration history сохранена.

## Модель и границы доказательства

Initial содержит шесть таблиц: Dialogs, DialogTurns, CanonicalItems, ModelSteps, DialogContexts, DialogSettings. AddCatalogAndDurableRecovery добавляет DialogCatalog, DialogCatalogClocks, DialogCatalogChanges и DialogRecoveryOperations; текущая модель содержит десять таблиц. Parent-local composite PK/FK и порядок, nullable journal/settings/provenance, root immutable/concurrency и independent settings Version остаются прежними. UTC хранится bigint ticks. К каждой связанной дочерней таблице существует один cascade path от Dialogs; metadata не доказывает исполнение каскадов SQL Server.

Строковые JSON/output/envelope/continuation/journal/metadata используют `nvarchar(max)` без JSON-нормализации и ограничений длины. SQL Server constraints используют bracket identifiers и `DATALENGTH(...) > 0` вместо SQLite/PostgreSQL length(); завершающие пробелы учитываются. Nullable terminal/error semantics сохранены. ContentBytes продолжает считать UTF-8 прикладного содержимого, а не physical database size.

OwnerId отдельно хранится `varbinary(max)` — обратимые little-endian UTF-16 code units. Binary token сохраняет case, trailing spaces, NUL и непарные surrogate без trim/normalization/replacement/MaxLength. Это устраняет зависимость ordinal guard от SQL Server string padding. Actual converter, parameter mapping и перевод EF predicate проверяются без открытия БД; server equality/CAS ещё должны проверяться17. Update concurrency exceptions сохраняют существующий guarded Conflict-контракт; SQL Server PK collision/locking/serialization driver outcomes не классифицируются по raw message и не обещаются как typed Conflict до actual17.

## Явный maintenance

`AddAgentBridgeDatabaseMaintenance` подключает существующий `SqlServerMaintenanceProvider<AgentBridgeContextKey>` и общий coordinator/gate EFCoreLibrary. App явно задаёт SingleInitializer и до операций останавливает writes, DDL и другие экземпляры. Нет собственной SQL orchestration, automatic startup/backup/retry/fallback/recovery.

Для MSSQL обязательны `Backup.SqlServerBackupDirectory` и положительный `Backup.BackupRetentionPeriod`; `BackupDirectory` локального app host не заменяет серверный путь и для MSSQL не требуется. Server directory принимает Unix absolute, Windows drive или UNC независимо от ОС приложения; доступ service account, место, cleanup/retention обеспечивает оператор. SQLServer module требует `ConnectRetryCount=0`, прямой стабильный endpoint без failover/ReadOnly routing/AttachDBFilename. TLS/auth/credentials задаёт приложение.

Existing module поддерживает EngineEdition2/3/4 (обычный Standard/Enterprise/Express). Backup — native full COPY_ONLY/CHECKSUM и единственный подтверждённый HEADERONLY set + VERIFYONLY с FILE position/CHECKSUM; receipt содержит server locator/set GUID/position, не локальный файл/hash. Receipt не доказывает restore. Azure SQL Database/MI/Synapse и SQL Server Engine ARM64 не объявляются поддержанными.

## Подключение и дальнейшая проверка

Передайте выбранные sections Database/Backup в [compile-only пример](../../tests/Delivery/Consumer/SqlServerRegistration.cs), задав `Database.Provider=SqlServer`. ConnectionString приходит из secrets приложения. Пример API компилируется с текущими исходниками и не исполняется. Например: `services.AddAgentBridgeSqlServer();` → конфигурация `Database.Provider=SqlServer` → `services.AddAgentBridge(...)`. Для явных лимитов истории добавьте `AddAgentBridgeSqlServerBoundedHistory(new DialogHistoryLoadLimits(maxRecords, maxRecordBytes, maxQueries))`; [семантика лимитов](31-bounded-history.md).

Подготовка MSSQL DLL/NuGet и shared kits настроена для win-x64/linux-x64/linux-arm64; свежие комплекты после восстановления ещё не собраны. Linux ARM64 означает клиент к отдельно поддержанному серверу. Исторические проверки прежних поставок не подтверждают обновлённый комплект.

Actual17 требует отдельного согласования версии/редакции SQL Server, endpoint, TLS/auth, isolated test database, прав CREATE/metadata/backup/restore, серверного backup directory и cleanup. Эти ресурсы пока не заданы. Изолированные DI/factory/EF metadata/generated consistency/converter tests не подтверждают real CRUD/CAS/journal rollback/backup/restore, Ubuntu или ARM64 runtime.
