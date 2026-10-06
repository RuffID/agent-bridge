# 09 — Tokenizer, бюджет и сжатие

Статус: **проверен с ограничениями**. Предпосылки: 04, 08; подтверждена граница сохраняемого префикса.

## Цель и вопросы

Проверить полноту локального подсчёта, честную неизвестную оценку и принятие только допустимого сохранённого окна.

## Компоненты и зависимости

[ContextTokenCounter](../../../AgentBridge/Tokenization/ContextTokenCounter.cs), ModelEncodingMap/OrdinaryTokenizerFactory, [ContextBudgetGuard](../../../AgentBridge/Application/ContextBudgetGuard.cs), ContextCompactor, CompactRequestWriter/CompactJsonReader, IDialogContextWriter. Тесты ContextTokenCounterTests, ContextBudgetGuardTests, ContextCompactorTests и compact cases ResponsesJsonTests.

## Способ проверки и границы

Exact mapping против каталога, unknown/suffixed model, special-token literals, embedded словари; инструкции, providers, input, schemas и результаты вместе. Проверить threshold-1/равно/+1, estimate+reserve на границе, overflow, null estimate, opaque/multimodal/continuation, отсутствие подмены прошлым usage. Compact: только terminal prefix, не transient/new input; отдельная проекция controls без tools, собственный input guard, MaxPasses, пустой/растущий candidate, malformed response, unknown opaque estimate. Проверить save-before-activation, fresh UTC/stale token, сбой второго прохода после сохранённого первого. После любого compact outcome нужен отдельный полный guard.

## Разрешения

A; offline/fake gateway/writer проверки — B; реальная атомарность принятия окна — C; server count и live compact — D. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Таблица known/estimated/unknown, точные границы, состав compact и следующего generation, запись каждой принятой версии. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Подсчёт и каждый исход compact разобраны без обещания измерить opaque; различие fake/real writer явно записано. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Server billing/точный upstream count, сохранение каждой смысловой детали или возможность продолжить любое сжатое состояние.

## Результаты

### Состояние и источники

Дата: **2026-10-06, Asia/Novosibirsk**. Исполнитель: чат09 `01a10d0f-fa7e-7910-b3b1-b5c40e08c927`, cwd `D:/Media/User/source/repos/agent-bridge`. Только **A**: чтение исходников, требований, существующих tests/TRX, локальной истории/diff и ссылок. BPE и методы продукта/тестов не исполнялись; сборки, CLI, HTTP и БД не запускались.

| Репозиторий | Проверенный HEAD | Исходное состояние |
| --- | --- | --- |
| AgentBridge | `41072fc37bb718dd9f4bb0473912802260bb9164` | Чужие tracked Markdown00–08/Findings/Methodology/README:1003 добавленных/33 удалённых строк; untracked Coordination/OpenQuestions. Свой09 без diff, index пуст; недокументальных изменений нет |
| EFCoreLibrary | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Чисто, read-only |
| HttpClientLibrary | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Чисто, read-only |
| codex-lb | `f8ffbac2099a113fba54dfd8d77774f5bca80ffa` | Прежняя untracked `.vs/`; это local source, не deployed commit |

Прочитаны README/Baseline/Methodology/Findings/OpenQuestions/Coordination, принятые результаты и передача00–08, особенно04/08. Черновика09 не было. Применены root/ancestor, Documentation/Plans, Application/Tokenization/Configuration/Domain/tests, CodexLb/Responses и EF adapter/UnitOfWork AGENTS; root обязательных библиотек, HttpClientLibrary/Clients и codex-lb AGENTS; `csharp-project-rules` и `backend-uow-repositories` с необходимыми references. Прямой запрет исполнения имеет приоритет. Telegram/AquaByte не потребовались.

Полностью рассмотрены counter/map/factory, budget guard, compactor, compact writer/reader и три профильных core test-файла; адресно compact cases ResponsesJsonTests, builder, selection validator, context writer/scope/guard, actual EFCoreLibrary context/CRUD и HttpClientLibrary JSON transport. Runner/session рассмотрены только для отдельного full guard и захвата принятого compact. codex-lb — compact preparation и occurrence helpers, без подробного аудита остальных server paths.

### Все группы вопросов

Контракт: [main spec](../../../openspec/specs/agent-runtime/spec.md):113–186,359–366,562–564,832–845,849. **T**=[ContextTokenCounter.cs](../../../AgentBridge/Tokenization/ContextTokenCounter.cs), **G**=[ContextBudgetGuard.cs](../../../AgentBridge/Application/ContextBudgetGuard.cs), **C**=[ContextCompactor.cs](../../../AgentBridge/Application/ContextCompactor.cs), **R**=`adapters/AgentBridge.CodexLb/Responses`, **P**=`adapters/AgentBridge.Persistence.EfCore/UnitOfWork`. Имена Context*Tests.cs обозначают файлы в `tests/AgentBridge.Tests/`, ResponsesJsonTests.cs — в `tests/AgentBridge.CodexLb.Tests/`; ContextBuilder.cs/ModelSelectionValidator.cs — в `Application/`, OrdinaryTokenizerFactory.cs — в `Tokenization/`, техничка07 — `Documentation/Technical documentation/07-tokenizer-and-settings.md`. Остальные пути от корня AgentBridge. Строки сверены по текущим файлам; чтение assertion — **не pass**.

| Вопрос | Исследовано | Результат A | Evidence: символ/строки | Ограничение |
| --- | --- | --- | --- | --- |
| Exact mapping, каталог, unknown/suffix/case | Map→factory→CountAsync, pinned packages | Восемь exact IDs: шесть o200k и два cl100k; остальные Unsupported, без prefix/fallback. Каталог доступности и mapping — разные проверки | Tokenization/ModelEncodingMap.cs:7–11; T:31–37; Application/ModelSelectionValidator.cs:19–35; agent-bridge.csproj:17–19 | Live IDs не заданы, Q-002; исходный OpenAI source не запрашивался заново |
| Embedded словари, ordinary markers, cancellation/concurrency | Factory, Lazy и per-call PayloadCount; vectors/hash tests | Embedded deflate, pinned regex, normalizer/specialTokens=null; нет runtime download. Missing resource/неожиданная ошибка распространяется. Отмена до/после неделимого BPE; состояние подсчёта локально | OrdinaryTokenizerFactory.cs:12–34; T:38,86–98; ContextTokenCounterTests.cs:24–45,183–189,209–246 | BPE/resources/hashes/cache сейчас не исполнялись; large serialization/BPE не прерывается посередине |
| Known / full estimate / unknown | Payload parser и framing | Known — сумма известных payload. При известном input estimate=max(known,BPE JSON framing); при opaque/null estimate known не теряется. Это локальная оценка без доказанного server upper bound | T:39–67,101–166; Application/Models/ContextTokenCount.cs:7–17; техничка07:47–55 | Framing не server/billing count; custom counter может обосновать estimate при HasOpaqueContent=true |
| Весь prepared request | Instructions/Input/tools/schema/text/tool_choice | Учитываются instructions, все occurrences provider/history/new input, names/arguments/results, names/descriptions/полные tool schemas и input constraints. Model/effort/transport controls не KnownTokens; IDs/roles/framing только в локальной оценке | T:39–64,113–120,169–215; ContextTokenCounterTests.cs:79–120 | Счётчик не заменяет transport/schema validation и не проверяет права providers |
| Opaque/multimodal/continuation/usage | Known/unknown parts, fields и controls | Reasoning summary считается как известный текст, hidden state/файлы/изображения/unknown дают null estimate. Continuation не токенизируется как скрытая история; прошлый usage не используется | T:48–51,122–164,175–203,218–241; ContextTokenCounterTests.cs:125–178; ContextCompactorTests.cs:57–70 | Не доказаны полный opaque размер и возможность продолжить любое сжатое состояние |
| Threshold−1/равно/+1, reserve/equality/overflow | Selection validation→guard→trigger | Trigger при estimate>=threshold. Guard допускает estimate==window−reserve, превышение Rejected, null Unsupported; subtraction в long исключает overflow. Threshold+reserve валидируется через long. Только InputContextWindow | G:26–57; ModelSelectionValidator.cs:14–35; C:74–76; ContextBudgetGuardTests.cs:18–35,42–103; ContextCompactorTests.cs:90–100 | Равенство допустимо по оценке; failure identity сохранена до late cancel, success после cancel не принят, G:38–44 |
| Terminal prefix0 и transient boundary | Builder once, history/providers/tail split | History=active.Items+terminal turns до первого InProgress. Providers, весь tail и unsaved input не сохраняются compact. Through0 оставляет все turns в tail; transient-only→NoPersistableHistory | C:42–61,76; ContextBuilder.cs:52–90,110–127; ContextCompactorTests.cs:23–52,239–270,387–391 | Prefix — граница turns, не отдельных items; нет semantic dedup и удаления архивной истории |
| Compact projection/input guard | Separate CompactParameters/request/writer | Compact получает instructions/history и reasoning/service_tier/prompt_cache_key без tools/continuation. Полные generation controls/tools сохраняются. Собственный guard до gateway. Direct gateway отклоняет tools/continuation/unsupported controls, malformed/top-level duplicates | C:85–93,112–113,148–162; R/CompactRequestWriter.cs:14–37,43–64; ContextCompactorTests.cs:372–378; ResponsesJsonTests.cs:441–461 | Q-004 о глубине nested validation сохраняется; unknown reasoning control встроенный counter делает unknown budget |
| MaxPasses и исходы без сохранения | Loop/report, lifecycle/pairs/count | MaxPasses фиксируется на входе; passes++ только перед gateway. Empty/non-Completed/continuation, broken known pairs и count failure не активируются. Candidate estimate>=предыдущего полного estimate→NoReduction без save; лимит→PassLimitReached | C:28–41,66–99,102–123,139–144; ContextCompactorTests.cs:107–155,217–235,359–378 | Остановка по full estimate, не длине output. Opaque hidden pairs не проверяются; политика FIFO сервера не установлена |
| Malformed compact response и canonical state | Writer→actual HTTP→reader | Discriminator после trim response.compact*, output array объектов; absent/null status допустим. Error/failed→Failed, неподтверждённый lifecycle/missing output→Incomplete, invalid shape→Rejected. Полные output/envelope сохраняются; continuation=null | R/CompactJsonReader.cs:12–40; R/CodexLbModelGateway.cs:152–191; ResponsesJsonTests.cs:363–438 | Trim только проверки discriminator, исходный envelope сохранён; пустой Completed transport report отдельно отвергает C:102–105 |
| Candidate/unknown opaque acceptance | Полная композиция после замены окна | Candidate=providers+output+tail со всеми tools/controls; known pairs и counter проверены до save. Валидный Completed с null estimate сохраняется, затем UnknownBudget без следующего pass | C:112–142; ContextCompactorTests.cs:57–85 | UnknownBudget не разрешает generation; fake custom opaque estimate не подтверждает свой estimator |
| Save-before-activation/fresh UTC/stale token | Await writer и actual persistence boundary | Fresh UTC после HTTP/counter, expiry equality отказ; original либо successful-save token. Только success обновляет token/active/prepared/count. Context writer guard/domain/staging внутри scope; архив и fixed dates сохраняются | C:124–138; P/DialogContextUnitOfWork.cs:31–67; P/DialogWriteGuard.cs:17–36; Domain/Dialogs/Dialog.cs:244–266; ContextCompactorTests.cs:164–206,317–340 | Fake writer:511–519 не доказывает actual atomicity. P/UnitOfWorkScope.cs:38–91 — static commit/cleanup, не provider runtime |
| Сбой второго прохода/late exception | Report state и runner session token capture | Typed failure второго gateway/save оставляет первый accepted context/token, без retry. Unexpected/OCE распространяется; предыдущий save не откатывается. Runner session захватывает accepted state до возврата в compactor | C:67–68,93–99,116–142; ContextCompactorTests.cs:141–155,295–307; Application/AgentRunSession.cs:68–75,94–113 | Standalone exception не возвращает result: актуальное состояние читается новым reader; crash/disconnect не исследованы |
| Отдельный full guard после outcome | Runner compact→guard→generation | После полученного compact report guard считает полный PreparedRequest; compact error/blocked/expiry также препятствуют generation. Сам compactor ни одним статусом её не разрешает | Application/AgentRunner.cs:84–95; G:38–57; ContextCompactorTests.cs:121–134 | Полный runner/recovery — этап11; counter custom policy и серверная приемлемость не доказаны |

Actual dependency trace: [EFCoreLibrary UnitOfWorkContext](../../../../work/EFCoreLibrary/EfCore/UnitOfWorkContext.cs):7–20 делегирует save/database/clear общему контексту; [CreateItemRepository](../../../../work/EFCoreLibrary/EfCore/Repository/Base/CreateItemRepository.cs):10–17 только staging. P/EfUnitOfWorkSession.cs:12–25 использует Serializable без retry/ambient, затем scope commit/cleanup. [HttpJsonResponseClient](../../../../work/HttpClientLibrary/Clients/HttpJsonResponseClient.cs):21–30,55–56 владеет request/response/stream; CompactAsync использует этот actual путь вне UoW, per-call access и CompactTimeout, без generation fallback. Primary при throwing cleanup и реальные ресурсы не воспроизводились; **ABQA-002 не повышен до runtime-утечки**.

### Находки и уточнение Q-005

**Новых S09-FNN и S09-QNN нет.** Нарушение требований AgentBridge в исследованных путях не установлено. [ABQA-004/Q-003](Findings.md#abqa-004) остаются вопросом применимости старых deltas: использован main, CLI не запускалась. [Q-004](OpenQuestions.md#abqa-q-004) сохраняется для nested controls. Общие реестры не редактировались.

**[ABQA-Q-005](OpenQuestions.md#abqa-q-005) дополнен статическим compact trace**, статус «требуется уточнение», серьёзность не назначена. Для input с indices0–3: `call(x,argsA), call(x,argsB), output(x,resultA), output(x,resultB)` builder сохраняет баланс/порядок; Application/ToolExecutor.cs:192–224 FIFO связывает0→2/1→3. В [codex-lb requests.py](../../../../codex-lb/app/core/openai/requests.py):1348–1370 helper связывает1→2/0→3. `_compact_reconciled_tool_call_indices`:1624–1642 использует этот stack для output→call, но обратный call→output ограничивает поиск следующим call того же ID.

При supplied `selected_indices={3}`, пустом required set, default allow_pair_additions=true и достаточном token_budget static trace таков:1644–1656 добавляет0;1657–1665 не находит output между0 и следующим call1 и удаляет0;1671 возвращает `{3}`. Для `{2}` возвращает `{1,2}`. Это несогласованность двух направлений helper, а не выбранная аудитором FIFO-политика. Latest ordinary tool-output branch:1332–1339 действительно обращается к helper с singleton latest index. Preparation:1017–1029 вызывает trim;1048–1063 начинает выбор subset лишь при превышении локальной server JSON estimate100000 (constant904). Дальнейшие protected/required/state/side-effect sets и final fitting:1167–1189 могут изменить результат; **конечный upstream payload, live отказ, неверный ответ, replay и потеря данных не доказаны**.

AgentBridge отправляет canonical terminal history без occurrence rewrites и проверяет известный баланс полученного candidate, C:85–88,114–115. Opaque результат не доказывает сохранение внутренней association. Требуется контракт поддерживаемого deployment/repeated pending IDs и адресное evidence subset с различимыми args/results; новая server политика не выбрана. Это развитие существующего Q-005, без дублирующей находки. Подробный server/tools аудит за границами09.

### Историческое evidence и пропуски

Проверено наличие/Times/Counters сохранённых TRX: [stage17-affected-final](../../../artifacts/test-results/stage17/stage17-affected-final.trx), 2026-10-04 **12:36 UTC+07**,189/189 Passed (51 counter+20 guard и другие suites); [stage18-core-final](../../../artifacts/test-results/stage18/stage18-core-final.trx), **13:18**,172/172 (42 compactor+те же130 regression cases); [stage18-transport-final](../../../artifacts/test-results/stage18/stage18-transport-final.trx), **13:22**,165/165, включая36 compact cases по [отчёту18](<../AgentBridge Initial Implementation/18-context-compaction.md>):46. Повторы и перекрывающиеся suites не суммируются.

Read-only diff от `af72252` до HEAD: counter/guard tests, map/factory неизменны; ContextTokenCounter получил model-independent inspector и nullable внутренний tokenizer. От `73045a1`: compactor/tests/compact writer/reader неизменны. [Отчёт17](<../AgentBridge Initial Implementation/17-model-tokenizer.md>):30,60,68 и [техничка07](<../../Technical documentation/07-tokenizer-and-settings.md>):23–43 фиксируют первичные mapping/package/vocabulary источники и hashes. Их первоначальные сетевые чтения и полный binary/transitive provenance сейчас не восстановлены. Это **историческое evidence, не current pass**.

| Пропущено / уровень | Причина и влияние | Нужное окружение/разрешение; будущий сценарий |
| --- | --- | --- |
| Current BPE/vocab/DI/guard/compactor/compact fixtures, B | Прямой запрет исполнения; assertions/TRX не новый pass | .NET10/pinned packages, проверенная fresh build/output chain; отдельные точные разрешения. Векторы/hashes/ordinary markers, whole request, threshold/equality/overflow, все lifecycle/save/cancel/second-pass ветви. Исторические команды17–18 известны, готовая текущая команда с достоверным --no-build не установлена |
| Actual context-save atomicity, C | DB/SQL запрещены; fake writer не transaction proof | Выделенные SQLite/PostgreSQL и разрешённые existing integration сценарии: две версии, stale/delete/recreate, expiry equality, rollback/commit/cleanup uncertainty. Actual EFCoreLibrary, не SQLServer/MySQL; fresh UTC/token. Окружение/команды Q-002 неизвестны |
| Server count/live compact/opaque continuation, D | Внешний HTTP запрещён; JSON framing/fixtures не upstream proof | Exact deployed commit/model/endpoint/accounts/data/расходы и разрешённые операции. Compact→save→full guard; local estimate/server acceptance и смысловая сохранность opaque раздельно. Готовой команды нет |
| Q-005 occurrence subsets, B/D | Helpers не вызывались, адресного distinguishable case в просмотренных AgentBridge tests не найдено; это не основание дефекта | Отдельное поручение на недостающее средство проверки; singleton/all subsets, protected sets и final payload с разными args/results, затем заданный deployment. Новые tests/harness здесь запрещены, команда отсутствующего case неизвестна |

### Итог и передача

**Проверен с ограничениями.** Все группы09 получили результат A и предел; новых кандидатов нет, Q-005 дополнен. Не доказаны current runtime pass, server/billing count, полная opaque оценка/смысловая сохранность, actual DB atomicity, live occurrence compatibility, OS crash/network loss или exactly-once.

Передать10 Q-005 и различие builder balance/FIFO/server subset;11 successful-save token capture, failure второго прохода и обязательный full guard;12 unknown status budget отдельно от полного generation;14 неизменность профильных tests и перекрытие исторических TRX, непроверенные cleanup/occurrence permutations;15 Q-003/004/005 без новой политики. Детальный аудит следующих этапов не выполнен; координатор сам читает файл, сообщения в другие чаты не отправляются.

Финальный контроль A: собственный diff/ссылки/кодировка проверены; исходное задание сохранено, кроме статуса. Strict UTF-8 без BOM/LF, без U+FFFD/mojibake/четырёх вопросительных знаков. SHA256 остальных21 audit-файла совпали до/после правки; index пуст, недокументальных изменений нет, обязательные зависимости чисты и HEAD неизменны. Единственная собственная правка — этот09.
