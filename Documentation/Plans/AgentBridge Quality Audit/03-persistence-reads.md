# 03 — Чтение и целостность сохраняемых данных

Статус: **проверен с ограничениями**. Предпосылки: 02; карта доменных инвариантов.

## Цель и вопросы

Проверить, что чтение не смешивает владельцев, поколения и состояния диалога, а сериализация не теряет протокольные данные.

## Компоненты и зависимости

[DialogReader](../../../adapters/AgentBridge.Persistence.EfCore/Reading/DialogReader.cs), ExpiredDialogReader, Repositories/*RecordQueries, Mapping и [AgentBridgeDbContext](../../../adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs). EFCoreLibrary: IContextGetItemByIdRepository, IContextGetItemByPredicateRepository. Тесты DialogReaderTests, BaseRepositoryAdapterTests, PersistencePayloadTests, PersistenceModelTests.

## Способ проверки и границы

Проследить parent-aware ключи turn/step, сортировку до limit, no-tracking и повторную проверку root/settings после детей. Негативные случаи: чужой owner до чтения детей, delete/recreate между чтениями, изменение revision/selection, orphan item, повреждённый JSON/FormatVersion, непринятый compact, отсутствующий parent. Проверить unknown/opaque fields, полные envelopes, отдельность Items и ModelSteps, nullable historical journal/settings/provenance. Сверить ContentBytes с сохраняемым UTF-8, исключив служебные метаданные и физический размер БД.

## Разрешения

A; metadata/serialization/fake-repository тесты — B; реальные query translation, collation и restart — C. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Карта шести таблиц, ключей/порядка, field round-trip и read interleavings; отдельные отметки fake/actual provider. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Каждая read-проекция, parent filter и формат хранения имеет результат или явно непроверенную provider-границу. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Транзакционный снимок чтения, атомарные записи и enforcement FK на основании одной EF metadata.

## Результаты

### Состояние и границы

Дата: **2026-10-05, Asia/Novosibirsk**. Исполнитель: отдельный чат03; cwd `D:/Media/User/source/repos/agent-bridge`. Выполнен только **A**: статический разбор исходников, требований, существующих tests/TRX и локального diff. Методы, фабрики, запросы и тестовые сценарии не исполнялись.

| Репозиторий | Повторно проверенный HEAD | Состояние на входе |
| --- | --- | --- |
| AgentBridge | `41072fc37bb718dd9f4bb0473912802260bb9164` | Чужой tracked diff: Markdown00/01/02/Findings/Methodology/README, 440 добавленных/14 удалённых строк; untracked Coordination/OpenQuestions. Свой03 без diff; недокументальных изменений нет |
| EFCoreLibrary | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Чисто; `EFCoreLibrary.csproj`:9, Version0.0.5; только чтение |
| HttpClientLibrary | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Чисто; `HttpClientLibrary.csproj`:7, FileVersion0.0.0.5; только чтение |

Прочитаны README/Baseline/Methodology/Findings/OpenQuestions/Coordination, принятые [00](00-contract-baseline.md), [01](01-configuration-and-access.md), подробно [02](02-domain-invariants.md), исходное задание03; применимые root/Documentation/Plans/Application/Domain/EF adapter/UnitOfWork/tests/Integration AGENTS и root обеих обязательных библиотек. Проверка ancestor путей выше репозиториев дополнительных AGENTS не выявила. Применены `csharp-project-rules` и `backend-uow-repositories`; запрет B/C/D имеет приоритет. Исходное задание выше сохранено, черновика координатора03 не было.

Рассмотрены оба read ports, все шесть `*RecordQueries`, семь persistence DTO, все четыре Mapping-типа, DbContext, read DTO и loader метаданных; actual EFCoreLibrary ById/ByPredicate и их интерфейсы/adapter. Write-код прочитан только для происхождения сохраняемого формата и ContentBytes, без полного transaction-аудита04. Исходящего HTTP в этих путях нет: данные envelope/continuation здесь только сериализуются; полный обзор HttpClientLibrary относится06–07. codex-lb/TelegramCodexRelayBot/AquaByte-Ledger для03 не потребовались и заново не исследованы.

### Шесть таблиц и порядок

Источник — [AgentBridgeDbContext](../../../adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs), `Configure*`; номера строк ниже текущие. Это описание metadata, **не actual FK/check/cascade pass**. `ModelResponseRecord` — required complex payload шага/контекста, а `__AgentBridgeMigrationsHistory` — служебный ledger, не дополнительные mapped entities.

| Таблица | Ключ / parent / порядок | Evidence |
| --- | --- | --- |
| Dialogs | PK Id; incarnation/revision отдельно от Domain lifetime; owner BINARY/C, fixed UTC ticks; index ExpiresAtUtc/Id | DbContext:20–24,37–59; Models/DialogRecord.cs:10–24 |
| DialogTurns | PK DialogId/Id; FK Dialogs.Id cascade; unique DialogId/Sequence; nullable SettingsJson | DbContext:65–79; TurnRecordQueries.cs:10–17 |
| CanonicalItems | PK DialogId/TurnId/Sequence; composite FK DialogTurns.DialogId/Id cascade; ContentJson text | DbContext:85–91; ItemRecordQueries.cs:10–17 |
| ModelSteps | PK DialogId/TurnId/Id; тот же composite parent FK; unique DialogId/TurnId/Sequence; полный Response и nullable ToolAttemptsJson | DbContext:97–109; ModelStepRecordQueries.cs:10–22 |
| DialogContexts | PK DialogId/Version; FK Dialogs cascade; активна max Version, вся история версий доступна loader; prefix>=0/Completed metadata; nullable SelectedModel | DbContext:115–128; ContextRecordQueries.cs:10–17 |
| DialogSettings | PK/FK Id=DialogId, cascade; independent concurrency Version; Model/Effort text | DbContext:134–146; SettingsRecordQueries.cs:10–11; Models/DialogSettingsRecord.cs:11–19 |

### Все группы вопросов

Нормативный источник — [main spec](../../../openspec/specs/agent-runtime/spec.md):380–387 Restore;393–401 независимость JSON;451–501 формат/read paths;509–522 restart/bytes;628–646 settings. Сокращения: **P** = `adapters/AgentBridge.Persistence.EfCore`, **M** = `Application/Models`, **T** = `tests/AgentBridge.Persistence.EfCore.Tests`, **EF** = `D:/Media/User/source/repos/work/EFCoreLibrary`. Все `.cs` пути от соответствующего корня; номер строки проверен по текущему файлу. Tests в таблице — прочитанные сценарии, не текущий pass.

| Вопрос | Исследовано | Результат A | Evidence: символ/строки | Ограничение |
| --- | --- | --- | --- | --- |
| Actual library/base API | ById/ByPredicate, context adapter, ProjectReference | Root/settings используют глобальный ById; остальные predicates/include. Библиотека применяет AsNoTracking, фильтр, include-sort, затем Skip/Take и materialization. Custom query/замена CRUD для этих чтений не нужны | EF/EfCore/Repository/Base/GetItemByIdRepository.cs:13–27, GetItemByPredicateRepository.cs:12–54; EfCore/EfDbContextAdapter.cs:12; P/AgentBridge.Persistence.EfCore.csproj:12–15 | SQL translation и materialization не исполнялись |
| Parent filters / одинаковые локальные ID / missing parent | Все query methods; turn и step lookup | Turn predicate включает dialog+turn; step — dialog+turn+step. Collections ограничены dialog либо dialog+turn; missing lookup даёт null. Чужой локальный ID не является глобальным ключом | P/Repositories/TurnRecordQueries.cs:10–17; ModelStepRecordQueries.cs:10–22; ItemRecordQueries.cs:10–17; T/BaseRepositoryAdapterTests.cs:32–80 | LINQ fakes не actual provider; FK enforcement отдельно C |
| Порядок/active version/limit | Sort всех шести adapters, base include | Turns по Sequence; items/steps внутри turn по Sequence; contexts по Version. Active — descending Version, не дата; вся history остаётся. Cleanup expiry/Id сортируется до limit | P/Repositories/ContextRecordQueries.cs:10–17; DialogRecordQueries.cs:16–25; EF/GetItemByPredicateRepository.cs:42–54 по полному пути выше; T/BaseRepositoryAdapterTests.cs:85–98 | GUID order разных providers не сравнивался; требуется стабильный порядок внутри выбранного provider, не одинаковый межпровайдерный порядок |
| Чужой owner/NotFound до детей | Начало DialogReader | Null root→NotFound, Ordinal mismatch→Forbidden без snapshot/детей/staging; нет trim/casefold. Истёкший существующий диалог читается для metadata/history | P/Reading/DialogReader.cs:22–43,85–87; Application/Ports/IDialogReader.cs:6–12; T/DialogReaderTests.cs:97–131 | Trusted owner и права приложения не аутентифицируются библиотекой |
| No-tracking/scoped context/cancel/errors | Defaults queries, sequential await, gate | Reads no-tracking; tracking root/turn/settings только explicit flag инфраструктуры. Один read gate, без Task.WhenAll/UoW/SaveChanges; cancellation/неожиданные ошибки не превращаются в пустой успех | P/Reading/DialogReader.cs:22–48; ExpiredDialogReader.cs:18–23; P/UnitOfWork/PersistenceOperationGate.cs:10–21; T/DialogReaderTests.cs:138–162 | Gate одного DI scope не блокирует другие scopes/processes; общий lifetime уже рассмотрен01 |
| Root interleavings/delete/recreate/revision | Primitive capture и второй ById после детей | Фиксируются owner/incarnation/revision/dates/bytes до await. Delete→NotFound; changed owner→Forbidden; новая incarnation/revision→Conflict без новых token/retry | P/Reading/DialogReader.cs:36–60,85–87; T/DialogReaderTests.cs:192–228 | Это recheck, не transaction snapshot; требует root revision при штатных child writes. Corrupt/out-of-band writes без revision не покрываются |
| Независимый settings recheck | Nullable first/second selection, ToSelection | Settings читаются после детей и повторно после root; различие Version (null=0)→Conflict. DTO сохраняет первый immutable selection. Выбор не приравнен к history revision | P/Reading/DialogReader.cs:47–63,87; Models/DialogSettingsRecord.cs:19; M/DialogModelSelection.cs:7–21 | Полный interleaving на actual БД не проверен; нет обещания snapshot isolation между двумя rechecks |
| Orphan data/непринятый compact/отсутствие окна | Stable-root mapping и no fallback | Item/step без turn→exception, не молчаливое отбрасывание; active compact не Completed→exception. Null context остаётся null; prefix не фильтрует Turns/Items | P/Reading/DialogReader.cs:64–87; T/DialogReaderTests.cs:21–92,235–256 | Проверяется активный payload; public read не валидирует все historical contexts как Domain |
| Полные lifecycle/unknown/opaque/envelopes/items | Record.From/ToModelResponse, canonical clones и read grouping | Полный ordered OutputJson, EnvelopeJson, ContinuationJson и safe ErrorMessage/type раздельны. Четыре lifecycle восстанавливаются; unknown поля/large numbers/opaque/call_id остаются JSON. Items и ModelSteps — отдельные коллекции, envelope не второй input | P/Models/ModelResponseRecord.cs:26–80; P/Mapping/ModelResponseMapping.cs:11–20; P/Reading/DialogReader.cs:78–94; M/CanonicalModelItem.cs:10–16; ContractSnapshot.cs:9–26; T/PersistencePayloadTests.cs:19–60 | Сохраняется JSON-содержимое, не гарантируется byte-identical whitespace/escaping OutputJson; opaque не полный token estimate и continuation не upstream authorization proof |
| Corrupt JSON/FormatVersion/lifecycle | Все mapping readers и DTO constructors | Version!=1/undefined status/error mismatch/non-array output/scalar item-envelope-continuation отклоняются без fallback. Syntax parse failures распространяются. Undefined ErrorType дополнительно отвергает ServiceError constructor | P/Models/ModelResponseRecord.cs:43–72; P/Reading/DialogReader.cs:91–94; M/CanonicalModelEnvelope.cs:10–16, ModelContinuation.cs:10–16; Application/Results/ServiceError.cs:7–14; T/PersistencePayloadTests.cs:73–94; DialogReaderTests.cs:169–182 | Отказов по настоящим повреждённым provider rows не получено; сообщения exceptions не разрешают логировать payload |
| Nullable historical journal/identity/corruption | ToolAttemptMapping и StoredModelStep | Null journal→empty explicit attempts, без разрешения replay. Version1/fields parse; negative position/blank agent/undefined state/duplicates отклоняются; StoredModelStep требует output index реального function_call | P/Mapping/ToolAttemptMapping.cs:10–34; M/StoredToolAttempt.cs:7–15; StoredModelStep.cs:9–25; P/Reading/DialogReader.cs:80–81 | Replay/Started recovery подробно11; пустой journal не доказывает отсутствия прежнего внешнего действия |
| Nullable settings/provenance/секреты | TurnSettingsMapping, Models, reader | Null SettingsJson/SelectedModel/selection остаются неизвестными; version1 primitive settings проходят конструктор limits/model/effort. Corrupt/missing version/snapshot не заменяется defaults. SelectedModel отдельно от envelope server model; ModelAccess/key колонок нет | P/Mapping/TurnSettingsMapping.cs:10–20; M/TurnModelSettings.cs:7–17; P/Reading/DialogReader.cs:81–87; T/DialogSettingsMappingTests.cs:18–55; Integration/DialogSettingsIntegrationTests.cs:214–249 | Каталог/compatibility не доказываются восстановлением; schema migration/Down/Up05 здесь не исполнялись |
| Полная Domain rehydration / ABQA-005 | DialogStateLoader, public read DTO vs Domain | Loader читает все ordered turn/context metadata, проверяет bytes>=0/Completed, передаёт исходные root revision/LastChanged в Restore. Не replay и не новый persisted token; public DialogReader возвращает DTO и не вызывает Restore | P/UnitOfWork/DialogStateLoader.cs:12–23; Domain/Dialogs/Dialog.cs:67–128; M/DialogSnapshot.cs:5–25; T/DialogWritePortsTests.cs:49–60 | Существующая первопричина ABQA-005 проходит этот metadata route; actual corrupt row/штатное её происхождение не доказаны |
| UTC/collation/шесть таблиц | Model/converter/metadata tests | UTC ticks сохраняют declared integer precision; nonzero offset на запись отклоняется. Owner BINARY/C и application Ordinal. Composite keys/FK/cascade описаны выше; settings — шестая entity | P/AgentBridgeDbContext.cs:20–59,65–146; P/Mapping/UtcTicksConverter.cs:9–20; T/PersistenceModelTests.cs:17–36,43–102,109–121; DialogSettingsMappingTests.cs:18–29 | Actual collation/constraint enforcement/query translation/restart не pass по metadata |
| ContentBytes/UTF-8/duplicates/legacy metadata | StoredContentSize и места формирования/дельты | UTF-8 фактически сохраняемых ContentJson; OutputJson+EnvelopeJson+ContinuationJson+ErrorMessage каждого step и каждой принятой версии compact; ToolAttemptsJson учитывается дельтой new−old. Сохранённые копии output считаются каждая. Settings/model provenance/числовые metadata/index/provider overhead исключены | P/UnitOfWork/StoredContentSize.cs:10–13; TurnContentStaging.cs:29–56; DialogContextUnitOfWork.cs:57–63; DialogToolAttemptUnitOfWork.cs:86–92; DialogWriteResults.cs:24–29; T/DialogWritePortsTests.cs:69–100; Integration/PersistenceIntegrationTests.cs:80–90 | Reader возвращает stored root.ContentBytes без recount (DialogReader:41,87); checked arithmetic рассмотрена статически. Atomic update и реальная согласованность счётчика относятся04 |
| Cleanup candidates/equality/bounds | ExpiredDialogReader и base predicate | now UTC/limit>0 fail-fast; ExpiresAtUtc<=now включая equality, bounded sorted candidates с incarnation/revision. Нет удаления/цикла/retry или owner payload | P/Reading/ExpiredDialogReader.cs:15–23; P/Repositories/DialogRecordQueries.cs:16–25; T/ExpiredDialogReaderTests.cs:15–82 | Кандидат не разрешение удаления и не обещание отсутствия других expired rows; fresh delete guards04/12 |

### Находки и вопросы

**Новых S03-FNN кандидатов и S03-QNN вопросов нет.** Исследованные read/format paths соответствуют указанным требованиям в границах A. Это не доказательство отсутствия других дефектов или прохождения runtime-проверок. Общие реестры не изменены.

**[ABQA-005](Findings.md#abqa-005), без дублирования первопричины.** Для context-only snapshot этапа02 (`revision=1`, context created=t0+1min, root LastChanged=t0+2min, остальные поля корректны) `DialogStateLoader.LoadAsync`:14–23 передаёт именно эти значения в `Dialog.Restore`; его предварительные guards16 не обнаруживают несогласованный LastChanged. DbContext checks40–45/118–121 не выражают равенство этой хронологии. Public `DialogReader`:36–41,83–87 вообще не проецирует LastChanged/context created и не вызывает фабрику; чтение такого DTO само по себе не доказывает валидное Domain state. Это статическая связь с reader/loader, **не новая находка, не реальная строка БД и не runtime-воспроизведение**. Обычные writes как источник такого состояния здесь не установлены;04 проверяет происхождение/guards,14 — пробел адресного сценария. Серьёзность и достоверность ABQA-005 не повышены.

[ABQA-Q-002](OpenQuestions.md#abqa-q-002) остаётся общей неопределённостью окружений B/C/D; A03 завершён независимо. ABQA-001/003/004 и Q-003 не переоценивались. ABQA-002 не повышен до runtime-утечки: disposal не относится к этому read path.

### Tests и историческое evidence

Полностью прочитаны DialogReaderTests, BaseRepositoryAdapterTests, PersistencePayloadTests, PersistenceModelTests, ExpiredDialogReaderTests, DialogSettingsMappingTests и FakeBaseRepository. Адресно — DialogWritePortsTests49–100; Integration/PersistenceIntegrationTests27–92; DialogSettingsIntegrationTests34–54,214–249; AgentRunnerIntegrationTests70–96. Последние сценарии создают actual provider resources при запуске, **сейчас только прочитаны**. FakeBaseRepository:39–73,90–101 выполняет LINQ in-memory и синхронные BeforeRead; его no-tracking flag не actual EF tracking/SQL.

Статически разобраны TestRun/Times/UnitTestResult сохранённых TRX:

| Артефакт / дата UTC+07 | Что действительно прочитано | Предел |
| --- | --- | --- |
| [stage21/persistence-final.trx](../../../artifacts/test-results/stage21/persistence-final.trx), 2026-10-04 16:49 | 66 Passed записей шести основных isolated классов03, включая settings mapping | Исторический171-case suite по [отчёту21](<../AgentBridge Initial Implementation/21-settings-and-dialog-status.md>):24,49–63 использовал EF0.0.5; это не новый проход или полная source/build hash сверка |
| [integration-20261004/integration-final.trx](../../../artifacts/test-results/integration-20261004/integration-final.trx), 2026-10-04 09:11 | 8 Passed записей RestartRoundTripPreservesFullHistory | Actual SQLite/PostgreSQL в прежнем запуске на EF0.0.4; [исторический README](<../AgentBridge Initial Implementation/README.md>):11,79,103 запрещает выдавать его за всю0.0.5 |
| [stage21/settings-and-runner-db-final.trx](../../../artifacts/test-results/stage21/settings-and-runner-db-final.trx), 2026-10-04 16:49 | 4 Passed записи selection restart/provenance historical rows | Адресная actual persistence0.0.5 по отчёту21:24,56; не общий повтор старого persistence набора |
| [stage20/runner-db-verified.trx](../../../artifacts/test-results/stage20/runner-db-verified.trx), 2026-10-04 15:15 | 4 Passed записи RestartDoesNotReplayStartedOrLegacyTurn | Новый root DI container и synthetic fixtures, не OS crash/external exactly-once; provenance14 |

Эти числа относятся к выбранным записям, **не суммируются в покрытие или current pass**. Наличие исторического TRX не доказывает соответствие каждой бинарной сборки текущему41072fc. [Исторический09](<../AgentBridge Initial Implementation/09-base-repository-adapters.md>):35–55 сохраняет исходные isolated границы; его дополнительный DB абзац относится прежней проверке00–13. Исторические разрешения не использованы.

### Пропуски

| Что / уровень | Причина и влияние | Окружение/разрешение и будущий сценарий |
| --- | --- | --- |
| Current compile/isolated metadata/payload/read tests, B | Прямой запрет; assertions и current binary не исполнены | Отдельные точные команды persistence test csproj, .NET10/проверенные packages/build imports/временные outputs; GeneratePackageOnBuild=false, фильтр Dependency!=Database. Выполнить названные шесть классов, collisions/order/cancel/corrupt JSON/lifecycle/UTC/expiry equality. Точная команда сейчас не утверждается: разрешённый fresh build chain не установлен, historical --no-build может использовать stale binary |
| Root/settings controlled interleavings и journal corruptions, B | Статический путь есть; полный набор негативных permutations здесь не воспроизведён. Отсутствие адресного assertion не доказывает баг | Future сценарии: root delete/recreate/owner/revision; settings null→version1, update, removal между двумя reads при стабильном root; journal version/duplicates/out-of-range/non-function position. Existing root cases192–228 не заменяют settings/journal cases. Недостающий test/harness требует отдельного поручения вне аудита; не создан, готовая команда не установима |
| Actual query translation/collation/FK/order/restart/bytes, C | DB/SQL/migrations запрещены; static routes и metadata не actual DB pass | Собственные согласованные SQLite/PostgreSQL resources и точные команды existing Integration scenarios после фиксации Q-002/04–05. Round-trip полного JSON/порядка/legacy/settings; expiry ticks±1/equality/limit; collisions/cascade. Opt-in AGENTBRIDGE_INTEGRATION=1 и Dependency=Database сами по себе не разрешают запуск. SQLServer/MySQL не AgentBridge providers |
| ABQA-005 через persisted metadata, B/C | Фабрика/loader не вызваны, реальная corrupt строка не предъявлена; runtime-эффект неизвестен | Отдельный сценарий context-only revision1/неверный LastChanged и законный extra append revision для негативной границы; собственные disposable provider resources для C. Средство воспроизведения требует отдельного поручения; команда неизвестна |
| Atomic reads/writes/maintenance/recovery/live continuation, 04–07/11 | Полный аудит следующих этапов вне03; B/C/D запрещены | Передать маршруты ниже. Transaction snapshot, writer CAS/rollback/commit uncertainty, FK enforcement, OS crash/live upstream ownership требуют собственных evidence и разрешений; current report этого не обещает |

Build/tests/app/hosting/CLI/scripts/exe/harness/Docker/DB/SQL/migrations/backup/restore/native/HTTP не запускались; новые tests/harness/scripts не создавались. Production/dependencies/config/build/AGENTS/spec/history не изменены; Git только status/diff/rev-parse.

### Итог и передача

**Проверен с ограничениями.** Все группы03 имеют статический результат или явную provider/runtime-границу. Новых кандидатов/вопросов нет; связь **ABQA-005→DialogStateLoader** подтверждена по коду без дублирования. Current runtime pass, snapshot isolation, atomic ContentBytes/child writes, FK/collation enforcement, restart/crash и live continuation не доказаны.

Передать04 parent predicates/no-tracking и обязанность root revision при children changes, original persisted token, loader/ABQA-005, формулу ContentBytes и journal replacement delta;05 шесть entities/ticks/collation и nullable metadata;08 всю history + active prefix без cutoff/двойного envelope input;09 opaque сохранён, но full estimate не получен;11 nullable journal не разрешает replay;12 sorted expired candidates не deletion authorization;14 read/settings/journal permutations и provenance/overlap старых TRX. Полный аудит этих этапов не выполнен. Координатор самостоятельно прочитает файл и обновит общие реестры.

Финальный контроль A: собственный diff проверен; diff --check успешен. Все19 локальных path-ссылок доступны; ID-fragments ABQA-005/Q-002 сверены с реестрами. Strict UTF-8 без BOM/LF сохранены, U+FFFD/mojibake/четырёх вопросительных знаков нет. Исходное задание сохранено, кроме статуса и заполнения «Результаты». SHA256 восьми чужих Markdown на входе/выходе совпадают; index пуст, недокументальных изменений нет. Единственная собственная правка — этот03; зависимости чисты, HEAD неизменны. Предупреждение autocrlf при read-only diff не меняло файл или Git config.
