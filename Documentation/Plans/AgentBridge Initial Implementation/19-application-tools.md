# 19 — Выполнять разрешённые инструменты приложения

Статус: **Реализован, адресно проверен и принят координатором; локальный коммит утверждённого manifest36 разрешён**. Зависимости: **07, 14, 16**. CLI validation не выполнена, change не архивирован.

## Цель

Связать вызовы функций модели с зарегистрированными бизнес-обработчиками, явно определив владельца и ограничения выполнения.

## Задачи

- [x] Регистрировать описания инструментов, схемы аргументов и обработчики приложения.
- [x] Проверять идентификатор вызова, аргументы и разрешения приложения перед выполнением.
- [x] Формировать matching canonical результаты, совместимые с existing IDialogTurnWriter и последующим actual ContextBuilder; запись координирует приложение/будущий20.
- [x] Ограничить число шагов, calls, общий cooperative срок и параллельность; каждому handler/validator выделять отдельный DI scope.
- [x] Сохранять информацию об ошибке инструмента без подмены успешным результатом.
- [x] Запретить автоматические retries/повтор attempted step и дальнейшие действия живой session после uncertain interruption. По прямому решению пользователя durable recovery относится к20.

## Проверка и завершение

Проверить сценарий диалога с GetOrderStatus, запрещённые и неизвестные инструменты, некорректные аргументы и частичный сбой. Этап завершён, когда последующий вопрос использует предыдущий контекст инструмента без раскрытия данных другого пользователя.

Источник: [сценарий диалога и инструментов](<../../Business logic/02-dialogs-and-tools.md>).

## Результат и согласованная граница

2026-10-04 реализован только19 в существующем agent-bridge, без worktree/подагентов/других этапов. Исходные master/tree/index: HEAD18=73045a151b6b48accd0fece98f75858c5336a16c, дерево и индекс чистые. [API и пример](<../../Technical documentation/19-application-tools.md>), [change](../../../openspec/changes/application-tools/proposal.md), [нормативная delta](../../../openspec/changes/application-tools/specs/agent-runtime/spec.md). Change создан до кода; main содержит те же новые requirements. CLI отсутствует, установка/validation/archive не выполнялись.

Public AddAgentBridgeTool/Tools, IToolRegistry, IToolExecutor, обязательный IToolInvocationValidator, отдельные scopes, immutable session/identity/limits/batch/results реализованы. Complete canonical object arguments и ModelResponse.Completed проверяются до действия; app validator отвечает за **всю schema** и текущие права. Unknown/not-selected/forbidden/schema failure дают явный безопасный error output. Подтверждённые handler Fail сохраняются ошибкой; Timeout/exception/cancellation после начала — Unknown без output. LastResult сохраняет частичный отчёт после exceptions. Raw errors не копируются, результатов других владельцев нет.

Повторные call_id допустимы, включая call(x), call(x), output(x), output(x); закрытые пары не запускаются. Попытку определяют owner/dialog/incarnation/turn/agent + StoredModelStep.StepId + исходный output index. Повтор attempted step/concurrent Execute одной session запрещён. MaxSteps считает модельные tool steps, calls/concurrency bounded, время общее monotonic/cooperative, fixed UTC expiry не продлевается. Все workers/scopes awaited, в том числе при partial parallel failure и при handler, игнорирующем отмену. Late success сохраняется; primary+Dispose failure сохраняются вместе.

Прямой ответ пользователя: **«Да, durable recovery в этапе 20»**. В19 предоставлен optional awaited IToolExecutionCheckpoint.BeforeExecuteAsync после validator и до handler. Failure/exception/cancel/unknown save outcome не разрешают action/retry; callbacks могут выполняться параллельно. Перезапуск/новая session/null checkpoint не защищены памятью19. Durable журнал/AgentRunner20 не реализованы.

## Проверенные зависимости и build boundaries

Обязательные пути доступны: EFCoreLibrary (актуальный csproj Version0.0.5) и HttpClientLibrary (FileVersion0.0.0.5); перед чтением прочитаны их AGENTS. Read-only source paths codex-lb, TelegramCodexRelayBot и AquaByteLedger.Infrastructure/Services/DataBase существуют; их код для19 не понадобился. Соседние исходники не изменялись. Новых production dependencies нет, storage/transport/generated migrations не изменялись.

Root csproj/slnx сохранены; None Remove Documentation остаётся прежним, в slnx добавлен только новый technical document solution item. Перед build проверены конкретные root/test csproj, ancestor Directory.Build/Packages/NuGet/lock и существующие NuGet imports. Custom executable hooks не обнаружены. Restore не понадобился: зависимости не изменились, assets уже существовали. Каждый build/test использует GeneratePackageOnBuild=false, Build, не Rebuild/pack/publish. Команды ниже выполнялись с workdir D:\Media\User\source\repos\agent-bridge.

## Точные команды и результаты

```powershell
Get-Command openspec -ErrorAction SilentlyContinue
git status --short
git rev-parse HEAD
dotnet build agent-bridge.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
```

Root build: успех, 0 warnings/errors. Первый test build: **CS1503**, неправильный порядок аргументов DialogSnapshot в новом fixture. Исправлен fixture на actual token-first API. Следующий build: 0 errors, **CS0649** для ещё не использованного fixture DisposeError; после добавления intended cleanup-failure cases warning исчез. Последний test/root build: **0 warnings, 0 errors**. Production compile ошибок не было.

```powershell
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter FullyQualifiedName~ToolExecutorTests --logger 'trx;LogFileName=tools-first.trx' --results-directory artifacts\test-results\stage19 --verbosity minimal
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter FullyQualifiedName~ToolExecutorTests --logger 'trx;LogFileName=tools-review.trx' --results-directory artifacts\test-results\stage19 --verbosity minimal
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~ToolExecutorTests|FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ApplicationPortsTests|FullyQualifiedName~ContextTokenCounterTests|FullyQualifiedName~ContextBudgetGuardTests' --logger 'trx;LogFileName=tools-affected.trx' --results-directory artifacts\test-results\stage19 --verbosity minimal
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~ToolExecutorTests|FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ApplicationPortsTests|FullyQualifiedName~ContextTokenCounterTests|FullyQualifiedName~ContextBudgetGuardTests' --logger 'trx;LogFileName=tools-final.trx' --results-directory artifacts\test-results\stage19 --verbosity minimal
```

| TRX | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| tools-first.trx | 27 | 0 | 0 |
| tools-review.trx | 33 | 0 | 0 |
| tools-affected.trx | 196 | 0 | 0 |
| **tools-final.trx** | **199** | **0** | **0** |

Final TRX: **42 новых tools cases**, 59 ContextBuilder, 27 ApplicationPorts, 51 actual offline tokenizer, 20 budget guard. Повторные runs/исторические этапы не суммируются как unique. Сквозная isolated проверка использует actual public DI/executor/ContextBuilder; следующий вопрос сохраняет GetOrderStatus context и не доступен чужому owner. Checkpoint tests проверяют awaited ordering, refusal/exception/cancel и отсутствие action/retry. Parallel tests проверяют разные scoped states, bound2, awaited neighbor после primary failure. ScopedState и checkpoint — doubles, не реальные DbContext/atomic durable journal.

```powershell
git diff --check
git diff --cached --stat
git diff --stat
git diff --name-only
git status --short --untracked-files=all
git rev-parse --abbrev-ref HEAD
```

Ordinary diff check успешен, index пуст; master/HEAD18 сохранены. Git предупреждает о возможном LF→CRLF при будущем checkout из-за существующих настроек; текущие файлы UTF-8 без BOM/LF, исходные EOL сохранены, Git config не изменялась. Проверка main/delta — статическая, не CLI validation. UTF-8/mojibake/U+FFFD/четыре вопросительных знака и manifest проверены перед передачей. До приёмки add/commit не выполнялись; remote операции и branch changes запрещены.

Первичный encoding audit дал ложное срабатывание: case-insensitive PowerShell -match принял часть нормального русского слова «распространяется» за pattern mojibake. Исправлена только проверка на -cmatch; strict UTF-8 decode и итоговый audit всех36 файлов успешны, replacement/BOM/CRLF/четырёх вопросительных знаков нет. Solution XML разобран, новые/report links проверены, main содержит точную delta; это статические проверки.

## Пропуски и ограничения

- **Пропущено по указанию пользователя:** live HTTP/codex-lb/OpenAI/upstream/accounts/tokens, Telegram, application/demo, hosting/local HTTP server/TestServer/WebApplicationFactory, произвольные project/user scripts, deployments/публикации/uploads/remote Git.
- Persistence/БД на19 не менялись, новых persistence risks нет: реальные SQLite/PostgreSQL integrations не повторялись. Исторические39 DB cases были до EFCoreLibrary0.0.5; результаты19 не подтверждают её текущую real DB работу или SQL Server/MySQL. Fake scoped state/checkpoint не доказывают real persistence atomicity/restart safety.
- OpenSpec CLI не найден. CLI validation не выполнена; статическое совпадение main/delta не считается её заменой, change не архивирован.
- Нет AgentRunner20, settings21, cleanup22, cross-component23 или24–25. Общий tool/model loop, реальный GetOrderStatus приложения, durable запись/restore после restart и внешняя совместимость не проверены.
- Cooperative timeout не гарантирует жёсткое прерывание произвольного бизнес-обработчика. Caller обязан передавать всю схему/права validator и соблюдать cancellation в handler; outputs/args не предназначены для логов.

## Полный manifest перед приёмкой

Координатор принял этап19 2026-10-04 в согласованной границе durable recovery20. Проверены production/docs, исправления по ревью, final TRX199/0/0, HEAD18 и пустой index. Разрешён локальный English Conventional Commit ровно36 перечисленных файлов после проверки ordinary/staged diff. После приёмки меняются только эти acceptance notes/status; код, тесты и scope не расширяются. Ограничения CLI/real DB, cooperative cancellation и передача20 сохранены. Собственный hash возвращается отдельным отчётом после commit, в этот commit не записывается. Этап20 не начинать без отдельного поручения.

Всего36 файлов: 15 изменённых и21 новых (включая все untracked). Исходных чужих изменений не было; только agent-bridge. Секреты, generated migrations, bin/obj/artifacts/TRX и соседние проекты не входят в manifest; stage/index пуст.

- `AGENTS.md` — изменён
- `Application/AGENTS.md` — изменён
- `Application/Ports/IToolHandler.cs` — изменён
- `Configuration/AGENTS.md` — изменён
- `Documentation/Business logic/02-dialogs-and-tools.md` — изменён
- `Documentation/Plans/AgentBridge Initial Implementation/19-application-tools.md` — изменён
- `Documentation/Plans/AgentBridge Initial Implementation/README.md` — изменён
- `Documentation/README.md` — изменён
- `Documentation/Technical documentation/09-application-ports.md` — изменён
- `Documentation/Technical documentation/README.md` — изменён
- `README.md` — изменён
- `agent-bridge.slnx` — изменён
- `openspec/specs/agent-runtime/context.md` — изменён
- `openspec/specs/agent-runtime/spec.md` — изменён
- `tests/AGENTS.md` — изменён
- `Application/Models/ToolExecutionBatch.cs` — новый (untracked)
- `Application/Models/ToolExecutionIdentity.cs` — новый (untracked)
- `Application/Models/ToolExecutionLimits.cs` — новый (untracked)
- `Application/Models/ToolExecutionResult.cs` — новый (untracked)
- `Application/Models/ToolExecutionSession.cs` — новый (untracked)
- `Application/Models/ToolExecutionStatus.cs` — новый (untracked)
- `Application/Models/ToolRegistration.cs` — новый (untracked)
- `Application/Ports/IToolExecutionCheckpoint.cs` — новый (untracked)
- `Application/Ports/IToolExecutor.cs` — новый (untracked)
- `Application/Ports/IToolHandlerScope.cs` — новый (untracked)
- `Application/Ports/IToolInvocationValidator.cs` — новый (untracked)
- `Application/Ports/IToolRegistry.cs` — новый (untracked)
- `Application/ToolExecutor.cs` — новый (untracked)
- `Application/ToolRegistry.cs` — новый (untracked)
- `Configuration/AgentBridgeToolsExtensions.cs` — новый (untracked)
- `Documentation/Technical documentation/19-application-tools.md` — новый (untracked)
- `openspec/changes/application-tools/context.md` — новый (untracked)
- `openspec/changes/application-tools/proposal.md` — новый (untracked)
- `openspec/changes/application-tools/specs/agent-runtime/spec.md` — новый (untracked)
- `openspec/changes/application-tools/tasks.md` — новый (untracked)
- `tests/AgentBridge.Tests/ToolExecutorTests.cs` — новый (untracked)

## Передача этапу20

Только после приёмки19 и отдельного поручения. Реализовать durable журнал start/finish по identity19; checkpoint success до handler, unknown save outcome и восстановленный incomplete start блокируют повтор. Сериализовать короткие writes/version token updates одного диалога даже при параллельных handlers, выделять scope/UoW на callback. Завершать transaction до external action, не делить Async/DbContext между tasks, не обновлять token ради принятия stale результата и не делать retries. Сохранять full model step/calls и confirmed outputs через existing short writer; LastResult после exception/cancel должен участвовать в честной finalization. Unknown остаётся без выдуманного function_call_output и блокирует следующий context; не удалять историю и не переисполнять action автоматически. Full generation guard после любого compact/tool outcome обязателен. Собственные ресурсы19 — только ignored compile/TRX; контейнеров/БД/volumes/native test processes не создавалось.
