# Реестр проблем AgentBridge

[План](README.md) · [Методика](Methodology.md) · [Baseline](Baseline.md)

Реестр открыт на подготовительном чтении; статический проход A выполнен 2026-10-05–06 на HEAD 41072fc37bb718dd9f4bb0473912802260bb9164 и завершён с ограничениями. Десять устойчивых записей сверены в [итоге15](15-final-reconciliation.md); B/C/D не исполнялись. Запись не разрешает исправление. Результаты и история снятого блокера отдельных чатов — [Coordination](Coordination.md).

## Классификация

Категории: дефект реализации; подозрение на дефект; пробел проверки; расхождение документации; неоднозначность требования; новое пожелание. Для пожелания нужен отдельный запрос, оно не считается нарушением действующего контракта.

Статус достоверности: **подтверждено / подозрение / требуется уточнение**. Он не означает «исправлено». Опровергнутая гипотеза сохраняет ID с отдельной заметкой о решении и доказательством; ID не переиспользовать.

Серьёзность: S1 — утечка доступа, потеря данных или опасное повторное действие; S2 — отказ ключевого сценария, некорректное сохранение или значимая утечка ресурсов; S3 — ограниченный сбой/неверное описание; S4 — малое неудобство. Всегда обосновывать фактический либо условный ущерб. Для пробела и пожелания допустимо «не назначена» до оценки риска.

## Список

| ID | Название | Категория | Серьёзность | Достоверность | Дальнейшая проверка |
| --- | --- | --- | --- | --- | --- |
| ABQA-001 | Текущие описания отстают от завершённой реализации | Расхождение документации | S3 | Подтверждено статически | 00, 15 |
| ABQA-002 | Ошибка освобождения Body может пропустить освобождение response | Подозрение на дефект зависимости | S2, предварительно | Подозрение | 07, 14 |
| ABQA-003 | OpenSpec CLI evidence отсутствует для 18 открытых changes | Пробел проверки | Не назначена | Подтверждено как пробел | 00, 15 |
| ABQA-004 | Старые transport deltas сохраняют промежуточный запрет compact | Расхождение OpenSpec | S3, предварительно | Требуется уточнение | 00, 06, 09, 15 |
| ABQA-005 | Restore принимает время последнего изменения без соответствующей мутации | Дефект реализации | S3 | Подтверждено статически | 02, 03, 04, 14 |
| ABQA-006 | Отказ после CREATE допускает обслуживание тем же gate | Дефект зависимости | S2 | Подтверждено статически | 05, 14, 15 |
| ABQA-007 | Вторичный cleanup теряет первичный код maintenance | Дефект зависимости | S3 | Подтверждено статически | 05, 14, 15 |
| ABQA-008 | Отмена после пустого SSE EOF возвращает Canceled вместо OCE | Дефект реализации | S3 | Подтверждено статически | 07, 14, 15 |
| ABQA-009 | Cleanup-only OCE runner маскируется как caller cancellation | Дефект реализации | S3 | Подтверждено статически | 11, 14, 15 |
| ABQA-010 | Gated test work остаётся pending при раннем assertion failure | Пробел проверки / cleanup тестов | S4 | Подтверждено статически | 14, 15 |

<a id="abqa-001"></a>

## ABQA-001 — Текущие описания отстают от завершённой реализации

- **Ожидание:** требование «Честное закрытие первоначального плана» в [spec](../../../openspec/specs/agent-runtime/spec.md); текущий навигатор и бизнес-описание должны отличать завершённую реализацию от исторических checkpoints.
- **Факт на baseline:** Plans/README.md строка 7 сообщает «этапы 11–25 не начаты»; Business logic/01-purpose-and-scope.md строка 34 относит прикладной сценарий, HTTP и хранение к будущим этапам. Business logic/02-dialogs-and-tools.md строка 34 называет AgentRunner20 будущим. Это противоречит текущему коду и commit c8da604.
- **Воспроизведение:** локально прочитать версии файлов через git show c8da604:Documentation/Plans/README.md и аналогично два бизнес-файла; сопоставить с [отчётом 25](<../AgentBridge Initial Implementation/25-usage-guide-and-closure.md>) и [AgentRunner.RunAsync](../../../Application/AgentRunner.cs).
- **Доказательства:** [навигатор планов](../README.md), [назначение](<../../Business logic/01-purpose-and-scope.md>), [диалоги](<../../Business logic/02-dialogs-and-tools.md>); прежний текст доступен в указанном commit. Исполняемый публичный сценарий находится в Application/AgentRunner.cs, строки 15–23.
- **Влияние/S3:** читатель ошибочно считает существующие возможности нереализованными; runtime-дефект из этого не следует.
- **Ограничения:** чтение документов и кода, без runtime. Сохранённые исторические STOP в первоначальных отчётах сами по себе не дефект.
- **Связи:** этап 00 проверяет и остальные текущие технические описания/инструкции; одинаковое устаревание не дублировать новой записью.
- **Состояние документации после подготовки:** перечисленные бизнес-страницы и текущий навигатор актуализированы отдельной разрешённой частью поручения. Это не исправление кода и не прохождение этапа 00; запись сохраняет исходное расхождение. Технические документы и AGENTS не переписывались.

<a id="abqa-002"></a>

## ABQA-002 — Ошибка освобождения Body может пропустить освобождение response

- **Ожидание:** [Clients/AGENTS.md HttpClientLibrary](../../../../work/HttpClientLibrary/Clients/AGENTS.md) описывает владение ресурсами; требование «Владение streaming lifecycle» в [spec](../../../openspec/specs/agent-runtime/spec.md) требует освобождения потока/обёртки при выходе.
- **Статический факт:** [HttpStreamResponseResult](../../../../work/HttpClientLibrary/Models/HttpStreamResponseResult.cs), методы Dispose и DisposeAsync, строки 22–32 на HEAD 6d0528d: вызов Body.Dispose/DisposeAsync предшествует response.Dispose без finally. Если первый вызов выбросит исключение, второй по этому пути не выполнится.
- **Зависимый путь:** [CodexLbModelGateway](../../../adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs), GenerateStreamAsync, await using около строки 95.
- **Условия и будущая проверка:** stream с исключением на disposal, наблюдаемый HttpResponseMessage; проверить синхронный и асинхронный пути, первичную ошибку чтения/callback вместе с cleanup. Сейчас такой пример не создавался и не исполнялся.
- **Фактическое поведение:** подтверждён только порядок вызовов в исходнике; достижимость на конкретном runtime-потоке, утечка и изменение исходной ошибки не воспроизведены.
- **Влияние/S2 предварительно:** при достижимости возможны неосвобождённый response и потеря первичной причины сбоя потокового обращения. Масштаб зависит от реализации Body; не объявляется доказанной утечкой соединений.
- **Ограничения:** соседняя библиотека только для чтения. На подготовке полного обзора disposal-тестов не было; адресное покрытие затем рассмотрено в07/14, см. дополнения ниже. Это не исполнение throwing-disposal сценария и не доказательство утечки.
- **Связи:** этап 07 — lifecycle, этап 14 — существующее покрытие. Новая запись для того же порядка disposal не нужна. Исправлений нет.

## ABQA-003

- **Название:** OpenSpec CLI evidence отсутствует для 18 открытых changes.
- **Категория / статус / серьёзность:** пробел проверки; подтверждено по tasks и историческим отчётам, не ошибка продукта; серьёзность не назначена, риск — неподтверждённое закрытие workflow.
- **Контракт:** [spec](../../../openspec/specs/agent-runtime/spec.md):19–27, честное закрытие и запрет архивирования непроверенных changes.
- **Факт:** [этап00](00-contract-baseline.md#все-открытые-changes) перечисляет все18; девять tasks содержат открытый CLI-пункт. Остальные чекбоксы не являются логом CLI. Отчёт25 прямо говорит, что CLI validation не выполнена.
- **Условия / проверка:** перед заявлением о готовности workflow сверить точные main/delta specs доступным CLI с отдельным B-разрешением; сейчас только A.
- **Доказательства:** openspec/changes/*/tasks.md; [отчёт25](<../AgentBridge Initial Implementation/25-usage-guide-and-closure.md>), раздел «Ограничения и пропуски»; HEAD41072fc.
- **Влияние:** нельзя объявить прохождение OpenSpec validation/готовность archive. Наличие реальной ошибки синтаксиса не установлено.
- **Ограничения:** CLI не запускалась/не устанавливалась; текущая доступность неизвестна. Статическая сверка не заменяет CLI.
- **Связи:** ABQA-Q-002; одна запись охватывает общий пробел всех18, без дублей по этапам.

### Переоценка ABQA-001, 2026-10-05

Три исходные ссылки исправлены до аудита commit41072fc; историческое расхождение сохраняет ID. Текущая Technical documentation/01-architecture.md:7,36 явно отделяет checkpoint от реализации14–22. После запуска этого аудита внешний [навигатор планов](../README.md):8 продолжает говорить «этапы 00–15 не начаты». Это ограниченное S3 расхождение документации того же класса; файл вне разрешённой области, не исправлялся. Исторические STOP не являются действующими статусами.

## ABQA-004

- **Название:** старые transport deltas сохраняют промежуточный запрет compact.
- **Категория / достоверность:** расхождение документации/OpenSpec; требуется уточнение нормативной применимости. Текстовое различие подтверждено, runtime-дефект и ошибка CLI не доказаны.
- **Серьёзность:** S3 предварительно — читатель незакрытых changes может получить противоречивый API-контракт; фактическая поломка продукта не установлена.
- **Ожидаемый контракт:** [main spec](../../../openspec/specs/agent-runtime/spec.md):115,276 разрешает отдельный compact transport; Documentation/AGENTS требует согласованности требований.
- **Наблюдение:** [JSON delta](../../../openspec/changes/responses-json-adapter/specs/agent-runtime/spec.md):19 запрещает compact/streaming; [SSE delta](../../../openspec/changes/responses-sse-adapter/specs/agent-runtime/spec.md):30 запрещает compact. [Compact delta](../../../openspec/changes/context-compaction/specs/agent-runtime/spec.md) содержит четыре ADDED блока без MODIFIED прежнего safe-error блока.
- **Условия / проверка:** читать эти строки при трактовке всех открытых delta как текущих правил; сравнить историю d8a673f →1d99721 →73045a1. История объясняет промежуточный запрет, но не устанавливает будущую sync/rename семантику.
- **Доказательства:** [отчёт00](00-contract-baseline.md), кандидат S00-F01, HEAD41072fc; координатор повторно проверил названные строки. Реализация CompactAsync существует и не объявляется дефектом.
- **Влияние:** неопределённость применимости накопленных delta/готовности workflow, не доказанный отказ compact.
- **Ограничения:** CLI не запускалась; владелец не выбирал в аудите новую нормативную политику. Документы требований не изменялись.
- **Связи:** [ABQA-Q-003](OpenQuestions.md#abqa-q-003); ABQA-003 — отдельный общий пробел CLI evidence. Новая запись не дублирует его причину.

### Дополнение ABQA-001 по результату00

На HEAD41072fc Technical documentation/01-architecture.md:52,66 распространяет оговорку о будущих типах на AgentSettingsService, хотя Application/AgentSettingsService.cs:10,16,28,50 содержит реализованный public API. Business logic/README.md:28 также сообщает, что аудит пока не начат. Статическое расхождение подтверждено, S3; документы вне разрешённой области не изменялись. Координатор повторно проверил эти ссылки.

## ABQA-005

- **Название:** Restore принимает LastChangedAtUtc без соответствующего изменения.
- **Категория / достоверность:** дефект реализации; подтверждено статическим разбором условий, не runtime-воспроизведением.
- **Серьёзность:** S3 — некорректная хронология проходит валидирующую фабрику и может позднее вызвать ограниченный отказ записи. Потеря данных, повреждённая реальная БД или штатная генерация такого snapshot не установлены.
- **Контракт:** [spec](../../../openspec/specs/agent-runtime/spec.md):380–387 требует отклонять нарушения локальных инвариантов до выдачи агрегата. [Dialog](../../../Domain/Dialogs/Dialog.cs):38–39 определяет LastChangedAtUtc как время последнего принятого изменения; CommitRevision:338–342 фиксирует now.
- **Наблюдение:** Restore:73–79,81–128 проверяет bounds и нижнюю границу revision, но принимает revision=1, пустые turns, один context(version=1,prefix=0,created=t0+1min) при lastChanged=t0+2min, created=t0, expiry=t0+1day. Все даты UTC, ID/owner корректны.
- **Статическое доказательство:** minimumRevision после единственного context равен1; все predicates121–124 ложны. Для живого Domain без turns одна mutation revision1 — TryApplyContext:244–267 — создаёт context и LastChanged с одним now. Append:132–148 требует существующий InProgress turn; settings не меняет root; Delete не восстанавливается как живой root. Это не допустимый скрытый append.
- **Условия / будущая проверка:** передать описанный context-only snapshot в Restore; ожидается отказ. Затем отдельно проверить допустимый snapshot с append и extra revision, где LastChanged вправе быть позже child timestamp. Пример только выведен по исходникам, исполняемый harness/test не создавался.
- **Влияние:** принятый неверный LastChanged становится барьером NextRevision:327–334; TryBeginTurn при t0+1min+1tick статически приводит к исключению, хотя время позже единственного context. Реальное проявление не воспроизведено.
- **Доказательства:** [этап02](02-domain-invariants.md), исходный кандидат S02-F01; HEAD41072fc. [DialogRestorationTests](../../../tests/AgentBridge.Tests/DialogRestorationTests.cs):14–38,54–74 содержит корректный append round-trip и12 других corruptions, но не доказывает этот случай. Отсутствие теста не основание доказательства дефекта.
- **Ограничения:** A; нет вызова фабрики, actual persistence или изменения кода. Достижимость через сохранённые данные рассматривается03–04. Нельзя требовать равенство LastChanged/child времени для всех snapshots, поскольку append timestamps отдельно не хранятся.
- **Связи:**03–04,14; другая первопричина, чем ABQA-001–004; новых бизнес-решений не требуется.

### Дополнение ABQA-005 по результату03

[DialogStateLoader](../../../adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogStateLoader.cs):14–23 передаёт root.Revision/LastChangedAtUtc и context.CreatedAtUtc прямо в Restore; предварительные проверки16 не исключают контрпример02. Координатор сверил этот путь. Public DialogReader:36–41,83–87 возвращает DTO без LastChanged и не вызывает Restore. Это связь с загрузкой внутри write scope, не новая первопричина и не доказательство corrupt строки/штатного происхождения/реального отказа. Статус и S3 не повышены; происхождение проверяет04.

### Дополнение ABQA-005 по результату04

Из корректного baseline рассмотренные штатные writes не порождают конкретный context-only контрпример: [DialogContextUnitOfWork](../../../adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogContextUnitOfWork.cs):57–65 и [DialogWriteResults](../../../adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogWriteResults.cs):24–29 переносят обе даты принятого Domain-изменения. Settings root не меняет, Append/journal требуют существующий turn. Координатор сверил эти пути. Дефект валидации внешнего snapshot остаётся, S3; источник реальной corrupt строки не найден, actual atomicity не проверялась.

## ABQA-006

- **Название / категория / статус:** отказ discovery после CREATE не блокирует gate; дефект реализации EFCoreLibrary; подтверждено статически.
- **Серьёзность:** S2 — нарушена обязательная защита дальнейшего обслуживания после незавершённой первой установки. Реальная потеря данных или успешность повторного update не установлены.
- **Контракт:** [spec](../../../openspec/specs/agent-runtime/spec.md):731–733 требует запрета последующего обслуживания тем же gate после отказа начавшейся установки.
- **Факт:** [DatabaseMaintenance.ChangeAsync](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs):46–56 после успешного Create/Inspect/Pin/binding заменяет Stage=Initialization на Discovery. ExecuteAsync:102–108 проверяет текущую Stage, а не факт CREATE.
- **Условия / статический сценарий:** Missing → CREATE успешен → PendingAsync:56 throws обычную ошибку/OCE → pin cleanup успешен. Stage=Discovery, условие107 ложно, lease освобождается113; следующий Inspect/Update того же root допускается к provider. Аналогичный промежуток перед migration — Inspection:68–71.
- **Проверка / evidence:** координатор независимо сверил исходники, [gate](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/SingleInitializerGate.cs):9–26 и [отчёт05](05-migrations-and-maintenance.md). HEAD AgentBridge41072fc, EFCoreLibrary3a8a531. Existing DatabaseMaintenanceTests:67–75,203–212 проверяют другие границы.
- **Влияние:** приложение может продолжить обслуживание без требуемого внешнего recovery; наличие или состояние частично созданной реальной БД не исследовано.
- **Ограничения / будущее evidence:** только A, не исполнено. B-сценарий failure/OCE в pending/final recheck после CREATE и проверка запрета provider calls следующего scope; C — собственная БД. Новое средство воспроизведения требует отдельного поручения; точной готовой команды нет.
- **Связи:**05/14/15; Q-002 для окружений. Другая первопричина, чем Restore ABQA-005 и cleanup diagnosis ABQA-007.

## ABQA-007

- **Название / категория / статус:** secondary cleanup теряет первичный безопасный код; дефект EFCoreLibrary, подтверждён статическим порядком исключений.
- **Серьёзность:** S3 — утрачивается причина для диагностики/recovery. В основном примере gate блокируется, DDL не начинается; утечка и потеря данных не заявляются.
- **Контракт:** [spec](../../../openspec/specs/agent-runtime/spec.md):750–751 и [Errors/AGENTS](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Errors/AGENTS.md) требуют сохранить первичный код при secondary cleanup.
- **Факт:** [DatabaseMaintenance](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs):53–79 использует await using без capture primary. [EfMigrationOperations.ConnectionLease.DisposeAsync](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/EfCore/EfMigrationOperations.cs):40–43 выдаёт CleanupUnconfirmed без PrimaryError; coordinator:111 копирует null.
- **Условия / сценарий:** BackupAsync throws MaintenanceException(BackupNotConfirmed), затем CloseConnectionAsync throws. Dispose заменяет первичное исключение; наружу CleanupUnconfirmed/PrimaryError=null. Без secondary ошибки typed code сохраняется. Это статический контрпример, не runtime-воспроизведение.
- **Связанные проявления той же утраты primary:** [BackupProcessRunner.RunAsync](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Processes/BackupProcessRunner.cs):29–35 заменяет ошибку на CleanupUnconfirmed при unknown stop; [SqliteBackupStepper.CopyAsync](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.Sqlite/Backup/SqliteBackupStepper.cs):32–35 может заменить pending error/OCE ошибкой Finish. Координатор прочитал эти ветви; не считает их отдельными дубликатами нарушения сохранения primary.
- **Evidence:** [отчёт05](05-migrations-and-maintenance.md), HEAD EF3a8a531/AgentBridge41072fc; существующие DatabaseMaintenanceTests:175–194 проверяют cleanup отдельно либо получают уже готовый primary от fake, не комбинацию pin.
- **Влияние / ограничения:** безопасная причина сбоя теряется при сочетании двух ошибок. Достижимость на конкретном driver/native/process не проверена; ресурсная утечка не доказана. Только A.
- **Будущая проверка:** отдельное B-разрешение на комбинации typed failure+FailCleanup, process primary+unknown stop, native primary+finish failure; новое средство проверки отдельно поручается. Сейчас не создано и не запущено, готовая команда неизвестна.
- **Связи:**05/14/15, Q-002. Не повышает ABQA-002 до доказанной HTTP runtime-утечки; отличается от пропуска gate ABQA-006.

## ABQA-008

- **Категория / статус / серьёзность:** дефект реализации, подтверждён статическим разбором; S3 — ограниченное расхождение результата и контракта исключения/токена отмены, без доказанной потери данных.
- **Контракт:** [spec](../../../openspec/specs/agent-runtime/spec.md):253 требует caller OCE с исходным token до данных; [техничка15](<../../Technical documentation/15-responses-sse-adapter.md>):34 связывает Canceled с canonical response/item/delta.
- **Факт:** [CodexLbModelGateway.GenerateStreamAsync](../../../adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs):112–114 после успешного EOF/cleanup проверяет caller без HasData. В catch127–130 эта граница проверяется. [ResponseSseState](../../../adapters/AgentBridge.CodexLb/Responses/ResponseSseState.cs):15–18,121–137 создаёт для пустого состояния Canceled с пустым output/null envelope/continuation.
- **Статический сценарий:** HTTP2xx, пустой body → EOF без Apply → успешный disposal отменяет caller без исключения → возвращается Ok(Canceled), хотя данных не было. Deadline не сработал, callbacks0. Ожидается OCE с исходным caller token. Без отмены пустой EOF остаётся Incomplete; при canonical data поздний Canceled допустим.
- **Evidence / проверка:** координатор прочитал gateway/state/spec; HEAD41072fc. [ResponsesSseTests](../../../tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs):239–252,433–442,637–660 содержит отдельные возможности empty stream и cancel-on-disposal, но не это сочетание. [Отчёт07](07-sse-and-cancellation.md). Методы не вызывались; отсутствие assertion не основание дефекта.
- **Влияние:** consumer получает lifecycle result вместо требуемого исключения; фактическое влияние на приложение не воспроизведено.
- **Ограничения / будущее evidence:** A; реальный app/network stream не исследован. Отдельный B-case empty EOF+successful cancel disposal с partial/terminal и read-OCE controls; отсутствующее средство проверки требует нового поручения, готовой команды нет.
- **Связи:**07/14/15; Q-002. Не дубль ABQA-002: в этом сценарии cleanup успешен, утечка не заявляется.

### Дополнение ABQA-002 по результату07

Статус остаётся **подозрение**, S2 предварительно. Gateway await using:95–111 условно может заменить pending callback/JSON/I/O exception ошибкой cleanup; callbackFailed исключает transport catches, но не защищает primary от throwing disposal. [Отчёт07](07-sse-and-cancellation.md) перечисляет просмотренное покрытие: ResponsesSseTests:514–519,657–660 и HttpLoggingTests:217–253,294–327,429 проверяют успешный cleanup/read failure, не throwing Body.Dispose/DisposeAsync. Адресного case в просмотренном наборе не найдено; это не доказательство реальной утечки. Primary preservation и release response при secondary failure требуют отдельного B-средства проверки. Код зависимости не менялся.

## ABQA-009

- **Название / категория / статус:** отдельная OCE из scope cleanup маскируется обычным Canceled; дефект реализации, подтверждён статическим контрпримером.
- **Серьёзность:** S3 — утрата технической причины сбоя. Blocking сохраняется; повтор действия, потеря данных и утечка ресурса не доказаны.
- **Контракт:** [spec](../../../openspec/specs/agent-runtime/spec.md):391 запрещает маскировать unexpected exceptions ожидаемым отказом; [AgentRunner](../../../Application/AgentRunner.cs):14–15 обещает распространение после honest finalization.
- **Наблюдение / условия:** успешный Append port сохраняет step/token; caller к моменту disposal отменён; DisposeAsync выдаёт отдельную OCE с другим token, например default. [AgentRunScope](../../../Application/AgentRunScope.cs):12–20 при primary=null пропускает cleanup OCE. [AgentRunSession](../../../Application/AgentRunSession.cs):104–113 уже приняла token/step и блокирует дальнейшие writes. AgentRunner:155–157 проверяет только caller flag; FinishAsync:174–175 возвращает Canceled/TerminalSaved=false/Error=null вместо exception.
- **Статическая проверка:** проследить successful Begin→generation→Append→cleanup OCE(default) при canceled caller. Handler0/refresh0/повторных writes0. Caller не отменён либо disposal IOException — general catch159–164 распространяет ошибку. Primary+cleanup AggregateException также не поглощается. Обычная caller cancellation при успешном cleanup остаётся отдельным допустимым исходом.
- **Evidence:** HEAD41072fc; [отчёт11](11-agent-run-and-recovery.md), исходный S11-F01. [AgentRunnerTests](../../../tests/AgentBridge.Tests/AgentRunnerTests.cs):582–597 содержит настраиваемые AfterWrite/ScopeException, но такая комбинация не исполнена/не добавлена;457–466 проверяет другой IOException case. Координатор независимо сверил все названные ветви и spec391.
- **Влияние / ограничения:** consumer теряет диагностику cleanup и видит штатную отмену. Достижимость на конкретном provider/runtime не воспроизведена. Отсутствие теста не основание дефекта; существующие fixtures лишь описывают возможный вход.
- **Будущее evidence:** отдельное поручение на адресный B-case cleanup-only OCE с отличным token и три негативных контроля; затем точное разрешение запуска. Сейчас новых tests/harness нет, готовой команды такого case нет.
- **Связи:**11/14/15, ABQA-Q-002. Не дубль HTTP disposal ABQA-002, maintenance primary code ABQA-007 или empty SSE EOF ABQA-008.

### Дополнение ABQA-009 по результату12

[Этап12](12-status-and-cleanup.md) даёт независимый отрицательный контроль: ExpiredDialogCleanup:68–98 различает operation OCE и отдельную DI scope cleanup OCE по ReferenceEquals; последняя распространяется с Interrupted и сохранённым Deleted. Existing ExpiredDialogCleanupTests.DisposeCancellationIsAnUnexpectedCleanupFailure:188–201 проверяет этот сценарий даже с caller token. Тест только прочитан. ABQA-009 остаётся локализованной в AgentRunner, новая находка cleanup не создана.

## ABQA-010

- **Категория / достоверность / серьёзность:** пробел проверки, организация cleanup тестов; подтверждено статически; S4 — ограниченная ненадёжность failure path теста. Product MUST, runtime leak и hang тестового runner не заявляются.
- **Ожидаемое поведение:** вопрос cleanup/await [этапа14](14-test-evidence-and-gaps.md); тест должен завершать и наблюдать начатую gated работу при раннем выходе, а не только при successful assertions.
- **Наблюдение / условия:** [UnitOfWorkScopeTests.SharedGateRejectsReadAndWriteWhileFirstOperationIsPending](../../../tests/AgentBridge.Persistence.EfCore.Tests/UnitOfWorkScopeTests.cs):125–140 создаёт first, ожидающий completion.Task; assertions132–137 предшествуют SetResult138/await139. При assertion failure управление выходит, first остаётся pending без await/finally. Нормальный путь освобождает и ожидает task.
- **Связанное проявление:** [EFCoreLibrary CoordinatorTests.Fatal_failure_poison_precedes_release_to_waiter](../../../../work/EFCoreLibrary/tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs):144–166. Failure ждёт fail.Task152 без token; timeout/assertion до162 пропускает SetResult и awaits163–164. Та же причина, один ID; соседний репозиторий только читался.
- **Доказательство / способ проверки:** прямой control flow на AgentBridge41072fc/EF3a8a531; координатор независимо прочитал оба метода. [Отчёт14](14-test-evidence-and-gaps.md), исходный S14-F01. Correct control — ContextCompactorTests.ActivationWaitsForSuccessfulSave:335–340 с finally. Ничего не исполнялось.
- **Влияние:** при регрессии тест не гарантирует собственное завершение/наблюдение созданной задачи. Фактическое влияние на test runner/следующие cases и удержание ресурсов не установлены; main case fake/in-memory.
- **Ограничения / будущее evidence:** historical successful row не проверяет failing assertion branch. Отдельное test-only поручение на наблюдаемый ранний assertion/timeout exit, затем B-разрешение точной команды. Новые средства не созданы, готовой команды отсутствующего case нет.
- **Связи:**04/05/14/15. Не дубль ABQA-002/007/009, которые относятся к production cleanup. Нового вопроса не требуется.

### Сверка evidence по результату14

[Матрица14](14-test-evidence-and-gaps.md) сопоставила все области00–13 с test methods/actual-double границами и historical TRX. ABQA-002 остаётся подозрением: throwing Body.Dispose/DisposeAsync и реальная утечка не воспроизведены. Для ABQA-005–009 отмечены отсутствующие различающие комбинации; отсутствие тестов не было основанием подтверждения дефектов. Stale settings-core-third259 исключён из evidence по историческому отчёту21; пересекающиеся suites и повтор8 delivery cases не суммированы.

## Шаблон новой записи

- ID и краткое название.
- Категория; серьёзность и обоснование; статус достоверности.
- Нарушенный контракт/ожидаемое поведение с точным источником.
- Фактическое поведение отдельно от предполагаемого эффекта.
- Условия, минимальное воспроизведение, негативная граница.
- Код: файл, метод, строки, commit; требования; evidence/команда/лог.
- Влияние и затронутые пользовательские сценарии.
- Ограничения проверки и что нужно для подтверждения.
- Связанные ID, причина отсутствия дубля, этап дальнейшей проверки.
- Последующие уточнения с датой; без исправления продукта.

Пустая матрица покрытия или непроведённая интеграция записывается как пробел с ограничениями. Запрос расширить поддерживаемые модели или добавить recovery UI без нарушенного требования — пожелание, а не баг.
