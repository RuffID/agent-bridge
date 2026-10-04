# 15 — Реализовать потоковую передачу Responses через SSE

Статус: **Реализован, адресно проверен и принят координатором; локальный коммит разрешён**. Зависимость **14** принята: исходный HEAD `d8a673fff514a93d2d26d7b986e3a36e018e290c`, master. Этапы16–25 не начаты; checkpoint18 не достигнут.

## Цель

Передавать результат по мере поступления, не принимая частичный текст за успешное завершение хода агента.

## Задачи

- [x] Читать SSE через обёртку потокового ответа HttpClientLibrary.
- [x] Разбирать многострочные данные событий, входные данные, поступающие фрагментами, и необходимые события жизненного цикла.
- [x] Различать успешное завершение, ошибку, неполный результат и отмену.
- [x] Сохранять состояние инструментов и непрозрачных выходных элементов вместе с текстовыми приращениями.
- [x] Считать EOF без подтверждённого завершения незавершённым результатом.
- [x] Освобождать потоки и обёртки ответов при любом способе выхода.

## Проверка и завершение

Проверить искусственные потоки с корректным завершением, ошибкой, преждевременным EOF, результатом только от инструментов и отменой. Этап завершён, когда вызывающая сторона получает явное итоговое состояние, а частичный результат не сохраняется как успешный.

Источник: [требования к потоковой передаче](<../../Technical documentation/03-http-and-codex-lb.md>).

## Фактическая реализация

Прочитаны применимые AGENTS, C# style/build/agents и DI/service-result/OpenSpec context skills, main spec/context, бизнес/техничка, текущий план и отчёты зависимостей04/07/13/14. EFCoreLibrary0.0.5/HttpClientLibrary0.0.0.5 и обязательные read-only source paths доступны. До чтения HttpClientLibrary/codex-lb прочитаны их инструкции; actual stream wrapper/client и текущие codex-lb SSE collectors сверены. Telegram/AquaByte код не понадобился и не анализировался. Соседи не изменялись.

OpenSpec change responses-sse-adapter создан до production coding. Public AddCodexLbResponses → resolver/IModelGateway → actual HttpApiClient.SendStreamAsync → HttpStreamResponseResult. Callback выбирает stream=true/store=false; null оставляет JSON14. Exact model/effort/parameters/input/tools и continuation rules14 сохранены, API не расширялся. [Фактический SSE API и пример](<../../Technical documentation/15-responses-sse-adapter.md>).

SseEventReader читает строгий UTF-8, optional начальный BOM, fragmented bytes/CR/LF/CRLF/comments/multiline data. Незакрытый frame на EOF не dispatch, [DONE] не completion. ResponseSseState сохраняет indexed added/done items и partial text/refusal/function arguments/reasoning summary; unknown/opaque поля полных canonical объектов не отбрасываются. Непустой terminal output авторитетен и полностью заменяет collected items; absent/empty допускает backfill. Raw envelope не переписывается локальной сборкой.

Completed требует terminal response.completed, completed status, output либо collected items и отсутствия explicit error. Failed/error/incomplete/EOF сохраняют output/envelope/continuation; server cancelled без caller confirmation не Canceled. Caller до данных — OCE с исходным token, после данных — Canceled; deadline до данных — typed Timeout без отчёта, после данных — Failed/Timeout с output. Typed failure приоритетнее поздней отмены, caller приоритетнее deadline. JSON/UTF-8/known link errors после данных сохраняют Failed/Rejected; unknown I/O/OCE распространяется. Никаких retry/account/key/model fallback.

Callbacks sequential awaited в текущем task; callback exceptions исключаются из transport catches и распространяются тем же объектом, включая JSON/HTTP/OCE/decoder. Ни одного callback после возврата. Wrapper освобождается await using на всех выходах; reader оставляет underlying stream wrapper-владельцу. HttpClient/handlers принадлежат приложению. Callback должен соблюдать token; его task не отсоединяется ради timeout. Continuation binding общий с14 — trusted-data guard, не upstream account proof.

Промежуточные замечания координатора учтены: nonempty terminal output заменяет collected extras; initial fragmented BOM поддержан; gated test tasks завершаются/наблюдаются в finally при падении промежуточных assertions/WaitAsync. Это не новые business/API решения. Compact/composition/tokenizer/orchestration не реализованы.

## Проверки 2026-10-04

Все команды с workdir `D:\Media\User\source\repos\agent-bridge`. До build проверены конкретные csproj, ancestor/build/import/package files: DefaultItemExcludes сохранён, custom Exec/hooks/imports/lock files не добавлены; HttpClientLibrary Directory.Build.props касается только его tests. Restore не потребовался; root csproj/slnx не менялись, pack/publish не выполнялись.

```powershell
dotnet build adapters\AgentBridge.CodexLb\AgentBridge.CodexLb.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage15\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage15\ -m:1 --verbosity minimal
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage15\ --logger 'trx;LogFileName=stage15-codexlb-first.trx' --results-directory artifacts\test-results\stage15 --verbosity minimal
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage15\ --logger 'trx;LogFileName=stage15-codexlb-final.trx' --results-directory artifacts\test-results\stage15 --verbosity minimal
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage15\ --filter 'FullyQualifiedName~CallbacksAreSequentialAndAwaited|FullyQualifiedName~CancellationAndDeadlineWhileReading|FullyQualifiedName~CallerCancellationBeforeData' --logger 'trx;LogFileName=stage15-task-cleanup.trx' --results-directory artifacts\test-results\stage15 --verbosity minimal
git status --short --untracked-files=all
git diff --stat
git diff --check
git diff --cached --stat
git rev-parse HEAD
Get-Command openspec -ErrorAction SilentlyContinue
```

| Проверка | Фактический результат |
| --- | --- |
| Первый adapter build | Exit0, 0 warnings/errors; ядро/actual HttpClientLibrary net10 также собраны |
| Первый test build и transport suite | Build0warnings/errors; **183 passed / 0 failed / 0 skipped** |
| Усиленный transport suite с send cancellation/deadline/invalid UTF-8/done/summary | Build0warnings/errors; **191 passed / 0 failed / 0 skipped**, 55 новых SSE cases |
| Test task cleanup после ревью координатора | Build0warnings/errors; адресный повтор **7 passed / 0 failed / 0 skipped**; production после191 неизменён |
| Финальный compile-check после XML/DI documentation | Test project и dependency chain: exit0, 0 warnings/errors; suite без новых behavioral изменений не повторялся |
| Финальный static review | Manifest28; strict UTF-8 без BOM, LF; нет mojibake/U+FFFD/четырёх question marks; Markdown links доступны после URL decoding; diff --check exit0, staged diff пуст; HEAD14/master сохранены |
| Staged check после приёмки | Первый cached check выявил одну лишнюю пустую строку на EOF нового delta spec (untracked файл не входил в прежний normal diff); строка удалена точечной правкой, повторный cached check exit0; manifest28 подтверждён |

Все новые cases используют public DI/gateway/actual HttpClientLibrary, fake handlers/local streams. Проверены completed/tools-only/opaque/order/unknown, nonempty terminal authority/backfill/BOM, partial arguments/text/summary, multiline/fragmentation и разные newline, failed before/after data/incomplete/EOF/DONE/unclosed terminal, malformed JSON/UTF-8/link, caller/deadline/send/read/late cancellation/priority, callback exceptions/sequential await/disposal, HTTP safe error/logs/no retry и cross JSON/SSE continuation без старого anchor. Compile/TRX outputs находятся в игнорируемом artifacts и не входят в manifest.

Полный suite после test-only cleanup не повторялся: неизменные cases имеют результат191/0/0, изменённые дополнительно7/0/0. Это не198 уникальных tests. Core121/DB/provider/maintenance/library44+44 не повторялись: их production/API/библиотеки не изменены. Исторические562 и этап14 transport136+core121=257 остаются предыдущими доказательствами; integration на EFCoreLibrary0.0.5 не утверждается.

## Ограничения и непройденные проверки

**Пропущено по указанию пользователя:** реальные codex-lb/OpenAI/upstream/аккаунты/токены; приложение/Telegram/hosting/TestServer/WebApplicationFactory/local HTTP servers; arbitrary project/user/demo scripts/executables; рабочие DB/SQL/secrets/data; pack/publish; PR/push/fetch/pull/external uploads. Это не runner skips и не успех. Docker/SQLite/PostgreSQL/native DB checks не понадобились и не выполнялись.

Fake HTTP не подтверждает live compatibility/account ownership или app handlers/scopes. SQLite/PostgreSQL исторических тестов не доказывают SQLServer/MySQL. Никаких установок программ/новой генерации migrations/архивирования changes.

OpenSpec CLI отсутствует: повторный Get-Command не нашёл openspec. Strict CLI validation **не выполнена**, соответствующая change task остаётся открытой; программы не устанавливались, change не архивирован. Main spec/context синхронизированы статически. Business/API questions не открыты. Координатор принял код/спеки/документы и actual TRX191/0/0 плюс адресный7/0/0, включая BOM/terminal authority/test task cleanup; CLI limitation принята как невыполненная проверка. Разрешён локальный English Conventional Commit ровно28 файлов ниже после normal/staged diff/check/manifest. Собственный будущий hash15 не записывается заранее; фактический hash возвращается отдельным итоговым отчётом. Этапы16–25 не начинались.

## Полный утверждённый manifest

**28 файлов:** до staging20 tracked modifications, 8 новых/untracked; только agent-bridge. Чужих правок/outputs/каркаса/соседних файлов в составе нет. Основной diff и UTF-8/EOL проверены перед передачей; до разрешения staged diff пуст. После приёмки разрешён explicit add только этих28 paths, затем staged diff/check/manifest и один непустой local commit; без add . / add-A, смены branch/identity/remotes или отправки.

```text
M AGENTS.md
M Application/AGENTS.md
M Documentation/Business logic/02-dialogs-and-tools.md
M Documentation/Plans/AgentBridge Initial Implementation/15-responses-sse-adapter.md
M Documentation/Plans/AgentBridge Initial Implementation/README.md
M Documentation/README.md
M Documentation/Technical documentation/01-architecture.md
M Documentation/Technical documentation/03-http-and-codex-lb.md
M Documentation/Technical documentation/14-responses-json-adapter.md
M Documentation/Technical documentation/README.md
M README.md
M adapters/AgentBridge.CodexLb/AGENTS.md
M adapters/AgentBridge.CodexLb/Configuration/CodexLbResponsesExtensions.cs
M adapters/AgentBridge.CodexLb/Responses/AGENTS.md
M adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs
M adapters/AgentBridge.CodexLb/Responses/ResponseRequestWriter.cs
M openspec/specs/agent-runtime/context.md
M openspec/specs/agent-runtime/spec.md
M tests/AGENTS.md
M tests/AgentBridge.CodexLb.Tests/ResponsesJsonTests.cs
?? Documentation/Technical documentation/15-responses-sse-adapter.md
?? adapters/AgentBridge.CodexLb/Responses/ResponseSseState.cs
?? adapters/AgentBridge.CodexLb/Responses/SseEventReader.cs
?? openspec/changes/responses-sse-adapter/context.md
?? openspec/changes/responses-sse-adapter/proposal.md
?? openspec/changes/responses-sse-adapter/specs/agent-runtime/spec.md
?? openspec/changes/responses-sse-adapter/tasks.md
?? tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs
```
