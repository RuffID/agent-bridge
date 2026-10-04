# 16 — Сформировать контекст приложения и диалога

Статус: **Реализован и адресно проверен, принят координатором; локальный коммит разрешён**. Зависимости **07, 14** приняты, actual transport отчёты14–15 прочитаны. Исходный HEAD `1d99721c4c4689cb06f9d71be2ba02380cdd0458`, master; этапы17–25 не начаты, checkpoint18 не достигнут.

## Цель

Формировать упорядоченные входные данные, которые будут переданы модели.

## Задачи

- [x] Объединять инструкции агента, разрешённый контекст приложения, активное состояние контекста и последующие сообщения.
- [x] Сохранять роли инструкций и полные пары вызовов функций и их результатов; известный call без результата явно запрещает готовый запрос, не удаляя историю.
- [x] Определить ответственность за провайдеры контекста, отмену и детерминированный порядок данных.
- [x] Включить новое сообщение и необходимые описания инструментов в полный подготовленный запрос для будущего расчёта17; tokenizer/budget здесь не реализовать.
- [x] Оставить поиск по старой истории за пределами реализации.

## Проверка и завершение

Проверить порядок контекста, изоляцию владельцев, сбой провайдера и продолжение со сжатым состоянием. Этап завершён, когда формирование контекста сохраняет системную роль текста и обязательное состояние протокола.

Источник: [поведение контекста](<../../Business logic/03-context-and-compaction.md>).

## Фактическая реализация

Прочитаны applicable ancestor/root/Application/Domain/Configuration/tests/Documentation/Plans AGENTS, csharp style/build/agents, service-result и OpenSpec context rules, main spec/context, бизнес/техничка, текущий план и actual отчёты зависимостей07/14/15. Обязательные EFCoreLibrary/HttpClientLibrary directories доступны. Код16 не затрагивает DB/HTTP/DI/options; новых зависимостей или csproj/slnx нет. EFCoreLibrary0.0.5 и HttpClientLibrary0.0.0.5 здесь не объявляются перепроверенными интеграцией. Telegram/AquaByte не анализировались, соседи не изменены.

До production coding создан change `context-composition` proposal/tasks/spec/context. Минимальный public `ContextBuilder` получает ordered IContextProvider selection; BuildAsync принимает actual call, уже прочитанный DialogSnapshot, новый ModelRequest и явное nowUtc. Owner/dialog/expiry и целостность prefix/истории проверяются до provider I/O. Snapshot не содержит AgentId: persisted agent ownership не вводится; actual AgentId идёт provider, transport binding остаётся14–15. Read DTO/token и успешная подготовка не разрешают write; исходные incarnation/revision проверяет короткий write UoW.

Input порядок: provider contributions → active context Items → полный tail turns после ThroughTurnSequence → только ещё не сохранённый newRequest.Input. Instructions остаётся отдельным системным полем, model/effort/tools/parameters/явный continuation сохранены. Envelope/continuation не input; ModelSteps не повторный источник output и не fallback. Prefix0 сохраняет всю историю, InProgress нельзя покрывать; терминальный Incomplete допустим для prefix, но не объявляется успешным turn. Snapshot/fixed expiry/history не меняются.

Пользователь через координатора согласовал две политики до dependent coding: любые canonical provider items с исходными ролями; явный отказ при известном function_call без output, включая partial arguments после обрыва. Проверка проходит всю prepared последовательность, сохраняет пары через границы источников, не разбирает скрытые вызовы opaque/unknown state и не исправляет arguments/output. Malformed call_id/orphan output — безопасный Validation; оставшийся незакрытый call — Conflict без ModelRequest. Авторизация вкладов и scope providers принадлежат приложению. Каждый Task последовательно awaited с caller token; typed failures передаются тем же объектом, unexpected exceptions неизменны, нет fan-out/retry/empty fallback.

Промежуточное статическое ревью координатора выявило лишнее первоначальное ограничение глобальной уникальности call_id. До первого test run оно снято после чтения codex-lb AGENTS и actual `app/core/openai/requests.py` (`_compact_matching_tool_call_index`/`_compact_reconciled_tool_call_indices`): каждый output закрывает один незакрытый предшествующий call того же ID, отдельные пары вправе повторять ID. Регрессии проверяют закрытые/открытые повторные пары между turns и provider/window/tail/new input. Соседний код только прочитан, Python не исполнялся.

API/пример/границы: [техническая документация16](<../../Technical documentation/16-context-composition.md>). Main spec/context, бизнес-документация, README/навигаторы и ближайшие инструкции синхронизированы.

## Проверки 2026-10-04

Все shell commands с workdir `D:\Media\User\source\repos\agent-bridge`. До build проверены root/test csproj, ancestor Directory.Build.props/targets, NuGet.config/lock files и generated NuGet imports: дополнительных project hooks/Exec/lock files нет, PackageReferences прежние. Restore не потребовался. Generated, каркас и соседи не изменены.

```powershell
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage16\ -m:1 --verbosity minimal
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage16\ --filter 'FullyQualifiedName~ContextBuilderTests' --logger 'trx;LogFileName=stage16-composition-first.trx' --results-directory artifacts\test-results\stage16 --verbosity minimal
# После усиления cancellation/roles/window-new/repeated pair regression — повтор build той же командой:
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage16\ --filter 'FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ApplicationPortsTests' --logger 'trx;LogFileName=stage16-composition-final.trx' --results-directory artifacts\test-results\stage16 --verbosity minimal
git status --short --untracked-files=all
git diff --stat
git diff --name-only
git diff --check
git diff --cached --stat
git rev-parse HEAD
Get-Command openspec -ErrorAction SilentlyContinue
```

| Проверка | Фактический результат |
| --- | --- |
| Первый test build, включая production core | Exit0, 0 warnings/errors |
| Первый composition run | **50 passed / 0 failed / 0 skipped** |
| Финальный build после усиления проверок/production cancellation | Exit0, 0 warnings/errors |
| Финальный affected composition + ApplicationPorts filter | **86 passed / 0 failed / 0 skipped**: 59 новых composition, 27 существующих contract cases |
| Финальная static проверка | Manifest23 совпадает с status; строгий UTF-8 без BOM/LF, нет U+FFFD/mojibake/четырёх question marks; все196 локальных Markdown targets доступны; delta requirements дословно присутствуют в main spec; diff --check exit0; staged diff пуст; HEAD/master сохранены |

Это не136 уникальных tests: первый run перекрывается финальным. Проверены deterministic order/roles/full request/schema/parameters/explicit continuation, no ModelSteps/envelope duplication, unknown/opaque, compressed prefix0–3/full current turn/terminal Incomplete, cross-source pairs включая window→new input, повторные call_id и нехватка outputs, partial call refusal без изменения storage snapshots/lifecycle, owner/dialog/expiry tick boundary, corrupt prefix/history/compact, provider failure/late typed priority/unexpected same exception/caller cancellation/sequential await и task cleanup. Test tasks отменяются/await в finally. Compile/TRX outputs только в ignored artifacts.

Остальные core cases, transport191, persistence164/integration39, maintenance89, HttpClientLibrary44+44 не повторены: их production/API/схемы/библиотеки не изменены. Исторические suite results не считаются проверками нового16. Fake providers/DTO не доказывают DB restart, relational isolation, настоящий provider I/O или live HTTP/account ownership.

## Ограничения и непройденные проверки

**Пропущено по указанию пользователя:** приложение/Telegram/hosting/TestServer/WebApplicationFactory/local HTTP servers; live codex-lb/OpenAI/upstream/аккаунты/токены; arbitrary project/user/demo scripts/executables; рабочие DB/SQL/secrets/data; pack/publish; push/fetch/pull/PR/external uploads. Это запреты, не runner skips и не успех. Docker/SQLite/PostgreSQL/native checks не нужны для pure composition и не выполнялись.

OpenSpec CLI отсутствует, strict CLI validation **не выполнена**; установка не предпринималась, change не архивирован. Соответствующая task остаётся открытой. Нет открытых business questions после двух пользовательских ответов. Tokenizer/budget/compact/tools execution/orchestration/search и этапы17–25 не реализованы.

Координатор принял ContextBuilder, tests/spec/docs и actual TRX86/0/0, подтвердил оба пользовательских решения и повторное использование call_id по actual контракту. Утверждён ровно manifest23 ниже; разрешён локальный English Conventional Commit после normal/staged diff/check и explicit add только этих paths. CLI limitation принята как невыполненная проверка, change не архивируется. Собственный будущий hash16 не записывается заранее; полный фактический hash возвращается отдельным отчётом после commit. Branch/identity/remotes не меняются; без add . / add-A, чужих outputs, push или этапа17.

## Полный manifest для приёмки

**23 файла: 16 tracked modifications, 7 новых/untracked**, только agent-bridge. Каркас/Domain/adapters/generated/соседи и отчёты00–15 не изменены. Ручные правки apply_patch.

```text
M AGENTS.md
M Application/AGENTS.md
M Application/Models/ContextContribution.cs
M Application/Models/ModelRequest.cs
M Documentation/Business logic/02-dialogs-and-tools.md
M Documentation/Business logic/03-context-and-compaction.md
M Documentation/Plans/AgentBridge Initial Implementation/16-context-composition.md
M Documentation/Plans/AgentBridge Initial Implementation/README.md
M Documentation/README.md
M Documentation/Technical documentation/01-architecture.md
M Documentation/Technical documentation/09-application-ports.md
M Documentation/Technical documentation/README.md
M README.md
M openspec/specs/agent-runtime/context.md
M openspec/specs/agent-runtime/spec.md
M tests/AGENTS.md
?? Application/ContextBuilder.cs
?? Documentation/Technical documentation/16-context-composition.md
?? openspec/changes/context-composition/context.md
?? openspec/changes/context-composition/proposal.md
?? openspec/changes/context-composition/specs/agent-runtime/spec.md
?? openspec/changes/context-composition/tasks.md
?? tests/AgentBridge.Tests/ContextBuilderTests.cs
```
