# 03 — Cleanup scope агента и caller cancellation

[Навигатор](README.md). Статус: **принят в A/B-границе**. Зависимость: 00. Находка: **ABQA-009, S3, подтверждена статически**.

## Цель и область

Не превращать неожиданный OperationCanceledException из DisposeAsync scope в штатный Canceled только потому, что caller уже отменён. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), запрет маскировать unexpected exceptions.

Область: [AgentRunScope](../../../Application/AgentRunScope.cs), [AgentRunSession](../../../Application/AgentRunSession.cs), [AgentRunner](../../../Application/AgentRunner.cs), адресные AgentRunnerTests. Не менять durable schema, SQL, checkpoints или бизнес-обработчики.

## Работы

1. Воспроизвести successful Append → принятие step/token → caller canceled → cleanup-only OCE(default/другой token). Зафиксировать текущее исчезновение исключения.
2. Различать происхождение operation cancellation и cleanup failure на владеющей scope границе. Проверки только caller flag или только равенства token недостаточно: cleanup может выдать OCE с тем же caller token.
3. Сохранить подтверждённые token/step и blocking дальнейших writes. Ошибка cleanup должна оставаться наблюдаемой после honest finalization; не делать повтор Append, terminal write при blocked session или повтор handler.
4. Сохранить действующие primary-only и primary+cleanup пути, exception identity/stack в пределах текущего контракта. Не оборачивать все ожидаемые отказы в общий successful result.
5. Сверить аналогичную границу ExpiredDialogCleanup как отрицательный контроль; не переносить на неё исправление без собственного дефекта.

## Проверки

B через публичный runner/DI с fake write ports и управляемым scope disposal:

- cleanup-only OCE с default, другим и тем же caller token при canceled caller;
- caller не отменён; cleanup IOException; primary+cleanup aggregate;
- обычная caller cancellation при успешном cleanup;
- принятые step/token сохранены, дополнительных write/handler/model calls нет;
- существующий контроль ExpiredDialogCleanup.DisposeCancellationIsAnUnexpectedCleanupFailure.

Compile-check ядра и `tests/AgentBridge.Tests/AgentBridge.Tests.csproj`; restart реального процесса относится к19, а не к этим doubles.

## Критерии завершения

Cleanup-origin exception не скрыт ни в одном токенном варианте; штатная отмена остаётся штатной. Подтверждённая запись не теряется, blocked состояние и no-replay сохранены. Фактические тестовые результаты записаны отдельно от будущего C/D.

## Результаты

### Изменение и manifest

2026-10-06, Asia/Novosibirsk. Поручение координатора от имени пользователя ограничено03; HEAD на входе `b3844884cc9bfd09bdda86eddeb5b29feec42dd9`. Прочитаны root/local AGENTS, csharp-project-rules и references style/build/agents-maintenance, весь03, README/Decisions, принятые результаты00–02, Findings/итог15 и текущая main spec. Принятые решения не переоткрывались; исходное задание выше сохранено. Исторические отчёты не изменены.

Собственный exact manifest только в `D:/Media/User/source/repos/agent-bridge`:

- `Application/AgentRunScope.cs` — сообщает cleanup-only origin внутренним callback на владеющей DisposeAsync границе; original exception распространяется через throw, primary+cleanup остаётся AggregateException.
- `Application/AgentRunSession.cs` — передаёт callback в короткие write scopes. Accepted token/step по-прежнему захватываются до disposal; catch блокирует дальнейшие writes.
- `Application/AgentRunner.cs` — read/write cleanup origin фиксируется отдельно для каждого run. Caller flag без origin больше не определяет штатную отмену. Если actual executor нормализовал cleanup OCE в caller OCE, runner распространяет исходную cleanup OCE с identity/stack после honest finalization. Aggregate не подменяется.
- `Application/AGENTS.md` — устойчивый инвариант origin/identity/accepted state/no replay.
- `tests/AgentBridge.Tests/AgentRunnerTests.cs` — адресные public DI регрессии, controls и наблюдатель public LastResult с делегированием actual ToolExecutor; без reflection, friend assemblies и нового production public API.
- `Documentation/Plans/AgentBridge Audit Remediation/03-runner-cleanup-cancellation.md` — статус и результаты03.

Другие repo: **нет собственного manifest**. Coordination принадлежит координатору, исходно изменён и исключён. Read-only status в конце: EFCoreLibrary/HttpClientLibrary чистые, codex-lb сохраняет чужую untracked `.vs/`. Не выполнялись add/commit/push и прочие Git mutations. Durable schema/checkpoint protocol/SQL/business handlers, generated source, csproj, spec и ExpiredDialogCleanup не изменены.

### Различающие результаты

| Run / TRX в `artifacts/stage03/` | Exit | Passed / Failed / Skipped | Вывод |
| --- | --- | --- | --- |
| `before.trx` | 1 | 10 / 4 / 0 | Три cleanup OCE скрыты; четвёртое падение — неверное ожидание количества Dispose в positive test |
| `before-v2.trx` | 1 | 11 / 3 / 0 | До production fix скрыты ровно default/other/same caller OCE при canceled caller |
| `origin.trx` | 1 | 63 / 3 / 0 | Первая правка уже исправляет Append/read, но actual executor нормализует checkpoint OCE; три checkpoint cases различают этот путь |
| `final.trx` | 0 | 66 / 0 / 0 | После сохранения original checkpoint OCE все адресные tests проходят |
| `final-v2.trx` | 0 | 66 / 0 / 0 | Финальный source с дополнительными public LastResult assertions, fresh build |

Before-v2 через public runner/DI/fake scoped write ports: successful Begin → generation → Append → runner принимает returned token/step → при Dispose callback отменяет caller → Dispose выбрасывает OCE. В трёх canceled caller cases returned result проверен как Canceled/TerminalSaved=false/Error=null с accepted token revision2/step/response; затем assertion original exception identity падает на Actual=null. Caller not canceled (все три токена), IOException, operation-only cancellation, primary+cleanup и отрицательный ExpiredDialogCleanup control проходят до fix. Во всех случаях отсутствуют повтор Append/handler/model/terminal writes после failed cleanup. Before assertions проверяют accepted state/no replay до различающего exception assertion.

Первый build тестов Exit1/CS0122 из-за nameof private Store.Write; заменён на строку имени в stack assertion. После failed build stale DLL не запускалась. В первом before run positive fixture имел ожидание Dispose1; actual DI отслеживает и Store, и alias write-port и вызывает успешный Dispose2. Исправлено только ожидание с комментарием; throwing Dispose прерывает disposal на первом вызове. Before-v2 повторно построен при неизменённом production. Никакая ошибка fixture не объявлена дефектом продукта.

Final-v2 включает Append cleanup OCE для default/other/caller × canceled/not canceled; IOException × caller flag; checkpoint cleanup OCE для трёх токенов при canceled caller; read cleanup с тем же caller token; ordinary caller cancellation при successful cleanup; operation-only OCE и primary+cleanup OCE aggregate. При accepted Append revision2, один step с original response, InProgress, events только begin/append, model1/handler0/read1. На successful cleanup caller cancel public result содержит revision3 и accepted step/LastResponse, TerminalSaved=true/Turn.Canceled и ровно одну finish запись. На primary-only rejected Append revision1/steps0, blocked finalization без finish; aggregate сохраняет original primary/cleanup и оба source stack.

Checkpoint case проходит actual ToolExecutor: accepted revision3/step/Started journal сохранены в fake store, handler0, events только begin/append/start, ни outcomes write, ни finish не вызываются. Наблюдатель только делегирует public IToolExecutor и сохраняет returned public ToolExecutionSession; его LastResult содержит matching StepId/NotStarted/no output/CanContinue=false после awaited executor. Original cleanup identity/stack сохраняется даже при промежуточной нормализации отмены. Сам ToolExecutor не изменён.

Existing AgentRunner controls проверяют full loop, providers/access/settings once, positive Completed, compact accepted tokens, late terminal cancellation, partial/unknown/journal, LastResult предыдущего шага/no replay и primary+save+scope aggregates. Весь ExpiredDialogCleanupTests — отрицательный контроль общего helper, включая DisposeCancellationIsAnUnexpectedCleanupFailure с тем же caller token. Counts пересекающихся запусков не суммируются.

### Preflight, команды и артефакты

Cwd всех команд: `D:/Media/User/source/repos/agent-bridge`. `dotnet --version` Exit0: `10.0.401`. Проверены конкретные core/test csproj, единственный ProjectReference tests→core, DefaultItemExcludes, ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json/lock до D:/, existing net10.0 assets и generated NuGet imports. Source custom Exec/Target hooks в цепочке не найдены; package versions/config не менялись. Assets указывают actual NuGet config `D:/Media/User/AppData/NuGet/NuGet.Config` и два standard Visual Studio configs: nuget.org/offline source; Telegram source disabled. Первая попытка чтения предполагаемого AppData/Roaming NuGet path была неуспешна; actual path найден по assets и прочитан. Restore/network/install не выполнялись. Это не полный аудит installed SDK/package hooks.

Прочитаны адресные tests/doubles, test project не содержит real HTTP/DB/native/process traits; выбранные классы не запускают hosting/сеть/БД/SQL/Docker. Ordinary dotnet build/test разрешены skill/tests AGENTS после preflight. Фильтр BEFORE ниже выбирает14 случаев; FINAL выбирает весь AgentRunnerTests и ExpiredDialogCleanupTests (66).

Точные build команды по порядку, все Exit0/warnings0/errors0, кроме явно указанной первой:

```powershell
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-before-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-before-v2-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-before-v3-build.log
dotnet build agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-core-after-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-after-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-origin-build.log
dotnet build agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-core-final-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-final-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ -flp:logfile=artifacts/stage03-final-v2-build.log
```

Точные test команды; каждому --no-build предшествовала успешная свежая сборка того же project/config/output:

```powershell
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ --filter 'FullyQualifiedName~AgentRunnerTests.AppendCleanupCancellationRemainsUnexpected|FullyQualifiedName~AgentRunnerTests.AppendSuccessfulCleanupKeepsOrdinaryCallerCancellation|FullyQualifiedName~AgentRunnerTests.AppendOperationCancellationPreservesPrimaryAndCleanup|FullyQualifiedName~AgentRunnerTests.SuccessfulWriteThenScopeDisposeFailureBlocksFurtherWrites|FullyQualifiedName~AgentRunnerTests.ScopeCleanupPreservesPrimaryReadOrWriteFailure|FullyQualifiedName~ExpiredDialogCleanupTests.DisposeCancellationIsAnUnexpectedCleanupFailure' --logger 'trx;LogFileName=before.trx' --results-directory artifacts/stage03 --diag artifacts/stage03-before-test.log
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ --filter 'FullyQualifiedName~AgentRunnerTests.AppendCleanupCancellationRemainsUnexpected|FullyQualifiedName~AgentRunnerTests.AppendSuccessfulCleanupKeepsOrdinaryCallerCancellation|FullyQualifiedName~AgentRunnerTests.AppendOperationCancellationPreservesPrimaryAndCleanup|FullyQualifiedName~AgentRunnerTests.SuccessfulWriteThenScopeDisposeFailureBlocksFurtherWrites|FullyQualifiedName~AgentRunnerTests.ScopeCleanupPreservesPrimaryReadOrWriteFailure|FullyQualifiedName~ExpiredDialogCleanupTests.DisposeCancellationIsAnUnexpectedCleanupFailure' --logger 'trx;LogFileName=before-v2.trx' --results-directory artifacts/stage03 --diag artifacts/stage03-before-v2-test.log
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ --filter 'FullyQualifiedName~AgentRunnerTests|FullyQualifiedName~ExpiredDialogCleanupTests' --logger 'trx;LogFileName=origin.trx' --results-directory artifacts/stage03 --diag artifacts/stage03-origin-test.log
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ --filter 'FullyQualifiedName~AgentRunnerTests|FullyQualifiedName~ExpiredDialogCleanupTests' --logger 'trx;LogFileName=final.trx' --results-directory artifacts/stage03 --diag artifacts/stage03-final-test.log
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage03/ --filter 'FullyQualifiedName~AgentRunnerTests|FullyQualifiedName~ExpiredDialogCleanupTests' --logger 'trx;LogFileName=final-v2.trx' --results-directory artifacts/stage03 --diag artifacts/stage03-final-v2-test.log
```

Артефакты только в игнорируемых artifacts. Relative BaseOutputPath создаёт отдельную папку в каждом project directory. Final SHA256 (Get-FileHash Exit0):

| Файл относительно cwd | SHA256 |
| --- | --- |
| `artifacts/compile-check/stage03/Debug/net10.0/AgentBridge.dll` | `88615FB2F3E607C46EE6B3CFA7E6337BAB6D2A281D0329E6A8B1F3DBE2694A0F` |
| `tests/AgentBridge.Tests/artifacts/compile-check/stage03/Debug/net10.0/AgentBridge.Tests.dll` | `DD24BEC068A2D4D0A6A69A68FD54E3D6EE8C14902C13EB5F5AB8072B76D3BCE2` |

Before output затем обновлён after builds; build logs/TRX сохранены раздельно, final hash не обозначает before binary. TRX Counters прочитаны непосредственно. Ручные изменения через apply_patch; UTF-8 без BOM/LF сохранены, strict UTF-8/U+FFFD/четыре question marks/mojibake и git diff --check проверены без находок.

### Граница передачи

Actual: public DI scopes и disposal, AgentRunner/Session/Scope, builder/compactor/guard/ToolExecutor и его public LastResult. Doubles: storage/gateway/counter/context/handler. Throwing run не возвращает AgentRunResult и не имеет public LastResult runner: accepted state наблюдается в fake write acknowledgements и no-further-write events; доступная public result projection проверяется отдельно на positive cancellation control. Новый API ради теста не введён. Это B lifecycle/origin evidence, не real SQL atomicity, provider disposal, OS restart или external exactly-once.

Полный core suite и EF/HTTP suites не запускались: область03 проверена адресно. Maintenance первопричины01/02 не изменены; их accepted evidence не выдается за свежий прогон03. C17/real provider/process/native и D19/restart/live остаются открыты. Запуски приложений/hosting/БД/HTTP/SQL/Docker/native business operations/project scripts/CLI/deployment отсутствуют. Блокеров локальной A/B-приёмки нет. Этап готов к review; до явной приёмки/поручения координатора commit не выполняется.

**Приёмка03, 2026-10-06:** координатор от имени пользователя принял этап в A/B после проверки source/test diffs, callback ownership/checkpoint normalization, public LastResult observer, исходного задания, UTF-8/LF шести manifest файлов, diff --check, before-v2 actual14:11/3 и final-v2 actual66/66/rows66, fresh concrete builds и точных команд. Поручен отдельный локальный fix-коммит только шести файлов manifest; Coordination исключён. Code/tests после final-v2 не менялись. C/D ограничения сохраняются; push не разрешён.
