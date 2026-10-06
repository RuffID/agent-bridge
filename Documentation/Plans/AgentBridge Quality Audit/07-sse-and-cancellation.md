# 07 — SSE, отмена и частичные ответы

Статус: **проверен с ограничениями**. Предпосылки: 06; известны JSON lifecycle и resource ownership.

## Цель и вопросы

Проверить сборку потока, сохранение частичных данных и завершение всех ресурсов/обработчиков на отказах.

## Компоненты и зависимости

[SseEventReader](../../../adapters/AgentBridge.CodexLb/Responses/SseEventReader.cs), ResponseSseState, CodexLbModelGateway.GenerateStreamAsync, HttpClientLibrary/Models/HttpStreamResponseResult.cs. [ResponsesSseTests](../../../tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs), HTTP logging tests после чтения их локальных инструкций.

## Способ проверки и границы

Фрагментация по байтам UTF-8, BOM, LF/CRLF/CR, multiline data, comments, пустой/незакрытый frame, EOF и DONE без terminal. Проверить порядок индексов, partial arguments/text/reasoning, конфликт собранных items и авторитетного непустого terminal output. Callback должен ожидаться последовательно; ошибка callback не становится ошибкой сервера. Рассмотреть caller/deadline до данных, после данных и при disposal, typed failure до поздней отмены, I/O exception, cancel callback и ошибку освобождения. Проверить ABQA-002 через существующее покрытие; не создавать тест в рамках аудита. Не обещать принудительную остановку чужого callback.

## Разрешения

A; локальные streams/handlers существующих тестов — B. Реальный disconnect сервера — D; сценарий, требующий нового harness, остаётся пробелом. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Последовательности событий, partial/terminal отчёты, число и порядок callbacks, пути dispose и сохранённые исключения. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Framing, lifecycle, cancellation priority и cleanup paths рассмотрены с отдельным статусом ABQA-002. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Поведение реальной сети/прокси и отсутствие resource leak на основании обычного успешного disposal.

## Результаты

### Состояние и границы

Дата: **2026-10-05–06, Asia/Novosibirsk**; начало по Coordination — 2026-10-05 23:52, завершение — 2026-10-06. Исполнитель: чат07 `01a10cfa-a0fb-7b10-a4fd-e858c457c0e9`, cwd `D:/Media/User/source/repos/agent-bridge`. Только **A**: исходники, требования, существующие тесты/TRX, локальные ссылки и read-only Git. Методы продукта/тестов не исполнялись. Черновика результатов07 не было; исходное задание сохранено, кроме статуса.

| Репозиторий | Повторно проверенный HEAD | Исходное состояние |
| --- | --- | --- |
| AgentBridge | `41072fc37bb718dd9f4bb0473912802260bb9164` | Чужой tracked diff десяти Markdown00–06/Findings/Methodology/README:842 добавленных/27 удалённых строк; untracked Coordination/OpenQuestions. Свой07 без diff; index пуст, недокументальных изменений нет |
| HttpClientLibrary | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Чисто; actual streaming/error/diagnostics/ownership, read-only |
| EFCoreLibrary | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Чисто; root AGENTS прочитан. В рассмотренных SSE-компонентах нет DB-доступа; EF-аудит05 не повторяется |

Прочитаны README/Baseline/Methodology/Findings/OpenQuestions/Coordination и принятые итоги00–06. Baseline подготовки на c8da604 не заменяет baseline запуска из [Coordination](Coordination.md). Применены ancestor/root, Documentation/Plans, CodexLb/Responses, Application/Diagnostics/tests AGENTS, root обязательных библиотек и HTTP Clients/tests; скилл `csharp-project-rules`. Прямой запрет B/C/D имеет приоритет. Дополнительные репозитории не потребовались: source contract codex-lb уже рассмотрен06, он не live evidence07.

Проверены parser/state/gateway, общая continuation, независимые ModelResponse/ModelStreamUpdate/IModelGateway, actual HTTP streaming/error/diagnostics и существующие ResponsesSseTests/HTTP logging tests. Нормы: [main spec](../../../openspec/specs/agent-runtime/spec.md):237–258,599–626; [техничка15](<../../Technical documentation/15-responses-sse-adapter.md>):20–36. Подробный аудит composition/tools/runner08–11 не выполнялся.

### Все группы вопросов

Обозначения: **R**=`adapters/AgentBridge.CodexLb/Responses`; **T**=`tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs`; **H**=`D:/Media/User/source/repos/work/HttpClientLibrary`. Имена C#-файлов ниже имеют `.cs`, если расширение опущено. Все номера строк сверены по текущим файлам; результат — **A**, прочитанная assertion не новый pass.

| Вопрос | Исследовано | Результат A | Evidence: файл/символ/строки | Ограничение |
| --- | --- | --- | --- | --- |
| Выбор транспорта/ownership | GenerateAsync и actual SendStreamAsync | Non-null callback→stream=true/SSE, null→JSON; один HTTP send, ResponseHeadersRead, без retry/reconnect. Request освобождает H, успешный body/response передаётся wrapper; gateway владеет deadline/linked CTS, приложение HttpClient/handlers | R/CodexLbModelGateway:40–53,95; R/ResponseRequestWriter:33–37; H/Clients/HttpApiClient:67–68; H/Clients/HttpStreamingResponseClient:17–41 | App handlers и live routing не проверены; 2xx означает заголовки, не completion |
| UTF-8 fragmentation/BOM | StreamReader, decoder и byte fixtures | Strict UTF8Encoding(false,true), auto encoding detection отключён, только первый U+FEFF снимается; partial bytes объединяет reader. Неверные bytes→DecoderFallbackException→safe Rejected, с known state→Failed | R/SseEventReader:13–24; R/CodexLbModelGateway:122–125; T:128–152,637–649 | Поведение BCL/native network не исполнялось; тест фрагментирует локальный stream |
| LF/CRLF/CR/multiline/comments | ReadLineAsync, field parsing/reset | Reader выделяет строки; пустая строка dispatch только hasData. Data соединяются LF, снимается один пробел после colon; comments/id/retry/unknown fields не создают callbacks/reconnect. JSON type приоритетнее event field, event — fallback | R/SseEventReader:18–44; R/ResponseSseState:25–26; T:155–195 | Empty data frame доходит до JSON parser и отклоняется; пустой frame без data игнорируется |
| EOF/незакрытый frame/DONE | Yield boundary и loop | EOF не dispatch накопленный незакрытый frame; exact [DONE] пропускается и не устанавливает terminal. Empty stream/unknown event/created со status completed/отсутствующий terminal response→Incomplete | R/SseEventReader:25–45; R/CodexLbModelGateway:97–100,112–116; R/ResponseSseState:63–69,121–130; T:198–253 | EOF не подтверждает успешность сервера; malformed bytes даже в незакрытом suffix могут дать Rejected раньше EOF |
| Indices/links/order | SortedDictionary, Index/Item/Parts | Output сортируется по nonnegative Int32 output_index, а не arrival. Delta требует существующий item; заданный строковый item_id должен совпасть. Content/summary gap и неверные kinds отклоняются, полный done item заменяет snapshot | R/ResponseSseState:11,71–89,123,149–186; T:166–180,301–313 | Не схема всех unknown JSON типов; sparse output indices допустимы, part gaps не исправляются |
| Partial text/function arguments/reasoning/refusal | Delta/done handlers и Report | Delta дописывается, done value заменяет строку; EOF сохраняет неполные arguments/text, reasoning summary/refusal/unknown/opaque fields. Callback только output_text.delta либо полный output_item.done; partial arguments не выдаются как готовый tool | R/ResponseSseState:91–115,121–130; T:29–49,203–214 | Summary/text branches рассмотрены; refusal/reasoning_text не имеют отдельных assertions в T. Их фактическое исполнение не доказано |
| Terminal/authoritative output | Envelope отдельно, collected backfill | Completed только response.completed + response.status=completed + output array либо collected items + no error. Непустой response.output очищает прежние extras; absent/empty допускает backfill. Raw envelope не переписывается; tools-only/unknown output не требует текста | R/ResponseSseState:29–50,63–69; T:143–152,161–195,224–234 | [] в terminal допустим без collected data; отсутствие response исключает Completed даже с items |
| Failed/incomplete/server cancelled | Explicit errors/status и остановка чтения | Error/response.failed/non-null response.error/response.status failed→Failed с sensitive envelope и safe typed error. Response.incomplete или completed с иным status→Incomplete. Server cancelled сам не caller Canceled. Terminal прекращает следующие events/callbacks | R/ResponseSseState:46–69; R/CodexLbModelGateway:108,113; T:219–276 | Unknown событие с новым canonical response сохраняет envelope, но не доказывает его успешный lifecycle |
| Sequential callback/exception identity | Await и callbackFailed filter | Следующий event и возврат ждут callback; нет Task.Run/fan-out/background. JsonException/HTTP/OCE/decoder/other callback exception rethrow, transport catches исключены; после ошибки новых callbacks нет | R/CodexLbModelGateway:101–109,118–135; T:318–385 | Identity сохраняется при успешном cleanup; throwing disposal может заменить pending exception, см. ABQA-002. Чужой callback принудительно не останавливается |
| Caller/deadline до данных | Pre-call check/send/read catches | Pre-canceled caller не отправляет HTTP. OCE send/read при caller→новый OCE с исходным caller token; deadline→typed Fail(Timeout); OCE без подтверждённого источника распространяется | R/CodexLbModelGateway:28,48–53,127–135; T:56–121,394–407 | **[ABQA-008](Findings.md#abqa-008):** успешный empty EOF/disposal идёт другой веткой и не сохраняет OCE до данных |
| Caller/deadline после данных/при disposal | HasData, Cancel/Fail и late checks | HasData включает canonical response/item/delta/error. Caller→Canceled с output/envelope/continuation; deadline→Failed/Timeout. Caller проверяется раньше deadline, explicit state failure сохранён. После полного terminal успешный disposal с caller cancel сохраняет report | R/ResponseSseState:17–18,29–34,133–145; R/CodexLbModelGateway:112–135; T:88–101,394–442 | Late deadline disposal отдельно не assert; canceled callback, выбросивший OCE, остаётся callback exception по предыдущей строке |
| Typed failure раньше поздней отмены | Catch order и failure preservation | HTTP failure нормализуется раньше OCE; JSON/decoder failure→Rejected раньше token checks. Model failure возвращается до caller/deadline; Fail/Cancel не заменяют уже установленный failure | R/CodexLbModelGateway:113–135; R/ResponseSseState:133–145; T:262–298,433–458; H/Clients/HttpErrorResponseHandler:16–25 | Если вторичный cleanup сам throws, исходный failure может не достичь этих catches; это не доказанная runtime-утечка |
| Unexpected I/O/cancel callback | Transport vs consumer origin | IOException без synthetic expected failure распространяется; callback OCE тем же объектом даже при canceled token. Cooperative deadline не отделяет callback task; его зависание без соблюдения token не обещано прервать | R/CodexLbModelGateway:105–106,118–135; T:362–385,465–473 | Реальный disconnect/несотрудничающий consumer не запускались; callbacks предварительные, не terminal save |
| Cleanup всех выходов/ABQA-002 | Reader leaveOpen, wrapper await using, send catch | Completion/EOF/parse/cancel/callback/I/O выходы проходят reader disposal и wrapper DisposeAsync; ранний send/error/read-stream failure — H catch. Wrapper Body disposal предшествует response.Dispose без finally; при throwing Body второй вызов пропускается | R/SseEventReader:13; R/CodexLbModelGateway:93–137; H/Clients/HttpStreamingResponseClient:21–41; H/Models/HttpStreamResponseResult:22–32; T:514–519,657–660 | ABQA-002 остаётся подозрением S2 предварительно. Обычный successful disposal не proof отсутствия leak/сохранения primary при двух ошибках |
| Continuation и безопасная диагностика | Shared mapper/HTTP logger | SSE применяет общий binding/allowlist; новый missing id удаляет старый anchor. Output/envelope независимы и чувствительны. Successful SSE H не читает ради logging; metadata event не completion, raw error logger не публикует | R/ResponseSseState:32–33,56–57; R/ResponseContinuationMapper:25–71; T:478–496; H/Clients/HttpErrorResponseHandler:16–21; HttpResponseDiagnostics:32–66; H/HttpClientLibrary.Tests/HttpLoggingTests:217–253 | Не upstream ownership, не full opaque estimate; app log scopes/handlers вне гарантии |

### Находки и вопросы

**[ABQA-008](Findings.md#abqa-008) — отмена после пустого EOF возвращает Canceled вместо исходного OCE до данных.** Категория: дефект реализации; **подтверждено статическим разбором**, не runtime-воспроизведением. **S3:** нарушен ограниченный контракт исключения/токена при отмене пустого обращения; completion/утрата данных/утечка не заявляются.

- Контракт: main spec:253, caller cancellation до данных с исходным token; техничка15:34 уточняет переключение на Canceled после canonical response/item/delta. Общий `Application/Ports/IModelGateway.cs`:11–12 говорит о наличии отчёта; это не отменяет более точную SSE-границу данных.
- Факт: `R/CodexLbModelGateway.GenerateStreamAsync`:112–114 после успешного EOF/cleanup вызывает Report и без проверки HasData возвращает state.Cancel при canceled caller. В catch127–130 та же граница использует HasData и до данных бросает OCE. `R/ResponseSseState`:15–18,121–137 для пустого состояния создаёт output=[], envelope/continuation=null, status=Canceled.
- Статический сценарий: успешный HTTP2xx с пустым body → EOF без единого Apply → HasData=false → успешное Body.DisposeAsync отменяет caller без exception → response.Dispose успешен →114 возвращает Ok(Canceled). Deadline не сработал, callbacks0. Ожидание по SSE-контракту — OCE с исходным caller token. Негативные границы: без отмены пустой EOF остаётся Incomplete; при actual canonical data late cancel вправе вернуть Canceled.
- Evidence на HEAD41072fc: существующая T fixture поддерживает пустой stream:239–252,637–649 и cancel-on-disposal:433–442,627,657–660, но в late-cancel case всегда есть Partial+terminal. Сочетание empty EOF+OnDispose cancellation не выполнялось и не добавлялось. Дефект выведен из условий production-ветки, не отсутствия assertion.
- Влияние: потребитель получает lifecycle result вместо ожидаемого исключения и исходного token. Достижимость на реальном app/network stream не установлена. Будущий B-case — пустой EOF, cancel при успешном disposal, сравнение с partial/terminal и OCE при read; точной готовой команды этого отсутствующего case нет. Связи:14/15; отдельная причина от ABQA-002, disposal здесь успешен. Устойчивый ID назначает координатор.

**[ABQA-002](Findings.md#abqa-002) — без повышения достоверности.** Порядок Body→response без finally подтверждён повторно по actual H:22–32. При условном throwing Body.Dispose/DisposeAsync response.Dispose по этому пути не достигается; await using gateway:95–111 также может заменить pending callback/JSON/I/O exception вторичной ошибкой cleanup. При callbackFailed=true transport catches вообще исключены, но это не сохраняет первичный объект при ошибочном cleanup. Комбинация не воспроизведена, связь с утечкой реальных соединений не доказана.

Просмотренные `T.AssertDisposed`:514–519 и `FragmentedStream.Dispose`:657–660, H `Success_LogsMetadataAndPreservesOwnership`:217–253/`TrackingStream.Dispose`:429 проверяют успешный cleanup. H `Error_ReadFailure_IsNotMaskedAndDisposes`:294–307 и canceled read313–327 не используют throwing disposal. Статический поиск DisposeAsync/throw-dispose в actual H и полный просмотр T не обнаружили адресного sync/async Body-disposal-failure case; это пробел проверенного набора, не самостоятельная находка и не proof leak. Дополнительно H `HttpStreamingResponseClient`:39–40 последовательно освобождает request/response без сохранения primary при вторичном исключении; конкретная достижимость также не установлена. ABQA-007 EF maintenance имеет другой контракт и не подтверждает HTTP runtime-эффект.

**Новых S07-QNN нет.** [ABQA-Q-002](OpenQuestions.md#abqa-q-002) охватывает окружения/ресурсы/разрешения B/D; ABQA-Q-003/004 относятся к workflow/controls06 и здесь не решаются. Требования/бизнес-политика не менялись.

### Историческое evidence и пропуски

Прочитаны XML UnitTestResult/Times/Counters: [stage15-codexlb-final.trx](../../../artifacts/test-results/stage15/stage15-codexlb-final.trx), 2026-10-04 11:30 UTC+07,191 Passed, из них55 ResponsesSseTests; [stage15-task-cleanup.trx](../../../artifacts/test-results/stage15/stage15-task-cleanup.trx),11:33,7 Passed повторных SSE cases. **191+7 не198 уникальных тестов.** Это старые local handler/stream результаты, не current pass и не live codex-lb.

Происхождение сопоставлено с [отчётом15](<../AgentBridge Initial Implementation/15-responses-sse-adapter.md>):40–76 и commit `1d99721`. `git diff 1d99721 HEAD` для SseEventReader/ResponseSseState/ResponsesSseTests пуст; diff gateway касается только CompactAsync. SHA/MVID всей исторической binary chain и соответствие каждого transitive dependency запуску не восстановлены; полный evidence-аудит14 не заменён. Ни один старый case не воспроизводит [ABQA-008](Findings.md#abqa-008) либо throwing disposal ABQA-002.

| Что / уровень | Причина, влияние | Нужное окружение/разрешение; будущий сценарий |
| --- | --- | --- |
| Current compile/существующие SSE и H tests, B | Прямой запрет; source/TRX не подтверждают текущее исполнение | .NET10, проверенные imports/packages/выделенные outputs и отдельное разрешение точных concrete csproj-команд; адресный ResponsesSseTests framing/terminal/callback/cancel плюс H ownership/logging. Не использовать historical --no-build без binary provenance |
| Empty-EOF late cancel и disposal failures, B | Соответствующего полного existing case нет; новый test/harness запрещён. Непроверены actual callback exception+cleanup, sync/async Body failure и response cleanup | Отдельное поручение вне аудита на недостающее средство проверки, затем точное B-разрешение. [ABQA-008](Findings.md#abqa-008): empty vs partial/terminal; ABQA-002: primary+secondary, наблюдаемый response/request, оба disposal paths. Сейчас ничего не создано/запущено |
| Live framing/disconnect/caller/deadline/app consumer, D | HTTP/hosting/интеграции запрещены; fixtures не сеть/прокси, cooperative await не принудительная остановка | Согласованные endpoint/deployed commit/model/account/test data/расходы, consumer и точные операции; обрыв до данных/после delta/после terminal, отмена и release connection. Synthetic EOF не реальный disconnect; command до Q-002 не установима |
| Durable сохранение partial/terminal/crash, C/D и11 | Вне подробного07 и запрещённая динамика; streaming report не terminal save/atomic persistence | Продолжить статический11; actual scoped persistence/OS crash и lost acknowledgement только по отдельным resources/permissions. Fake writer/new DI root не эти доказательства; no replay не exactly-once |

Готовые команды нового исполнения не установлены: fresh build/output chain и будущие B/D ресурсы не согласованы, отсутствующие cases требуют отдельного поручения. Пропуски не успех и не отдельная находка на каждый запрет. Build/tests/scripts/exe/harness/app/hosting/CLI/установка/Docker/DB/SQL/migrations/backup/restore/native/live HTTP не выполнялись; новые средства проверки не создавались.

### Итог и передача

**Проверен с ограничениями.** Все группы07 получили результат A и предел. Один новый кандидат **[ABQA-008](Findings.md#abqa-008)**, ABQA-002 остался подозрением. Не доказаны current runtime pass, отсутствие resource leak, сохранение primary при throwing cleanup, реальный disconnect/app callback behavior либо durable terminal save. Исправлений нет; единственная собственная правка — этот07.

Передать08 canonical partial function arguments/раздельность envelope и output;09 unknown/opaque не full estimate;11 предварительность updates и различие Completed transport/terminal save без обещания external exactly-once;14 [ABQA-008](Findings.md#abqa-008), ABQA-002 combinations,55 SSE/7 repeated artifacts и binary provenance;15 кандидат и неизменённый уровень ABQA-002. Общие реестры/статусы редактирует координатор, отдельное сообщение ему не отправляется.

Финальный контроль A: собственный diff прочитан, diff --check успешен; локальные path-ссылки доступны, ABQA-002/Q-002 anchors сверены. Strict UTF-8 без BOM/LF, без U+FFFD/mojibake/четырёх вопросительных знаков; исходное задание сохранено, кроме статуса. SHA256 остальных21 Markdown до/после совпадают; index пуст, недокументальных изменений нет, обе зависимости чисты/HEAD неизменны. Предупреждение autocrlf при read-only diff не меняло файл/config.
