# 10 — Инструменты, права и внешние действия

Статус: **проверен с ограничениями**. Предпосылки: 01, 04, 08; известны app authorization и checkpoint contract.

## Цель и вопросы

Проверить допуск действия, идентичность попытки, ограниченную параллельность и честность Unknown.

## Компоненты и зависимости

[ToolRegistry](../../../Application/ToolRegistry.cs), [ToolExecutor](../../../Application/ToolExecutor.cs), ToolExecutionSession/Identity/Limits, IToolInvocationValidator/IToolExecutionCheckpoint, AgentBridgeToolsExtensions. [ToolExecutorTests](../../../tests/AgentBridge.Tests/ToolExecutorTests.cs), compile-only AccountSummaryValidator/Tool.

## Способ проверки и границы

Неизвестное/невыбранное имя, duplicate registration, metadata mismatch, неверная object schema, отказ актуальных прав, incomplete model step. Проверить repeated call_id против StepId/output position, закрытые пары и повтор attempted step. Validator до checkpoint, confirmed checkpoint до handler; отказ/exception/cancel checkpoint запрещает действие. Каждый invocation получает отдельный scope, все workers ожидаются при частичном сбое. Проверить MaxSteps/MaxCalls/MaxConcurrency, expiry и cooperative timeout; late success соседа, handler Fail против exception после начала, primary+Dispose errors. Отдельно описать гонку изменения бизнес-прав между validation и action как ответственность обработчика приложения.

## Разрешения

A; существующие isolated executor tests — B; внешние изменяющие действия не выполнять. Реальные бизнес-системы потребуют отдельного D-разрешения вне обычного прогона. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Порядок validator/checkpoint/handler/outcome, таблица identity и статусов каждого соседа, владельцы scopes/tasks. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Все ветви допуска, bounds и частичных исходов имеют вывод; граница session-memory против durable journal ясна. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Ровно одно действие во внешней системе, атомарность бизнес-БД или полную JSON Schema validation без обязательного validator приложения.

## Результаты

### Состояние и границы

Дата: **2026-10-06, Asia/Novosibirsk**. Исполнитель: чат10 `01a10d1c-6dd4-7f00-b169-54eb667efb76`, cwd `D:/Media/User/source/repos/agent-bridge`. Выполнен только **A**: чтение исходников, existing tests/исторического TRX, локальной истории/diff и ссылок. Код продукта и тестов не исполнялся.

| Репозиторий | Проверенный HEAD | Исходное состояние |
| --- | --- | --- |
| AgentBridge | `41072fc37bb718dd9f4bb0473912802260bb9164` | Чужие Markdown00–09/Findings/Methodology/README:1073 добавленных/36 удалённых строк; untracked Coordination/OpenQuestions. Свой10 без diff, index пуст; недокументальных изменений нет |
| EFCoreLibrary | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Чисто; actual CRUD/UoW, только чтение |
| HttpClientLibrary | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Чисто; actual HTTP ownership, только чтение |
| codex-lb | `f8ffbac2099a113fba54dfd8d77774f5bca80ffa` | Local HEAD совпадает с Coordination; deployed commit не установлен |

Прочитаны README/Baseline/Methodology/Findings/OpenQuestions/Coordination и принятые результаты/передача00–09; особенно01/04/08/09. Черновика10 не было. Прочитаны применимые root/Application/Configuration/Documentation/Plans/tests/Delivery, EF adapter/UnitOfWork, CodexLb/Responses AGENTS; перед соседними исходниками — их root и HTTP Clients AGENTS. Применены `csharp-project-rules`, `backend-uow-repositories` и его reference. Запрет B/C/D имеет приоритет над обычными правилами запуска. Telegram/AquaByte не потребовались.

Полностью рассмотрены registry/executor, ToolExecutionSession/Identity/Limits/Batch/Result/Status/Registration/Invocation/Output, ModelToolDefinition и tool ports, DI extensions, ToolExecutorTests; адресно AgentRunSession/Scope/Runner, DialogToolAttemptUnitOfWork/общий scope, actual EFCoreLibrary и HttpClientLibrary, compile-only AccountSummaryValidator/Tool. Общий runner/recovery остаётся этапом11.

### Все группы вопросов

Контракт: [main spec](../../../openspec/specs/agent-runtime/spec.md):72–105,361,430–438,857–870; [бизнес-граница](<../../Business logic/08-tools-and-permissions.md>):3–25. **E**=[ToolExecutor.cs](../../../Application/ToolExecutor.cs), **S**=[ToolExecutionSession.cs](../../../Application/Models/ToolExecutionSession.cs), **R**=[ToolRegistry.cs](../../../Application/ToolRegistry.cs), **T**=[ToolExecutorTests.cs](../../../tests/AgentBridge.Tests/ToolExecutorTests.cs). Остальные пути от корня AgentBridge; `Models/` и `Ports/` ниже означают `Application/Models/` и `Application/Ports/`. Строки сверены с текущими файлами; assertions **не pass**.

| Вопрос | Исследовано | Результат A | Evidence: символ/строки | Ограничение |
| --- | --- | --- | --- | --- |
| Registry/duplicate/exact selection | Registration→DI→Definitions/OpenScope | Полные независимые name/description/schema/strict; Ordinal имена. Duplicate отклоняется и extension, и registry. Описания читаются без создания handler; unknown scope=null, unselected не открывает scope | Configuration/AgentBridgeToolsExtensions.cs:13–36; R:10–24,31–48; Models/ToolRegistration.cs:9–20; T:23–36,90–105 | Регистрация/выбор не авторизация; custom registry и app factories не проверены исполнением |
| Schema/metadata/validator | DTO snapshot, Parse, ExecuteOne | Parameters должен быть object, полная schema сохраняется. Handler metadata exact сравнивается, включая raw schema. Mandatory app validator до checkpoint/action; общей JSON Schema engine в ядре нет | Models/ModelToolDefinition.cs:9–20; E:128–140,217–219; Ports/IToolInvocationValidator.cs:6–11; T:113–123,398–408 | Strict не заменяет app validation; fixture проверяет только свою простую schema |
| Lifecycle/shape/весь шаг | Parse до Enter/scopes | Только Completed; непустые call_id/name/arguments string, JSON object, optional status только completed. Known output требует предшествующий call и string/array output. Любая malformed ветвь отклоняет весь шаг до действий; canonical items не переписываются | E:35–40,190–232,236–246; T:135–164 | Не общий transport/schema validator; unknown/opaque item не трактуется как hidden call |
| Identity/owner/isolation | Session→invocation→attempt | Фиксированы owner/dialog/turn/agent, incarnation исходного token; identity добавляет StepId и исходный index во всём output. call_id не execution key; token не доказательство прав | S:18–41; Models/ToolExecutionIdentity.cs:6–21, Models/ApplicationCallContext.cs:5–31; E:219–224; T:49–57,169–194,382–393 | Standalone session не читает owner/step из БД; trusted call и текущие права предоставляет приложение |
| FIFO/закрытые пары/repeated IDs | Queue/Dequeue и ordered pending | Каждый known output закрывает самый ранний pending call своего exact ID. Закрытые occurrences исключаются из исполнения; новые pending одного ID различаются позициями. Результаты/output упорядочены по исходным calls, а не completion | E:192–225; Models/ToolExecutionBatch.cs:8–22; T:183–194,364–370 | Builder проверяет баланс, не occurrence map; Q-005 ниже, live/server pairing не доказан |
| Повтор/concurrent Execute/MaxSteps/Calls | Locked Enter→Finish | Running/stopped/attempted StepId→Conflict. MaxCalls считает pending; MaxSteps — шаги с pending, до действия. Шаг без pending не расходует лимит действий. Отказ bounds не запускает workers | S:68–92; E:36–40; Models/ToolExecutionLimits.cs:7–29; T:199–229,305–312 | Память одной session, без durable restart; count=0 повтор не исполняет действие |
| Expiry/общий timeout | Monotonic Remaining, timer, проверки перед action | Budget=min(remaining,fixed expiry−fresh UTC); equality expired/timeout. Бюджет начинается при создании session и не сбрасывается. Time/cancel проверяются до scope, после validator и checkpoint | S:59–64; E:42–53,120–122,139–150; T:219–229,472–509 | Кооперативный deadline; clock/handler приложения должны соблюдать контракт |
| Validator→checkpoint→handler | Await и pre/post guards | Optional checkpoint вызывается после успешного validator. Только confirmed success допускает handler; отказ→NotStarted без output, exception/cancel распространяется с LastResult и остановкой session. Следующая попытка не retry | E:137–151,163–180; S:85–91; Ports/IToolExecutionCheckpoint.cs:6–14; T:545–594,620–673 | Null/custom checkpoint не доказывает committed Started; гонку между проверкой прав и действием закрывает handler |
| Durable checkpoint/short scope | Runner session callback и actual UoW | Runner передаёт session как checkpoint; writes/token updates serialized, fresh UTC и original/success token, отдельный scope на запись. Start journal принимает позицию только saved Completed step/InProgress turn; refusal/unknown блокирует дальнейшие writes | Application/AgentRunner.cs:73–77,119–121; Application/AgentRunSession.cs:41–47,84–116; EF UnitOfWork/DialogToolAttemptUnitOfWork.cs:38–55,98–103 | Static commit ordering, не actual DB atomicity/crash proof; полный runner11 |
| MaxConcurrency/scopes/workers | Parallel.ForEachAsync и async scope ownership | Bounded workers; каждый invocation получает новый scope, handler/validator разделяют только его scoped ресурсы. Конструкторы разрешаются лениво внутри защищённой области. Parent await завершается после активных workers/scopes | E:58–79,125–137,172–182; R:31–48; T:336–377,678–717 | ScopedState не DbContext; singleton/shared state приложения отдельными scopes не исправляется |
| Succeeded/Rejected/Unknown/NotStarted | Handler result→canonical output/report | Success содержит JSON всего Content и исходный call_id. Known Fail→Rejected safe error; handler Timeout→Unknown. Начатый handler exception/cancel→Unknown без fake output; pre-action остановка→NotStarted. Raw error message не копируется | E:143–169,249–261; Models/ToolExecutionStatus.cs:6–13; T:234–287 | Handler обязан честно отличать confirmed refusal от uncertain side effect; successful output сам чувствителен |
| Partial outcomes/late neighbor | Record до cleanup, array→Batch→Finish | Известные соседние результаты сохраняются; Unknown/NotStarted с ошибкой отменяет следующих workers. CanContinue требует outputs всех pending и отсутствие batch error. Report доступен после exception/cancel | E:66–105,174; S:53,85–92; Models/ToolExecutionBatch.cs:8–22; T:251–271,678–717 | ServiceResult.Ok(batch) не успех всех бизнес-действий и не permission generation |
| Caller/deadline/uncooperative handler | Catches, late result и awaited tasks | Caller OCE с исходным caller token; deadline даёт report с ошибкой. Confirmed late success не отбрасывается. Несотрудничающий handler всё равно ожидается, жёсткого прерывания нет; session остановлена | E:73–105,159–169; T:293–330,452–538 | Реальные race/interleavings не исполнялись; unexpected фиксируется первым, не обещан aggregate всех независимых worker failures |
| Primary+cleanup | ExecuteOne finally/parent rethrow | Current outcome фиксируется до awaited DisposeAsync. Primary и его scope cleanup failure объединяются; cleanup-only распространяется, сохраняя confirmed output. Unexpected exception имеет приоритет перед переводом caller/deadline | E:71–98,163–182; T:413–447 | Runtime DI disposal нескольких app resources не проверялся; HTTP ABQA-002 — другая граница |
| Outcomes persistence/outside I/O | Session save и journal UoW | Confirmed outputs и terminal journal stages готовятся в одной transaction с root guards/version; Unknown без output. Matching LastResult после exception сохраняется отдельно. Handler/gateway не передаются в write callback | Application/AgentRunSession.cs:51–65; Application/AgentRunner.cs:131–151; EF UnitOfWork/DialogToolAttemptUnitOfWork.cs:59–96; EF UnitOfWork/UnitOfWorkScope.cs:38–91 | Fake writer не atomic persistence; полноценная finalization/recovery11 и provider runtime не доказаны |
| Права/TOCTOU/внешние дубликаты | App ports и compile-only consumer | App validator проверяет current owner/agent/business object, handler продолжает защищать операцию. Пример повторно валидирует и вызывает authorized source. Durable identity не исключает новую попытку того же бизнес-действия с другим TurnId/StepId | Ports/IToolHandler.cs:11–15; tests/Delivery/Consumer/AccountSummaryValidator.cs:13–20, tests/Delivery/Consumer/AccountSummaryTool.cs:23–27; бизнес-граница:19–25 | Реальное приложение/authorizer отсутствует; атомарная проверка прав/бизнес-БД/idempotency принадлежат приложению |

В таблице **EF UnitOfWork/** = `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/`. Actual dependency trace: [EFCoreLibrary UnitOfWorkContext](../../../../work/EFCoreLibrary/EfCore/UnitOfWorkContext.cs):7–20 делегирует save/database/clear общему контексту; CreateItemRepository.cs:10–17 и UpdateItemRepository.cs:10–17 в `EfCore/Repository/Base/` только staging. EF `EfUnitOfWorkSession.cs`:12–25 использует Serializable/no retry; общий scope возвращает success после commit/cleanup, до handler. [HttpJsonResponseClient](../../../../work/HttpClientLibrary/Clients/HttpJsonResponseClient.cs):21–30,55–56 владеет request/response/stream. Executor не содержит HTTP/EF вызовов; прикладной I/O остаётся за app ports. **ABQA-002 не повышен до runtime-утечки.**

### Находки и Q-005

**Новых S10-FNN и S10-QNN нет.** Нарушение действующего контракта в исследованных executor paths не установлено. Авторизация бизнес-действия, gap B/C/D и внешняя exactly-once не переименованы в дефекты. Общие реестры не редактировались.

**[ABQA-Q-005](OpenQuestions.md#abqa-q-005), требуется уточнение, без новой F/серьёзности:** независимо сверены E:192–224 и [codex-lb requests.py](../../../../codex-lb/app/core/openai/requests.py):1348–1370,1624–1671. Для различимых `call(x,argsA), call(x,argsB), output(x,resultA), output(x,resultB)` FIFO закрывает0→2/1→3, executor не выполняет ни одну закрытую occurrence. Для prefix0–2 закрыта0, pending1 сохраняет identity `(StepId,OutputIndex=1)` и аргументы B. Builder `Application/ContextBuilder.cs`:133–167 только уменьшает balance, не подтверждает association.

Server helper связывает первый output2 с call1, второй3 с call0. Static trace09 `{3}→{3}`, `{2}→{1,2}` при достаточном budget/пустом required подтверждён чтением ветвей1644–1671; helper не исполнялся. Обратный поиск ограничен следующим call того же ID. Дальнейший fitting/opaque и deployed payload могут менять результат. **Live отказ, потеря истории, неверный ответ и повтор действия не доказаны; server FIFO-политика не выбрана.** Identity executor и durable журнал не устанавливают внешнюю семантику compact; вопрос сохраняется для14/15.

### Историческое evidence и пропуски

Сохранённый [tools-final.trx](../../../artifacts/test-results/stage19/tools-final.trx) прочитан как XML: start/finish **2026-10-04 14:20:48–49 UTC+07**,199/199 Passed;42 записей ToolExecutorTests Passed. [Отчёт19](<../AgentBridge Initial Implementation/19-application-tools.md>):53–66 фиксирует команды и doubles. Остальные157 — пересекающиеся builder/ports/tokenizer/guard suites; с runs27/33/196 и другими этапами не суммируются. Read-only diff от `de58342f5c80e46279e1d7b59fe0665c893c5d4b` до HEAD для executor/registry/session/ToolExecutorTests пуст. Это **историческое evidence**, не current pass или восстановленный binary/transitive provenance.

| Пропущено / уровень | Причина и влияние | Нужное окружение/разрешение; будущий сценарий |
| --- | --- | --- |
| Current registry/executor/DI/isolated tests, B | Прямой запрет запуска; assertions/TRX не новое исполнение | .NET10, checked imports/packages/fresh build/выделенные outputs и отдельные точные разрешения. Existing ToolExecutorTests: schema/selection/FIFO, bounds/concurrency, checkpoint refusal/exception/cancel, late success и primary+cleanup. Historical команда19:56 известна, текущий достоверный --no-build chain не установлен |
| Durable Start/outcomes/rollback/restart, C | DB/SQL/processes запрещены; static scope/fakes не atomicity | Собственные SQLite/PostgreSQL, actual EFCoreLibrary, разрешённые existing runner integration scenarios: committed Started до handler, journal+outputs rollback, stale/delete/expiry. Synthetic ack/new DI root не network loss/OS crash; resources/команды Q-002 не заданы |
| Current app permissions/action/side effects, D | Реального app authorizer/business ports и разрешённых систем нет; action запрещён | App composition root, trusted identities, выделенные business objects/операции; права меняются между validator и action, handler атомарно проверяет состояние. Unknown external outcome сверяется приложением без automatic retry. Exactly-once требует отдельного бизнес-контракта; готовой команды нет |
| Q-005 distinct occurrences/compact, B/D | Helper не вызывался; просмотренный repeated-ID test использует одинаковые аргументы. Отсутствие адресного test не доказательство бага | Отдельное поручение на недостающее средство проверки, затем exact deployment/model/данные: различные args/results, closed/partial pairs, singleton subsets/final fitting/opaque. Новые test/harness здесь запрещены, команда неизвестна |

B/C/D, сборки/tests/CLI/project-user scripts/exe/harness/app/hosting/Docker/DB/SQL/migrations/backup/restore/native/внешний HTTP не запускались; новые tests/harness/scripts не создавались. Пропуски не являются успехом и не размножены отдельными Findings.

### Итог и передача

**Проверен с ограничениями.** Все группы10 имеют результат A и предел. Новых кандидатов нет; Q-005 уточнён по identity/closed FIFO pairs. Не доказаны current runtime pass, полная schema/права реального приложения, provider atomicity, real crash/network loss, live compact association или external exactly-once.

Передать11 обязательный checkpoint/Started barrier, matching StepId LastResult после awaited workers, accepted token до scope cleanup и atomic outcomes без refresh/retry;14 исторические42 tools cases/перекрытие199, app resource disposal и distinct occurrence permutations;15 открытый Q-005 без выбора политики. Детальный аудит следующих этапов не выполнен; координатор сам читает файл, сообщения в другие чаты не отправляются.

Финальный контроль A: собственный diff/ссылки/кодировка проверены; исходное задание сохранено, кроме статуса. Strict UTF-8 без BOM/LF, без U+FFFD/mojibake/четырёх вопросительных знаков. SHA256 остальных21 audit-файла совпали до/после; index пуст, недокументальных изменений нет, обязательные зависимости чисты и HEAD неизменны. Единственная собственная правка — этот10.
