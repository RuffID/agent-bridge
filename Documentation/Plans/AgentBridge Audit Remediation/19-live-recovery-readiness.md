# 19 — Матрица готовности live transport и crash recovery

[Задание и Results19](19-live-contracts-and-recovery.md) · [Решения](Decisions.md) · [Синтетические fixtures и пустая карточка ресурсов](19-live-recovery-fixtures.json).

Состояние на **2026-10-06**: подготовка A; **C/D заблокированы**. Endpoint отложен пользователем, реальные provider проверки17 тоже отложены. Здесь нет разрешения запуска, настроенного live consumer или crash harness. JSON — данные для будущего средства проверки, не готовый HTTP request/config и не результаты тестов. Все `null` в карточке ресурсов означают «не определено», а не default.

## Контракт и сила evidence

Действующий источник — [main spec](../../../openspec/specs/agent-runtime/spec.md). Q-004/005 согласованы и не требуют повторного обсуждения. [Results03](03-runner-cleanup-cancellation.md#результаты), [04](04-empty-sse-cancellation.md#результаты), [06](06-http-disposal.md#результаты), [10](10-nested-controls-contract.md#результаты), [11](11-repeated-call-pairing.md#результаты), [12](12-sql-server-provider.md#результаты), [13](13-strict-configuration.md#результаты), [14](14-simplified-registration.md#результаты), [15](15-multiplatform-delivery.md#результаты), [16](16-isolated-regression-and-delivery.md#результаты), [17](17-provider-verification.md#результаты), [18](18-runtime-delivery.md#результаты) — historical local evidence, не новый прогон19.

| Уровень | Доступное evidence | Что остаётся |
| --- | --- | --- |
| A19 | Сверка текущих sources/spec, матрица, синтетические fixtures, карточка ресурсов, проверки JSON/ссылок/UTF-8 | Исполнение каждого сценария |
| B03/04/06/10/11/13/14/16 | Actual runner/transport/HTTP library/BPE с local handlers, streams, storage doubles; final request preparation codex-lb11 | Deployed server/account/model, actual MSSQL journal, OS restart/сеть |
| B17 | Settings parser и compile prepared MSSQL basic persistence case | Prepared case не crash case; реальные MSSQL проверки не запущены |
| C18 | Три Windows runtime runs, managed/native Load/Free, DI/BPE минимального app graph | Provider operations, Linux runtime, production app graph, crash |
| C/D19 | **Нет запусков** | Все строки матрицы ниже остаются `not_run` |

JSON `sourceRefs` указывает существующий метод для сравнения assertions; это не executable binding и не заявление полного покрытия строки. AgentRunnerIntegrationTests использует SQLite/PostgreSQL и новый DI root в том же процессе, gateway doubles и synthetic exception после real commit; его исходник не доказывает MSSQL, process death или real acknowledgement loss. Отсутствующий harness — незакрытая подготовка исполнения, а не подтверждённый дефект продукта.

## Общая процедура будущего запуска

1. После отдельного допуска координатора заполнить `resourceForm`: endpoint/deployed commit/model/capabilities, accounts, TLS, app/test processes, MSSQL identity, budgets, fault mechanisms и cleanup. Значения secrets хранятся только в локальном app-owned secret source. В карточке допустимо имя источника, но не API key/connection string/password/token.
2. Подготовить конкретный consumer и команды под эти ресурсы. Перед запуском прочитать его source/imports/targets/assets/traits и применимые AGENTS/skills. Использовать actual facade/HttpClientLibrary/EFCoreLibrary; app владеет configuration, ILogger, HTTP lifecycle, authorization и tools. Не создавать обходной HTTP/EF/SQL pipeline. Сборка конкретного проекта с отдельным output, без restore/network по умолчанию; EF graph — `GeneratePackageOnBuild=false`. No-build только после успешного fresh build того же output.
3. Зафиксировать immutable input/source/kit/deps hashes, SDK/runtime/OS/RID, фактические версии packages и deployed server. Local codex-lb HEAD не выдавать за deployed commit. Current kits16/Windows18 не подтверждают текущий deployment. .NET на Pi5 отсутствует по сообщению пользователя; Linux x64 ресурс не задан. Не устанавливать/copy/run автоматически.
4. Выполнять только утверждённый список случаев. Каждый case имеет отдельный request/action/process budget, один attempt; отключить app/handler retries и key/model fallback. Превышение лимита прекращает следующие случаи и отмечает остаток `not_run`. Порог/резерв/full guard задавать явно из exact model catalog; неизвестный mapping/budget не заменять выбранной наугад моделью.
5. Наблюдать публичный result, safe error, authorized app request counts и actual persisted state через existing read/write ports. Private raw transcripts допускаются лишь на собственных synthetic данных в согласованном закрытом evidence directory; отчёт содержит allowlisted metadata и сравнения, без envelope/continuation/raw errors/exception text/stack/секретов. Даже hash ключа не публиковать.
6. Записать start/end UTC, exact command/cwd/exit, source/build hashes, наблюдаемую fault point, подтверждённые расходы, artifact hashes и cleanup receipt. Каждый вывод относится только к фактически пройденной границе. Успешный JSON не закрывает SSE/compact/recovery. Local KnownTokens не равен billing и не оценивает скрытый opaque state.

## Матрица transport и compact

Во всех строках фактическое состояние19 — **не запущено**. Fixtures/`sourceRefs` в JSON позволяют отличить готовые данные от отсутствующего executable сценария. Если deployment не позволяет наблюдать/создать нужную точку, строка остаётся blocked; случайный timing успех не заменяет контролируемый fault.

| ID | Future case и обязательные наблюдения | Предшественник / недостающее средство |
| --- | --- | --- |
| T01 | Catalog exact selected key: route base-prefix `/v1/models`, ID/effort/capabilities/budget из ответа; safe snapshot без secrets, пустой каталог без static fallback | ModelCatalogTests; endpoint/model/account |
| T02 | JSON: canonical input/output order, unknown/opaque/envelope, explicit completed; HTTP2xx/missing output/EOF не Completed | ResponsesJsonTests; live synthetic request |
| T03 | SSE: indexed canonical items, arguments/text/unknown; sequential awaited callbacks; terminal confirmation отдельно от DONE/EOF | ResponsesSseTests; live streaming и наблюдение final state |
| T04 | Compact: `/v1/responses/compact`, discriminator/status/output, CompactTimeout, continuation=null; активация только после save, история/expiry прежние | ResponsesJsonTests + ContextCompactorTests; live compact и собственный storage |
| T05 | JSON→SSE и SSE→JSON continuation: exact authorized binding; outgoing только previous_response_id/x-codex-turn-state; новый ответ без safe ID удаляет старый anchor | Cross-transport sourceRefs; controllable response/no-ID fixture на выделенном endpoint, не произвольные старые anchors |
| T06 | Неверный individual при настроенном shared: Unauthorized/Forbidden либо local Validation по форме, без повторной shared отправки; Individual/null отклоняется до HTTP | ModelCatalogTests/facade14; два согласованных test accounts/ключевых источника и app send observer |
| T07 | Safe errors: 401/403/other status mapping, закрытые type/code/param, raw headers/body/message отсутствуют в UI/logs; incomplete/oversized64KiB отдельно | ResponsesJsonTests; собственные error routes/fault mechanism и logger sink observer, не публикация raw ошибки |
| T08 | Caller cancel до canonical данных: OCE с исходным token, callbacks0; отдельно pre-send, blocked read, empty EOF/comments/DONE с successful disposal | SSE04; управляемые gates и disposal observer, live timing сам по себе недостаточен |
| T09 | Cancel после partial и после полного terminal: сохранены точные known output/envelope/continuation; explicit Failed не заменён поздним caller cancel; deadline отдельно | SSE04/06; контролируемые partial/terminal/error gates |
| T10 | Cleanup: попытка каждой ownership boundary, original primary identity/stack и immutable secondary Data, standalone OCE/JSON/HTTP cleanup без normalization; attempt/completed различаются | HTTP06; согласованный throwing cleanup seam поверх actual path, метрики соединений отдельно. Fake disposal не доказательство deployed leak |
| T11 | Known malformed/duplicates: каждый сырой parameters JSON из fixture, JSON/SSE/compact где поле поддержано → Validation/send0; text в compact остаётся Unsupported | NestedControls10; send observer в live consumer. Это local pre-HTTP invariant, сервер не получает bad payload |
| T12 | Unknown fields/duplicates и arbitrary schema сохраняются полностью; known nullable поля приняты; top-level policy не расширена | NestedControls10; только reasoning в compact, text/schema только generation; модель может отдельно отказать неизвестному control |
| T13 | FIFO distinct A/B, grouped и alternating, retained first/second/full: конечный wire payload сохраняет callA/resultA либо callB/resultB и order; после save closed pairs → handler0 | Pairing11; deployed request-preparation trace на synthetic input + actual compactor/save/builder/executor |
| T14 | Protected required pair и fitting cap: counterpart остаётся целиком в final payload либо explicit refusal; insufficient budget не теряет полпары | LB11 focused source; server trace доступен только после подтверждения deployed commit/наблюдения полного final payload, helper trace недостаточен |
| T15 | Wrong/rewritten/duplicate/incomplete/orphan либо неоднозначный identical subset → отказ до save; latest accepted window/token/history/expiry неизменны, handler0 | ContextCompactorTests; actual accepted-window oracle и управляемый returned candidate. Обычный live compact не гарантирует генерацию каждого bad candidate |
| T16 | Second pass bad association/exception/cancel сохраняет window/token первого successful save; нет refresh/retry/replay | ContextCompactorTests/Runner03; gates после подтверждённого первого save |
| T17 | Full guard после compact включает instructions/providers/new input/tools/controls/retained items; fitting compact не разрешает oversized generation | ContextBudgetGuard/RepeatedAssociationSubsetDoesNotReplaceFullBudget; actual offline tokenizer/exact mapped model, small configured boundary |
| T18 | После confirmed tool outcome guard пересчитывает whole request, oversized output не вызывает следующую generation; confirmed output сохранён | AgentRunnerTests.GuardChecksWholePayloadAfterToolOutcome; synthetic bounded tool и send counters |
| T19 | Opaque candidate может быть сохранён как UnknownBudget, estimate=null; отдельный guard даёт Unsupported и generation0; no fake server count/billing | ActualOfflineCounter/opaque runner; настоящий opaque output разрешённой модели либо честно blocked, синтетический cipher не live evidence |

T11 проверяется на выбранном adapter в live consumer с sends0; это не доказательство server rejection. T13/14 server final payload и T15/16 injected returned candidate — разные виды evidence и должны иметь отдельные receipts. Test fixture не является инструкцией заставить модель возвращать заданный JSON. Synthetic opaque marker только для сравнения preservation в подготовленных данных; live opaque нужно получить от actual model без подмены.

## Матрица actual process recovery

Для каждого случая нужен выделенный процесс P1 и **новый OS процесс P2**, actual MSSQL через existing EF adapter, внешнее наблюдение test action counter, независимый read scope и сохранённые process-start/PID/exit receipts. PID без времени создания может быть переиспользован. Убийство должно адресоваться проверенному собственному P1; new ServiceProvider, caught exception или cooperative cancel не являются аварией процесса.

Synthetic action только увеличивает счётчик в выделенном тестовом ресурсе, не выполняет бизнес-действий. Будущее средство обязано сделать увеличение наблюдаемым другим процессом и определить его durable semantics. In-memory counter исчезает с P1; локальный «action entered» log не доказывает завершение. Если crash попадает между increment и подтверждением наблюдателю, результат отмечать неизвестным, не выдумывать значение. Observer не должен превращаться в автоматический retry/idempotency сервис.

| ID | Stop/fault point и oracle после P2 | Недостающая подготовка / предшественник |
| --- | --- | --- |
| R01 | До Begin commit: action0; read определяет, есть ли turn. Без существующего turn допустимость первого запуска проверяется отдельно; unknown commit не трактуется как отсутствие записи | Process gates + MSSQL; runner initial path |
| R02 | Begin/model step подтверждены, до Started: action0, journal пуст/legacy; повтор existing TurnId → Interrupted/Conflict без model/handler; история неизменна | Process gate до Start; RestartRefusesExistingTurn и legacy integration source |
| R03 | После durable Started, до action: observer видит Started/identity до handler, action0; P2 same TurnId не меняет token/journal и handler0 | Process gate после Start ack; RestartDoesNotReplayStartedOrLegacyTurn |
| R04 | Action подтверждён внешним счётчиком, до outcomes: action1, Started без fake output; P2 action остаётся1 и не replay; Started не переписывать в Unknown только ради отчёта | Durable external test counter + hard stop; B UnknownAction отличается от crash journal |
| R05 | Outcomes SQL staging/SaveChanges до commit: действительное persisted состояние определяет read; atomic journal+outputs без полупары. Не обещать rollback без подтверждения real boundary | MSSQL fault seam; OutcomeSaveFailureRollsBackOutputsAndJournalTogether пока SQLite/PG source |
| R06 | После outcomes commit/до ack или terminal: persisted confirmed outcomes+outputs вместе либо honest unknown при неопределённом commit; P2 handler0, count неизменен, existing turn blocked | MSSQL actual ack/fault observation; synthetic postcommit throw отдельно |
| R07 | После terminal commit, до возврата клиенту: read подтверждает terminal status/token/snapshot; same TurnId в P2 → existing-turn Interrupted, не новый Completed и не повтор finish/action | Process gate после Finish ack; terminal-save runner source |
| R08 | Real disconnect/ack loss до/после Started/outcomes/terminal: серверная commit identity и read сверяются; текущая session blocked без token refresh/retry, action после неизвестного Start не выполняется | Собственный network fault controller и отдельное разрешение точных endpoints/processes. UnknownStartCommit source — synthetic exception, не этот случай |
| R09 | Operation cancel/primary+scope cleanup OCE при canceled caller: exception origin/identity, accepted token/step и no subsequent writes; P2 проверяет фактическую запись без replay | Throwing scope seam и original-token observation; Runner03 B не provider disposal/crash |
| R10 | Partial parallel actions: awaited confirmed neighbor сохраняется с matching StepId/position; Unknown без fake output; hard crash отдельно от exception и cooperative cancellation | Изолированные controlled gates/counter per occurrence; ParallelPartialFailure source |
| R11 | Exact identity/owner authorization: wrong owner/agent, stale incarnation/revision, duplicate position, repeated call_id FIFO; приложение authorizes users/tools до action; закрытые пары handler0 | Own users/data/validator и actual write guards. Local continuation binding не app authorization |
| R12 | Fixed expiry/delete boundary, settings snapshot: fresh UTC/now==expiry отказывает late write; независимая смена selection не меняет turn snapshot; history/expiry не продлеваются, секреты не persisted | MSSQL cases и управляемые часы/selection; запрет автоматического recreation/cleanup, delete только согласованный собственный диалог |

До падения записать доступные подтверждения identity: owner alias, DialogId/incarnation/revision, TurnId/AgentId, StepId/output position, состояние попытки, settings snapshot/version, ExpiresAtUtc, model/compact provenance и canonical equality. После P2 сравнить через actual readers; секретный ModelAccess не сохраняется и не публикуется. Process restart не восстанавливает тот же объект exception: original exception identity — наблюдение внутри P1, P2 проверяет durable state.

Для R05/06 outcome journal и canonical outputs читаются одним защищённым актуальным snapshot; concurrent Conflict не лечить retry ради passing теста. Unknown action и неизвестный commit — разные неопределённости. No-replay не обещает внешнее exactly-once и не сообщает, завершился ли потерянный внешний эффект.

## Карточка ресурсов, расходы и очистка

Пустая `resourceForm` в JSON содержит обязательные поля без адресов/моделей/defaults. Перед future run владелец задаёт:

- Transport: точный endpoint/base-prefix и deployed commit/build, TLS/trust policy, exact model IDs/capabilities/tokenizer mapping, shared/individual account aliases и local secret source names; разрешённые catalog/JSON/SSE/compact/error/fault операции, app observer и безопасный logger sink.
- Budgets: max requests **включая catalog/errors/compact**, max model turns/tool actions/compact passes, per-case и общий wall time, отдельный max billable tokens/cost с currency и источником actual usage; остановка при недоступности расходов, без приравнивания local BPE к billing. Никаких числовых значений за пользователя не выбрано.
- Process/storage: OS/RID/runtime/kit/deps graph обеих app/test machines, owner/P1/P2 executable и start command, process-start identity, exact MSSQL endpoint/version/edition/EngineEdition/TLS/auth source/database identity, разрешённые existing schema/read/write операции; initialize/migrate остаются отдельно согласуемыми, как в17.
- Faults: exact boundary, наблюдение before/after commit/ack, gate readiness, hard stop command и его target identity, network disconnect mechanism/границы, throwing disposal seam; synthetic exception receipt отдельно от real process/network fault.
- Counter/evidence: выделенный durable test counter path/store и per-occurrence schema, разрешённый increment/read, существующие baseline values, synthetic run namespace, закрытый evidence directory/ACL/retention и whitelist полей отчёта.
- Cleanup: владелец, exact созданные dialogs/processes/files/database identity, подтверждённый путь в выделенном test workspace, permit list удаления и receipt. Prefix/GUID не доказывают владение. MSSQL17 DisposeAsync освобождает только DI и **не удаляет БД**; существующий SQLite/PG fixture с automatic cleanup не запускать как MSSQL replacement.

Реальная DB/SQL/migration generation/apply/backup/restore, Docker, hosting, remote installation/copy/deployment, production/business data и destructive cleanup сейчас не разрешены. Test commands/scripts разрешены; это не определение реальных ресурсов. Команда запуска и удаления появится только после review конкретного средства и утверждения ресурса. Новая информация после handoff не возобновляет writes без допуска координатора.

## Шаблон будущего evidence

`evidenceTemplate` в JSON фиксирует caseId, variant/fixture, source hashes/versions, command/cwd/exit, process identities, faults, безопасные наблюдения, расходы, cleanup и outcome. Для каждого включённого варианта строки отдельно указать `passed`, `failed`, `blocked`, `not_run` или `unknown`; `not_run` не success/skip. Gate, чья нужная точка не достигнута, не доказывает fault scenario.

Current readiness: matrix/data подготовлены и статически проверены; executable live/crash consumer, fault controllers и durable counter **не созданы**. Compile-only case17 и runtime probe18 не переименовываются в harness19. Следующий шаг после ресурсов — адресная реализация средства и preflight/compile, затем согласованные C/D запуски. Полная приёмка19/20 до них не заявляется.
