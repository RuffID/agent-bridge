# 04 — Отмена после пустого SSE EOF

[Навигатор](README.md). Статус: **принят в A/B-границе**. Зависимость: 00. Находка: **ABQA-008, S3, подтверждена статически**.

## Цель и область

При caller cancellation до canonical данных распространять OCE с исходным caller token, включая успешный EOF/disposal. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), [SSE contract](<../../Technical documentation/15-responses-sse-adapter.md>) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md).

Область: [CodexLbModelGateway](../../../adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs), при необходимости ResponseSseState, [ResponsesSseTests](../../../tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs). HTTP2xx/empty body создаётся fake handler/local stream, без HTTP server.

## Работы

1. Добавить контрпример: empty body → EOF без Apply → успешный disposal отменяет caller → ожидать OCE с исходным token вместо Ok(Canceled).
2. Привести successful EOF путь к той же границе наличия canonical данных, что используется в catch OCE. Comments/keepalive/[DONE] сами по себе не должны выдавать фиктивные данные.
3. Сохранить partial output/envelope/continuation при допустимом Canceled после данных, приоритет explicit Failed над поздней отменой и deadline semantics.
4. Сохранить sequential awaited callbacks, callback exception identity и владение response. Throwing disposal из ABQA-002 проверяется отдельно в06.

## Проверки

- B: empty EOF + cancel-on-successful-disposal; empty EOF без отмены остаётся Incomplete.
- Empty stream read-OCE, comments-only stream, caller/deadline и отсутствие callbacks.
- Partial/terminal canonical data + поздняя отмена; explicit failure + поздняя отмена.
- Callback failure распространяется; response освобождён; fixture не запускает сервер или браузер.
- Compile-check конкретных CodexLb production/test проектов; адресный SSE-набор.

## Критерии завершения

До данных наблюдается исходный caller token и требуемое исключение. При наличии данных сохранены результаты, Failed priority и callback semantics. Проверка не заявляет live сетевую отмену; соответствующее evidence относится к19.

## Результаты

### Изменение и manifest

2026-10-06, Asia/Novosibirsk. Реализован только04 в разрешённой A/B-границе, приёмка ожидается. HEAD на входе `d15a4d0b1bcdaf0d8bf29885051a0ba5f1a01902`; зависимость00 принята, результаты01–03 и ограничения03 прочитаны. Основание: весь04, README/Decisions, root/local AGENTS, csharp-project-rules с references style/build/agents-maintenance, Findings/итог15 аудита и текущая main spec. Исходное задание выше сохранено; исторические отчёты и принятые решения не изменены.

Собственный exact manifest только в `D:/Media/User/source/repos/agent-bridge`:

- `adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs` — после успешного EOF/disposal при отменённом caller и отсутствии HasData выбрасывает caller OCE; существующий catch распространяет OCE с исходным caller token. Explicit Failed проверяется раньше; report с данными по-прежнему Canceled.
- `adapters/AgentBridge.CodexLb/Responses/AGENTS.md` — фиксирует тот же устойчивый инвариант EOF/disposal и отсутствие canonical data у comments/keepalive/DONE.
- `tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs` — различающие empty/keepalive-DONE disposal cases; comments-only/no-cancel и caller/deadline controls без callbacks; partial/terminal cancellation сохраняет точные output/envelope/continuation.
- `Documentation/Plans/AgentBridge Audit Remediation/04-empty-sse-cancellation.md` — статус и результаты04.

`ResponseSseState` менять не потребовалось. Нет public API/schema changes. Другие repo: **нет собственного manifest**. HttpClientLibrary HEAD `6d0528d940d1d8494c722c22464051dd961d6bf7`, исходники не изменены; EFCoreLibrary read-only. Coordination исходно изменён координатором и исключён; codex-lb сохраняет чужую untracked `.vs/`. Git mutations не выполнялись.

### Различающее before/after evidence

До production fix добавлен public `AddCodexLbResponses` → `IModelGateway` → actual `HttpApiClient`/HttpClientLibrary → fake handler/local fragmented stream тест `EmptyEofCancellationOnSuccessfulDisposalUsesCallerToken`. HTTP2xx, успешный EOF, successful stream disposal вызывает caller.Cancel. Оба case (пустое тело; keepalive/id/retry/DONE) фактически упали: `Assert.ThrowsAny() Failure: No exception was thrown`, **0 passed/2 failed**, Exit1, `before.trx`, rows2. Исходный successful путь возвращал Ok(Canceled).

После точечной правки свежий SSE suite: **62 passed/0 failed/0 skipped**, Exit0, `after.trx`, rows62. В него входят оба различающих case, empty/no-cancel Incomplete, empty и comments-only read cancellation с исходным caller token, deadline до/после данных, zero callbacks до данных, partial/terminal output/envelope/continuation при late caller cancellation, explicit Failed priority, sequential awaited callbacks, callback exception identity и request/response/content/stream disposal. Новые positive partial/terminal cases сохраняют function arguments, русский text, envelope id и bound previous_response_id/x-codex-turn-state. Existing Failed и callback controls повторно исполнены тем же адресным suite. Пересекающиеся before/after результаты не суммируются.

### Preflight, команды и артефакты

Проверены exact test/adapter/core/HTTP csproj, ProjectReference closure и исключения bin/obj/artifacts. Ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json просмотрены до filesystem root; найден только HTTP Directory.Build.props с output-настройкой исключительно HTTP test project (не входит в closure). Custom targets/Exec/import hooks, lockfiles и custom NuGet sources в этой цепочке не обнаружены. Existing project.assets.json доступны для всех четырёх проектов; restore/network не выполнялись. SSE source проверен: HTTP только fake handler, streams локальные; нет БД/process/native/Docker/hosting. Тестовые waiting tasks отменяются и await в finally, scope/HttpClient освобождаются fixture.

Все команды из cwd `D:/Media/User/source/repos/agent-bridge`, Debug, конкретные проекты, без solution/Rebuild. Каждому no-build предшествовала успешная свежая сборка того же test project/output:

```powershell
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage04/ -flp:logfile=artifacts/stage04-before-build.log
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage04/ --filter FullyQualifiedName~ResponsesSseTests.EmptyEofCancellationOnSuccessfulDisposalUsesCallerToken --logger 'trx;LogFileName=before.trx' --results-directory artifacts/stage04
dotnet build adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage04/ -flp:logfile=artifacts/stage04-adapter-build.log
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage04/ -flp:logfile=artifacts/stage04-after-build.log
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage04/ --filter FullyQualifiedName~ResponsesSseTests --logger 'trx;LogFileName=after.trx' --results-directory artifacts/stage04
```

Все три build Exit0, warnings0/errors0. Первый test Exit1 (ожидаемый before), второй Exit0. TRX Counters и result row counts прочитаны напрямую. Outputs только в игнорируемых artifacts; относительный BaseOutputPath создаёт отдельную папку каждого project, включая transitive HTTP build без изменения его исходников. Before binaries затем обновлены after builds; before evidence — отдельный log/TRX, не final hash.

Final SHA256:

- `adapters/AgentBridge.CodexLb/artifacts/compile-check/stage04/Debug/net10.0/AgentBridge.CodexLb.dll`: `80BE176192DF3EAE76BB3C83DAFC0E48D3B5E1D5D659176C480219438DA4CC07`.
- `tests/AgentBridge.CodexLb.Tests/artifacts/compile-check/stage04/Debug/net10.0/AgentBridge.CodexLb.Tests.dll`: `B175EAC8BB969F8E6B1FE3A3C299CFD89EE854892C2184100457D2BAC4CFF9B1`.

### Граница передачи

Actual: публичная DI/adapter registration, gateway, SSE reader/state, actual HttpClientLibrary serialization/wrapper/disposal и canonical DTO. Doubles: HttpMessageHandler, HTTP2xx payload и fragmented stream с cancel-on-successful-disposal/blocked read. Это локальное B evidence; live сетевой cancellation/account routing/endpoint и restart не доказаны. Throwing disposal/ABQA-002 остаётся в06; HTTP library не расширялась. Maintenance и schema не затронуты. Полный adapter/core/EF suite не запускался: область04 проверена адресно.

Ручные правки через apply_patch; encoding/whitespace и собственный diff проверены. Нет apps/hosting/real HTTP/БД/SQL/Docker/native/process business operations/project scripts/CLI/deployment. C17/real providers и D19/live/restart открыты. Локальных блокеров A/B нет. Этап готов к review; **до явной приёмки и поручения координатора commit не выполняется**.

**Приёмка04, 2026-10-06:** координатор от имени пользователя принял этап в A/B после независимой проверки production/test/local AGENTS diffs, полного отчёта и сохранности исходного задания, UTF-8/LF четырёх файлов, diff --check, actual before0/2 и after62/62/rows62, свежих сборок и точных команд, ownership/Failed/callback controls. Поручен отдельный локальный fix-коммит только четырёх файлов manifest; Coordination исключён. Code/tests после fresh after не менялись. Live19 и throwing disposal06 остаются открытыми; push не разрешён.
