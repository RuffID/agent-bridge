# 12 — Явная инициализация БД и бэкап

Статус: **Реализован и принят; запрещённые проверки пропущены**. Зависимости: **05, 11**. Локальная фиксация разрешена после приёмки; факт коммита, subject и полный hash подтверждаются отдельным отчётом. Этап 13 не начат.

## Дополнительная интеграционная проверка 2026-10-04

Настоящий AddAgentBridgeDatabaseMaintenance/EFCoreLibrary coordinator выполнен без приложения: inspect Missing, explicit initialize, already-exists отказ, no-pending Unchanged без backup, update с pending/backup до миграции, verification. Windows pg_dump/pg_restore **18.6** и Docker PostgreSQL **18.6** реально отработали; SQLite native API создал и восстановил реальную копию. Restore всегда в отдельной БД: совпали live схема/check/FK/index и все данные, полный payload дополнительно прочитан публичным портом; length/hash артефакта проверены.

Проверены реальные backup-directory/executable/DDL/auth/major failures, сохранение прежней БД при остановке до DDL, poisoned gate после migration failure в новом scope того же root, сохранность backup и отказ corrupted PostgreSQL restore. Обслуживание требует **выделенного scope**: Npgsql после CRUD может скрыть пароль ConnectionString; ошибочное reuse теста исправлено отдельным maintenance scope, библиотеку не обходили и не меняли. SQL Server/MySQL/Unix/TLS/unknown process-stop/deployment условия не подтверждены.

**15 maintenance/migration integration cases** входят в общий **39 passed**, дополнительно **164 persistence isolated и 89 library isolated passed**, failed/skipped 0. [Точные команды, первоначальные ошибки тестов и cleanup](README.md#дополнительный-интеграционный-запуск-2026-10-04). OpenSpec CLI отсутствует; changes не архивированы. История прежних пропусков ниже сохранена; host/startup приложения не запускался.

## Цель

Предоставить вызываемое приложением обслуживание по схеме AquaByte-Ledger через EFCoreLibrary.

## Задачи

- [x] Подключить проверку существующей БД, pending migrations, подтверждённый backup и порядок migrations через существующий coordinator EFCoreLibrary.
- [x] Предоставить отдельный явный InitializeNewAsync без маскировки ошибок подключения.
- [x] Передать пути и provider-specific settings библиотечным SQLite/PostgreSQL модулям; format/scope принадлежат provider.
- [x] Проверить backup settings; потребовать явный положительный срок приложения до рабочего использования, без default. Конкретное production значение не выбрано библиотекой.
- [x] Подключить общий root gate EFCoreLibrary и её no-overwrite workspace; проверить gate/collision failure заглушками, механизм публикации — статически.
- [x] Сохранить полезные библиотечные результаты, безопасные stage/error logs и caller cancellation; обязательный backup/migration failure останавливает вызов.

## Проверка и завершение

Настоящие registration/options/provider metadata проверены без подключения, workflow — настоящим библиотечным coordinator с fake provider/migration boundaries. DLL/DI сами не запускают обслуживание; отдельного host, startup hook или фоновой задачи нет.

## Реализованная граница

`AddAgentBridgeDatabaseMaintenance` имеет IConfiguration/`Action<DatabaseBackupOptions>` перегрузки и обязательный MaintenanceExecutionMode.SingleInitializer. Выбранный provider берётся из DatabaseOptions. Метод отдельно от persistence регистрирует `AddRelationalMaintenance<AgentBridgeContextKey>`, библиотечные providers и scoped `IDatabaseMaintenance<AgentBridgeContextKey>`. Gate/process ownership singleton, context/provider/coordinator scoped. Сеть, SQL, native и dump wrappers остаются EFCoreLibrary; соседние библиотеки read-only.

API: InspectAsync(timeout, ct), UpdateExistingAsync(timeout, ct), InitializeNewAsync(timeout, ct), Capabilities. Нет pending — Unchanged без backup; update с pending — подтверждённый receipt до migration, повторная проверка target, verification; initialization — отдельный путь без фиктивной копии. MaintenanceException сохраняет Code/PrimaryError, caller cancellation — исходный token, deadline — отдельный код. Итоговые стадии доступны через ILogger, progress callback не добавлен. Unknown migration/cleanup outcome poisons root gate; новый scope не снимает запрет. Нет retry/reset/purge/restore/automatic recovery.

DatabaseBackupOptions требуют абсолютный directory и явный положительный BackupRetentionPeriod без default. PostgreSQL дополнительно требует абсолютный pg_dump path, major 10+, конечный положительный cleanup timeout. Валидация локальная, без I/O и значений в ошибках. Retention исполняет приложение; это не срок диалогов. До рабочего использования приложение выбирает реальный срок и предоставляет каталог/deployment dependencies. Receipt не подтверждает восстановимость.

## Фактические проверки

Все команды выполнены с cwd `D:\Media\User\source\repos\agent-bridge`, ветка master. До restore/build проверены конкретные csproj, ancestor Directory.Build.*/Directory.Packages.props/NuGet.config/lock, generated imports и пакетные targets. Проектных hooks нет; стандартные EF/Extensions/xUnit/test SDK imports и native asset items не запускают приложение. Restore ограничен официальным NuGet source; GeneratePackageOnBuild=false во всех командах исключает упаковку EFCoreLibrary. Outputs — artifacts/compile-check и artifacts/test-results, не для коммита.

```powershell
dotnet restore 'tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj' -p:GeneratePackageOnBuild=false --source https://api.nuget.org/v3/index.json --verbosity minimal
dotnet build 'adapters\AgentBridge.Persistence.EfCore\AgentBridge.Persistence.EfCore.csproj' -c Debug --no-restore -p:BaseOutputPath=artifacts\compile-check\ -p:GeneratePackageOnBuild=false -m:1 --verbosity minimal
dotnet build 'tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj' -c Debug --no-restore -p:BaseOutputPath=artifacts\compile-check\ -p:GeneratePackageOnBuild=false -m:1 --verbosity minimal
dotnet test 'tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj' -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts\compile-check\ -p:GeneratePackageOnBuild=false -m:1 --logger 'trx;LogFileName=stage12-persistence.trx' --results-directory 'artifacts\test-results' --verbosity minimal
```

Restore успешен. Production build — 0 warnings/errors. Первый test build дал 1 xUnit2031 warning и 0 errors; Assert.Single исправлен адресно, команда test build повторена: **0 warnings, 0 errors**. Финальный test runner: **164 passed, 0 failed, 0 skipped**, 50 новых (19 registration/options, 31 coordinator). Existing 114 persistence tests входят в этот запуск; generated provider artifacts согласованы прежними metadata tests без SQL. Другие test projects и приложение не запускались.

Покрыты DI no-autostart/scoped context/shared gate, provider capabilities и closed connection metadata, binding и backup validation; inspection/update/initialize, no-pending, receipt fields/time/artifact, target switch, auth/permission/connectivity, backup/collision failure, initialization collision, migration/verification/cleanup failures, shared poison, caller cancellation/deadline/typed-error priority, cancelled gate waiter и safe stage logs. Fake assertions не доказывают actual locking/DDL/cascades/restart, файловую collision protection или восстановимость.

## Пропуски и приёмка

Реальные БД (включая SQLite in-memory/EF InMemory), SQL, применение/rollback migrations, native backup, pg_dump, внешние процессы, backup/restore, HTTP/hosting/TestServer/WebApplicationFactory/Docker и project/user scripts — **Пропущено по указанию пользователя**. Это пользовательские пропуски вне test runner; runner сообщает 0 skipped. OpenSpec CLI отсутствует в PATH: CLI validation не выполнена, change не архивирован. Generated migrations этапа 11 не изменены и повторно не генерировались. Root csproj/slnx сохранены; Domain/Application и соседние библиотеки не изменены. После приёмки разрешены локальные add/commit ровно 29 перечисленных файлов. Нет push, смены ветки или дополнительных чатов/агентов. Локальная фиксация разрешена после приёмки; факт коммита, subject и полный hash подтверждаются отдельным отчётом.

Статически проверены 29 изменённых/новых файлов: строгий UTF-8 без BOM, исходный LF сохранён, без U+FFFD/mojibake/четырёх вопросительных знаков/trailing whitespace и битых локальных Markdown-ссылок. Git diff --check без ошибок; Git предупреждает о будущей LF→CRLF нормализации согласно настройкам, существующие файлы не переформатированы. EFCoreLibrary git status --short пуст; build outputs игнорируются. Нормативные требования синхронизированы статически между main spec и delta; это не OpenSpec CLI validation. Блокеров реализации нет; production retention/deployment/restore остаются обязанностью приложения и реальная совместимость не проверена.

## Точные изменённые файлы

Все пути от корня agent-bridge; 18 изменённых и 11 новых, всего 29. Список включает untracked исходники; outputs в него не входят.

- `AGENTS.md`
- `README.md`
- `adapters/AgentBridge.Persistence.EfCore/AGENTS.md`
- `adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseBackupOptions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseBackupOptionsValidator.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseMaintenanceRegistrationExtensions.cs`
- `tests/AGENTS.md`
- `tests/AgentBridge.Persistence.EfCore.Tests/DatabaseMaintenanceRegistrationTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/DatabaseMaintenanceTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeMaintenanceBoundary.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/MaintenanceTestLogger.cs`
- `Documentation/README.md`
- `Documentation/Business logic/04-storage-and-retention.md`
- `Documentation/Business logic/05-application-configuration.md`
- `Documentation/Technical documentation/01-architecture.md`
- `Documentation/Technical documentation/02-efcorelibrary.md`
- `Documentation/Technical documentation/05-configuration-and-lifecycle.md`
- `Documentation/Technical documentation/06-database-maintenance.md`
- `Documentation/Technical documentation/11-provider-migrations.md`
- `Documentation/Technical documentation/README.md`
- `Documentation/Plans/AgentBridge Initial Implementation/README.md`
- `Documentation/Plans/AgentBridge Initial Implementation/12-database-startup-and-backup.md`
- `openspec/specs/agent-runtime/spec.md`
- `openspec/specs/agent-runtime/context.md`
- `openspec/changes/database-startup-and-backup/proposal.md`
- `openspec/changes/database-startup-and-backup/tasks.md`
- `openspec/changes/database-startup-and-backup/context.md`
- `openspec/changes/database-startup-and-backup/specs/agent-runtime/spec.md`

Источник: [анализ обслуживания](<../../Technical documentation/06-database-maintenance.md>).
