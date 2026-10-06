# 08 — Сборка рабочего контекста

Статус: **проверен с ограничениями**. Предпосылки: 02–03, 06–07; доступны правила snapshots и canonical items.

## Цель и вопросы

Проверить, что модель получает разрешённые данные в верном порядке и ни потерянных, ни повторных элементов истории.

## Компоненты и зависимости

[ContextBuilder](../../../Application/ContextBuilder.cs), ContextRequest, IContextProvider, CanonicalModelItem, StoredDialogContext/Turn. [ContextBuilderTests](../../../tests/AgentBridge.Tests/ContextBuilderTests.cs), ApplicationPortsTests.

## Способ проверки и границы

Составить пример providers → active window → turns после prefix → unsaved input; инструкции, tools и controls проверить отдельно. Provider selection и права принадлежат приложению; роли не повышаются. Проверить owner/expiry до I/O, sequential awaits, второй provider fail/cancel без пустого fallback. Негативные границы: prefix 0, gap, InProgress внутри prefix, duplicates из ModelSteps, envelope как input. Сопоставить call/output через границы источников, repeated call_id по FIFO, orphan output, незакрытый call и partial arguments. Opaque не вскрывается и не даёт права придумать output.

## Разрешения

A; существующие public builder tests — B. Бизнес-источники не вызывать; внешние данные заменить только уже имеющимися doubles. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Поэлементная схема контекста, ожидаемый порядок, call pairing и отказы без мутации. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Каждый источник input и каждый отказ пары/префикса получили проверку и доказательство происхождения. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Размер полного запроса, качество выбранных приложением данных, авторизацию внешнего сервиса или восстановление сведений после сжатия.

## Результаты

### Состояние и границы

Дата: **2026-10-06, Asia/Novosibirsk**; запуск всего аудита начат 2026-10-05. Исполнитель: чат08 `01a10d05-4214-71f1-b32b-fa54d7211662`, cwd `D:/Media/User/source/repos/agent-bridge`. Выполнен только **A**: статическое чтение, сравнение исходников/истории, existing tests/TRX, ссылок и diff. Методы продукта, providers и тестов не вызывались.

| Репозиторий | Повторно проверенный HEAD | Исходное состояние |
| --- | --- | --- |
| AgentBridge | `41072fc37bb718dd9f4bb0473912802260bb9164` | Чужие tracked Markdown00–07/Findings/Methodology/README:934 добавленных/30 удалённых строк; untracked Coordination/OpenQuestions. Свой08 без diff, index пуст; недокументальных изменений нет |
| EFCoreLibrary | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Чисто; actual base read repository, read-only |
| HttpClientLibrary | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Чисто; actual HTTP port, read-only |
| codex-lb | `f8ffbac2099a113fba54dfd8d77774f5bca80ffa` | Прежняя untracked `.vs/`; локальный source, не deployed commit |

Прочитаны README/Baseline/Methodology/Findings/OpenQuestions/Coordination и принятые результаты/передача00–07, особенно02–03/06–07. Baseline подготовки на c8da604 не заменяет baseline запуска из Coordination. Черновика08 не было. Применены ancestor/root/Documentation/Plans/Application/Domain/tests, EF adapter/UnitOfWork, CodexLb/Responses AGENTS, root обеих обязательных библиотек и codex-lb; `csharp-project-rules`, при сверке read boundary — `backend-uow-repositories` и его reference. Прямой запрет B/C/D имеет приоритет. Telegram/AquaByte не потребовались; соседние проекты не изменены.

Полностью рассмотрены ContextBuilder/ContextBuilderTests, composition DTO/порт и snapshot helpers; адресно ApplicationPortsTests, полный DialogReader и четыре ordered query adapters, actual EFCoreLibrary GetItemByPredicateRepository, transport writer/gateway/IHttpApiClient и два compact pairing helpers codex-lb. ToolExecutor рассмотрен только в части occurrence pairing; подробные tools/compact/runner проверки остаются09–11. Исходное задание выше сохранено.

### Все группы вопросов

Нормативный [main spec](../../../openspec/specs/agent-runtime/spec.md):188–236 — три composition требования;453–459 — разделение reports/items;830–845 — terminal prefix. **B**=[ContextBuilder.cs](../../../Application/ContextBuilder.cs), **M**=`Application/Models`, **T**=[ContextBuilderTests.cs](../../../tests/AgentBridge.Tests/ContextBuilderTests.cs), **P**=`adapters/AgentBridge.Persistence.EfCore`, **R**=`adapters/AgentBridge.CodexLb/Responses`. Пути от корня AgentBridge; строки сверены по текущим файлам. Все результаты — **A**, чтение assertion не pass.

| Вопрос | Исследовано | Результат | Evidence: символ/строки | Ограничение |
| --- | --- | --- | --- | --- |
| Providers → window → tail → new input | BuildCoreAsync, ordered коллекции | Последовательные вклады, затем active.Items, все turn.Items при Sequence>prefix и ещё не сохранённый newRequest.Input. Без окна/prefix0 включается вся история | B:18,52–81; T:20–62,71–96 | Уже сохранённое сообщение нельзя повторно передавать как new input; semantic dedup по тексту/JSON не предусмотрен |
| Instructions/tools/settings/controls | Конструирование полного ModelRequest | Instructions остаются отдельным полем; exact model/effort, tools, parameters и явно переданный continuation сохраняются | B:89–90; M/ModelRequest.cs:7–23,30–39; T:28–53 | Поддержка controls/каталог/полный бюджет проверяются отдельно06/09 |
| Выбор providers, permissions, actual identities | Constructor, ContextRequest/IContextProvider | Выбранную ordered коллекцию задаёт приложение; provider получает actual call/AgentId/TurnId и только new input. Владение DI scope и авторизация данных принадлежат приложению | B:16–18,53–57; M/ContextRequest.cs:7–17; Application/Ports/IContextProvider.cs:6–10; T:305–319,413–429 | Builder не аутентифицирует caller, не выбирает business sources и не вводит persisted AgentId |
| Роли/unknown поля | AddRange без преобразования, canonical clone | Любые исходные роли, включая system/developer/future-role, сохраняются; бизнес-данные автоматически не повышаются до system. JSON остаётся полным объектом | B:64–81; M/CanonicalModelItem.cs:10–20; ContractSnapshot.cs:21–27; T:22–26,127–136,489–499 | Это разрешённая политика spec205, а не доказательство безопасности произвольного provider/app |
| Owner/dialog/UTC/expiry до provider I/O | BuildAsync→ValidateDialog→foreach | UTC/caller precheck; чужой owner→Forbidden, чужой dialog→Conflict; now>=expiry→Expired до providers. Fixed dates не меняются | B:27–29,40–57,97–108; M/DialogSnapshot.cs:45–48; T:223–257,474–478 | UTC передаёт приложение; после долгого provider нет fresh-clock recheck. Write guards обязательны отдельно; status-only internal путь32–34 допускает expired чтение, public BuildAsync передаёт false |
| Sequential await/error/cancel | Provider цикл и выходы | Каждый Task awaited до следующего; второй Fail возвращает тот же ServiceError без частичного запроса, даже при поздней отмене. Unexpected exception/OCE распространяется; successful contribution сопровождается caller check | B:54–65,75,88; T:324–407,517–548 | Cooperative token не принуждает несотрудничающий provider закончить; нет fan-out, таймера, retry или empty fallback |
| Prefix0/gaps/reorder/duplicate/InProgress | ValidateDialog и DTO guards | Prefix0 допустим; Sequence должна быть1..N, ID уникальны, prefix<=Count; covered InProgress и non-Completed compact→Conflict до I/O. Failed/Canceled/Incomplete terminal turns могут покрываться | B:110–125; M/StoredDialogContext.cs:9–14; StoredDialogTurn.cs:12–25; T:71–96,271–300,504–512 | Prefix — граница turns, не cutoff отдельных items; builder не валидирует полную Domain chronology/исторические context versions |
| ModelSteps/envelope/continuation duplicates | Единственный read source истории/окна | Turn.Items включаются один раз; ModelSteps.Output не обходится. Window=Compaction.Output; ни compact envelope/continuation, ни step metadata не становятся input или implicit continuation | B:68–90; M/StoredDialogTurn.cs:35–38; StoredDialogContext.cs:22–25; T:32–52,82–92; ApplicationPortsTests.cs:267–306,311–322 | Повторные canonical occurrences из разных источников сохраняются; это не непреднамеренное удвоение из reports |
| Пары через границы источников | ValidateFunctionPairs после полной композиции | Проверяется единая последовательность; call provider/window/tail может закрываться позднейшим output window/tail/new. Interleaving разных ID сохраняется | B:83,131–167; T:105–136 | Проверка не отслеживает внешнее выполнение/side effect |
| Repeated call_id/FIFO/число outputs | Ordinal pending counters, occurrence queue | Каждый call увеличивает счётчик, каждый output уменьшает ровно на1; закрытые пары и несколько pending одного ID допустимы. Один output не закрывает два calls. Builder не создаёт occurrence map, поэтому его баланс совместим с FIFO, но сам не доказывает конкретное соответствие позиций | B:133–165; T:438–469; Application/ToolExecutor.cs:192–224 — Queue/Dequeue FIFO | Внешний compact helper использует иной выбор occurrence: [ABQA-Q-005](OpenQuestions.md#abqa-q-005) ниже. Подробные handler/journal границы10–11 не проверены |
| Malformed/orphan/output-before-call | Known type/call_id predicates | Missing/non-string/whitespace ID→Validation; orphan/лишний/output-before-call/другой case→Validation. Непустой pending в конце→Conflict; result.Data отсутствует | B:138–167,84–86; T:196–216 | Это pair validation, не полная schema validation arguments/output; последняя принадлежит tool/transport границам |
| Partial arguments/opaque | Raw items, outer known type only | Partial call без output→Conflict на любом turn lifecycle, без ремонта/удаления/фиктивного output. Unknown/opaque не раскрывается; nested скрытый call не учитывается; arguments/output не переписываются | B:137–167; T:127–136,146–185 | Opaque не подтверждает скрытую пару, совместимость моделей или full token estimate09 |
| Отказы без мутации/независимые коллекции | DTO getters/Copy/Clone; локальный input | Новый список запроса; source history/token/fixed dates/reports не меняются. Коллекции копируются и защищены, JsonElement.Clone отделяет lifetime | B:52–90; M/ContractSnapshot.cs:9–27; DialogSnapshot.cs:18–25; T:154–166,413–429; ApplicationPortsTests.cs:18–62 | Immutable DTO не transaction snapshot; provider side effects и persisted atomicity сюда не входят |
| Происхождение ordered stored input и HTTP boundary | Actual read repository→DTO→writer | Reader возвращает всю историю + active max Version; ordered base queries не режут items по prefix. JSON writer передаёт input по порядку через WriteTo; HTTP идёт через actual HttpClientLibrary вне builder | P/Reading/DialogReader.cs:43–87,91–94; P/Repositories/TurnRecordQueries.cs:15–17, ItemRecordQueries.cs:15–17, ContextRecordQueries.cs:10–12, ModelStepRecordQueries.cs:20–22; R/ResponseRequestWriter.cs:34–50; R/CodexLbModelGateway.cs:40–57,95 | Actual [EFCoreLibrary GetItemByPredicateRepository](../../../../work/EFCoreLibrary/EfCore/Repository/Base/GetItemByPredicateRepository.cs):12–54 применяет predicate/order/no-tracking и async materialization; [IHttpApiClient](../../../../work/HttpClientLibrary/Abstractions/IHttpApiClient.cs):11–16 задаёт JSON/stream port. Translation/isolation/реальный HTTP не исполнялись; read recheck не snapshot |

### Находки и вопрос совместимости

**Новых S08-FNN кандидатов на дефект нет.** В исследованном builder нарушений трёх composition требований статически не установлено. ABQA-005 остаётся дефектом Domain.Restore, а не новой ошибкой composition; loader/штатные writes не переаудировались. ABQA-008 не дублируется: empty cancellation report сам не содержит partial call. ABQA-002 остаётся подозрением, без runtime-повышения. Общие реестры не изменены.

**[ABQA-Q-005](OpenQuestions.md#abqa-q-005) — соответствие occurrence pairing при compact с несколькими pending одинакового ID.** Категория: вопрос совместимости; статус: **требуется уточнение**, серьёзность не назначена. Для09–10/14; новая нормативная политика не выбрана, дефект не подтверждён.

- Факт: AgentBridge ToolExecutor:192–224 использует FIFO; builder:133–167 проверяет только баланс. В локальном [codex-lb requests.py](../../../../codex-lb/app/core/openai/requests.py):1348–1370 `_compact_matching_tool_call_index` использует append/pop/последний unmatched; nested matching_call_index:1624–1633 делает то же. `_compact_reconciled_tool_call_indices`:1635–1642 ограничивает matching outputs следующим call того же ID. Это код выбора compact subset, не реализация ContextBuilder и не доказательство live generation pairing.
- Минимальная различающая последовательность: call(x,argsA), call(x,argsB), output(x,resultA), output(x,resultB). Builder принимает её баланс без перестановки; FIFO связывает первый output с первым call, названный compact helper для первого output выбирает второй call. Для последовательно закрытых call/output/call/output этого различия нет.
- Неопределённость: требует ли фактическое окружение сохранения именно FIFO occurrence association через compact и как выбранный server subset влияет на него? Ответ устанавливается контрактом нужного deployment и исследованием09–10, затем разрешённым адресным B/D evidence; local HEAD не deployed endpoint.
- Влияние пока условное: при частичном выборе элементов могут различаться сохраняемые calls/outputs; неверный ответ модели, повтор действия или потеря данных **не доказаны**. Spec205 требует предшествующий незакрытый call, но не устанавливает внешнюю server FIFO-политику. Не расширять его молча. Владелец интеграционного контракта и evidence09–10 разрешают вопрос; A08 завершён независимо. Это отдельная семантика от Q-002 (ресурсы), Q-004 (nested controls).

### Историческое evidence и пропуски

Прочитан сохранённый [stage16-composition-final.trx](../../../artifacts/test-results/stage16/stage16-composition-final.trx): Times start/finish **2026-10-04 11:59:22–23 UTC+07**, Counters86/86 Passed,59 ContextBuilderTests и27 ApplicationPortsTests. [Отчёт16](<../AgentBridge Initial Implementation/16-context-composition.md>):42–65 фиксирует команды, fake providers/actual DTO и пределы. Первый50-case run перекрывает финальный; числа не суммируются. `git diff 085779a15c4126ac567a6d1307f497dcae8dc9c5 HEAD` показывает неизменный ContextBuilderTests и добавленный status-only путь/видимость pair helper в builder. Это **историческое evidence**, не current pass: binary/transitive provenance всего старого запуска не восстановлен.

| Пропущено / уровень | Причина и влияние | Нужное окружение/разрешение; будущий сценарий |
| --- | --- | --- |
| Current compile/существующие public builder/contract tests, B | Прямой запрет; code/TRX не текущее исполнение | .NET10, проверенные csproj/imports/packages и отдельные outputs; точное разрешение build/test. Порядок/prefix/guards/roles/partial/FIFO balance, второй provider Fail/OCE, gated success/cancel и immutable sources. Исторические команды16 известны, но готовая текущая команда не установлена: fresh binary/output chain не проверялась, старый --no-build использовать нельзя |
| [ABQA-Q-005](OpenQuestions.md#abqa-q-005), B/D | Нет адресного исполнения distinguishable repeated pending calls; новое средство проверки запрещено. Внешняя occurrence compatibility неизвестна | В09–10 статически проследить compact subset; затем отдельное поручение на недостающий test и разрешение local/live case, exact deployed commit/model/endpoint/data. Проверить calls с разными args/results и выбор каждого occurrence; готовой команды отсутствующего case нет |
| Реальные app providers/права/HTTP/expiry после ожидания, D | Business sources/hosting/network запрещены; fake provider не доказывает actual authorization или cooperative I/O | App composition root/selected providers/trusted owner и согласованные synthetic данные/операции. Fail второго источника, caller cancel и истечение в ожидании; проверить downstream fresh write guard. Окружение/команды неизвестны, Q-002 |
| Relational ordering/конкуренция/persistence, C | DB/SQL запрещены; static reader/recheck не isolation | Собственные согласованные SQLite/PostgreSQL ресурсы и точные разрешения; full items+reports/window round-trip, delete/recreate/revision race. Fake writer не atomic persistence; SQLServer/MySQL не провайдеры AgentBridge |

B/C/D, приложения/сборки/tests/scripts/exe/harness/CLI/установка/Docker/DB/SQL/migrations/backup/restore/native/внешний HTTP не выполнялись. Новые tests/harness/scripts не создавались. Ограничения не превращены в отдельные находки на каждый запрещённый запуск.

### Итог и передача

**Проверен с ограничениями.** Все группы08 получили статический результат и предел. Новых F нет; **[ABQA-Q-005](OpenQuestions.md#abqa-q-005)** внесён координатором в реестр. Не доказаны current runtime pass, actual provider authorization, server occurrence pairing, relational snapshot/atomicity, полный opaque budget или durable recovery.

Передать09 точную композицию/opaque unknown, [ABQA-Q-005](OpenQuestions.md#abqa-q-005) и отсутствие implicit compact continuation;10 FIFO occurrence map vs builder balance, partial отказ до generation и app provider permissions;11 только ещё не сохранённый new input/Items как единственный источник, sequential providers и отдельные fresh write guards;12 отдельный internal status expiry path;14 исторические59+27 без overlap/current pass и missing repeated-occurrence scenario;15 отсутствие нового F и вопрос совместимости. Детальный аудит этих этапов не выполнен; общие реестры обновляет координатор, сообщение в другой чат не отправляется.

Финальный контроль A: собственный diff, ссылки/anchors и кодировка проверены; исходное задание сохранено, кроме статуса. Strict UTF-8 без BOM/LF, без U+FFFD/mojibake/четырёх вопросительных знаков; SHA256 остальных21 Markdown аудита совпадают до/после правки, index пуст. Единственная собственная правка — этот08; недокументальных изменений нет, обязательные зависимости чисты, HEAD неизменны.
