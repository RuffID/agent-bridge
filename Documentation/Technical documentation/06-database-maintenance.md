# Проверка БД, бэкап, миграции и очистка

## Принятое решение

AgentBridge использует тот же функциональный порядок обслуживания, что AquaByte-Ledger: проверить подключение, определить pending migrations, создать резервную копию существующей БД, применить миграции. Ошибка подключения, backup или migration не маскируется и не превращается в успешный startup.

Пользователь разрешил общий реляционный контракт EFCoreLibrary и четыре optional maintenance-модуля: SQLite, PostgreSQL, SQL Server, MySQL. Этап 05 реализован и принят; запрещённые проверки пропущены. EFCoreLibrary commit: `a1747388ab0eb2be6da3031535fd88e7533b8df1`, семь проектов без warnings/errors, 89 passed/0 failed/0 skipped. AgentBridge подключает только SQLite/PostgreSQL. Этап 12: **Реализован и принят; запрещённые проверки пропущены**. SQL Server backup-код и прямое соединение в обход библиотеки не копируются.

Подключающее приложение явно вызывает инициализацию/обновление БД перед использованием агента. Очистка истёкших диалогов предоставляется библиотекой и вызывается приложением по расписанию. Подключение DLL само по себе не запускает миграции или фоновую задачу.

## Проверенные классы AquaByte-Ledger

Статическая проверка исходников выполнена 2026-10-03.

Повторная сверка этапа 00: [DataBaseCheckUpService.CheckOrUpdateDB](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs#L14) синхронно вызывает `CanConnect`, `GetPendingMigrations`, при непустом списке `CreateSqlServerBackup`, затем `Migrate`. Исключения не перехватываются; `CanConnect=false` останавливает операцию, ветки первой установки нет. Cancellation/deadline в этих сигнатурах отсутствуют — требования ниже описывают будущий контракт, а не готовые возможности образца.

| Класс | Текущее поведение | Применение в AgentBridge |
| --- | --- | --- |
| `DataBaseCheckUpService<TContext, TContextKey>` | Использует `IAppDbContext<TContextKey>`, проверяет подключение и pending migrations; backup выполняется до `Database.Migrate()` | Сохранить последовательность и fail-fast через контракт EFCoreLibrary |
| `BackupService<TContext>` | Открывает `SqlConnection`, выполняет SQL Server `BACKUP DATABASE` | Функциональный образец backup, но не готовая реализация SQLite/PostgreSQL |
| `IBackupService<TContext>` | Содержит SQL Server-специфичный `CreateSqlServerBackup()` | Будущий общий контракт обслуживания должен учитывать выбранный provider |
| `BackupFilePathBuilder` | Формирует `.bak` из entry assembly и timestamp с точностью до секунды | Отдельная обязанность формирования пути, без SQL Server формата по умолчанию |
| `IBackupFilePathBuilder` | Отделяет формирование пути от создания backup | Сохранить разделение обязанностей |

В регистрации AquaByte-Ledger пути привязаны к MSSQL: `AppContext.BaseDirectory/Backups` на Windows и `/var/opt/mssql/backups` на Linux. В универсальную библиотеку эти provider-specific пути не переносятся как общие значения.

## EFCoreLibrary

Текущий `IAppDbContext<TContextKey>` уже предоставляет `DatabaseFacade`. Это позволяет использовать EF migrations через библиотечный adapter. Базовые delete-репозитории и `SaveChangesAsync` уже достаточны для удаления строк диалогов.

Реализованный API: `IDatabaseMaintenance<TKey>.Capabilities`, `InspectAsync(timeout, ct)`, `UpdateExistingAsync(timeout, ct)`, `InitializeNewAsync(timeout, ct)`. Общий проект `EFCoreLibrary.Maintenance` зависит от Relational/Logging `10.0.11`, а конкретный EF provider выбирает приложение. `IDatabaseMaintenanceProvider<TKey>` позволяет расширение; в версии `0.0.5` сигнатуры CRUD/UoW и алгоритмы обслуживания сохранены. Models содержат inspection, outcome/applied migrations и backup receipt с operation/target/provider/format/scope, UTC-интервалом и local/server artifact.

Исторически на этапе 00 в Abstractions/EfCore/Extensions отсутствовал check/backup/migrate-сервис. Теперь он находится отдельно в `maintenance/`; `DatabaseFacade` используется общим EF adapter для migrations зарегистрированного контекста. [Проект AquaByte-Ledger Infrastructure](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/AquaByteLedger.Infrastructure.csproj) использует `net10.0`, EF Core/Relational/SqlServer `10.0.11`, SqlClient `7.0.2` и EFCoreLibrary DLL из libs; это не подтверждает бинарную совместимость ранее собранных DLL. Их замена и проверка поставки остаётся последующим этапам.

Регистрация выбранного модуля: `AddEfCoreSqliteMaintenance<TKey>(SqliteMaintenanceOptions, SingleInitializer)`, `AddEfCorePostgreSqlMaintenance<TKey>(DumpOptions, SingleInitializer)`, аналогично `.SqlServer`/`.MySql`. Namespace enum — `EFCoreLibrary.Maintenance.Models.MaintenanceExecutionMode`. Options неизменяемые; provider и coordinator scoped, gate/process recovery singleton, без захвата scoped EF context. DI ничего не запускает. Реальные примеры и зависимости: [README EFCoreLibrary](../../../work/EFCoreLibrary/README.md#опциональное-обслуживание-реляционных-бд).

В версии `0.0.5` namespace соответствуют папкам исходников:

| Типы и операции | Namespace | Актуальные исходники |
| --- | --- | --- |
| Maintenance/provider/migration/command/process/recovery интерфейсы | `EFCoreLibrary.Maintenance.Abstractions` | [IDatabaseMaintenance.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Abstractions/IDatabaseMaintenance.cs) |
| Результаты, capabilities, receipts, artifacts, process-модели и enum | `EFCoreLibrary.Maintenance.Models` | [MaintenanceExecutionMode.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Models/MaintenanceExecutionMode.cs) |
| Безопасные ошибки | `EFCoreLibrary.Maintenance.Errors` | [MaintenanceException.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Errors/MaintenanceException.cs) |
| DumpOptions | `EFCoreLibrary.Maintenance.Options` | [DumpOptions.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Options/DumpOptions.cs) |
| Coordinator, budget и gate | `EFCoreLibrary.Maintenance.Coordination` | [DatabaseMaintenance.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs) |
| Общая регистрация | `EFCoreLibrary.Maintenance.Extensions` | [MaintenanceRegistration.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Extensions/MaintenanceRegistration.cs) |
| SQLite регистрация | `EFCoreLibrary.Maintenance.Sqlite.Extensions` | [SqliteMaintenanceRegistration.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.Sqlite/Extensions/SqliteMaintenanceRegistration.cs) |
| SQLite options | `EFCoreLibrary.Maintenance.Sqlite.Options` | [SqliteMaintenanceOptions.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.Sqlite/Options/SqliteMaintenanceOptions.cs) |
| SQLite provider | `EFCoreLibrary.Maintenance.Sqlite.Providers` | [SqliteMaintenanceProvider.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.Sqlite/Providers/SqliteMaintenanceProvider.cs) |
| SQLite native backup контракты | `EFCoreLibrary.Maintenance.Sqlite.Abstractions` | [ISqliteBackupApi.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.Sqlite/Abstractions/ISqliteBackupApi.cs) |
| SQLite native backup API и stepper | `EFCoreLibrary.Maintenance.Sqlite.Backup` | [SqliteBackupApi.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.Sqlite/Backup/SqliteBackupApi.cs) |
| PostgreSQL регистрация | `EFCoreLibrary.Maintenance.PostgreSql.Extensions` | [PostgreSqlMaintenanceRegistration.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.PostgreSql/Extensions/PostgreSqlMaintenanceRegistration.cs) |
| PostgreSQL provider | `EFCoreLibrary.Maintenance.PostgreSql.Providers` | [PostgreSqlMaintenanceProvider.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.PostgreSql/Providers/PostgreSqlMaintenanceProvider.cs) |
| Workspace | `EFCoreLibrary.Maintenance.Backup` | [BackupWorkspace.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Backup/BackupWorkspace.cs) |
| Commands | `EFCoreLibrary.Maintenance.Database` | [DatabaseCommands.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Database/DatabaseCommands.cs) |
| Process runner | `EFCoreLibrary.Maintenance.Processes` | [BackupProcessRunner.cs](../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Processes/BackupProcessRunner.cs) |

Сценарий очистки использует базовое чтение по сроку истечения и `Delete`/`DeleteRange` через сценарный UoW. Custom query не становится предпочтительным только потому, что операция называется обслуживанием.

## Порядок обслуживания

Для существующей БД:

1. Проверить подключение и совместимость выбранного provider.
2. Удерживать открытый EF connection, повторно проверить его фактическую цель и определить pending migrations зарегистрированного AgentBridgeDbContext.
3. При наличии изменений создать и подтвердить успешную резервную копию через EFCoreLibrary.
4. Повторно сверить target, затем применить migrations зарегистрированного контекста.
5. Проверить отсутствие pending и вернуть приложению результат проверки, backup и обновления. Без pending возвращается Unchanged без backup.

Для первой установки требуется явный путь инициализации. Старый `CanConnect() == false → ошибка` сам по себе не создаёт новую БД. Отсутствующая БД и ошибка подключения различаются; backup ещё не существующей БД не имитируется.

Приложение явно выбирает SingleInitializer и останавливает другие экземпляры, writes и DDL. Gate локальный на root DI container, распределённой блокировки нет. Контекст выделяется обслуживанию на весь scope, без параллельного CRUD. Внешняя транзакция и EF execution strategy с повторами запрещены; нужен стабильный прямой endpoint без routing/failover/reconnect. Удержание connection не является гарантией поведения прокси.

## Резервная копия

Backup должен быть согласованной копией выбранного provider. Простое копирование активного SQLite-файла не принимается как универсальная замена provider API, поскольку база может работать с WAL. SQL Server-команда не подходит PostgreSQL.

Backup directory задаётся приложением; формат и scope фиксированы возможностями выбранного модуля. Локальный артефакт получает уникальное имя и публикуется без перезаписи после закрытия, flush и SHA256 с owner-only правами. Неполный receipt и чужие operation/target/provider/format/scope/time не разрешают migration. Автоматического удаления успешных backup нет: retention принадлежит оператору.

| Provider | Реализованный механизм и границы |
| --- | --- |
| SQLite | Native backup main порциями 128 страниц с проверкой DONE/finish и ограничением BUSY/LOCKED. App-owned native runtime; обычный файл без memory/URI/custom VFS/encryption/attachments. Inspection проверяет main path; тесты не инициализируют SQLite. |
| PostgreSQL | Npgsql `10.0.3`, внешний pg_dump custom format выбранной БД, без global roles. Явные endpoint/TLS, совпадающий major (10+), private PGPASSFILE. CREATE через `postgres` с проверкой версии и правами приложения. |
| SQL Server | SqlClient `6.1.6`, native full COPY_ONLY/CHECKSUM, HEADERONLY + VERIFYONLY. Server directory/locator, set GUID и position вместо локального файла/hash. Только EngineEdition 2/3/4; ConnectRetryCount=0, без failover/ReadOnly routing. 4060 требует admin proof с VIEW ANY DATABASE. |
| MySQL | MIT MySqlConnector `2.6.2` + mysqldump выбранной InnoDB БД, routines/triggers/events. Доступный partial_revokes должен быть 0, прямые global SELECT/SHOW VIEW/TRIGGER/EVENT доказывают видимость; нужны также права чтения routines. MariaDB/mixed engines не поддержаны; accounts/grants, tablespaces и replication topology исключены. |

PostgreSQL/MySQL допускают только переносимые в dump connection options, явный TLS off либо проверку имени/CA и один TCP endpoint. Executable задаётся абсолютным путём; версия проверяется, shell/произвольных флагов нет. Пароли передаются только через private file. Oracle EF provider не входит в mandatory dependencies: его restore подтвердил лишь EF10 feasibility; выбор EF provider и runtime DLL graph остаётся за приложением.

Backup является отдельной копией данных. Удаление истёкшего диалога из рабочей БД не изменяет старые backup-файлы. Они не учитываются в мягком пороге диалога и не восстанавливаются автоматически во время обычного продолжения агента.

## Ошибки и диагностика

Обслуживание поддерживает cancellation и конечный кооперативный budget. В `ILogger<T>` попадают operation ID, стадия и закрытый код; raw exception/driver text, stdout/stderr, пути и credentials не логируются. Внешний безопасный API — координатор `IDatabaseMaintenance<TKey>`. Caller cancellation возвращает OperationCanceledException, локальный deadline — безопасный DeadlineExceeded; typed failure не подменяется состоянием токенов.

Неизвестная остановка процесса/pipe tasks или уже отправленной server command даёт CleanupUnconfirmed и отравляет gate. Ошибки после начала initialization/migration/verification также запрещают повтор. Process runner сохраняет handle и credentials до explicit `IMaintenanceRecovery.RetryCleanupAsync(timeout)`; ошибка файловой очистки сохраняет callback и PrimaryError без raw текста. Recovery не сбрасывает gate и не подтверждает целостность схемы. SQL Server pre-dispatch failure сохраняет safe code; после dispatch остановку доказывает оператор, а не локальный process recovery.

Изолированные проверки исполнялись на Windows: файловые DACL проверены, Unix-ветка 0600 не исполнялась. Реальные БД/SQL/native backup/pg_dump/mysqldump, migrations и restore — **Пропущено по указанию пользователя**. Receipt и compile-check не являются проверкой реальной восстановимости. Restore API и автоматическое восстановление не входят в этап 05.

Ошибка backup при обязательном backup перед migration останавливает применение migration. Ошибка удаления не выдаётся за очищенный диалог. Удаление и позднее сохранение результата согласуются с актуальностью диалога.

## Подключение AgentBridge этапа 12

`DatabaseMaintenanceRegistrationExtensions.AddAgentBridgeDatabaseMaintenance` находится в `AgentBridge.Persistence.EfCore.Configuration`. Две перегрузки принимают непосредственно раздел `IConfiguration` либо `Action<DatabaseBackupOptions>` и обязательный `MaintenanceExecutionMode`. Метод вызывается после `AddDatabaseConfiguration` и `AddAgentBridgePersistence`. Он не вызывает обслуживание и не меняет прежнюю persistence-регистрацию.

Внутри регистрируется `AddRelationalMaintenance<AgentBridgeContextKey>` EFCoreLibrary. Scoped factory выбирает **библиотечный** SqliteMaintenanceProvider/PostgreSqlMaintenanceProvider по валидированному DatabaseOptions и переводит backup options в неизменяемые SqliteMaintenanceOptions/DumpOptions. Общий singleton SingleInitializerGate не захватывает контекст; provider, coordinator и EfMigrationOperations scoped, используют тот же `IAppDbContext<AgentBridgeContextKey>`. Второй coordinator, собственный SQL/backup/native/process wrapper не добавлены. Модули SQL Server/MySQL не входят в graph AgentBridge.

| DatabaseBackupOptions | Проверка и обязанность |
| --- | --- |
| BackupDirectory | Обязательный корректный абсолютный путь без управляющих символов. Приложение заранее предоставляет доступный каталог; существование/права проверяет EFCoreLibrary при операции, каталог не создаётся регистрацией. |
| BackupRetentionPeriod | Обязательный явно заданный положительный TimeSpan, default отсутствует. Приложение исполняет retention; AgentBridge не удаляет копии и не запускает расписание. |
| PostgreSqlDumpExecutablePath | Для PostgreSQL: абсолютный путь pg_dump без PATH/shell/произвольных flags. Доступность executable проверяется библиотекой при backup. |
| PostgreSqlServerMajorVersion | Для PostgreSQL: явно заданный int 10+. Provider проверяет совпадение с сервером и pg_dump. |
| PostgreSqlCleanupTimeout | Для PostgreSQL: положительный TimeSpan не более 4 294 967 294 мс, отдельный budget cleanup библиотеки. |

Все свойства изначально null. SQLite не требует PostgreSQL-полей. `DatabaseBackupOptionsValidator` проверяет форму без I/O и выдаёт OptionsValidationException с именами полей без значений. Bind/Configure используют ValidateOnStart; приложение без host может вызвать IStartupValidator.Validate(), либо получить IOptions.Value. BuildServiceProvider не запускает операции. Maintenance resolution валидирует backup options **до** создания контекста. IOptions фиксируют настройки контейнера; изменять их объекты во время работы нельзя — новую конфигурацию обслуживает приложение через новый контейнер после остановки операций.

Конкретный production срок хранения backup не выбран библиотекой. До первого рабочего использования приложение обязано выбрать его явно; без выбора API не разрешает обслуживание. DialogRetentionOptions и фиксированный ExpiresAtUtc к этой политике не относятся. Регистрируемый retention — обязательная декларация приложения, а не обещание автоматического удаления копий.

Пример фактического API (configuration, services, timeout и ct принадлежат приложению):

```csharp
using AgentBridge.Persistence.EfCore;
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

services.AddDatabaseConfiguration(configuration.GetSection("AgentBridge:Database"));
services.AddAgentBridgePersistence();
services.AddAgentBridgeDatabaseMaintenance(
    configuration.GetSection("AgentBridge:Backup"), MaintenanceExecutionMode.SingleInitializer);

// После остановки других экземпляров, writes и DDL приложение выделяет scope.
// provider — уже построенный контейнер приложения, без автоматического host.
provider.GetRequiredService<IStartupValidator>().Validate();
using IServiceScope scope = provider.CreateScope();
IDatabaseMaintenance<AgentBridgeContextKey> maintenance =
    scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();
DatabaseInspection inspection = await maintenance.InspectAsync(timeout, ct);
// Для существующей БД приложение явно выбирает обновление.
DatabaseMaintenanceResult updated = await maintenance.UpdateExistingAsync(timeout, ct);
```

Для отдельной первой установки приложение вместо UpdateExistingAsync вызывает InitializeNewAsync(timeout, ct); автоматически выбирать этот метод по исключению нельзя. Inspect не разрешает последующую запись по устаревшему результату: оба изменяющих метода заново проверяют существование и target внутри библиотечного gate. Scope полностью принадлежит обслуживанию, concurrent CRUD запрещён приложением. Приложение поставляет выбранную migrations DLL и SQLite native runtime либо pg_dump; compile graph не является готовой поставкой DLL.

Результаты остаются библиотечными: DatabaseInspection (Exists/TargetIdentity/PendingMigrations), DatabaseMaintenanceResult (Outcome/AppliedMigrations/Backup) и Capabilities. Успешный update даёт Migrated с receipt; без pending — Unchanged без receipt; initialization — Initialized без receipt. Receipt и target содержат технические данные, local artifact — путь: их не следует логировать или отдавать пользовательскому UI целиком.

Стадии Waiting/Inspection/Discovery/Initialization/Backup/Migration/Verification принадлежат общему coordinator. ILogger приложения получает текущую **итоговую** стадию завершения или отказа, OperationId и безопасный Code; callback прогресса и список всех стадий результата не добавлены. MaintenanceException сохраняет Code/PrimaryError без raw driver text/inner exception. Фактическая отмена возвращает OperationCanceledException с исходным caller token, собственный timeout — DeadlineExceeded. Typed failure сохраняет приоритет над одновременной отменой. Нет автоматических повторов, reset gate, purge, restore или вызова recovery в AgentBridge. CleanupUnconfirmed и неизвестные исходы migration/verification блокируют общий gate; создаваемый заново scope блокировку не снимает.

Проверки этапа: production build и финальный test build без warnings/errors; **164 persistence tests passed / 0 failed / 0 skipped**, из них 50 новых. Настоящие provider resolution/capabilities/EF metadata проверены без connection; порядок и partial failures — с fake boundaries. Fake collision доказывает остановку coordinator; actual FileMode.CreateNew/публикация File.Move(..., false) подтверждены статическим чтением EFCoreLibrary, файловая/native/process интеграция здесь не исполнялась. Подробные команды, ограничения и список файлов: [этап 12](<../Plans/AgentBridge Initial Implementation/12-database-startup-and-backup.md>).

## Источники

- [Нормативный контракт](../../openspec/specs/agent-runtime/spec.md)
- [Реализация maintenance](../../../work/EFCoreLibrary/maintenance/)
- [Отчёт этапа 05](<../Plans/AgentBridge Initial Implementation/05-efcorelibrary-maintenance.md>)

- [DataBaseCheckUpService](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs)
- [BackupService](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/BackupService.cs)
- [BackupFilePathBuilder](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/BackupFilePathBuilder.cs)
- [EFCoreLibrary: IAppDbContext](../../../work/EFCoreLibrary/Abstractions/Database/IAppDbContext.cs)
- [EFCoreLibrary: DeleteItemRepository](../../../work/EFCoreLibrary/EfCore/Repository/Base/DeleteItemRepository.cs)

Связанные документы: [EFCoreLibrary](02-efcorelibrary.md), [хранение и удаление](<../Business logic/04-storage-and-retention.md>).
