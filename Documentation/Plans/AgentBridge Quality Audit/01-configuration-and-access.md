# 01 — Конфигурация, DI, ключи и выбор модели

Статус: **проверен с ограничениями**. Предпосылки: 00; схема ответственности приложения и библиотеки.

## Цель и вопросы

Проверить явность регистрации и границы прав, безопасные ошибки, настройку provider и приоритет модели/effort/ключа. Отделить defaults от бизнес-обязательств.

## Компоненты и зависимости

[Configuration](../../../Configuration), [CodexLbOptions](../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbOptions.cs), [CodexLbModelAccessResolver](../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelAccessResolver.cs), [CodexLbModelSettingsReader](../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelSettingsReader.cs), [ModelSelectionValidator](../../../Application/ModelSelectionValidator.cs), persistence/Configuration и [Diagnostics](../../../Diagnostics). Тесты ConfigurationTests, ModelSelectionTests, ModelCatalogTests, DiagnosticsTests.

## Способ проверки и границы

Проследить DI lifetimes и отсутствие I/O при регистрации; обязательные app factories, сохранение custom counter/inspector, отсутствие host. Проверить null, пустой/невалидный индивидуальный ключ, исключение источника, 401/403 и отсутствие shared fallback. Проверить exact ID/effort, неизвестный input budget, threshold+reserve ровно на границе и переполнение, отсутствие provider, неверные timeout/retention. Сверить override → сохранённый выбор → defaults и snapshot активного run. Проверить отсутствие секретов в settings/status, ошибках и диагностике, включая JsonStructure. Смена ключа между resolver и catalog не должна менять pinned access.

## Разрешения

A; существующие isolated DI/catalog/logger тесты — B, без живого HTTP. Источник ключей только синтетический. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Таблица регистраций и владельцев ресурсов, варианты выбора ключа/модели, safe-field перечень, ссылки на тесты отказов. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Все группы настроек и пути доступа имеют проверенный статический маршрут и результаты выбранных разрешённых проверок. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Доступность модели реальному аккаунту, полноту следующего контекста и runtime DI конкретного приложения.

## Результаты

### Предварительная запись координатора — сохранена как источник

Нижеследующая запись существовала до начала отдельного чата01. Она не является независимым результатом исполнителя и не подтверждает запуск DI/HTTP. Фактический отчёт отдельного чата находится в разделе «Независимая проверка этапа01» после этой записи; исходное задание выше сохранено.

### Состояние и источники

2026-10-05, уровень A, HEAD `41072fc37bb718dd9f4bb0473912802260bb9164`. Исходное дерево чистое; текущие изменения только Markdown аудита. [Baseline запуска и блокер чатов](Coordination.md). Из00 приняты ограничения CLI и внешних окружений. Применены root/local Configuration, Application, Diagnostics, adapter и tests AGENTS; csharp-project-rules. Ссылки ниже — текущие исходники, не результаты запуска.

### Вопросы и результаты

| Вопрос | Результат A | Доказательство, строки от корня | Ограничение |
| --- | --- | --- | --- |
| Локальные options и отсутствие I/O при регистрации | Binding/ValidateOnStart, операции не вызываются; range guards явные | [Core registration](../../../Configuration/AgentBridgeConfigurationExtensions.cs):12–69; DatabaseConfigurationExtensions.cs:29–34 | Фабрика приложения может делать I/O, её поведение не доказано |
| Lifetimes и custom services | Runner/compactor/settings/cleanup scoped; registry/executor/counter singleton; guard transient; TryAdd сохраняет custom counter/inspector | Configuration/AgentBridge*Extensions.cs; PersistenceRegistrationExtensions.cs:19–67 | Runtime DI потребителя не запускался |
| Provider и ресурсы | Provider nullable/обязателен, нет default SQLite; scoped context/base repos/UoW; app владеет HttpClient, host отсутствует | [PersistenceRegistrationExtensions](../../../adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs):19–67; CodexLbModelCatalogExtensions.cs:14–30 | Реальное соединение не открывалось |
| Null/пустой/ошибочный индивидуальный ключ | Только null выбирает shared; whitespace/control дают Validation; исключение источника распространяется | [Resolver](../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelAccessResolver.cs):15–30 | Upstream Bearer-validity не доказана |
| 401/403/no fallback и pinned key | Reader WithAccess не читает source повторно; tests содержат assertions одного вызова/Authorization | [SettingsReader](../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelSettingsReader.cs):29–60; ModelCatalogTests.cs:59–154 | Fake handler, тесты только прочитаны |
| Exact model/effort и бюджет | Ordinal ID/effort, положительный InputContextWindow; сумма threshold+reserve через long; равенство допустимо | [ModelSelectionValidator](../../../Application/ModelSelectionValidator.cs):14–36; ModelSelectionTests.cs:12–73 (36096/36095, overflow/null) | Это настройки, не полный запрос/поддержка tokenizer |
| Timeout, retention, invalid address | Timeout конечен в пределах timer; отсутствие provider/connection отклоняется; fixed expiry вычисляется отдельно | CodexLbConfigurationExtensions.cs:31–53; core registration:57–69; ConfigurationTests.cs:161 | Достижимость таймера/максимальных дат не исполнялась |
| Safe fields и diagnostics | Snapshot/status не содержат key; logger закрытые поля Operation/IDs/Status/ErrorCode/DurationMs без Exception | [DiagnosticOperation](../../../Diagnostics/AgentBridgeDiagnosticOperation.cs):45–80; Application/Models/AgentSettingsSnapshot.cs, DialogStatus.cs | Ambient logging приложения вне гарантии; JsonStructure dependency подробно06 |
| Override → saved → defaults / active snapshot | Reader фиксирует primitives до I/O и pinned access; saved selection/orchestration вынесены в11–12 | SettingsReader.cs:22–47; Application/AGENTS.md | Этап01 не подтверждает atomic persisted snapshot |

### Находки, вопросы и непроведённые проверки

Новых подтверждённых дефектов или отдельных кандидатов нет. Существующие [ABQA-Q-002](OpenQuestions.md#abqa-q-002) и [ABQA-003](Findings.md#abqa-003) остаются открыты. Утверждение об отсутствии секретов ограничено рассмотренными DTO/logger путями, не всеми sinks приложения.

Не запускались ConfigurationTests, ModelSelectionTests, ModelCatalogTests, DiagnosticsTests и runtime DI. Причина — запрет B; нужны .NET10 SDK, локально восстановленные пакеты, отдельное разрешение на конкретные существующие csproj/фильтры без restore/host/HTTP. Будущий сценарий: Resolve→смена source→ReadWithAccess с fake handler, проверки 401/403 и safe logger; исходный метод `PinnedAccessSettingsUseActualLibraryWithoutResolvingSourceAgain` уже существует. Точная команда должна учитывать актуальные build/import-файлы, поэтому здесь не объявляется готовой к запуску.

### Вывод и передача

**Проверен с ограничениями**. Статические ветви выбора и регистрации согласованы с spec:304–333,628–650,686–693; исполнение/реальные права не подтверждены. Этап02 получает fixed expiry, explicit owner и различие settings snapshot/history token; этапы06/11/12 проверяют оставшиеся транспортные и persisted границы.

### Независимая проверка этапа01

#### Состояние и границы

Дата: **2026-10-05, Asia/Novosibirsk**. Исполнитель: чат01 `01a10caa-f2c4-7091-8fab-7623a5b3ad64`; cwd `D:/Media/User/source/repos/agent-bridge`. Выполнен только **A**: исходники, требования, инструкции, существующие tests/evidence, ссылки и read-only Git. Методы библиотеки/тестов не исполнялись.

| Репозиторий | Проверенный HEAD | Исходное состояние отдельного чата01 |
| --- | --- | --- |
| AgentBridge | `41072fc37bb718dd9f4bb0473912802260bb9164` | Чужие Markdown-правки00/01/Findings/Methodology/README и untracked Coordination/OpenQuestions; index пуст, production/test/build изменений нет |
| EFCoreLibrary | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Status пуст, read-only |
| HttpClientLibrary | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Status пуст, read-only |

Исходный diff01 — предварительный отчёт/статус координатора,30 изменённых строк по diffstat; он и задание сохранены. [Coordination](Coordination.md) — baseline запуска, [Baseline](Baseline.md) — более ранняя подготовка. Прочитаны README/Methodology/Findings/OpenQuestions/Coordination и принятый [итог00](00-contract-baseline.md); ABQA-001/002/003/004 и Q-002/003 не переоценены без новых доказательств.

Прочитаны applicable root/Documentation/Plans/Configuration/Application/Diagnostics/CodexLb/Responses/Persistence/UnitOfWork/tests AGENTS, root обеих обязательных библиотек, HTTP Clients и EF maintenance ancestor/Extensions AGENTS. Применены csharp-project-rules, aspnetcore-project-rules, backend-uow-repositories с references/backend-uow.md. Прямой запрет B/C/D выше обычных разрешений скиллов. Дополнительные соседние проекты не потребовались; бизнес-политика не выбиралась.

#### Все группы вопросов

Нормативный источник — [main spec](../../../openspec/specs/agent-runtime/spec.md):304–333 catalog/keys/budget;520–558 retention;599–626 HTTP logging/errors;628–693 safe settings/selection/status;695–713 maintenance/backup. Пути ниже от корня AgentBridge; префиксы **HTTP/** и **EF/** означают корни HttpClientLibrary и EFCoreLibrary в D:/Media/User/source/repos/work. Строки проверены по текущим файлам. Результат A не является runtime pass.

| Вопрос | Исследовано | Результат A | Evidence: файл/символ/строки | Ограничение |
| --- | --- | --- | --- | --- |
| Options/defaults/обязательность | Все core/CodexLb/Database/Backup группы |8 steps,7 дней,10 МиБ,32000+4096 tokens,3 passes;medium/180s+180s переопределяемы. Model/address/provider/connection обязательны; backup retention без default | Configuration/AgentOptions:6–10, DialogRetentionOptions:6–10, ContextCompactionOptions:6–13; CodexLbOptions:6–22; DatabaseOptions:6–10, DatabaseBackupOptions:6–19 | Default effort не обещает поддержку |
| Binding/регистрация без I/O | Все core extensions, catalog/responses, persistence/maintenance и actual HTTP constructor | Options binding/ValidateOnStart; factories отложены. В просмотренных телах нет вызова HTTP/DB/agent/backup/scheduler | Configuration/AgentBridgeConfigurationExtensions:12–69; adapters/AgentBridge.CodexLb/Configuration/CodexLbModelCatalogExtensions:15–30; PersistenceRegistrationExtensions:16–67; DatabaseMaintenanceRegistrationExtensions:23–68; HTTP/Clients/HttpApiClient:35–55 | App factories/constructors могут делать I/O; app root неизвестен |
| Lifetimes/custom services | TryAdd/Add и constructors registry/executor | Scoped orchestration/EF/model services, singleton registry/counter/inspector, transient budget guard; TryAdd сохраняет custom. Counter и shape inspector — отдельные порты | Configuration/AgentBridgeTokenizationExtensions:13–18, AgentBridgeSettingsExtensions:13–20, AgentBridgeRunnerExtensions:13–20, AgentBridgeToolsExtensions:13–36; Application/ToolRegistry:10–24,31–48; ToolExecutor:12–22 | ValidateScopes/ValidateOnBuild не исполнялись |
| Provider/actual EF scope/ownership | Nullable provider, switch, adapter/base repositories/UoW, HTTP pipeline | Нет SQLite fallback; общий scoped IAppDbContext/репозитории/UoW, SensitiveDataLogging=false. HttpClient/handlers — app; request/response — pipeline | DatabaseConfigurationExtensions:29–34; PersistenceRegistrationExtensions:19–67; EF/Extensions/ServiceCollectionExtensions:13–32, EfCore/UnitOfWorkContext:7–20, EfDbContextAdapter:8–24; HTTP/Clients/HttpJsonResponseClient:20–30,55–56 | Без DB connection/transaction; stream disposal07 |
| Неверные ranges/timeouts/address/retention | Predicates, expiry arithmetic, Backup/ToolExecutionLimits | Положительные limits/period;reserve>=0,long sum<=int.MaxValue; timeout>0 и <=uint.MaxValue−1ms; HTTP(S) без userinfo/query/fragment. Non-UTC/overflow expiry fail-fast. Backup проверяет только форму/явные значения | Core extension:52–69; CodexLbConfigurationExtensions:31–53; DialogRetentionOptions.CalculateExpiresAtUtc:17–29; DatabaseBackupOptionsValidator:10–57; Application/Models/ToolExecutionLimits:7–19 | Не проверены файлы/pg_dump/timer runtime/backup |
| Null/empty/invalid individual/source error | ResolveAsync и source port | Только null выбирает shared; null обоих→Unauthorized; whitespace/control→Validation; source exception/caller cancellation распространяются без fallback | CodexLbModelAccessResolver.ResolveAsync:15–30; Application/Ports/IIndividualModelKeySource:5–9; tests/AgentBridge.CodexLb.Tests/ModelCatalogTests:73–125,328–337 | Формат не доказывает upstream-validity |
|401/403/no retry/canonical route | Catalog.ReadAsync/actual request factory | GET base-prefix+/v1/models, request-local Authorization, без client_version.401→Unauthorized,403→Forbidden,прочие→Rejected; fixed errors, без shared retry | CodexLbModelCatalog.ReadAsync:19–51; HTTP/Clients/HttpRequestMessageFactory:30–35,59–74; ModelCatalogTests.HttpRejectionIsSafeAndNeverRetries:129–154 | Fake handler не live routing; app handlers могут нарушить свой контракт |
| Source mutation/pinned access/cancel | Reader и узкий run route | ReadWithAccess не вызывает source повторно; primitives capture до I/O. Typed failure раньше late-cancel, success проверяет cancel | CodexLbModelSettingsReader:17–60; Application/AgentRunner:57–62,84–85,105; ModelCatalogTests:59–69,350–383; Application/Ports/IModelSettingsReader:14–19 | Legacy reader→Unsupported, live rotation06 |
| Exact model/effort/динамика/snapshots | JSON shape/metadata/duplicates и validator | Exact string ID, Ordinal effort; каждый read новый каталог, empty не fallback. Unknown metadata/API/input budget→Unsupported; unknown raw fields не копируются, lists read-only copies | ModelCatalogJsonReader:10–62,76–135; Application/ModelSelectionValidator:14–36; Models/ModelCapabilities:7–27; ModelCatalogTests:159–260,282–307 | Live schema/exact IDs06/Q-002; capabilities не Responses/tokenizer proof |
| Equality/overflow/unknown budget | Long addition и nullable input window | Threshold+reserve equality допустимо, превышение1→Validation; int overflow исключён. ContextWindow/MaxOutputTokens не substitute | ModelSelectionValidator:14–36; core extension:64–67; tests/AgentBridge.Tests/ModelSelectionTests:12–73 | Не полный request/opaque/server count |
| Override→saved→defaults/active snapshot | Reader, AgentSettingsService, начало run | Model/effort выбираются независимо request??saved, затем defaults; override не пишет выбор. Fixed settings/access используются далее; BeginWithSettings получает immutable primitives | Application/AgentRunner:33–36,57–85,105; AgentRunSession:11–12,25–33; Models/TurnModelSettings:7–32; AgentSettingsService:16–46; AgentRunnerTests:19–71 | Fake store не atomic persistence/restart;11–12 отдельно |
| Independent settings version/fresh write | Select checks и entry UoW/base staging | Original token/version без refresh; catalog/compatibility до writer, fresh UTC. Settings UoW меняет только отдельную selection/version; turn snapshot отдельный JSON | AgentSettingsService.SelectAsync:28–46; adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogSettingsUnitOfWork:15–37, DialogTurnUnitOfWork:17–21,88–103; Mapping/TurnSettingsMapping:6–20 | CAS/Serializable/driver/cleanup04/12 не доказаны исполнением |
| Safe settings/status/owner | DTO и встроенная projection/guards | Нет key/address/connection/instructions/headers/envelope/items; owner/dialog snapshot recheck. Selected и последняя server model раздельны | Application/Models/AgentSettingsSnapshot:4–20, DialogStatus:7–47, ContextTokenCount:7–27, ModelAccess:7–20; AgentSettingsService:50–107; AgentSettingsTests:99–113,223–274 | Произвольные custom errors/model IDs и ambient logs — app boundary |
| Compatibility/unknown status/permissions | Inspector/optional port и stored-only status route | Opaque другой/unknown selected модели требует app proof; server name не заменяет provenance. Status без transient input/tools/instructions; unknown/error/expiry запрещают CanContinue, metadata сохраняется. AgentId/source/tool права — app | Application/ContextModelGuard:9–46,49–52; AgentSettingsService:50–87; DialogStatus:39–47; AgentRunner:13–19; AgentSettingsTests:152–274 | Полная композиция08/tokenizer09/security10/status12 отдельно |
| Safe errors/ILogger/Serilog | Local messages, catalog errors, closed diagnostics | Fixed validation/error messages; HTTP exception Message только status. Core logger enum/GUID, без Exception/Message/Data; AddLogging/TryAdd не создают Serilog sink/host | Diagnostics/AgentBridgeDiagnosticsExtensions:9–16, AgentBridgeDiagnostics:24–45, AgentBridgeDiagnosticOperation:45–80; HTTP/Exceptions/HttpRequestFailedException:15–23; DiagnosticsTests:17–100,104–212,254–293 | Unexpected source/I/O exception fail-fast, не разрешение логировать content |
| JsonStructure opt-in/full JSON/SSE | Captured mode, actual shared diagnostics/error/stream path | Default None, enum guard; parse/count только Complete+opt-in. InvalidJson/неполные states фиксированы; без property.Name/values/raw body. Success stream не читается ради logging | CodexLbModelCatalogExtensions:20–27; HTTP/Clients/HttpResponseDiagnostics:23–38,42–88, HttpErrorResponseHandler:16–25, HttpStreamingResponseClient:17–35; ModelCatalogTests:129–154 | Deep capture/SSE/disposal06–07/14, assertions только прочитаны |

Сокращения путей: CodexLbOptions/CodexLb*Extensions — adapters/AgentBridge.CodexLb/Configuration; CodexLbModelAccessResolver/CodexLbModelSettingsReader/CodexLbModelCatalog/ModelCatalogJsonReader — adapters/AgentBridge.CodexLb/Models; Database*/PersistenceRegistrationExtensions — adapters/AgentBridge.Persistence.EfCore/Configuration. Core extension — Configuration/AgentBridgeConfigurationExtensions.cs; AgentBridge*Extensions — Configuration, кроме diagnostics в Diagnostics. Application-типы и Models — Application/ и Application/Models; TurnSettingsMapping — adapters/AgentBridge.Persistence.EfCore/Mapping. В строках EF названия UnitOfWorkContext/EfDbContextAdapter находятся в EF/EfCore. Все source references означают .cs; ModelCatalogTests — tests/AgentBridge.CodexLb.Tests, остальные сокращённые тестовые имена — tests/AgentBridge.Tests.

#### Регистрации, владельцы и safe fields

| Группа | Lifetime и владелец | App dependency / источник |
| --- | --- | --- |
| CodexLb/catalog/gateway | Scoped HttpApiClient/resolver/catalog/reader/gateway | Options/logging/key source/HttpClient factory; app освобождает HttpClient/handlers. CodexLbModelCatalogExtensions:14–30; CodexLbResponsesExtensions:13–18 |
| Runner/compact/settings/cleanup | Scoped; TryAdd singleton TimeProvider.System | Ordered ContextBuilder/providers, model/storage ports, cleanup schedule/authorization — app. Configuration/AgentBridgeRunnerExtensions:12–20, AgentBridgeCompactionExtensions:10–16, AgentBridgeSettingsExtensions:9–20, AgentBridgeDialogCleanupExtensions:10–16 |
| Counter/inspector/guard | Singleton counter и отдельный inspector; transient guard | Custom counter/shape policy отдельно; TryAdd. AgentBridgeTokenizationExtensions:12–18; AgentBridgeSettingsExtensions:16–19 |
| Tools | Singleton registry/executor/definition; scoped handler/validator invocation | App definition/schema/current rights/names/limits. AgentBridgeToolsExtensions:13–36; Application/ToolRegistry:31–48 |
| Persistence | Scoped DbContext/adapter/base repos/session/gate/UoW/read ports | Caller scope, явный provider/connection; два deletion ports — один scoped object. PersistenceRegistrationExtensions:19–67; actual EF registration выше |
| Maintenance | Scoped coordinator/provider/migrations; singleton gate/commands/runner/recovery/SQLite API | Explicit SingleInitializer; остановка writes/DDL/других instances и retention — app. DatabaseMaintenanceRegistrationExtensions:45–77; EF/maintenance/EFCoreLibrary.Maintenance/Extensions/MaintenanceRegistration:15–27 |
| Diagnostics | Singleton diagnostics через ILogger приложения | LoggerFactory/providers/sinks — app; AgentBridgeDiagnosticsExtensions:9–16 |

Safe-field inventory: **settings** — token, model capabilities/effort/threshold/reserve, selection version, retention/bytes/pass/step limits; **status** — dialog/token/dates/expiry,bytes/soft threshold/reached,compact count,settings/saved selection,selected model/effort отдельно от server model,known/nullable estimate/encoding/opaque,error,nullable threshold/CanContinue; **turn snapshot** — model/effort/threshold/reserve/input window. Источники — DTO и AgentSettingsService.Snapshot:105–107 из таблицы. Raw options/ModelAccess/canonical envelope не safe UI DTO.

#### Tests/evidence и кандидаты

Прочитаны ConfigurationTests/ModelSelectionTests/CodexLbConfigurationTests/DatabaseConfigurationTests/ModelCatalogTests/DiagnosticsTests, адресно AgentSettingsTests, AgentRunnerTests19–71, PersistenceRegistrationTests, DatabaseMaintenanceRegistrationTests и ContextTokenCounterTests192–204. Негативные fixtures — synthetic source/fake handler/local streams/fake storage; чтение ValidateScopes/ValidateOnBuild/secret assertions не текущий pass.

Статически прочитаны XML metadata/results [stage13-core.trx](../../../artifacts/compile-check/stage13/results/stage13-core.trx), [stage13-codexlb.trx](../../../artifacts/compile-check/stage13/results/stage13-codexlb.trx), [settings-core-final.trx](../../../artifacts/test-results/stage21/settings-core-final.trx), [settings-transport-first.trx](../../../artifacts/test-results/stage21/settings-transport-first.trx). TestRun/Times —2026-10-04; UnitTestResult содержит прежние Passed configuration/budget/401/403 и stage21 pinned/safe settings cases. Происхождение: [исторический13](<../AgentBridge Initial Implementation/13-model-catalog-and-keys.md>):35–57; [исторический21](<../AgentBridge Initial Implementation/21-settings-and-dialog-status.md>):49–73,80–82. Overlap/повторы не суммированы; соответствие всех artifacts текущему HEAD не доказано, provenance14. Старые разрешения/паузы не использованы.

**Новых S01-FNN кандидатов и S01-QNN вопросов нет.** Просмотренные маршруты согласованы с main; это не отсутствие runtime дефектов. [ABQA-001](Findings.md) не дублируется. [ABQA-002](Findings.md) остаётся подозрением, не runtime-утечкой;07/14. [ABQA-003](Findings.md#abqa-003)/[ABQA-004](Findings.md#abqa-004)/[Q-003](OpenQuestions.md#abqa-q-003) остаются00/15. [ABQA-Q-002](OpenQuestions.md#abqa-q-002) ограничивает environment/app/live выводы, не блокирует A01.

#### Пропуски

| Что | Причина и влияние | Окружение/разрешение и будущий сценарий |
| --- | --- | --- |
| Compile/isolated DI/options/catalog/logger/settings, B | Прямой запрет; текущая сборка/DI/assertions не исполнены | Отдельные точные команды трёх test csproj, .NET10/локальные packages и свежая проверка build/import/output chain. Invalid options/provider/timeouts; source mutation/pinned/401/403; custom services; safe logs; override/saved/default/legacy. Исторические формы13:40–44,21:61–63 не готовая новая команда; stale binary/--no-build не использовать |
| App graph/factories/authorization, B/D | App root/source/validators/sinks неизвестны; библиотечная карта не весь graph | Согласованный consumer, ValidateScopes/ValidateOnBuild, trusted owner/AgentId/custom services с изолированными ports; hosting сейчас запрещён. Точная команда неизвестна |
| Atomic settings/CAS/provider concurrency/restart, C | DB/SQL/migrations запрещены; fake writer не atomicity | Выделенные SQLite/PostgreSQL, разрешённые resources/cleanup и existing integration scenarios independent version/active run/races/stale/rollback после04/11/12. Команда зависит Q-002; другие providers не охватываются |
| Live catalog/keys/models/routing, D | HTTP/hosting запрещены; fixtures не live proof | Согласованные endpoint/commit/accounts/exact models/расходы, actual отказ без fallback и pinned source/catalog/generation;06. Команда до Q-002 не установима |
| Deep transport/status/recovery/provenance, следующие A/C/D | Только границы01; полного аудита06–12/14 здесь нет | Продолжить A по entry points таблицы. B/C/D отдельно; compile/PE/new DI root/synthetic ack не native load/OS crash/disconnect/exactly-once |

Новых tests/harness/scripts нет. Build/tests/CLI/app/hosting/Docker/DB/SQL/migrations/backup/restore/native/HTTP не запускались. Product/dependency/spec/history не изменены; Git только read-only.

#### Итог и передача

**Проверен с ограничениями.** Все группы01 исследованы A, включая safe settings/status, active primitive snapshot и actual JsonStructure. Предварительный вывод «новых кандидатов нет» независимо подтверждён в этих границах. Runtime DI/авторизация/HTTP, atomic persistence/конкуренция/restart и полный будущий контекст не доказаны.

Передать02 fixed UTC expiry/owner/token vs selection version;04 независимый settings CAS/actual EF scope;05 nullable provider/backup retention/SingleInitializer;06 pinned per-call key/request Authorization/JsonStructure/401–403;07 прежний ABQA-002;08–09 catalog не tokenizer/opaque proof;10 app permissions/validator scopes;11–12 AgentRunner57–85/AgentRunSession31–33/AgentSettingsService28–87 и safe-field/UoW entry routes;13–14 lifetime boundaries/старые TRX без current pass. Общие реестры изменяет координатор.

Финальный контроль A,2026-10-05 22:42 UTC+07:00: собственный diff прочитан; diff --check успешен. Все34 path-ссылки документа доступны, новые fragments соответствуют отдельным ID-заголовкам. UTF-8 без BOM/LF сохранены, U+FFFD/mojibake/четырёх вопросительных знаков нет. Задание и предварительная запись сохранены; единственная собственная правка — этот файл. Index пуст, недокументальных изменений в status нет; обе зависимости чисты, HEAD неизменны. Предупреждение Git об autocrlf не меняло файл или config.
