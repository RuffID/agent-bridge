# 06 — Проверка и условное исправление HTTP disposal

[Навигатор](README.md). Статус: **принят в A/B-границах**. Зависимость: 00; 04 для итоговой SSE-регрессии. Находка: **ABQA-002, подтверждено и исправлено в локальной B-границе; S2 остаётся предварительно**.

## Цель и область

Проверить освобождение response при throwing Body.Dispose/DisposeAsync и сохранение первичной ошибки обращения. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), streaming lifecycle в [OpenSpec](../../../openspec/specs/agent-runtime/spec.md).

Владелец wrapper — [HttpStreamResponseResult](../../../../work/HttpClientLibrary/Models/HttpStreamResponseResult.cs), проект `HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj` соседней библиотеки. В AgentBridge проверить gateway ownership. Изменения HttpClientLibrary требуют отдельного поручения; не добавлять параллельный HTTP-клиент.

## Работы

1. Создать изолированный наблюдаемый response/content и stream, выдающий контролируемое исключение при sync/async disposal. Проверить оба wrapper paths, повторное освобождение и ownership Content/Body по реальным .NET компонентам fixture.
2. Проверить, достигается ли response cleanup после ошибки Body, что реально завершено и какое исключение получает вызывающий. Отличать вызов Dispose от гарантии успешного освобождения при новом secondary failure.
3. При подтверждении нарушения точечно исправить библиотечную ownership границу, обеспечив попытку освобождения response и честный результат ошибок. Сохранить agreed public API и успешные sync/async пути.
4. Отдельно проверить gateway: read/JSON/callback primary exception + throwing cleanup. Место сохранения primary определить по actual ownership; правка wrapper сама по себе не доказывает сохранение callback exception через await using.
5. Если нужен новый публичный error/lifecycle contract, согласовать его до реализации. При опровержении сохранить контрпример, границу вывода и отказ от необоснованной правки.

## Проверки

B без live HTTP: wrapper sync/async; успешный и throwing Body; наблюдаемый response/content cleanup; primary-only/cleanup-only/двойной отказ; canceled caller. В gateway использовать actual HttpClientLibrary с fake handler/local streams и проверить no retries, Failed/caller/deadline/callback priority после04.

Compile-check конкретных library и adapter/test проектов. Реальная утечка соединений либо её отсутствие в deployment не выводятся из fixture; при необходимости отдельное evidence19.

## Критерии завершения

ABQA-002 получает «подтверждено и исправлено в указанной границе», «опровергнуто в указанной границе» либо остаётся подозрением с точным недостающим evidence. Условная серьёзность не превращается в доказанный ущерб. Primary и cleanup результаты не теряются молча.

## Результаты

### Основание, baseline и согласование

2026-10-06, Asia/Novosibirsk. Выполнен только06. Прочитаны задание, README/Decisions, принятые00/04, Findings/итог15 аудита, действующая main spec и root/local AGENTS. Применён csharp-project-rules с style/build-validation/agents-maintenance. Исторические отчёты, предшествующие исправления03/05, EF и LB исходники не изменялись.

На входе AgentBridge HEAD `4c68efe20b447344f34a6bcdee6552898e64f9e9`, HttpClientLibrary HEAD `6d0528d940d1d8494c722c22464051dd961d6bf7`. HTTP clean, AB имеет чужую правку `Coordination.md`; она исключена. EF clean, LB имеет чужой untracked `.vs/`, исключён целиком. Разрешение пользователя, переданное координатором, явно охватывает transport/tests/docs AB и streaming lifecycle/tests/владеющие AGENTS HTTP. Работа ведётся в existing checkout без branch/worktree и Git mutations.

После before пользователь непосредственно ответил на вопрос исполнителя06: **«Согласовать предложенный контракт»**. Согласованы original primary identity/stack, immutable secondary `Data["HttpClientLibrary.CleanupExceptions"]`, combined typed bypass normalization, original cleanup-only и repeat wrapper no-op после первой попытки. Координатор независимо проверил прямой ответ и явно допустил продолжение единственного checkout writer. Датированное дополнение внесено в Decisions и нормы/текущую техничку. Empty список отмечает standalone cleanup origin без secondary; повторный throw primary не создаёт self-reference и не теряет origin/previous snapshot. Secondary occurrences сохраняются в порядке наблюдения, один secondary на разных boundaries может повторяться. Exception/Data не являются safe UI/log DTO.

### Exact manifest по двум репозиториям

`D:/Media/User/source/repos/work/HttpClientLibrary`:

- `Models/HttpStreamResponseResult.cs` — первая sync/async попытка Body, затем обязательная попытка response; primary сохраняется через ExceptionDispatchInfo. Общий Interlocked guard делает дальнейшие sync/async вызовы no-op, включая failed first attempt; XML remarks описывают ошибки и origin.
- `Clients/HttpCleanupErrors.cs` — внутреннее сохранение immutable ordered secondary snapshot, standalone origin и sync/async cleanup. Нового public type/API/key нет.
- `Clients/HttpStreamingResponseClient.cs` — primary acquisition/error сохраняется при request/response cleanup до передачи wrapper.
- `Clients/HttpJsonResponseClient.cs` — только владеющие JSON stream и response disposal boundaries: body read/deserialize primary не заменяется async stream либо outer response cleanup. `SendNoBodyAsync` и mapping/serialization/диагностика не менялись.
- `Clients/AGENTS.md` — устойчивые ownership/error/repeat правила.
- `HttpClientLibrary.Tests/HttpStreamResponseDisposalTests.cs` — 14 новых cases: обе формы disposal, Body-only/response-only/combined, actual cached StreamContent body, repeat после success/failure, existing secondary snapshot, same-instance first-throw stack и acquisition controls.

`D:/Media/User/source/repos/agent-bridge`:

- `adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs` — explicit finally вместо await using сохраняет callback/read primary; presence cleanup списка исключает normalizing catches в JSON/SSE/compact, включая standalone caught-типы.
- `adapters/AgentBridge.CodexLb/Responses/AGENTS.md` — соответствующие ownership/error invariants.
- `tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs` — 26 новых cases, включая JSON через same public DI fixture; callback/read/JSON primary, immutable secondary, OCE default/other/caller, cleanup-only JSON/HTTP, same-object reuse и late caller cancellation. Content/stream attempted и completed наблюдаются отдельно.
- `Documentation/Plans/AgentBridge Audit Remediation/Decisions.md` — согласование пользователя06.
- `openspec/specs/agent-runtime/spec.md` — согласованное requirement ошибок HTTP disposal; старые changes не переписаны, CLI не запускалась.
- `Documentation/Technical documentation/14-responses-json-adapter.md`, `Documentation/Technical documentation/15-responses-sse-adapter.md` — текущее описание error/ownership границы; исторические test counts сохранены.
- `Documentation/Plans/AgentBridge Audit Remediation/06-http-disposal.md` — статус и этот отчёт; исходное задание выше сохранено.

Coordination, root README/slnx, EF/LB, generated/build artifacts не входят в manifest. Production core/Application/Domain не менялись.

### Различающее before и after

**Wrapper before:** local stream выбрасывает заданный primary при первой sync/async попытке. Настоящий HttpResponseMessage с наблюдаемым Dispose и настоящий StreamContent должны попытаться завершить cleanup. На обеих TFM в окончательном before **2 passed / 2 failed / 0 skipped**, Exit1, rows4: Body failure сохраняет primary/stack, но response attempts=0 вместо1. Два successful/cached-body/repeat controls проходят. Это воспроизведение пропущенного ownership cleanup, не доказательство deployment connection leak.

Первая fixture версия ошибочно требовала Same(original stream, ReadAsStreamAsync): actual StreamContent возвращает cached ReadOnlyStream. Два control failures той версии не являются дефектом библиотеки. Проверка исправлена на cached Body identity/readonly и повторена свежей сборкой. Async fixture также исправлена: exception реально бросается внутри async DisposeAsync, чтобы stack assertion проверял место throw, а не faulted ValueTask без исходного throw. Окончательный before ниже отражает только actual regression. Промежуточные пересекающиеся runs не суммируются и не объявляются independent evidence.

**Gateway before:** public AddCodexLbResponses → IModelGateway → actual HttpApiClient → fake handler/local stream. Callback/read original exception заменяется cleanup-secondary; malformed JSON primary также заменяется outer response cleanup. **3 passed / 3 failed / 0 skipped**, Exit1, rows6. Primary-only controls проходят, combined случаи падают. JSON before выявляет именно owning `ReadBodyAsync` await using и outer response using; wrapper-only fix не исправляет эту границу.

**Дополнительное различающее stack evidence:** после первой реализации усилен same-instance JSON case проверкой исходного ReadAsync frame. До захвата ExceptionDispatchInfo перед cleanup **1 passed / 1 failed / 0 skipped**, Exit1, `before-same-stack.trx`, rows2: повторный throw того же объекта сохранял identity/origin, но терял первичный read stack. Захват primary теперь происходит до cleanup на wrapper, acquisition, JSON stream/response и gateway boundaries. Два новых wrapper sync/async cases отдельно проверяют первое место throw. Это уточнение той же002, не отдельная новая находка.

**After:** полная изолированная HTTP библиотечная регрессия **58/0/0 на net8.0** и **58/0/0 на net10.0**, Exit0, rows58 каждая. Новых disposal cases14 в каждом run. Actual response/content attempts=1 после Body failure; при успешном оставшемся cleanup Completed=true. При новом secondary response/content попытка наблюдается, Completed=false; original primary/stack и secondary identity сохранены. Response-only exception исходный, empty secondary origin; repeat не выполняет новые попытки и не заявляет successful release.

JSON+SSE gateway регрессия **198 passed / 0 failed / 0 skipped**, Exit0, rows198: ResponsesJsonTests110 и ResponsesSseTests88. Последний класс включает ранее принятые62 cases04 и26 новых cases06, часть новых вызывает JSON транспорт. Сохранены sequential callbacks/await, no retry, safe diagnostics, empty/comments/DONE caller token, deadline до/после данных, partial/terminal output/envelope/continuation и explicit Failed vs late caller controls04. Новые tests подтверждают combined primary identity/stack, ordered immutable secondary, standalone cleanup OCE(default/other/caller)/JsonException/HttpRequestFailedException без ошибочной normalization, same-object origin без self-reference, late caller не подменяет IOException primary. При cleanup failure typed normalization намеренно исключена по согласованному контракту; это не успешный report и не successful release.

### Preflight, точные команды и артефакты

Проверены конкретные HTTP library/tests, AB adapter/tests и transitive core csproj, ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json до filesystem root, existing assets и package imports. HTTP Directory.Build.props задаёт existing test intermediate/output; assets тестов находятся в `HTTP/artifacts/obj/HttpClientLibrary.Tests/project.assets.json`. Custom project Exec/hooks/imports/lockfiles не обнаружены; NuGet assets используют existing cached packages и api.nuget.org source, restore/network не выполнялись. Test SDK/xUnit runner — разрешённая инфраструктура изолированных tests, не приложение. Fixtures проверены на no real HTTP/DB/native/process/hosting; новые tasks awaited без background work. EF/provider проекты в эти commands не входят; no-body implementation не менялась.

Все команды из cwd `D:/Media/User/source/repos/agent-bridge`, Debug, конкретные csproj, без solution/Rebuild/restore. Каждый no-build использует свежую успешную сборку соответствующего project/output. Окончательные before commands:

```powershell
dotnet build ../work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ -flp:logfile=../work/HttpClientLibrary/artifacts/stage06-before-final-build.log
dotnet test ../work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug -f net8.0 --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ --filter FullyQualifiedName~HttpStreamResponseDisposalTests --logger 'trx;LogFileName=before-final-net8.0.trx' --results-directory ../work/HttpClientLibrary/artifacts/stage06
dotnet test ../work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug -f net10.0 --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ --filter FullyQualifiedName~HttpStreamResponseDisposalTests --logger 'trx;LogFileName=before-final-net10.0.trx' --results-directory ../work/HttpClientLibrary/artifacts/stage06
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ -flp:logfile=artifacts/stage06-before-build.log
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ --filter 'FullyQualifiedName~PrimarySurvivesThrowingCleanup' --logger 'trx;LogFileName=before-gateway.trx' --results-directory artifacts/stage06
```

Оба before builds Exit0/warnings0/errors0; все три before tests Exit1 с ожидаемыми failures выше. Gateway before JSON assertion ожидала safe Rejected по прежнему контракту; после согласования combined case проверяет JsonException/secondary Data, primary-only Rejected control сохраняется.

Дополнительный stack before (build Exit0/warnings0/errors0, test Exit1):

```powershell
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ -flp:logfile=artifacts/stage06-same-stack-before-build.log
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ --filter FullyQualifiedName~ReusedJsonExceptionKeepsCleanupOrigin --logger 'trx;LogFileName=before-same-stack.trx' --results-directory artifacts/stage06
```

Окончательные after commands:

```powershell
dotnet build ../work/HttpClientLibrary/HttpClientLibrary.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ -flp:logfile=../work/HttpClientLibrary/artifacts/stage06-library-build.log
dotnet build ../work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ -flp:logfile=../work/HttpClientLibrary/artifacts/stage06-final-tests-build.log
dotnet test ../work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug -f net8.0 --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ --logger 'trx;LogFileName=after-final-net8.0.trx' --results-directory ../work/HttpClientLibrary/artifacts/stage06
dotnet test ../work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug -f net10.0 --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ --logger 'trx;LogFileName=after-final-net10.0.trx' --results-directory ../work/HttpClientLibrary/artifacts/stage06
dotnet build adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ -flp:logfile=artifacts/stage06-adapter-build.log
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ -flp:logfile=artifacts/stage06-final-tests-build.log
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage06/ --filter 'FullyQualifiedName~ResponsesSseTests|FullyQualifiedName~ResponsesJsonTests' --logger 'trx;LogFileName=after-final-gateway.trx' --results-directory artifacts/stage06
```

Все after commands Exit0, builds warnings0/errors0. TRX counters/result rows прочитаны напрямую, counts не складываются с пересекающимися intermediate runs. Logs/TRX/output только в игнорируемых artifacts. Относительный BaseOutputPath создаёт output в каждом referenced project. Standalone adapter/core compile выполняются через exact adapter closure, а не solution.

Final SHA256 DLL:

- HTTP net8 `artifacts/compile-check/stage06/Debug/net8.0/HttpClientLibrary.dll`: `01663FEAD406BB599D8FD50EEF8638902C9928425AEAE5E482CF3DB2574F94A7`; совпадает с копией HTTP test output.
- HTTP net10 `artifacts/compile-check/stage06/Debug/net10.0/HttpClientLibrary.dll`: `707D37EA15E73F3A0C0381FB2D4C13CE86B7C948021A5A620ED4C95A5AB33A24`; совпадает с копиями HTTP test и AB gateway test output.
- AB adapter `adapters/AgentBridge.CodexLb/artifacts/compile-check/stage06/Debug/net10.0/AgentBridge.CodexLb.dll`: `522494A55E4789FCBB4FACB32E5BA494CFA11FAC97A6EACD737ECE7EDE20AF2C`; совпадает с AB test output.
- AB tests `tests/AgentBridge.CodexLb.Tests/artifacts/compile-check/stage06/Debug/net10.0/AgentBridge.CodexLb.Tests.dll`: `67E011BF69B56FE0DEEB989410A695C59E5B24EAE9373CB331FB36E325239125`.

### Границы вывода и передача

Actual: public DI, gateway/parser/state, actual HttpClientLibrary ownership/serialization/response и реальный .NET HttpResponseMessage/StreamContent/cached ReadOnlyStream. Doubles: handler, bytes/local streams и контролируемые disposal/read exceptions; real sockets/server/connection pool не использованы. ABQA-002 теперь **подтверждено и исправлено именно в этой B-границе**. Нельзя утверждать наличие либо отсутствие deployment connection leak. Серьёзность S2 не повышена доказанным ущербом. C17/provider/native и D19/live/restart остаются открытыми; нет DB/SQL/migrations/apps/hosting/Docker/network/deployment/project scripts/CLI.

Ручные правки apply_patch, без normalization всего файла. HTTP исходный worktree имеет mixed LF/CRLF, а Git blob нормализован в LF. По реконструированному исходному raw tool-read проверены EOL неизменных уникальных строк wrapper22/stream34/JSON31, mismatch0 после исправления двух context строк wrapper; это не independent raw-byte snapshot. AB code/tests исходно LF; точечный diff сохранён. Проверяются UTF-8, U+FFFD, четыре вопросительных знака, mojibake и diff --check собственного manifest. Документальные правки после final builds не меняют code/tests DLL.

Этап готов к review. **Commit не выполнен; до явной приёмки и отдельного поручения координатора Git mutations не выполняются.**

**Приёмка06, 2026-10-06:** координатор от имени пользователя принял этап в A/B после независимой проверки final production/helper diffs, EDI до cleanup/same-instance, spec/Decisions, before/final TRX (HTTP58/58 на net8 и58/58 на net10; gateway198/198; same-stack before1/2), UTF-8 четырнадцати файлов и diff checks. Поручены два отдельных локальных Conventional Commits строго по HTTP6/AB8 manifest выше, без Coordination. Code/tests после final checks не менялись. ABQA-002 закрывается только в B ownership/error границе; S2 остаётся предварительно, deployment leak/C17/D19 не доказаны. Push не разрешён.
