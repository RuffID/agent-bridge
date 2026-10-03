# 11 — Первые миграции провайдеров

Статус: **Реализован и принят; запрещённые проверки пропущены**. Подготовка ранее проверена и принята; пользователь разрешил обе команды генерации. Generated артефакты обоих providers созданы и проверены статически и через metadata, без применения к БД. Зависимости: **08, 10**.

## Цель

Создать проверенные артефакты схемы для выбранных провайдеров SQLite/PostgreSQL.

## Задачи

- [x] Определить целевой и startup-проекты, design-time контекст и совместимые с провайдером EF-инструменты.
- [x] Подготовить две отдельные library assemblies/factories, runtime выбор identity, solution и изолированные metadata проверки.
- [x] Согласовать точные команды генерации и соблюдать требования разрешений перед запуском: пользователь «Разрешаю обе команды генерации».
- [x] Сгенерировать нужные артефакты миграций инструментами, не редактируя snapshot вручную.
- [x] Проверить исходную metadata модели: собственные таблицы, FK/cascade, expiry/Id index, BINARY/C, UTC ticks и fixed-field/concurrency guards. Owner-list index не требуется текущим портам чтения по ID.
- [x] Проверить generated Up/Down, designer и snapshot обоих providers как единый набор: статически и сравнением relational models без SQL/Up/Down исполнения.
- [x] Ограничить design-time модель пятью собственными таблицами AgentBridge, без моделей приложения.

## Реализованная подготовка

Общий `AgentBridgeDbContext` и прежний mapping не изменены. `AgentBridgeMigrationsAssemblies.SQLITE/POSTGRESQL` задаёт identities; `AddAgentBridgePersistence` выбирает их по provider. Два новых проекта ссылаются на общий EF-адаптер, обратной зависимости нет. Приложению для обслуживания потребуется выбранная migrations DLL. Startup этапа 12 не реализован.

| Provider | Target и startup project | Factory |
| --- | --- | --- |
| SQLite | `adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj` | `SqliteAgentBridgeDbContextFactory` |
| PostgreSQL | `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj` | `PostgreSqlAgentBridgeDbContextFactory` |

CLI workflow: dotnet-ef **10.0.11**, Design/EF/Relational/SQLite **10.0.11**, Npgsql provider **10.0.3**. Design PrivateAssets=all только в target/startup projects; Tools/PMC не используются. Factory создаёт только options/context/model с синтетическим подключением, без application configuration, host или открытия соединения; forwarded args отклоняются. GenerateRuntimeConfigurationFiles=true готовит library startup для tooling.

`DialogRecord` сохраняет отдельный persisted incarnation/revision/owner/fixed dates/contentBytes. Composite keys и каскады, полный response/compact, все prior versions и история сохранены. OwnerId без trim/casefold/MaxLength; BINARY/C. Время — UTC ticks INTEGER/bigint; root index ExpiresAtUtc/Id. Local Domain lifetime и persisted token не смешиваются. Generated schema согласована с текущей relational metadata; enforcement на БД не проверен.

## Фактические проверки

Все команды выполнялись из `D:\Media\User\source\repos\agent-bridge`, ветка `master`. Перед restore/build проверены применимые AGENTS, project/ancestor Directory.Build/Directory.Packages/NuGet/lock, actual EFCoreLibrary csproj/API и package-generated imports. Custom executable hooks не обнаружены; пользовательские scripts не запускались. Outputs — `artifacts/compile-check` каждого проекта. Существующих migrations/designer/snapshot до подготовки не обнаружено.

```powershell
dotnet restore tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -p:GeneratePackageOnBuild=false -p:BaseOutputPath=./artifacts/compile-check/
dotnet build adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet build adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
```

На подготовке restore успешен; три builds — **0 warnings / 0 errors** (общий изменённый EF-адаптер собран по ProjectReference). Тесты подготовки — **112 passed / 0 failed / 0 skipped**, включая **4 новых**. Factory/runtime модели и assembly совпадают; только пять собственных таблиц, ticks/collation/guards/cascade и root index сохранены. Args не раскрываются в отказе. Tests не открывают connection и не исполняют Up/Down.

Для output metadata отдельно выполнены только MSBuild evaluations (без targets/restore/build): при временном `$env:BaseOutputPath = './artifacts/compile-check/'` следующие команды вернули TargetPath в уже собранные outputs; DLL/deps.json/runtimeconfig.json существуют для обоих providers. Прежнее environment значение восстановлено в finally.

```powershell
dotnet msbuild adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj -p:Configuration=Debug -p:GeneratePackageOnBuild=false -getProperty:TargetPath,OutputPath,AssemblyName,TargetFramework,ProjectAssetsFile,GenerateRuntimeConfigurationFiles
dotnet msbuild adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj -p:Configuration=Debug -p:GeneratePackageOnBuild=false -getProperty:TargetPath,OutputPath,AssemblyName,TargetFramework,ProjectAssetsFile,GenerateRuntimeConfigurationFiles
```

## Согласованные и выполненные команды генерации

Пользователь прямо разрешил обе команды: **«Разрешаю обе команды генерации»**. Выполнен ровно следующий блок из указанного cwd, строго **SQLite → PostgreSQL**, однократно. Обе команды вернули **Done**, общий exit code **0**; environment восстановлен в finally. Штатный CLI tool **10.0.11** вызван по конкретной установленной DLL без install/update; `--no-build` сохранил проверенные outputs и не запускал Build/restore/pack.

```powershell
Set-Location -LiteralPath 'D:\Media\User\source\repos\agent-bridge'
$stage11PreviousBaseOutputPath = $env:BaseOutputPath
try {
    $env:BaseOutputPath = './artifacts/compile-check/'
    dotnet 'C:\Users\Spike\.dotnet\tools\.store\dotnet-ef\10.0.11\dotnet-ef\10.0.11\tools\net8.0\any\dotnet-ef.dll' migrations add InitialAgentBridgeSchema --project adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj --startup-project adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj --context AgentBridge.Persistence.EfCore.AgentBridgeDbContext --configuration Debug --framework net10.0 --no-build --output-dir Migrations --namespace AgentBridge.Persistence.Migrations.Sqlite.Migrations
    if ($LASTEXITCODE -ne 0) { throw 'SQLite migration generation failed.' }
    dotnet 'C:\Users\Spike\.dotnet\tools\.store\dotnet-ef\10.0.11\dotnet-ef\10.0.11\tools\net8.0\any\dotnet-ef.dll' migrations add InitialAgentBridgeSchema --project adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj --startup-project adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj --context AgentBridge.Persistence.EfCore.AgentBridgeDbContext --configuration Debug --framework net10.0 --no-build --output-dir Migrations --namespace AgentBridge.Persistence.Migrations.PostgreSql.Migrations
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL migration generation failed.' }
} finally {
    $env:BaseOutputPath = $stage11PreviousBaseOutputPath
}
```

Tooling получает project metadata через штатную MSBuild property evaluation; environment BaseOutputPath наследуется и указывает на проверенный `artifacts/compile-check/Debug/net10.0`, а не default bin. `--no-build` исключает Build/restore/pack, поэтому `GeneratePackageOnBuild=true` соседней EFCoreLibrary не исполняется. Все реально выполненные restore/build/test выше имеют явный `-p:GeneratePackageOnBuild=false`. Не копировать outputs вручную и не запускать harness. Если исходники изменятся после проверки, сначала требуется разрешённый адресный compile-check с теми же properties.

Фактические migration IDs: **SQLite `20261003155233_InitialAgentBridgeSchema`**, **PostgreSQL `20261003155235_InitialAgentBridgeSchema`**. Созданы по migration/designer в `Migrations/`. Tooling разместил snapshots в `AgentBridge/Persistence/Migrations/Sqlite/Migrations/` и `AgentBridge/Persistence/Migrations/PostgreSql/Migrations/` внутри соответствующих проектов. Это фактический namespace-based output, а не ранее ожидаемый `Migrations/` snapshot path; generated файлы не перемещались и не редактировались вручную. Оба snapshots обнаруживаются штатным IMigrationsAssembly общего контекста.

## Проверки после генерации

Up/Down, designer и snapshot каждого provider прочитаны вместе. В обоих Up ровно пять собственных таблиц, четыре required cascade FK и три индекса; иных таблиц, seed/backfill, внешних schemas и SQL operations нет. Down удаляет CanonicalItems, DialogContexts, ModelSteps, затем DialogTurns и Dialogs — зависимости удаляются до родителей. Методы Up/Down не вызывались и не применялись.

- Keys: Dialogs(Id), DialogTurns(DialogId, Id), CanonicalItems(DialogId, TurnId, Sequence), ModelSteps(DialogId, TurnId, Id), DialogContexts(DialogId, Version).
- FK детей turn включают DialogId/TurnId и ссылаются на DialogTurns(DialogId, Id); turn/context ссылаются на Dialogs(Id). Все четыре — Cascade.
- Индексы: ExpiresAtUtc/Id; unique DialogId/Sequence у turn; unique DialogId/TurnId/Sequence у step. Owner index отсутствует.
- Все 20 исходных check constraints сохранены: root revision/content/time/owner/identity; turn sequence/status/finished; item sequence; step sequence/response version/status/error; context version/prefix/completed/response checks.
- SQLite Guid — TEXT, UTC ticks — INTEGER, OwnerId BINARY; PostgreSQL Guid — uuid, UTC ticks — bigint, OwnerId C. INTEGER/integer status/error/version; payload text без нормализации, output required, envelope/continuation/error nullable. В response/compact сохранены все семь колонок. ID/порядок не получают автогенерацию из provider identity defaults.

Designer и snapshot model bodies текстуально совпадают в каждой сборке. Новые две изолированные проверки GeneratedSnapshotAndDesignerMatchCurrentSchema обнаруживают единственную migration и snapshot через IMigrationsAssembly и сравнивают initialized relational models с текущей model через IMigrationsModelDiffer: **различий нет**. Migration.TargetModel создаёт только metadata; UpOperations/DownOperations, SQL generator, соединения и host не используются. Fixed-field AfterSaveBehavior остаётся runtime metadata общего mapping; snapshot/DDL не объявляется enforcement этого правила или атомарных guards.

После повторной проверки неизменённых csproj/ancestor и актуальных generated package imports выполнены те же три build команды provider/test проектов и test команда выше, без restore. **Три builds: 0 warnings / 0 errors; 114 passed / 0 failed / 0 skipped**, включая **6 новых** за этап (4 preparation + 2 generated consistency). Соседняя EFCoreLibrary не упаковывалась. Генерации повторно не запускались.

## Изоляция служебной истории по ревью

Runtime и обе factories используют единый AgentBridgeMigrationsHistory.TABLE_NAME = `__AgentBridgeMigrationsHistory` через явный MigrationsHistoryTable. Default `__EFMigrationsHistory` приложения не используется. Пять mapped таблиц остаются прежними; отдельная служебная история EF не является шестой persistence entity и не входит в generated Up/snapshot. Ею управляет штатный provider history repository при явном обслуживании; таблица здесь не создавалась и не читалась.

После изменения повторены ровно три concrete builds выше с GeneratePackageOnBuild=false: **0 warnings / 0 errors**. Запущены только адресные factory/history/snapshot проверки, без широкого повторения:

```powershell
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~ProviderDesignTimeTests
```

**6 passed / 0 failed / 0 skipped**. Runtime/factory options содержат точное отдельное имя/null schema; реальные SQLite/PostgreSQL IHistoryRepository metadata разрешаются без DB/SQL методов. Model diff остаётся пустым. Результат полного набора **114** выше получен до history isolation; широкий набор после неё не повторялся по указанию пользователя. Все шесть generated artifacts неизменны; повторная генерация не выполнялась.

## Точные изменённые файлы

- `AGENTS.md`
- `README.md`
- `agent-bridge.slnx`
- `adapters/AgentBridge.Persistence.EfCore/AGENTS.md`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/AgentBridgeMigrationsAssemblies.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/AgentBridgeMigrationsHistory.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs`
- `adapters/AgentBridge.Persistence.Migrations.Sqlite/AGENTS.md`
- `adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj`
- `adapters/AgentBridge.Persistence.Migrations.Sqlite/SqliteAgentBridgeDbContextFactory.cs`
- `adapters/AgentBridge.Persistence.Migrations.Sqlite/Migrations/20261003155233_InitialAgentBridgeSchema.cs`
- `adapters/AgentBridge.Persistence.Migrations.Sqlite/Migrations/20261003155233_InitialAgentBridgeSchema.Designer.cs`
- `adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge/Persistence/Migrations/Sqlite/Migrations/AgentBridgeDbContextModelSnapshot.cs`
- `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AGENTS.md`
- `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj`
- `adapters/AgentBridge.Persistence.Migrations.PostgreSql/PostgreSqlAgentBridgeDbContextFactory.cs`
- `adapters/AgentBridge.Persistence.Migrations.PostgreSql/Migrations/20261003155235_InitialAgentBridgeSchema.cs`
- `adapters/AgentBridge.Persistence.Migrations.PostgreSql/Migrations/20261003155235_InitialAgentBridgeSchema.Designer.cs`
- `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge/Persistence/Migrations/PostgreSql/Migrations/AgentBridgeDbContextModelSnapshot.cs`
- `tests/AGENTS.md`
- `tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj`
- `tests/AgentBridge.Persistence.EfCore.Tests/ProviderDesignTimeTests.cs`
- `Documentation/README.md`
- `Documentation/Technical documentation/README.md`
- `Documentation/Technical documentation/11-provider-migrations.md`
- `Documentation/Plans/AgentBridge Initial Implementation/README.md`
- `Documentation/Plans/AgentBridge Initial Implementation/11-initial-provider-migrations.md`
- `openspec/specs/agent-runtime/spec.md`
- `openspec/specs/agent-runtime/context.md`
- `openspec/changes/initial-provider-migrations/proposal.md`
- `openspec/changes/initial-provider-migrations/tasks.md`
- `openspec/changes/initial-provider-migrations/context.md`
- `openspec/changes/initial-provider-migrations/specs/agent-runtime/spec.md`

Всего 33 файла: 26 preparation файлов, 6 generated artifacts и AgentBridgeMigrationsHistory.cs. Индивидуальный список совпадает с git status. Строгие UTF8/XML и manual source LF/BOM проверки прошли; U+FFFD/mojibake/четыре вопросительных знака не обнаружены, git diff --check без ошибок. SHA256 всех шести generated файлов совпадают с зафиксированными после генерации; generated encoding/EOL сохранены как выдал tooling. Domain/Application, common DbContext mapping, соседние библиотеки не изменены. Подготовка ранее принята координатором; полный этап 11 проверен и принят координатором. Разрешён локальный коммит только перечисленных 33 файлов; fullhash сообщается отдельно после коммита.

## Проверка и завершение

Generated артефакты и compile-check выполнены в разрешённом объёме; этап проверен и принят координатором. БД, SQL script, Up/Down execution/apply/rollback, hosting, restart, реальные backup/native/dump processes — **Пропущено по указанию пользователя**; это не 0 skipped test runner. Metadata/fakes не доказывают relational enforcement, реальную атомарность/locking или restart. OpenSpec CLI отсутствует в PATH: CLI validation не выполнялась, change не архивирован. Stage 12 не выполнялся.

Источник: [обслуживание БД](<../../Technical documentation/06-database-maintenance.md>).

Фактический API: [provider migrations](<../../Technical documentation/11-provider-migrations.md>), [нормативные требования](../../../openspec/specs/agent-runtime/spec.md).
