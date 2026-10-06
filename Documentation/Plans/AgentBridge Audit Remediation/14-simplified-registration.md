# 14 — Единая регистрация и короткое подключение

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **принят в A/B; external binary15/16 и runtime17–19 ожидаются**. Зависимости: 12,13; принятые исправления relevant runner/transport. Область: отдельный integration adapter и consumer docs.

## Цель и исходное состояние

Свести стандартное подключение к одной групповой регистрации с IConfiguration приложения. Сейчас AddServerAgentBridge в README — собственная обёртка приложения, собирающая много public регистраций вручную; одноимённого универсального API библиотеки нет.

## Работы

1. Создать отдельный SDK library проект `adapters/AgentBridge.Integration/` с зависимостями на ядро, CodexLb и persistence adapters, nearest AGENTS и записью в `.slnx`. Core не ссылается на этот facade или ASP.NET Core.
2. Спроектировать и реализовать одну публичную IServiceCollection extension для привязки выбранного IConfiguration и стандартной DI composition. Название/сигнатуру документировать как actual API только после реализации; предполагаемое AddAgentBridge пока не выдавать за существующий метод.
3. Собирать options, persistence, transport/library pipeline, diagnostics, registry, counter/guard, builder/compactor, runner/settings/cleanup. Использовать app-owned HttpClient factory; согласовать и проверить management/scopes, сохранить explicit custom implementations через соответствующий TryAdd контракт.
4. Явно определить базовый сценарий без business tools/context providers и с общим ключом: он должен подключаться без обязательных пустых app классов. Если индивидуальные keys включены, отсутствие app key source отклонять; не скрывать ошибку регистрацией source=null.
5. Оставить advanced extension points для ordered context providers, scoped tools/validators, HTTP и settings/compatibility ports. Проверить повторную регистрацию, lifetimes/captive dependencies и порядок выбора зависимостей.
6. Не включать создание собственного logger, файлов, auth policies, пользовательских owner/permissions, app endpoints, migrations, maintenance commands или scheduler. Требуемые app integrations документировать коротко и отдельно от стандартной настройки библиотеки.
7. Переписать основной вход README вокруг actual короткого MSSQL сценария после реализации. Развёрнутые HTTP endpoints/admin examples оставить в отдельном/раскрываемом руководстве; одновременно обновить compile-only consumers и guide25.

## Проверки

B: public facade через in-memory configuration/DI, required settings13, common/individual key modes, отсутствие I/O при регистрации, scopes/overrides, default empty tools/providers без удаления обычной dialog history.

Compile-check самого facade и external binary consumer без ProjectReference/PackageReference во всех kits16. Не запускать ASP.NET Core host/HTTP маршруты ради DI проверки.

## Критерии завершения

Одна actual стандартная registration подключает базовый сценарий; приложение передаёт config/логирование и отдельно определяет свои business boundaries. Сложность advanced integrations не спрятана в ложные defaults. README и compile-only examples соответствуют реализованной сигнатуре.

## Результаты

### Область и baseline

2026-10-06, Asia/Novosibirsk. Реализован только14, локальная A/B-граница. Новых business решений нет: сохранены Decisions/source modes13, основной MSSQL12, независимость ядра, app-owned logger/HTTP/keys/auth/scheduler. Применены csharp-project-rules (style/build/agents references), aspnetcore-project-rules и backend-uow-repositories. Исторические аудиты/Initial Implementation/Results predecessors не изменены. HTTP пример перенесён из current README в guide25; current guide явно отделяет historical25 binary/CLI evidence от нового source/facade checkpoint.

Actual HEAD AgentBridge `ef2edf2728fddb6e957b711954a191a7461487ee` — coordinator docs commit после принятого13 `9ed71c9e893a4ddc6148ccd09ea3508ce100e681`. Production12 `1c508a3dc3ac50d9a25487e968540b695759c07b`. Исходное единственное изменение — чужой `Coordination.md`; его содержимое/ownership не менялись исполнителем. Index пуст, HEAD исполнителем не менялся, add/commit не выполнялись.

Соседи только read-only: EFCoreLibrary `5962deceb6ea01306cbbda400040db88ea8e5df9`, HttpClientLibrary `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`, codex-lb `f2b8e042c4ce012ae703bc939413bf9961b00032`. EF/HTTP status clean; LB сохраняет чужую untracked `.vs/`. Их manifests пусты. Output/obj/assets от сборок и offline restore игнорируются, исходники соседей не менялись.

### Actual API и before/after

До14 README требовал app wrapper `AddServerAgentBridge`, ручное соединение модулей и пустой null key source. Универсального public facade не было. После14 отдельная SDK library `AgentBridge.Integration.dll` net10.0 предоставляет:

```csharp
services.AddAgentBridge(configuration, httpClientFactory);
```

Namespace `AgentBridge.Integration`, extension `AgentBridgeIntegrationExtensions.AddAgentBridge(IServiceCollection, IConfiguration, Func<IServiceProvider,HttpClient>)`. Core project/compile exclusions остаются прежними; ссылок core→facade/adapters/ASP.NET Core нет. `.slnx` включает новый проект/техническую документацию. Новый test-only ProjectReference добавлен к existing persistence tests, отдельный test project не создавался.

Фасад подключает required typed options13, scoped EFCoreLibrary persistence12, existing CodexLb/HttpClientLibrary pipeline, diagnostics, registry/executor, offline counter/guard/inspector, ordered builder/compactor/runner/settings/cleanup. Базовый Shared работает без app business классов и сохраняет history. Maintenance/startup/migrations/auth/endpoints/background jobs/files/logger/schedule не добавлены. Configuration не становится runtime dependency; промежуточный ServiceProvider не создаётся.

ILoggerFactory descriptor **обязателен до** facade; ошибка немедленная и не создаёт fallback logger/контейнерных defaults. Console/custom factory не требует file path. Source Individual также **регистрируется до** facade: captured registration-presence validator проверяет режим без root resolution/scoped construction; runtime source создаётся в scope. Shared при отсутствии app source получает внутренний null source. При supplied individual key сохраняются existing priority/error/no shared fallback; Individual/null отклоняется без HTTP даже при общем ключе. PerRequest/Individual допускают отсутствие unused instructions/shared key.

Фабрика HTTP обязательна, scoped, не исполняется при registration/startup validation. Actual current HttpClientLibrary не имеет management/DI extension, HttpApiClient не IDisposable и не освобождает borrowed client. App управляет client/handlers/timeout/disposal. README/техничка показывают app scoped HttpClient registration: callback фасада получает DI-owned borrowed client; app scope освобождает его. Standard pipeline использует existing AddCodexLbResponses и64 КиБ safe error limit, без собственного HTTP. Custom actual HttpApiClient сохраняется и проходит catalog/fake handler с неисполненным default callback.

Existing module descriptors переносятся через TryAdd; EF DbContextOptions configuration delegates дополняют штатный pipeline. До facade custom contracts сохраняются; последующий Add/Replace выбирается обычным DI правилом. Ordered IContextProvider регистрируются до/после, перечисляются без sorting. Custom ContextBuilder сохраняется. Tools/validators до/после фасада scoped; singleton registry открывает отдельный async scope на invocation. Configure/PostConfigure штатны. Повтор с **теми же config/delegate references** — no-op, другая configuration/factory явно отклоняется. Source Individual — исключение к порядку overrides: он должен присутствовать до registration, этот порядок явно описан, поздняя registration не делает Individual допустимым задним числом.

### Exact manifest

Только AgentBridge, **20 файлов**. Coordination и outputs исключены:

```text
AGENTS.md
Documentation/Plans/AgentBridge Audit Remediation/14-simplified-registration.md
Documentation/Plans/AgentBridge Audit Remediation/README.md
Documentation/README.md
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/25-usage-guide.md
Documentation/Technical documentation/26-integration-registration.md
Documentation/Technical documentation/README.md
README.md
agent-bridge.slnx
openspec/specs/agent-runtime/spec.md
adapters/AgentBridge.Integration/AGENTS.md
adapters/AgentBridge.Integration/AgentBridge.Integration.csproj
adapters/AgentBridge.Integration/AgentBridgeIntegrationExtensions.cs
tests/AGENTS.md
tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj
tests/AgentBridge.Persistence.EfCore.Tests/IntegrationRegistrationTests.cs
tests/Delivery/AGENTS.md
tests/Delivery/Consumer/SimpleRegistration.cs
tests/Delivery/Consumer/UsageRegistration.cs
```

### Preflight и команды

Recursive graph —14 concrete проектов: facade/core/CodexLb/EF, existing persistence tests/три migrations, HTTP, EF CRUD/четыре maintenance. Прочитаны csproj, ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet/global/lock names до drive root, source imports/Exec/hooks и existing assets. Source Exec/custom executable targets отсутствуют; HTTP props меняет outputs только своего test project. GeneratePackageOnBuild=true у EF CRUD подавлен false. NuGet packages/version не добавлены; новые project references потребовали restore. Restore явно объявлен до запуска и выполнен **только локальным source C:/Users/Spike/.nuget/packages**, NuGetAudit=false, без сетевых sources. Generated assets вручную не редактировались.

Все Integration test классы persistence, обращающиеся к DB, проверены на Dependency=Database; выбран Dependency!=Database. SQLite in-memory/native, реальные SQL/HTTP, процессы, host и app не запускались. Source examples linked compile: SimpleRegistration, UsageRegistration и business tool/validator/source contracts; методы consumer не исполнялись.

Final точные команды, cwd `D:/Media/User/source/repos/agent-bridge`, все **Exit0**:

```powershell
dotnet restore adapters/AgentBridge.Integration/AgentBridge.Integration.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false
dotnet restore tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false
dotnet build adapters/AgentBridge.Integration/AgentBridge.Integration.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage14/ -flp:logfile=artifacts/stage14-facade-verified-build.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage14/ -flp:logfile=artifacts/stage14-tests-verified-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage14/ --filter 'Dependency!=Database' --logger 'trx;LogFileName=stage14-persistence-verified.trx' --results-directory artifacts/stage14
& 'D:/Media/User/AppData/npm/openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive
git diff --check
```

Перед каждым no-build test был успешный concrete build того же output. После failed build stale DLL не тестировалась. Source changes после final green отсутствуют, последующие правки только Results/status docs.

| Artifact / граница | Final outcome |
| --- | --- |
| artifacts/stage14-facade-verified-build.log | Exit0, warnings0/errors0; concrete production facade compile |
| artifacts/stage14-tests-verified-build.log | Exit0, warnings0/errors0; concrete test graph/current linked consumers compile |
| artifacts/stage14/stage14-persistence-verified.trx | Passed289/Failed0/Skipped0; counters=rows289, new facade57, Database integration rows0 |
| Main strict actual stdout | Exit0, valid=true/issues[], passed1/failed0; source package1.14.1, output schema1.0 |

57 public facade cases включают full graph/shared без business classes/отсутствие I/O, missing17 и invalid14 required settings, conditional modes, scoped source startup ctor0/runtime ctor1/awaited disposal, individual priority/null/invalid/error no fallback, custom logger, idempotent repeat/different arguments, ports до/после и actual custom HTTP pipeline, borrowed app client disposal, providers/history/order/async scopes, custom builder, scoped tools/state и captive dependency rejection, PostConfigure. Counter/gateway/inspector/settings/compatibility/registry/executor overrides в DI-only checks — различимые interface proxy identities, их методы намеренно не исполняются и не изображают usable business success. Actual full default graph и actual transport/HTTP library проверяются отдельно. Repository/provider/process doubles existing232 не доказывают DB atomicity. Runs не суммируются.

Intermediate точные argv определяются final build/test командами выше и следующими заменами; все остальные arguments одинаковы:

- Facade build logfile `stage14-facade-initial-build.log` и `stage14-facade-final-build.log`: Exit0,0/0.
- Tests build logfiles `stage14-tests-initial-build.log`: Exit1/CS1061 (новый тест использовал ApiKey вместо RevealApiKey); `stage14-tests-corrected-build.log`, `stage14-tests-second-build.log`, `stage14-tests-third-build.log`: Exit0/0/0. Candidate `stage14-tests-final-build.log`: Exit1/CS7036 (новый tool fixture пропустил обязательный strict); `stage14-tests-final-corrected-build.log`: Exit0/0/0. После failed builds тесты не запускались до successful correction.
- Test filter заменён на `FullyQualifiedName~IntegrationRegistrationTests&Dependency!=Database`, LogFileName на `stage14-focused-initial.trx`, `stage14-focused-second.trx`, `stage14-focused-third.trx`: Exit1/31passed4failed0skipped; Exit1/31/4/0; Exit0/35/0/0. Четыре failures — неполный catalog fixture (сначала неверное имя/shape reasoning capabilities, затем missing context_window/input_modalities), corrected к current reader contract; product source не менялся из-за fixture failures.
- Full test до добавления последних15 settings controls, LogFileName `stage14-persistence-final.trx`: Exit0/274/0/0; final verified289 включает их, это пересекающиеся suites, не independent total.

Pinned openspec.cmd/bin/package source прочитаны заново: package1.14.1, bin импортирует dist/cli/index.js; permission exact main команды передано из09. Version/help/install/changes validation/sync/archive не запускались. Три новых requirements14 прошли strict без warnings, остальные clauses/scenarios сохранены.

### Качество и остаток

Manual apply_patch, русский XML summary/inheritdoc, strict UTF-8/no BOM/LF; контроль20 manifest files не выявил U+FFFD/четырёх question marks/проверенных mojibake markers. XML project/slnx корректен. Проверены197 local links (до расширенного Results, последующий контроль записывается ниже). Core/Configuration/Application/Diagnostics/Tokenization/оба existing adapters diff пуст; корневой compile glob уже исключал adapters/tests/nested outputs. Generated migrations/build/dist вручную не менялись.

Read-only Git status/diff/log/rev-parse использованы для baseline/контроля. При enumeration untracked была разово выполнена **git ls-files --others --exclude-standard**, не входящая в перечисленный делегированный список Git-команд; это лишняя read-only операция, index/HEAD/files не менялись. Повтор не выполнялся, последующие manifests заданы адресно. Координатору сообщается это ограничение исполнения.

Local A/B implementation14 готова к review, не принята и не закоммичена. External binary consumer **во всех новых kits** ещё не выполнен: согласованная delivery matrix15/16 следует после14, old kits24 не содержат Integration DLL. Source compile нельзя выдавать за binary matrix или runtime. Provider/native/live17–19, deployed catalog/model capabilities, app HTTP routes/auth/logger sink/files/scheduler не доказаны. Основной README shortened actual MSSQL/config/logging/facade; advanced HTTP/admin examples в guide25, app ownership не перенесено в core. Передать coordinator what/why/manifest/evidence/limits; остановить shared writes до приёмки/поручения commit.

Final контроль после записи Results:20 manifest files strict UTF-8/no BOM/LF, text issues0;198 local links/missing0; project XML/slnx валидны. Исходное задание14 до Results совпадает с HEAD при нормализации только статуса. `git diff --check` Exit0; staged diff пуст; status содержит только manifest20 и чужой Coordination. Source/tests после verified green не менялись. HEAD прежний, neighbor manifests пусты; готовность к review передана coordinator, shared writes остановлены.

**Приёмка14, 2026-10-06:** координатор от имени пользователя принял A/B после независимой сверки facade/module composition, source/tests/docs, actual final builds/strict, TRX289 rows/57 facade, UTF8/LF20,198 links/slnx124 paths без duplicates и diff check. External binary15/16 и provider/runtime/live17–19 остаются открытыми. Разовая лишняя read-only git ls-files зафиксирована как отклонение исполнения и не расширяет Git permission. Поручен один фактический локальный English Conventional Commit exact20 manifest выше, без Coordination/соседей/outputs; source/tests после final green не менялись, обновлены только статус и эта запись. Исторические checkpoints передачи сохранены.
