# 21 — Настройки и статус диалога

Статус: **Реализован, проверен и принят координатором; локальный коммит разрешён**. Дата: **2026-10-04, Asia/Novosibirsk**. Зависимости: **13, 17, 20**. Этапы22–25 не начаты.

Исходный чистый HEAD20/master: `95c28fae3773efa9e8e4d0281282340f5ac9e6c0`. До приёмки HEAD не менялся, index был пуст; add/commit до приёмки запрещались. Работа выполнена только в AgentBridge; соседние исходники EFCoreLibrary/HttpClientLibrary и codex-lb не изменялись.

## Решения пользователя и результат

- [x] Выбор model/effort хранится в БД AgentBridge для конкретного диалога, независимо от AgentId приложения.
- [x] Отдельная CAS Version settings; активный ход продолжает работу со своим атомарно сохранённым снимком. Приоритет: override запроса → saved dialog selection → default приложения. Override не сохраняется.
- [x] Порт проверки совместимости opaque получает исходные selected/server model раздельно и новый выбор. Без подтверждения — Unsupported, контекст и выбор сохраняются.
- [x] Безопасные settings/status возвращают token/version, effective модель, effort, лимиты, даты/expiry, ContentBytes/мягкий порог, known/null estimate и число compact; секреты и raw содержимое не возвращаются.
- [x] Обе точные команды генерации AddDialogSettings разрешены и выполнены штатным EF tooling; generated вручную не редактировались.
- [x] Проверены конфигурация/new scopes, concurrent changes, unknown tokenizer, provenance/повторные opaque occurrences, expected/unexpected compatibility failures и реальная atomicity SQLite/PostgreSQL.

Фактический API: [техническая документация21](<../../Technical documentation/21-settings-and-dialog-status.md>). Бизнес-правила: [модели и статус](<../../Business logic/06-models-and-status.md>). SSOT: [main spec](../../../openspec/specs/agent-runtime/spec.md), [context](../../../openspec/specs/agent-runtime/context.md), [change](../../../openspec/changes/dialog-settings-and-status/proposal.md).

DialogSettings не меняет root Revision/LastChangedAtUtc/expiry/ContentBytes. Immutable TurnModelSettings фиксируется при BeginWithSettings. Nullable SettingsJson/SelectedModel сохраняют неизвестность historical provenance. ContentBytes учитывает прежнее содержимое (canonical items, full reports/compact, tool journal), новые settings/snapshot/provenance являются metadata. Down/Up подтверждает неизменность истории, fixed dates и count; удалённые metadata возвращаются null.

Typed stale/CAS и доказанный settings PK collision дают Conflict без refresh/retry. SQLite busy/PostgreSQL serialization failures и commit/cleanup uncertainty не маскируются ожидаемым Conflict. Standalone ContextCompactor не сохраняет selected provenance: legacy/standalone opaque требует app proof даже при совпадающем server имени. Status оценивает сохранённый input без transient providers/new input/tools; CanContinue не гарантирует бюджет следующего полного запроса.

## Фактические версии и compile-check

.NET SDK **10.0.401**, target **net10.0**; EFCoreLibrary **0.0.5**, HttpClientLibrary **0.0.0.5**. EF Core/SQLite **10.0.11**, Npgsql EF provider **10.0.3**. Microsoft.ML.Tokenizers/Data **2.0.0**, Microsoft.Bcl.Memory **10.0.4**, Microsoft.Extensions core **10.0.3**. xUnit **2.9.3**, runner **3.1.5**, Microsoft.NET.Test.Sdk **18.0.1**. Docker Desktop Linux Engine **29.8.1**, PostgreSQL **18**; native pg tools18 уже существовали, не устанавливались. Restore на этом этапе не потребовался.

Прочитаны актуальные проекты/imports/options/base repository API; executable hooks не обнаружены. Build выполнялся для конкретных projects, без Rebuild/pack/publish и с GeneratePackageOnBuild=false по всей цепочке. Последние успешные builds ядра/tests, EF/tests/обоих migrations и CodexLb/tests: **0 warnings / 0 errors**.

Все команды ниже выполнены из `D:\Media\User\source\repos\agent-bridge`:

```powershell
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
```

## Штатная генерация миграций

После compile-check выполнены ровно разрешённые команды, без применения к рабочим БД:

```powershell
$env:BaseOutputPath='artifacts\compile-check\'
$env:GeneratePackageOnBuild='false'
dotnet ef migrations add AddDialogSettings --project adapters\AgentBridge.Persistence.Migrations.Sqlite\AgentBridge.Persistence.Migrations.Sqlite.csproj --startup-project adapters\AgentBridge.Persistence.Migrations.Sqlite\AgentBridge.Persistence.Migrations.Sqlite.csproj --context AgentBridgeDbContext --output-dir Migrations --no-build --configuration Debug
dotnet ef migrations add AddDialogSettings --project adapters\AgentBridge.Persistence.Migrations.PostgreSql\AgentBridge.Persistence.Migrations.PostgreSql.csproj --startup-project adapters\AgentBridge.Persistence.Migrations.PostgreSql\AgentBridge.Persistence.Migrations.PostgreSql.csproj --context AgentBridgeDbContext --output-dir Migrations --no-build --configuration Debug
```

Созданы SQLite 20261004092118_AddDialogSettings и PostgreSQL 20261004092121_AddDialogSettings, designers и snapshots. Up добавляет DialogSettings и две nullable колонки; Down удаляет только новые settings/metadata. Existing Initial/20 не менялись и не регенерировались. Согласованность runtime/model/snapshot проверена изолированно, Up/Down — на собственных БД.

## Финальные проверки

| Evidence в artifacts/test-results/stage21 | Passed | Failed | Skipped | Граница |
| --- | ---: | ---: | ---: | --- |
| settings-core-final.trx | 265 | 0 | 0 | safe API, actual offline BPE/shape, DI/options, runner и связанные context paths |
| persistence-final.trx | 171 | 0 | 0 | base CRUD/UoW doubles, JSON mapping, six-table metadata, runtime/design-time/snapshots |
| settings-transport-first.trx | 43 | 0 | 0 | ModelCatalogTests, actual HttpClientLibrary + fake handler/local responses |
| settings-and-runner-db-final.trx | 46 | 0 | 0 | settings16 + runner30, actual SQLite/PostgreSQL/EFCoreLibrary0.0.5 |

**525 distinct адресных cases**, без суммирования предыдущих пересекающихся запусков. HTTP fake handler не доказывает live codex-lb/OpenAI; isolated UoW не доказывает SQL isolation. Последний DB запуск подтверждает restart/CAS, независимые версии при active runner/new run override, insert/update rollback после real SQL SaveChanges, owner/expiry/delete/recreate/cascade, конкурентные first insert/update, nullable historical provenance и Down/Up с неизменным ContentBytes. Исторический maintenance набор39 не повторялся; его compile/schema expectations обновлены до3 migrations/6 таблиц.

```powershell
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~AgentSettingsTests|FullyQualifiedName~AgentRunnerTests|FullyQualifiedName~ContextTokenCounterTests|FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ContextBudgetGuardTests|FullyQualifiedName~ContextCompactorTests|FullyQualifiedName~ModelSelectionTests|FullyQualifiedName~ApplicationPortsTests' --logger 'trx;LogFileName=settings-core-final.trx' --results-directory artifacts\test-results\stage21 --verbosity minimal
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'Dependency!=Database' --logger 'trx;LogFileName=persistence-final.trx' --results-directory artifacts\test-results\stage21 --verbosity minimal
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~ModelCatalogTests' --logger 'trx;LogFileName=settings-transport-first.trx' --results-directory artifacts\test-results\stage21 --verbosity minimal

# Только для собственных разрешённых БД; run.json затем удалён.
$taskRecord=Get-Content -Raw -Encoding UTF8 'artifacts\integration-stage21-77d30b45e0e3483682e033d5a0a6b760\run.json' | ConvertFrom-Json
$env:AGENTBRIDGE_INTEGRATION='1'
$env:AGENTBRIDGE_INTEGRATION_ROOT=$taskRecord.root
$env:AGENTBRIDGE_POSTGRES_CONNECTION="Host=127.0.0.1;Port=56863;Database=postgres;Username=$($taskRecord.user);Password=$($taskRecord.password);SSL Mode=Disable;Pooling=false"
$env:AGENTBRIDGE_PG_DUMP='D:\Programs\PostgreSQL\18\bin\pg_dump.exe'
$env:AGENTBRIDGE_PG_RESTORE='D:\Programs\PostgreSQL\18\bin\pg_restore.exe'
$env:AGENTBRIDGE_PG_MAJOR='18'
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~DialogSettingsIntegrationTests|FullyQualifiedName~AgentRunnerIntegrationTests' --logger 'trx;LogFileName=settings-and-runner-db-final.trx' --results-directory artifacts\test-results\stage21 --verbosity minimal
```

## Первоначальные сбои и исправления

1. Build CS0246: новый fixture ссылался на отсутствующий FakeDialogByIdRepositoryForSettings. Добавлен actual FakeSettingsByIdRepository; production fallback не добавлен.
2. Build CS0272: два теста присваивали private Probe.Dialog. Исправлены через существующий fixture Replace с optional Selection.
3. После этого failed build ошибочно был выполнен no-build запуск на старом binary: settings-core-third.trx (259 pass) **исключён из evidence**. Финальные core265 выполнены после успешной сборки.
4. settings-core-first.trx: 41 passed / 5 failed. Старые compact fixtures имели synthetic unknown count без selected provenance; fixtures исправлены, opaque guard не ослаблен. settings-core-second259 и fourth261 — промежуточные пересекающиеся результаты.
5. settings-db-first.trx: 10 passed / 4 failed. Test expectations ошибочно ожидали24h, общий CreateAsync создаёт36h; исправлены только ожидания. settings-db-second16 — промежуточный successful результат; финальный combined46 выше.
6. Были read/discovery ошибки guessed filenames/rg wildcard paths, включая отсутствующие Directory.Build.props/targets/NuGet.config; это не production/validation evidence.
7. Один apply_patch отклонён до изменений: инструмент не допускает Delete+Add одного target в одном patch. Delta заменена через Update и сопоставлена с main requirements.
8. Автоматическая policy отклонила cleanup команду с вычисляемой переменной пути. После проверки точного absolute directory, файлов и отсутствия дочерних ресурсов использованы literal-path Remove-Item для двух credentials и пустого каталога; cleanup завершён без recursive удаления.
9. Первая полная encoding-проверка ошибочно требовала отсутствие BOM у generated файлов. EF tooling создал шесть migrations/designer/snapshot файлов с UTF-8 BOM/CRLF; они сохранены штатными без ручной нормализации. Повторная проверка различает generated и ручные UTF-8/LF файлы.
10. Первая проверка ссылок не декодировала существующие %20 в путях и выдала четыре ложных finding. После URI decoding все локальные ссылки полного manifest проходят; исходные ссылки не менялись.

## Очистка собственных ресурсов

Создан только контейнер `abverify-stage21-77d30b45e0e3483682e033d5a0a6b760` с одноимённой stage21 label, ID 576daa5be8e95fec9561cc0a6cbd0acaa5074582d62219b929005d4bd3b328e2, port **127.0.0.1:56863→5432**, tmpfs /var/lib/postgresql:rw,size=1073741824 и без volumes. Скачанный postgres:18 digest **sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722** отсутствовал до задачи.

После тестов fixtures удалили свои SQLite directories/PostgreSQL DB. Проверены точные name/label/port/mounts и отсутствие чужих containers этого image. Выполнены stop/rm только собственного контейнера и image rm только собственного newly pulled postgres:18. Затем удалены только run.json/container.env и проверенный пустой stage21 resource directory. Контейнер/image/directory отсутствуют; credentials не печатались. TRX и compile outputs сохранены как ignored evidence; существующий Docker Desktop не останавливался.

## Пропуски и границы сдачи

**Пропущено по указанию пользователя:** real HTTP/codex-lb/OpenAI, hosting/application/dev server/Telegram, рабочие БД/SQL, SQL Server/MySQL, remote Git, этапы22–25. DB checks выполнялись только на собственных разрешённых SQLite/PostgreSQL. Соседи не изменялись; новых пакетов/инструментов не устанавливалось. Исторический запрет add/commit действовал до приёмки; после неё координатор разрешил локальный коммит ровно78 файлов manifest.

**Недоступная проверка:** OpenSpec CLI не найден, CLI validation не выполнена. Main/delta/context синхронизированы и просмотрены статически; это не называется CLI validation. Change не архивирован.

Ручные правки — apply_patch. Полные78 файлов проверены строгим UTF-8 decoder: ручные файлы без BOM, LF; шесть generated migration/designer/snapshot файлов сохраняют штатный UTF-8 BOM/CRLF. U+FFFD, четыре вопросительных знака и mojibake не обнаружены; локальные ссылки проверены. Git diff --check проходит; предупреждения core.autocrlf не являются изменением файлов, git config не менялся.

## Приёмка и передача

Координатор принял этап21 после проверки actual code/docs, TRX265 core +171 isolated +43 transport +46 real DB, точного manifest78 (Compare-Object пуст), diff check, пустого index и отсутствия ресурсов21. По исходному поручению пользователя и приёмке разрешён непустой локальный English Conventional Commit ровно78 файлов manifest, без artifacts/secrets. Parent должен быть `95c28fae3773efa9e8e4d0281282340f5ac9e6c0`; full hash, parent, состав и post-commit staged/tree состояние передаются координатору после коммита. Следующий этап этому исполнителю не поручен.

## Полный manifest — 78 файлов

Включает modified и untracked, generated designers/snapshots и этот отчёт. Только перечисленные файлы относятся к этапу21; artifacts/credentials не входят.

```text
AGENTS.md
Application/AGENTS.md
Application/AgentRunSession.cs
Application/AgentRunner.cs
Application/AgentSettingsService.cs
Application/ContextBuilder.cs
Application/ContextModelGuard.cs
Application/Models/AgentRunRequest.cs
Application/Models/AgentSettingsSnapshot.cs
Application/Models/ContextModelSource.cs
Application/Models/DialogModelSelection.cs
Application/Models/DialogSnapshot.cs
Application/Models/DialogStatus.cs
Application/Models/StoredDialogContext.cs
Application/Models/StoredDialogTurn.cs
Application/Models/TurnModelSettings.cs
Application/Ports/IContextContentInspector.cs
Application/Ports/IContextModelCompatibility.cs
Application/Ports/IDialogContextWriter.cs
Application/Ports/IDialogSettingsWriter.cs
Application/Ports/IDialogTurnWriter.cs
Configuration/AGENTS.md
Configuration/AgentBridgeRunnerExtensions.cs
Configuration/AgentBridgeSettingsExtensions.cs
Documentation/Business logic/05-application-configuration.md
Documentation/Business logic/06-models-and-status.md
Documentation/Plans/AgentBridge Initial Implementation/21-settings-and-dialog-status.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/README.md
Documentation/Technical documentation/07-tokenizer-and-settings.md
Documentation/Technical documentation/21-settings-and-dialog-status.md
Documentation/Technical documentation/README.md
README.md
Tokenization/AGENTS.md
Tokenization/ContextTokenCounter.cs
adapters/AgentBridge.Persistence.EfCore/AGENTS.md
adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs
adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs
adapters/AgentBridge.Persistence.EfCore/Mapping/TurnSettingsMapping.cs
adapters/AgentBridge.Persistence.EfCore/Models/DialogContextRecord.cs
adapters/AgentBridge.Persistence.EfCore/Models/DialogSettingsRecord.cs
adapters/AgentBridge.Persistence.EfCore/Models/DialogTurnRecord.cs
adapters/AgentBridge.Persistence.EfCore/Reading/DialogReader.cs
adapters/AgentBridge.Persistence.EfCore/Repositories/SettingsRecordQueries.cs
adapters/AgentBridge.Persistence.EfCore/UnitOfWork/AGENTS.md
adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogContextUnitOfWork.cs
adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogSettingsUnitOfWork.cs
adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogTurnUnitOfWork.cs
adapters/AgentBridge.Persistence.EfCore/UnitOfWork/UnitOfWorkScope.cs
adapters/AgentBridge.Persistence.Migrations.PostgreSql/AGENTS.md
adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge/Persistence/Migrations/PostgreSql/Migrations/AgentBridgeDbContextModelSnapshot.cs
adapters/AgentBridge.Persistence.Migrations.PostgreSql/Migrations/20261004092121_AddDialogSettings.Designer.cs
adapters/AgentBridge.Persistence.Migrations.PostgreSql/Migrations/20261004092121_AddDialogSettings.cs
adapters/AgentBridge.Persistence.Migrations.Sqlite/AGENTS.md
adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge/Persistence/Migrations/Sqlite/Migrations/AgentBridgeDbContextModelSnapshot.cs
adapters/AgentBridge.Persistence.Migrations.Sqlite/Migrations/20261004092118_AddDialogSettings.Designer.cs
adapters/AgentBridge.Persistence.Migrations.Sqlite/Migrations/20261004092118_AddDialogSettings.cs
agent-bridge.slnx
openspec/changes/dialog-settings-and-status/context.md
openspec/changes/dialog-settings-and-status/proposal.md
openspec/changes/dialog-settings-and-status/specs/agent-runtime/spec.md
openspec/changes/dialog-settings-and-status/tasks.md
openspec/specs/agent-runtime/context.md
openspec/specs/agent-runtime/spec.md
tests/AGENTS.md
tests/AgentBridge.CodexLb.Tests/ModelCatalogTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/DialogReaderTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/DialogSettingsMappingTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/FakeSettingsByIdRepository.cs
tests/AgentBridge.Persistence.EfCore.Tests/FakeWriteFixture.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md
tests/AgentBridge.Persistence.EfCore.Tests/Integration/DialogSettingsIntegrationTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/IntegrationDatabase.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/MaintenanceIntegrationTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/PersistenceModelTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/ProviderDesignTimeTests.cs
tests/AgentBridge.Tests/AgentRunnerTests.cs
tests/AgentBridge.Tests/AgentSettingsTests.cs
```
