# 00 — Требования, история и границы контрактов

Статус: **проверен с ограничениями**. Предпосылки: Подготовительные материалы; заново зафиксировать HEAD и исходные изменения.

## Цель и вопросы

Определить, какие правила согласованы, реализованы и действительно проверены. Найти противоречия между main/delta specs, текущими описаниями, публичными контрактами и историческими checkpoints.

## Компоненты и зависимости

[spec](../../../openspec/specs/agent-runtime/spec.md), [context](../../../openspec/specs/agent-runtime/context.md), [незакрытые changes](../../../openspec/changes), [отчёты 00–25](<../AgentBridge Initial Implementation/README.md>), README и оба раздела Documentation. Базовые версии и обязательные пути — [Baseline](Baseline.md).

## Способ проверки и границы

Составить матрицу «требование → реализация → тест/отчёт → предел доказательства». Сопоставить локальную историю с датами отчётов; отличить старый STOP от текущего статуса. Проверить все 18 changes, включая полностью отмеченные tasks без CLI evidence. Негативные случаи: отсутствующий файл/артефакт, недоступная зависимость, stale ссылка, требование без реализации, реализация без согласованного правила. Проверить ABQA-001; спорное правило оставить открытым, не переписывать spec под код.

## Разрешения

A. CLI validation относится к B и требует отдельного разрешения; отсутствие CLI зафиксировать, не устанавливать. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Матрица нормативных заголовков с файлами/методами и commit, перечень противоречий и незакрытых вопросов, доступность обязательных путей. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Каждая архитектурная область получила источник правила и маршрут проверки либо явное ограничение. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Корректность runtime, полноту тестового покрытия и прохождение OpenSpec CLI.

## Результаты

Ниже сохранена предварительная запись координатора. Независимый исполнительский отчёт находится в разделе «Независимая проверка этапа 00»; его выводы уточняют предварительную запись. Организационный блокер создания чата уже снят, ABQA-Q-001 закрыт; это не ограничение выполненного исследования A.

### Состояние и границы

2026-10-05, 21:58–22:02 UTC+07:00; исполнитель — координатор. HEAD AgentBridge `41072fc37bb718dd9f4bb0473912802260bb9164`; исходное дерево чистое. Точные HEAD и status соседей — [Coordination](Coordination.md#исходное-состояние). Исследование A; создание отдельного чата заблокировано [ABQA-Q-001](OpenQuestions.md#abqa-q-001). Во время проверки меняются только документы этого аудита.

Прочитаны root/Documentation/Plans AGENTS, все 16 заданий, README/Baseline/Methodology/Findings, текущий корневой README, нормативные заголовки и основные MUST-пункты spec, context/checkpoint25, локальные log/show и tasks всех 18 changes. Baseline подготовки относится к c8da604; commit 41072fc добавил аудит/бизнес-описания и пример ASP.NET Core. Старые результаты не переносились как текущие pass.

### Матрица вопросов

Пути ниже относительно корня AgentBridge; номера — строки на указанном HEAD. [Нормативный spec](../../../openspec/specs/agent-runtime/spec.md).

| Область / требование | Реализация и маршрут | Evidence и результат A | Ограничение |
| --- | --- | --- | --- |
| DLL/руководство/честное закрытие, spec:9–70 | tests/Delivery, README, отчёты24–25 | git log: 495e2b8 → c8da604 → 41072fc; карта25 явно разделяет compile/runtime | Артефакты заново оцениваются в13–14 |
| Tools и durable, spec:72–111,847–878 | Application/ToolExecutor, AgentRunner, AgentRunSession | AgentRunner.cs:16,22 содержит actual public RunAsync; tests/ToolExecutorTests и AgentRunnerTests существуют | Детальные ветви10–11 |
| Compact/token budget, spec:113–186 | ContextCompactor, ContextBudgetGuard, Tokenization | Карта текущих типов и портов соответствует описанным слоям | Known не означает full estimate; этап09 |
| Composition, spec:188–235 | ContextBuilder, StoredDialogContext/Turn | Владелец требований определён; tests/ContextBuilderTests | Все ветви pairing — этап08 |
| JSON/SSE/keys, spec:237–333 | adapters/AgentBridge.CodexLb | Реальные ProjectReference/границы в root и adapter AGENTS | Локальный серверный контракт —06; сеть запрещена |
| Domain/read/write, spec:359–507,807–845 | Dialog, DialogReader, UnitOfWork | Различены in-memory lifetime и persisted token | Provider enforcement — C, не подтверждён |
| Retention/settings/cleanup, spec:509–558,628–684,753–795 | AgentSettingsService, ExpiredDialogCleanup | Бизнес-страницы отражают expiry/Unknown/no-replay | Этап12 проверяет реализацию |
| Обслуживание, spec:335–357,695–751 | migrations, EFCoreLibrary maintenance | Только SQLite/PostgreSQL AgentBridge; внешние библиотеки доступны | Backup/restore не запускались |
| Исторические STOP и ABQA-001 | Plans/README:7–8, Technical documentation/01-architecture:7,36,52 | Старый дефект бизнес-описаний устранён в истории; contextual checkpoints не выдаются за текущие запреты | Внешний навигатор аудита:8 останется устаревшим после запуска, его изменение запрещено |
| Main/delta и CLI | openspec/changes/*/tasks.md, specs/agent-runtime/spec.md | 18 папок, все с tasks/spec; 9 содержат открытый CLI-пункт | Текстовая сверка не CLI validation; отсутствия смысловых конфликтов во всех сценариях не доказано |

### Все открытые changes

| Change | Отмечено / открыто в tasks | Результат |
| --- | --- | --- |
| agent-turn-orchestration | 6 / 0 | CLI не подтверждена |
| application-tools | 6 / 1 | CLI-пункт открыт |
| base-repository-adapters | 6 / 0 | CLI не подтверждена |
| context-compaction | 7 / 1 | CLI-пункт открыт |
| context-composition | 8 / 1 | CLI-пункт открыт |
| cross-component-verification | 5 / 1 | CLI-пункт открыт |
| database-startup-and-backup | 4 / 0 | CLI не подтверждена |
| dialog-settings-and-status | 8 / 1 | CLI-пункт открыт |
| dll-delivery | 7 / 0 | CLI не подтверждена |
| expired-dialog-cleanup | 5 / 0 | CLI не подтверждена |
| initial-provider-migrations | 7 / 0 | CLI не подтверждена |
| model-catalog-and-keys | 5 / 0 | CLI не подтверждена |
| model-tokenizer | 7 / 1 | CLI-пункт открыт |
| persistence-models | 6 / 0 | CLI не подтверждена |
| responses-json-adapter | 5 / 1 | CLI-пункт открыт |
| responses-sse-adapter | 5 / 1 | CLI-пункт открыт |
| scenario-unit-of-work | 5 / 0 | CLI не подтверждена |
| usage-guide-and-closure | 6 / 1 | CLI-пункт открыт |

Поиск заголовков всех delta specs в main выявил старые имена «Безопасные ошибки и ограниченный вызов JSON» и «Продолжение только своего вызова» в responses-json-adapter; main использует уточнённые заголовки с JSON. Это само по себе не доказательство утраты требования: последующие SSE изменения переименовывают/расширяют контракт. Автоматический sync/archive запрещён.

### Находки, вопросы, пропуски и передача

[ABQA-001](Findings.md#abqa-001--текущие-описания-отстают-от-завершённой-реализации) сохранён с переоценкой текущего состояния. [ABQA-003](Findings.md#abqa-003) фиксирует пробел CLI evidence. [ABQA-Q-001](OpenQuestions.md#abqa-q-001) — блокер чатов, [ABQA-Q-002](OpenQuestions.md#abqa-q-002) — будущие окружения.

OpenSpec CLI не устанавливалась, доступность сейчас не проверялась и CLI не запускалась. Нужны отдельное B-разрешение, уже доступный CLI и проверка его версии/синтаксиса перед точной командой strict specs/change validation. Build/test/DB/HTTP также запрещены. Успех этих проверок не заявляется.

Статус: **проверен с ограничениями**. Контрактная карта пригодна для последовательных этапов; полный аудит всех ветвей, отсутствие дефектов и runtime readiness не подтверждены. Дальше01: проверить DI, ключи и модельный выбор по текущим исходникам.

### Независимая проверка этапа 00

Дата: **2026-10-05, Asia/Novosibirsk (UTC+07:00)**; начало отдельного чата — 22:06 по Coordination. Исполнитель — отдельный чат этапа00 `01a10c99-a240-7741-8276-fa451768380c`. Проверка выполнена только способом **A**. Предварительный отчёт01 не использован как принятый результат; предшествующих исполнительских этапов нет.

AgentBridge HEAD повторно прочитан: `41072fc37bb718dd9f4bb0473912802260bb9164`. На входе уже изменены `00-contract-baseline.md`, `01-configuration-and-access.md`, `Findings.md`, README аудита; Coordination/OpenQuestions не отслеживаются. Это изменения координатора, а не исходный чистый baseline и не изменения этого исполнителя. Начальный tracked diff: 4 файла, 109 добавленных/8 удалённых строк; index пуст. Сам исполнитель изменяет **только этот Markdown** через apply_patch. До редактирования файл имел UTF-8 без BOM и LF (11428 байтов, CR отсутствует).

Обязательные зависимости заново проверены read-only: EFCoreLibrary HEAD `3a8a53187af3c5df049770dfd6727b5065159d1f`, Version `0.0.5`; HttpClientLibrary HEAD `6d0528d940d1d8494c722c22464051dd961d6bf7`, FileVersion `0.0.0.5`. Оба status пустые, пути и ProjectReference доступны. Для codex-lb/TelegramCodexRelayBot/AquaByte-Ledger HEAD остаются сведениями Coordination, а не результатом повторного обзора их исходников этим исполнителем. Обзор их API отнесён к зависимым этапам; наличие пути не доказывает совместимость.

#### Исследованные источники и способ

Прочитаны README/Baseline/Methodology/Findings/OpenQuestions/Coordination и задание00; applicable root/Documentation/Plans/Application/Domain/Configuration/Diagnostics/Tokenization/adapters/tests/Delivery/Integration AGENTS. Для обязательных библиотек прочитаны root, HTTP Clients и EF maintenance/общий проект/Abstractions AGENTS. Применены `csharp-project-rules` и `backend-uow-repositories`; их правила запуска не отменяют запрет B/C/D.

Нормативные заголовки main spec, MUST-положения, все18 delta specs и tasks сопоставлены статическим разбором текста. Сверены актуальные публичные порты/entry points, project references, навигация обоих разделов документации и бизнес-сценарии, context, карта исторического evidence00–25 и локальная история. Нижеследующая карта охватывает **все63 заголовка Requirement/Требование** main spec: русские заголовки тоже учитываются. Это карта владельцев/маршрутов проверки, не доказательство каждой внутренней ветви.

Пути и строки ниже относительно корня AgentBridge на HEAD41072fc. Если в строке перечислена только тестовая группа, это найденный исходник сценариев, **не pass**. Технические документы по номерам находятся в `Documentation/Technical documentation/`; исторические отчёты — в `AgentBridge Initial Implementation`.

#### Полная карта требований, реализации, тестов и истории

| Группа / строки main spec | Исследовано и результат A | Конкретный маршрут реализации / тестового evidence / истории | Ограничение и следующий этап |
| --- | --- | --- | --- |
| Руководство и честное закрытие, 9–28 (2 требования) | Разделены compile-only примеры25 и новый ASP.NET Core пример41072fc; исторический STOP не текущий запрет | `tests/Delivery/Consumer/UsageRegistration.cs`, `UsageFlow.cs`; отчёт25 «Карта evidence00–25»; commits c8da604 → 41072fc | Новый README пример не исполнялся и, по commit41072fc, не компилировался; 13–15 |
| DLL/XML/внешние требования, 29–56 (3) | Выбор provider/RID и metadata XML отделены от runtime загрузки | `tests/Delivery/Build/AgentBridge.Delivery.csproj`, `Consumer/AgentBridge.BinaryConsumer.csproj`, `Metadata/DeliveryMetadataTests.cs`; отчёт24; commit495e2b8 | Наличие manifest/PE не загрузка native; 13 |
| Сквозные сценарии, 57–71 (1) | Existing tests связывают actual transport и persistence; HTTP подставной | `tests/AgentBridge.Persistence.EfCore.Tests/Integration/CrossComponentIntegrationTests.cs`, `CrossComponentFixture.cs`; отчёт23; commit7e9d533 | Здесь прочитаны исходники/старый TRX, прогон не повторён; 14 |
| Инструменты/identity/durable граница, 72–112 (3) | Порты validator/checkpoint/handler существуют; standalone tools и durable run различены | `Application/ToolRegistry.cs`, `ToolExecutor.cs:30`, `Ports/IToolInvocationValidator.cs`, `IToolExecutionCheckpoint.cs`; `ToolExecutorTests.cs`; отчёт19/de58342 | Нет доказательства внешнего exactly-once или поведения handler; 10–11 |
| Compact transport/отмена/префикс/принятие, 113–161 (4) | Есть отдельный compact API и save-порт, без подмены генерацией | `CodexLbModelGateway.CompactAsync:140`, `Responses/CompactRequestWriter.cs`, `CompactJsonReader.cs`, `Application/ContextCompactor.cs:18`, `IDialogContextWriter`; `ResponsesJsonTests`, `ContextCompactorTests`; 18/73045a1 | Fake writer не атомарность; live compact не проверен; 06,09 |
| Tokenizer/full budget, 162–187 (2) | Known и nullable full estimate разделены; null запрещает generation | `Tokenization/ContextTokenCounter.CountAsync:27`, `ModelEncodingMap.Find`, `Application/ContextBudgetGuard.CheckAsync:20`, `Models/ContextTokenCount.cs`; `ContextTokenCounterTests`, `ContextBudgetGuardTests`; 17/af72252 | Exact map конечен; JSON estimate не server count и не opaque estimator; 09 |
| Composition/роли/pairing/guards, 188–236 (3) | Public builder принимает explicit ordered providers, snapshot и UTC; известные пары проверяются отдельно от opaque | `Application/ContextBuilder.BuildAsync:27`, `BuildCoreAsync`; `ContextBuilderTests`; 16/085779a | Сверен маршрут, не все ветви FIFO/partial/provider failure; 08 |
| SSE/lifecycle, 237–259 (2) | Stream выбирается callback; обёртка actual HTTP awaited/disposed | `CodexLbModelGateway.cs:95`, `SseEventReader.cs`, `ResponseSseState.cs`; `ResponsesSseTests`; 15/1d99721 | ABQA-002 остаётся подозрением; EOF/cleanup/cancel подробно в07 |
| JSON/safe error/continuation, 260–303 (3) | Gateway делегирует SendWithResponseAsync, output/envelope и continuation имеют разные роли | `CodexLbModelGateway.cs:57`, `ResponseRequestWriter`, `ResponseJsonReader`, `ResponseErrorReader`, `ResponseContinuationMapper`; `ResponsesJsonTests`; 14/d8a673f | Binding не upstream ownership; старые delta формулировки ниже; 06 |
| Каталог/selection/индивидуальный ключ, 304–334 (3) | Actual HTTP catalog/settings/access ports существуют, выбор per-call | `Models/CodexLbModelCatalog.ReadAsync:19`, `CodexLbModelSettingsReader.ReadAsync:17`, `CodexLbModelAccessResolver`, `Application/ModelSelectionValidator`; `ModelCatalogTests`, `ModelSelectionTests`; 13/95ef7b9 | Наличие модели в каталоге не tokenizer/compact proof; 01,06 |
| Provider migrations, 335–358 (1) | Общий DbContext, две library factories и отдельная history identity | `SqliteAgentBridgeDbContextFactory`, `PostgreSqlAgentBridgeDbContextFactory`, `Configuration/AgentBridgeMigrationsHistory.cs`; runtime `PersistenceRegistrationExtensions.cs:31,36`; `ProviderDesignTimeTests`; 11/0a49eb0,21/6482d59 | Шесть текущих mapped таблиц, old delta11 содержит пять; SQL/Up/Down запрещены; 05 |
| Write/Restore/независимые ports, 359–408 (3) | Ports документируют atomic guard; Domain Restore не заменяет persisted token | `Domain/Dialogs/Dialog.Restore:67`, `Ports/IDialogTurnWriter.cs:8–31`, `IDialogToolAttemptWriter.cs:7–15`, `UnitOfWork/UnitOfWorkScope.cs:17`, `EfUnitOfWorkSession.cs:12,23,25`; `DialogRestorationTests`, `DialogWritePortsTests`, `UnitOfWorkScopeTests`; 07/c614ff0,10/e2616e0 | Contract + fake tests не реальная isolation/commit; 02–04 |
| .NET/adapter/context/storage boundaries, 409–450 (4) | SDK library net10.0, core glob исключает самостоятельные проекты; обратных ProjectReference из ядра нет | `agent-bridge.csproj:1–25`, оба adapter csproj; `IContextProvider`, `IToolHandler`, `IModelGateway`; 01/58e7473,07/c614ff0,08/bb1a0ed | Source structure не runtime closure/DI приложения; 01,13 |
| Persistence format/base adapters, 451–508 (2) | Root/child DTO отделены от Domain; base repository/session API повторно сверено с0.0.5 | `AgentBridgeDbContext.cs:37–146`, `Reading/DialogReader.ReadAsync:20`, `Repositories/*RecordQueries`, `RecordStaging`; registration:43–44; `PersistencePayloadTests`, `DialogReaderTests`; 08/bb1a0ed,09/d4ab00c | Parent predicates/order/serialization глубже03; staging не commit |
| Restart/bytes/fixed expiry, 509–559 (3) | Нет обещания replay; retention вычисляется от создания, bytes threshold мягкий | `Configuration/DialogRetentionOptions.cs:8,11,18`, `Dialog.CreatedAtUtc/ExpiresAtUtc`, `UnitOfWork/StoredContentSize.cs`, `DialogWriteToken`; `DialogTests`, `PersistencePayloadTests`; 02/7f63741,06/309a3d6,20/95c28fa | Persisted restore и физический размер БД не доказываются одними DTO; 02–04,11–12 |
| Сжатие и обязательные библиотеки, 560–598 (3) | Compactor не удаляет историю; HTTP и CRUD зависят от требуемых библиотек | `ContextCompactor`, два adapter csproj; EF `Abstractions/Database/IAppDbContext`, `IUnitOfWorkContext`, `Extensions/ServiceCollectionExtensions.cs`; HTTP `Abstractions/IHttpApiClient` | Не аудит каждого внутреннего вызова; versions/HEAD проверены, внешнее поведение нет; 03–09 |
| Logging/HTTP error details, 599–627 (2) | Безопасная диагностика отделена от raw payload; lifecycle не определяется HTTP status | `Diagnostics/AgentBridgeDiagnostics`, `AgentBridgeDiagnosticOperation`, HTTP `Clients/AGENTS.md`, `Models/HttpStreamResponseResult`; `DiagnosticsTests`, `ModelCatalogTests`, `ResponsesJsonTests`; 03/4bceaf2,04/dc2b7f3 | Logger pipeline приложения не исполнялся; disposal порядок не доказывает runtime leak; 06–07 |
| Safe settings/CAS/provenance/status/keys, 628–694 (5) | Separate settings port и pinned turn snapshot существуют; model-independent guard отделён от token budget | `AgentSettingsService.ReadAsync:16/SelectAsync:28/GetStatusAsync:50`, `ContextModelGuard.CheckAsync:14`, `IDialogSettingsWriter.SaveAsync`, `IDialogTurnWriter.BeginWithSettingsAsync:15`, `Models/ModelAccess.cs`; `AgentSettingsTests`, `DialogSettingsMappingTests`; 21/6482d59 | Совместимость opaque требует app port; metadata не гарантируют CanContinue полного запроса; 01,12 |
| Explicit maintenance/backup/SingleInitializer/receipt, 695–752 (5) | DI подключения и operations раздельны; порт библиотеки Inspect/UpdateExisting/InitializeNew сверён | `Configuration/DatabaseMaintenanceRegistrationExtensions`, `DatabaseBackupOptions`, `DatabaseBackupOptionsValidator`; EF `Maintenance/Abstractions/IDatabaseMaintenance.cs:4–13`; `DatabaseMaintenanceRegistrationTests`, `DatabaseMaintenanceTests`; 05/6a4ac1c,12/1817acf | Fake provider/receipt не восстанавливаемый backup; native/process/DDL не запускались; 05 |
| Cleanup, 753–777 (1) | Есть явный public bounded invocation, read/deletion ports и immutable результат | `Application/ExpiredDialogCleanup.CleanupAsync:19`, `Ports/IExpiredDialogReader`, `IExpiredDialogDeletion`, `Configuration/AgentBridgeDialogCleanupExtensions`; `ExpiredDialogCleanupTests`, `Integration/ExpiredDialogCleanupIntegrationTests`; 22/89c28e8 | Cascade/late-write доказательства только исторические; 12 |
| Completion/late delete/no search, 778–806 (3) | ModelResponse lifecycle и TerminalSaved различены; отдельный search API не обнаружен в карте public ports | `Models/ModelResponse`, `AgentRunResult`, `AgentRunner.cs:118–125`, `IDialogDeletion`, `ContextBuilder`; `ResponsesSseTests`, `AgentRunnerTests`, `DialogWritePortsTests` | Это обзор объявленных ports, не proof всех runtime ветвей; 04,07–08,11–12 |
| Domain state/terminal prefix, 807–846 (2) | Getter identity/dates, локальный lifetime, валидирующая Restore и terminal operations существуют | `Domain/Dialogs/Dialog.cs:29–47,67,153,177,207`, `DialogStateVersion`, `DialogTurnStatus`; `DialogTests`, `DialogRestorationTests`; 06/309a3d6,10/e2616e0 | Lifetime не persisted incarnation; prefix не item cutoff; 02,08 |
| Full runner/durable/honest outcome, 847–878 (3) | Existing TurnId отклоняется; выбранный доступ фиксируется; Start/Outcomes идут через короткие scopes | `AgentRunner.RunAsync:22`, existing-turn guard:53–56, full guard:89; `AgentRunSession.cs:32,45,52,73`; `AgentRunnerTests.StoredSelectionAndOverrideRemainPinnedDuringRun`, `LegacyTurnWriterDoesNotStartWithoutSnapshotSupport`; 20/95c28fa | Новая DI root не crash ОС; synthetic acknowledgement не real disconnect; 10–11,14 |

63 требования распределены по группам без исключения области diagnostics, HTTP dependencies, safe status, отсутствия поиска или поставки. Наличие реализации найдено на уровне заявленных API/слоёв; полного соответствия всех сценариев эта карта не утверждает. Реализация без нормативного маршрута в исследованных entry points не установлена; это не результат сплошного обзора всех типов.

#### Независимая сверка всех18 changes

В каждой папке доступны `proposal.md`, `context.md`, `tasks.md`, `specs/agent-runtime/spec.md`. Counts checked/open предварительной таблицы подтвердились. Сравнение блоков выполнено с нормализацией whitespace и `#### Scenario/Сценарий`; требования `Requirement/Требование` сопоставляются по имени. **38 из43 блоков совпадают** после этой нормализации. Этот результат не равен CLI parsing, validation, sync или archive.

| Change / последний локальный commit папки | Блоков delta / совпавших с main | Результат и причина различий |
| --- | --- | --- |
| agent-turn-orchestration /95c28fa | 3/3 | Все6 tasks закрыты, отдельный текст прямо фиксирует отсутствие CLI validation |
| application-tools /de58342 | 3/3 | Открытый CLI-пункт; standalone/durable граница19–20 совпадает |
| base-repository-adapters /d4ab00c | 1/1 | Русские заголовки тоже учтены; tasks6/0 не подтверждают CLI, текст прямо отрицает её |
| context-compaction /73045a1 | 4/4 | Compact transport/guards совпадают; delta не содержит MODIFIED старого JSON safe-error требования |
| context-composition /085779a | 3/3 | Pairing/prefix/provider boundaries совпадают, CLI открыт |
| cross-component-verification /7e9d533 | 1/1 | Actual DB/fake HTTP граница совпадает, CLI открыт |
| database-startup-and-backup /1817acf | 2/2 | Explicit registration/backup options совпадают; tasks4/0 при явном пропуске CLI |
| dialog-settings-and-status /6482d59 | 5/5 | Scenario/Сценарий различается только языком заголовка; MODIFIED migrations заменяет старое описание пяти таблиц |
| dll-delivery /495e2b8 | 3/3 | DLL/XML/RID совпадают; tasks7/0 не доказательство CLI; отсутствие подтверждено общим отчётом25 |
| expired-dialog-cleanup /89c28e8 | 1/1 | Русское «Требование» учтено; tasks5/0 при прямом указании невыполненной CLI validation |
| initial-provider-migrations /0a49eb0 | 1/0 | Delta:5–18 — пять mapped таблиц и отдельные SQLite/PostgreSQL scenarios; main:337–355/поздний MODIFIED21 — общий provider scenario и без фиксированного числа пять. Объясняется этапом21 |
| model-catalog-and-keys /95ef7b9 | 3/3 | Exact selection/key priority совпадают; tasks5/0 при явном пропуске CLI |
| model-tokenizer /af72252 | 2/2 | Full estimate/guard совпадают, CLI открыт |
| persistence-models /bb1a0ed | 1/1 | Scenario/Сценарий — только текстовый формат; MUST и сценарии совпадают |
| responses-json-adapter /d8a673f | 3/0 | Generation переформулирована; два имени требований изменены в main, старый safe-error delta:19 требует отказ compact/streaming callback до HTTP. Историческая граница14, кандидат [ABQA-004](Findings.md#abqa-004) |
| responses-sse-adapter /1d99721 | 3/2 | Main:276 разрешает отдельный compact-контракт; delta:30 требует Unsupported. После18 main изменён, соответствующего MODIFIED блока compact change нет; [ABQA-004](Findings.md#abqa-004) |
| scenario-unit-of-work /e2616e0 | 2/2 | Atomic scope/Restore совпадают; tasks5/0 при явном отсутствии CLI/relational проверки на своём checkpoint |
| usage-guide-and-closure /c8da604 | 2/2 | Guide/closure evidence boundaries совпадают, CLI открыт |

Локальный log каждого change сверён с датами отчётов03–04 октября2026; doc commit41072fc датирован 04.10.2026 21:13:52 UTC+07 и не меняет OpenSpec/production. Исторические этапы04–05 развивали **соседние** библиотеки: doc commit AgentBridge не является commit реализации самой зависимости. Более поздние примеры README41072fc также не наследуют автоматически compile evidence Consumer25.

Один массовый read-only вызов `git log` для application-tools завершился `fatal: Out of memory`; два адресных повторных чтения application-tools/base-repository-adapters успешны и дали de58342/d4ab00c. Ошибка чтения не стала pass или дефектом продукта. Ошибочные предположения путей `Abstractions/IAppDbContext`, `Interfaces/IHttpApiClient`, `manifest.json` исправлены по listing: реальные контракты в `Abstractions/Database`, HTTP `Abstractions`, файлы `delivery.manifest.json` доступны. Поэтому эти первоначальные промахи не зарегистрированы как отсутствующие файлы.

#### Историческое evidence и негативные случаи

| Вопрос этапа | Факт A / конкретное evidence | Вывод и ограничение |
| --- | --- | --- |
| Текущий commit против старого STOP | История c8da604 завершает25,41072fc добавляет аудит; исторический README:3–7 прямо отделяет текущий checkpoint от старых STOP | Даты/последовательность совместимы; STOP14–18 не блокирует порученный аудит |
| Доступность базовых contract sources | EF `IAppDbContext`, `IUnitOfWorkContext`, context CRUD и `AddEfCoreBaseRepositories` прочитаны; HTTP `IHttpApiClient.SendStreamAsync/SendWithResponseAsync` прочитан | Не предполагались старые namespaces или ICopyable; full dependency audit не выполнен |
| Отсутствующие файлы/битые ссылки | Проверены837 локальных Markdown path-ссылок в README/Documentation/main/context/changes, missing=0 | Это проверка существования путей; anchors и все binary manifest SHA заново не проверены |
| Доступность исторических артефактов | `artifacts/test-results/stage23/cross-final.trx` —32/32 pass, `cross-isolated.trx` —25/25, даты04.10 17:50–17:51; stage25 `delivery-metadata-stage25.trx` —8/8, дата04.10 19:55, failed/notExecuted=0 | Это счётчики сохранённых TRX, не новый запуск, не полная сверка каждого TRX с текущими исходниками; suites не суммируются |
| Compile-only поставка/методы | Stage25 Sqlite/PostgreSql build logs доступны и содержат0 warnings/errors; reference receipts доступны; оба `delivery.manifest.json` имеют79 entries, win-x64, Debug, AgentBridgeHead7e9d533, dependencies HEAD совпадают | Manifest сохраняет исходный baseline перед24; не заявляется сборка на HEAD41072fc, повторная SHA verification или runtime/native loading |
| Применимость старой версии EF | Исторический README:11,13,79,103 явно разделяет0.0.4 интеграционный запуск и0.0.5 compatibility; поздние20–23 имеют собственные отчёты | Старый00–13 run не pass для всей0.0.5; detailed artifact provenance остаётся14 |
| ABQA-001 сейчас | Через git show c8da604 подтверждены старые «11–25 не начаты»/«сценарий и storage будущие»; HEAD41072fc исправляет эти места | Исторический ID сохранён. Ниже указаны дополнительные текущие места того же класса, без нового ID |
| Main/delta против требований без реализации | Все63 main headings получили маршрут, old deltas перечислены; публичный CompactAsync существует, отдельный search API в ports отсутствует | Не найден обязательный отсутствующий entry point в этом объёме. Непрочитанная ветвь не считается соответствующей; [ABQA-004](Findings.md#abqa-004)/[ABQA-Q-003](OpenQuestions.md#abqa-q-003) остаются |

Ни один сохранённый артефакт не объявлен текущим общим pass. Полная проверка test selection, TestDefinitions, source/build hashes, receipts/manifest SHA и всех suite overlaps отнесена к14/13.

#### Находки и спорные места

**ABQA-001 — переоценка без дублирования.** Помимо внешнего `Documentation/Plans/README.md:8` и `Business logic/README.md:28` («аудит пока не начат») найдено текущее противоречие `Technical documentation/01-architecture.md:52,66`: оговорка «Названия без явного статуса реализации ... будущие типы» распространяется на строку `AgentSettingsService` без статуса, хотя `Application/AgentSettingsService.cs:10–16,28,50` и registration21 уже реализованы. Факт документационного расхождения подтверждён статически; S3 — читатель получает неверную картину доступного settings API, runtime-ошибка не следует. Находка относится к существующему ABQA-001, а не [ABQA-004](Findings.md#abqa-004). Файлы вне своего00 не изменялись.

**[ABQA-004](Findings.md#abqa-004) — старые transport deltas сохраняют промежуточный запрет compact.**

- Категория: расхождение документации/OpenSpec; **требуется уточнение нормативной применимости**. Различие текстов подтверждено, ошибка runtime/CLI не доказана. Серьёзность S3 предварительно: при чтении всех неархивированных deltas как текущих правил API одновременно разрешает и запрещает compact; последствия ограничены трактовкой контрактов/workflow.
- Ожидаемый контракт: `Documentation/AGENTS.md` требует синхронизации проверяемых требований с main; tasks14/15/18 требуют синхронизации main. Текущие main:115,276 и `IModelGateway.CompactAsync` предусматривают отдельный compact transport.
- Факт: `openspec/changes/responses-json-adapter/specs/agent-runtime/spec.md:19` требует Unsupported для compact/streaming callback; `responses-sse-adapter/specs/agent-runtime/spec.md:30` требует Unsupported для compact. Main:276 отсылает к реализованному compact-контракту. Все четыре блока `context-compaction/specs/agent-runtime/spec.md` находятся под ADDED; MODIFIED старого safe-error блока отсутствует. Старые JSON заголовки также отличаются от имён main.
- Условия статической проверки: прочитать указанные строки на HEAD41072fc и сопоставить d8a673f →1d99721 →73045a1. Proposal14 явно ограничен этапом14; история объясняет происхождение прежнего запрета и не доказывает ошибку текущего кода.
- Влияние/граница: актуальное поведение аудируется по согласованному main; выбранная реализация compact не объявляется дефектом. Не установлено, являются ли оставшиеся delta намеренно неизменными checkpoints либо материалом будущего sync. Неизвестно, как установленная версия CLI разрешает переименованные/накопленные требования.
- Связи: [ABQA-Q-003](OpenQuestions.md#abqa-q-003); этап15. ABQA-003 фиксирует отсутствие CLI evidence, а [ABQA-004](Findings.md#abqa-004) — конкретное текстовое расхождение; новый устойчивый ABQA ID решает координатор. Spec/delta не переписывались, archive не выполнялся.

**ABQA-003** независимо подтверждён как пробел evidence всех18 changes. Девять открытых CLI-пунктов не исчерпывают этот пробел; остальные закрытые tasks имеют отдельные оговорки/исторический отчёт25. Ошибка синтаксиса или провал CLI не заявляется.

**ABQA-002** не повышен до подтверждённого runtime-дефекта: повторно прочитанный `HttpStreamResponseResult.Dispose/DisposeAsync:22–32` показывает только Body-before-response без finally. Достижимость исключения на actual Body, утечка ресурса и маскировка primary failure остаются07/14. Новый reproduction не создавался.

#### Открытые вопросы

- **[ABQA-Q-003](OpenQuestions.md#abqa-q-003):** какая workflow-семантика оставшихся неархивированных delta предполагается при уже интегрированном main и переименовании требований — исторические checkpoints или набор для последующего sync? Ответ/CLI evidence нужны для вывода о корректности OpenSpec workflow; текущие требования main и разрешённый A продолжают действовать. Нормативная политика не выбрана и не изменена.
- **ABQA-Q-002:** target ОС/RID/runtime/providers/tools/live codex-lb/model IDs и точные разрешения B/C/D не установлены. Это блокирует deployment/end-to-end выводы, а не эту карту A.
- **ABQA-Q-001:** закрыт координатором; фактически существующий отдельный чат00 снимает прежнее организационное ограничение. Старое упоминание блокера в предварительной записи выше — только её история.

#### Невыполненные проверки

| Что / способ | Причина и влияние | Необходимое окружение/разрешение и будущий сценарий |
| --- | --- | --- |
| OpenSpec strict specs/change validation, B | Прямой запрет CLI/установки; доступность и версия сейчас не проверялись. Нельзя подтвердить parser/sync/rename семантику/готовность archive | Уже доступный CLI, отдельное точное разрешение B, сверка версии и синтаксиса; проверить main и все18 changes, особенно последовательность JSON→SSE→compact и migrations11→settings21. Точная команда сейчас не установима; CLI не запускать/changes не архивировать |
| Compile и isolated suites, B | Прямой запрет. Наличие исходников/logs не подтверждает сборку/проход текущего HEAD | Отдельные конкретные csproj и разрешённые команды после проверки build chain/SDK; EF GeneratePackageOnBuild=false. Для00 новые tests/harness не создавать |
| Provider atomicity/migrations/backup/restore/native, C | Запрещены БД/SQL/processes; архитектурная карта не доказывает locking/cascade/rollback или восстановимость | Собственные согласованные SQLite/PostgreSQL ресурсы, runtime/pg_dump/native версии, точные команды и границы очистки; адресные existing tests04–05/11–14. Нельзя расширять вывод до SQL Server/MySQL |
| Runtime поставки/реальный crash/disconnect, C | Compile/PE и новые DI roots не эти сценарии; среда внедрения не задана | Отдельно согласованное приложение/RID и сценарий реальной загрузки/остановки/потери подтверждения; команда не определена, reproduction в этом проходе не создаётся |
| Live codex-lb/upstream, D | Запрещены внешний HTTP и hosting; fixtures только заданные локальные payload | Согласованные endpoint/commit/accounts/exact models/расходы и операции D; каталоги/JSON/SSE/compact/ownership сверять06–09. Наличие локального репозитория не live доказательство |
| Полная provenance/anchors/SHA/suite overlap, A последующих этапов | Здесь проверены source routes,837 path-ссылок и выборочные TRX/logs/manifest metadata, не все артефакты | Продолжить разрешённое read-only исследование13–14; сверить все ссылки/артефакты с их отчётом и кодом, без суммирования повтора24–25 |

#### Итог и передача

**Статус: проверен с ограничениями.** Все группы задания00 исследованы: текущие источники/история и обязательные пути, полная карта63 main требований, все18 changes/43 delta-блока, ABQA-001, CLI evidence, негативные случаи и границы A/B/C/D. Неподтверждёнными остаются полная корректность ветвей, CLI workflow и runtime совместимость.

Передать01 текущие HEAD/versions и карту settings/access/DI, не предварительный pass01. Передать06–09 [ABQA-004](Findings.md#abqa-004)/[ABQA-Q-003](OpenQuestions.md#abqa-q-003) и фактический main compact-контракт;07 — неизменённый статус ABQA-002;13–14 — исторические manifest/TRX/compile границы;15 — ABQA-001/003 и вопрос нормативной применимости deltas. Координатор самостоятельно обновляет реестры/статусы и присвоил устойчивые ID; исполнитель другие файлы не меняет.

Финальный контроль A, 2026-10-05 22:21 UTC+07:00: собственный diff прочитан; `git diff --check -- <свой00>` успешен,15 локальных ссылок отчёта доступны. Strict UTF-8 без BOM/LF сохранены, U+FFFD и четырёх вопросительных знаков нет; исходное задание и предварительная запись сохранены целиком с отдельной поясняющей вставкой. HEAD не изменён, index пуст, недокументальных изменений в status нет. Дополнительный diff Methodology принадлежит координатору. Приложения/скрипты/build/tests/CLI/DB/HTTP не запускались; исправления продукта не выполнялись.
