# 14 — Реализовать JSON-адаптер Responses

Статус: **Реализован и принят координатором; запрещённые проверки пропущены, локальный коммит разрешён**. Зависимости **04, 07, 13** приняты. Работа возобновлена пользователем 2026-10-04 после исторической паузы после13. Только этап14; этапы15–25 не начаты, checkpoint18 не достигнут.

## Цель

Поддержать канонические запросы и результаты Responses через HttpClientLibrary.

## Задачи

- [x] Описать сообщения, вызовы функций и их результаты, reasoning/compaction без ограничения только текстом.
- [x] Передавать instructions/model/exact effort/tools и поддержанные параметры в canonical `/v1/responses`.
- [x] Сохранять unknown/opaque поля, output order, полный envelope и отдельное bound continuation.
- [x] Нормализовать HTTP-ошибки по safe status/type/code/param только из Complete valid error JSON.
- [x] Поддержать caller cancellation/finite deadline и освобождение request/response/streams.
- [x] Исключить retry/key/model/account fallback и отклонять смешанное continuation до HTTP.

## Проверка и завершение

Использовать подставные HTTP-обработчики и образцы актуального контракта, включая результат без видимого текста и явные ошибки. Этап завершён, когда состояние протокола сохраняется при преобразовании запроса и результата.

## Фактическая реализация

Прочитаны применимые AGENTS, бизнес/техническая документация, main spec/context и материалы зависимостей04/07/13. Обязательные EFCoreLibrary0.0.5/HttpClientLibrary0.0.0.5 и read-only source paths доступны. Актуальные codex-lb v1_requests.py/requests.py/models.py/api.py/affinity.py сверены после его инструкций; соседние исходники не менялись. Telegram/AquaByte источники не понадобились для этого HTTP-контракта; их код не анализировался и не выполнялся.

OpenSpec change создан до production coding. Пользователь отдельно согласовал ModelRequestParameters snapshot и continuation binding к dialog/owner/agent/endpoint/key fingerprint без ключа. Public AddCodexLbResponses → resolver/IModelGateway → actual HttpApiClient передаёт canonical JSON, function definitions и поддержанные controls. Exact model/effort не подменяются; stream=false/store=false. Unknown output/envelope/continuation metadata независимы и сохраняются. Gateway не собирает history и не выполняет tools. [Фактический API и пример](<../../Technical documentation/14-responses-json-adapter.md>).

Reader отличает Completed/Incomplete/Failed без зависимости от visible text. Completed без output не подтверждён. Late caller cancellation после полного JSON возвращает Canceled с output/envelope/continuation; explicit typed failure сохраняет приоритет. До отчёта OCE содержит исходный caller token; caller приоритетнее deadline. Unknown I/O распространяется. Request/response/streams принадлежат HttpClientLibrary, deadline/linked CTS — gateway, HttpClient/handlers — приложению.

Continuation отправляет только previous_response_id/x-codex-turn-state. Binding mismatch — Conflict, unknown format — Unsupported, malformed anchor/header — Validation до HTTP. Unknown metadata сохраняются без превращения в headers/input. Новый absent/invalid id удаляет старый anchor; полный raw id остаётся в envelope. Binding — защита от случайного смешивания trusted app data, не доказательство upstream account ownership/авторизация. Серверную маршрутизацию по аккаунтам выполняет codex-lb.

Http failure возвращает safe CodexLbServiceError с semantic Type, фиксированным Message, status и closed allowlist ApiType/Code/Param; raw message/reason/body/headers/exception не публикуются/logger. Truncated/invalid/unsupported error body не разбирается как envelope. Model failure сохраняет полный error только в чувствительном canonical envelope. SSE callback и CompactAsync явно Unsupported без HTTP/callback; последующие методы не объявляются implemented.

## Проверки 2026-10-04

Все команды с workdir `D:\Media\User\source\repos\agent-bridge`. Перед build проверены конкретные csproj, DefaultItemExcludes, ancestor/build/package import-файлы: новых Exec/hooks/imports/lock-файлов нет; HttpClientLibrary Directory.Build.props касается только его тестов. Restore не требовался. Каркас csproj/slnx не изменён, pack/publish отсутствуют.

```powershell
dotnet build adapters\AgentBridge.CodexLb\AgentBridge.CodexLb.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage14\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage14\ -m:1 --verbosity minimal
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage14\ --logger 'trx;LogFileName=stage14-codexlb-final.trx' --results-directory artifacts\test-results\stage14 --verbosity minimal
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage14\ -m:1 --verbosity minimal
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage14\ --logger 'trx;LogFileName=stage14-core.trx' --results-directory artifacts\test-results\stage14 --verbosity minimal
git status --short --untracked-files=all
git diff --stat
git diff --check
git diff --cached --stat
git rev-parse HEAD
git branch --show-current
```

| Попытка | Фактический результат |
| --- | --- |
| Первый adapter build | 3 CS0452: ServiceResult<T> требует reference type; внутренние body/continuation DTO добавлены, generic контракт ядра сохранён |
| Adapter build после исправления | 0 warnings / 0 errors, также ядро и actual HttpClientLibrary net10 |
| Первый test build | 1 CS0108 warning: имя fake Handler.Send скрывало base member; переименовано в Respond |
| Test build после исправления / первый test запуск | 0 warnings/errors; 123 passed / 0 failed / 0 skipped |
| Усиленный запуск с late cancellation/id/error-body/I/O cases | 133 passed / 1 failed / 0 skipped: I/O fixture MemoryStream имел Content-Length=0, чтение обходилось HTTP-контрактом |
| Исправленный непустой I/O fixture | 134 passed / 0 failed / 0 skipped, build 0 warnings/errors |
| Финальный транспорт после missing-output и late HTTP failure cases | **136 passed / 0 failed / 0 skipped**; 75 новых; build **0 warnings / 0 errors** |
| Core regression | **121 passed / 0 failed / 0 skipped**; build **0 warnings / 0 errors** |

Финальная совокупность **257 passed / 0 failed / 0 runner-skipped**. Все 75 новых cases проходят public DI/gateway/actual HttpClientLibrary с fake handler/local streams. Проверены canonical unknown/opaque/order, schema/controls, no visible text, completed/incomplete/failed/unknown/missing-output, malicious/malformed/truncated/encoding/unsupported errors, key/endpoint/context binding, отсутствие старого anchor/произвольных headers, cancellation на send/success/error body, finite deadline/caller priority, late Canceled report/typed failure priority, disposal и safe structured/форматированные logs. Нет реальной сети, local HTTP servers либо hosting. TRX/build outputs в игнорируемом artifacts не входят в manifest.

## Ограничения и непройденные проверки

**Пропущено по указанию пользователя:** реальные codex-lb/OpenAI/upstream/аккаунты/токены, приложение/Telegram/hosting/TestServer/WebApplicationFactory/local HTTP servers, arbitrary project/user/demo scripts/executables, pack/publish, рабочие БД/секреты/данные, PR/push/fetch/pull/external uploads. Это не runner skips и не успех.

DB/SQLite/PostgreSQL/Docker/native backup/restore integration и 44+44 HttpClientLibrary tests не повторялись: схема/библиотеки не менялись, транспорт проверен actual library через fake HTTP. Исторические562 tests и EFCoreLibrary0.0.5 builds/164 isolated tests остаются доказательствами предыдущих запусков, не нового JSON transport. Fake HTTP не доказывает live compatibility/account ownership, SQLite/PostgreSQL не доказывают SQLServer/MySQL. App handlers могут менять поведение; приложение отвечает за отсутствие retry/смены аккаунта и собственные logging scopes.

OpenSpec CLI отсутствует (Get-Command openspec). CLI validation не выполнена; программы не устанавливались, change не архивирован. Specs/context проверены статически и синхронизированы; CLI task остаётся невыполненной. Нет открытых business questions после принятия расширения пользователем. Координатор принял код, документацию и actual TRX136+121=257/0/0, принял CLI limitation как невыполненную проверку и разрешил локальный English Conventional Commit ровно33 файлов manifest ниже. Перед add проверяются normal/staged diff, после add — staged diff/check и точный manifest; add . / add-A, чужие файлы, artifacts/каркас/соседи исключены. Полный hash подтверждается итоговым отчётом после commit, не записывается заранее в собственный коммит. Исходный HEAD `1a1fbc4e22ae1626bf50497a6d9dce658b12a0b0`, ветка master; до add staged diff пустой. Историческая пауза в этапе13 сохранена, текущие навигаторы отражают принятие14; следующие этапы не начинаются, checkpoint18 ещё впереди.

## Полный manifest для приёмки

33 файла: 16 modified и 17 untracked, manifest совпадает с git status. Соседние библиотеки/codex-lb, Domain/persistence/generated migrations, csproj/slnx и отчёты00–13 не менялись. Ручные правки — apply_patch. Финальная статическая проверка: все33 файла строго декодируются UTF-8 без BOM, U+FFFD/mojibake/четырёх question marks нет; LF сохранён, для16 tracked files git ls-files --eol подтверждает i/lf и w/lf. Все206 локальных Markdown targets существуют. git diff --check exit0, staged diff пустой; HttpClientLibrary и EFCoreLibrary status чистые. Git autocrlf warnings не означают изменение физических EOL. Первая inline PowerShell static-check команда получила ParserError на quoting/Unicode regex; повтор с regex Unicode escapes прошёл, исходники/скрипты при этой проверке не выполнялись.

Modified:

```text
AGENTS.md
Application/AGENTS.md
Application/Models/ModelRequest.cs
Documentation/Business logic/02-dialogs-and-tools.md
Documentation/Plans/AgentBridge Initial Implementation/14-responses-json-adapter.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/README.md
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/03-http-and-codex-lb.md
Documentation/Technical documentation/09-application-ports.md
Documentation/Technical documentation/README.md
README.md
adapters/AgentBridge.CodexLb/AGENTS.md
openspec/specs/agent-runtime/context.md
openspec/specs/agent-runtime/spec.md
tests/AGENTS.md
```

Untracked:

```text
Application/Models/ModelRequestParameters.cs
Documentation/Technical documentation/14-responses-json-adapter.md
adapters/AgentBridge.CodexLb/Configuration/CodexLbResponsesExtensions.cs
adapters/AgentBridge.CodexLb/Responses/AGENTS.md
adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs
adapters/AgentBridge.CodexLb/Responses/CodexLbServiceError.cs
adapters/AgentBridge.CodexLb/Responses/Models/ResponseContinuationState.cs
adapters/AgentBridge.CodexLb/Responses/Models/ResponseRequestBody.cs
adapters/AgentBridge.CodexLb/Responses/ResponseContinuationMapper.cs
adapters/AgentBridge.CodexLb/Responses/ResponseErrorReader.cs
adapters/AgentBridge.CodexLb/Responses/ResponseJsonReader.cs
adapters/AgentBridge.CodexLb/Responses/ResponseRequestWriter.cs
openspec/changes/responses-json-adapter/context.md
openspec/changes/responses-json-adapter/proposal.md
openspec/changes/responses-json-adapter/specs/agent-runtime/spec.md
openspec/changes/responses-json-adapter/tasks.md
tests/AgentBridge.CodexLb.Tests/ResponsesJsonTests.cs
```

Источник: [актуальный HTTP-контракт](<../../Technical documentation/03-http-and-codex-lb.md>).
