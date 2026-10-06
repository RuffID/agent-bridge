# 11 — Association повторных call_id при compact

[Навигатор](README.md) · [Принятое решение](Decisions.md). Статус: **принят в локальных A/B границах**. Зависимость: 00. **Q-005 согласован**: единый FIFO и отказ compact при неопределимой связи. Подтверждённой audit находки о дефекте на прежнем внешнем контракте нет.

## Цель и область

Реализовать совместимость repeated-ID occurrences через compact по принятому FIFO и проверить конечный subset. Источник — [решение Q-005](Decisions.md), исторический [вопрос](<../AgentBridge Quality Audit/OpenQuestions.md#abqa-q-005>), [аудит09](<../AgentBridge Quality Audit/09-token-budget-and-compaction.md>) и [аудит10](<../AgentBridge Quality Audit/10-tools-and-side-effects.md>).

Сверить actual ContextBuilder/ToolExecutor FIFO и codex-lb compact helper на выбранной версии. Durable identity StepId/output index и внешний call_id — разные границы; изменение pairing не разрешает повтор side effects.

## Работы

1. Воспроизвести различающий пример call(x,argsA), call(x,argsB), output(x,resultA), output(x,resultB) с различимыми args/results. Отдельно проверить call/output/call/output, где исходного различия нет.
2. Сохранить trace helper для selected={3} и selected={2}, а затем проверить final protected/required/fitting и opaque branches. Helper intermediate set не объявлять фактическим отправленным payload.
3. Закрепить принятый FIFO: первый результат относится к первому ещё не закрытому вызову с тем же call_id. Если связь нельзя достоверно установить, отклонять compact без активации/потери последнего окна и без повторного вызова handler.
4. Исправить несогласованные pairing boundaries в владеющем компоненте, синхронизировать контракт и различающие tests. Для codex-lb получить отдельное поручение на этот репозиторий. Не менять исходные canonical calls/outputs и не вводить глобальную дедупликацию по call_id как обход.
5. Проверить поведение на границе compact → сохранение контекста → последующий build/executor, включая incomplete occurrence и full request budget. Сохранить исходную историю, fixed expiry, attempt identity и no-replay.

## Проверки

B: различимые повторные IDs, оба selected sets, required/protected fitting, достаточный/недостаточный budget, incomplete prefix, контроль alternating sequence. Новый Python test/harness или script запускается только по точному отдельному разрешению; чтение trace не является B-run.

D: actual deployed compact и конечный payload в19 после согласования ресурсов. Без этого не утверждать live потерю данных, ошибочный результат или повтор действия.

## Критерии завершения

Принятый FIFO действует в local и включённой server boundary, final subset сохраняет правильные пары. Неопределимая связь даёт явный отказ без потери истории; no-replay и сохранение occurrences подтверждены отдельно от local balance. Непроверенная deployment часть остаётся в19.

## Результаты

### Область, baseline и полномочия

2026-10-06, Asia/Novosibirsk. Выполнен только11 по принятому Q-005: local Application и compact pairing codex-lb. Это исполнение согласованного контракта, а не повышение исторического аудита до подтверждённого deployed дефекта. A/B завершены, D остаётся19. Реальные HTTP/БД/SQL/Docker/native/hosting/deployment не выполнялись. Commit/add/push/новые branches/worktrees не выполнялись; до приёмки writer останавливается после передачи.

AgentBridge входной/конечный HEAD `fc0fe215031f777089f95cecac0dfe7c9f8ff76a`; единственная чужая tracked правка — Coordination.md, исключена из manifest и не менялась исполнителем. codex-lb local HEAD `f8ffbac2099a113fba54dfd8d77774f5bca80ffa`; foreign `.vs/` исключена и сохранена. Это local HEAD, не deployed version. EFCoreLibrary/HttpClientLibrary read-only, собственного manifest нет.

Прочитаны задание11, README/Decisions, принятые predecessor Results00/03/05/06/09/10 и owning sources; audit09/10 Q-005 trace, Findings/15, текущая main spec; root/Application/tests/Documentation/Plans AGENTS. Использован csharp-project-rules с csharp-style/build-validation/agents-maintenance. Storage/domain/HTTP код не менялся, backend UoW skill не требовался. LB root AGENTS, project-conventions/conventions, git-workflow и owning compact requirements/context прочитаны; OpenSpec-first ff/apply/context-docs/verify/sync/archive workflow выполнен в собственной focused области.

### Реализация и before/after

**AgentBridge:** ContextBuilder сохраняет прежний balance/порядок, ToolExecutor уже использует FIFO и прежнюю identity StepId/output position. Новый внутренний CompactFunctionPairInspector проверяет source history и новый output перед candidate count/save: known calls/outputs связываются FIFO; retained пары совпадают по полному JsonElement содержимому и исходному порядку. First и last ordered exact matching должны совпадать; первое совпадение и баланс не доказывают uniqueness. Неизвестная/подменённая/повторённая/неполная association отказывает до save. Скрытые opaque calls не выдумываются; валидное opaque окно с null full estimate сохраняет прежний UnknownBudget.

До production правки добавлен только baseline test: source `call(x,A),call(x,B),output(x,A),output(x,B)` с разными args/results и candidate `[callB,outputA]`. Свежие build/test Exit0, baseline1 подтвердил TargetReached/save1/неверную пару. После правки нормативный test требует Failed/Rejected/save0/original token/history. Baseline assertions заменены regression assertions; before TRX отдельно сохранён. Это различающий local test через actual compactor/builder и doubles, не upstream run.

Final адресные20 включают retained A/B/full/alternating → successful context save → subsequent public builder → actual public executor closed pairs/results0/registry scopes0; swapped/rewrite/duplicate/incomplete/orphan refusals; cross-ID обратный порядок outputs; incomplete source prefix до gateway; second-pass association failure сохраняет первый save/token11/окно и fixed expiry; полная estimate1050 включает provider900 и retained subset, отдельный guard даёт Rejected; retained known pair+opaque даёт UnknownBudget и Unsupported guard. Две полностью одинаковые исходные пары, сокращённые до одной, дают Rejected/save0/старое принятое окно и token; полная одинаковая последовательность и одинаковые calls с разными outputs допускаются. Identity/tool executor/replay code не изменены.

**codex-lb:** один original-index FIFO map по `(call_id,compatible call type)` используется terminal output matching и обоими направлениями subset reconciliation. Для supplied0–3 intermediate `{3}` теперь `{1,3}`, `{2}` — `{0,2}`. Known supplied pair добавляется/удаляется атомарно; required known counterpart сохраняется вместе до existing image-elision/full-wire fitting либо явного refusal. Explicit continuity/unmatched terminal contract, state/side-effect priority, head/suffix/markers и image elision сохранены. Здесь исходные indices известны даже при одинаковых bytes; возвращённое окно AgentBridge требует самостоятельного uniqueness proof. IDs не переписываются, global dedup не введён.

LB before production правки: actual unchanged requests.py с новыми baseline tests дал2 passed: helper `{3}`→`{3}`, `{2}`→`{1,2}`, matching2→1/3→0; public `ResponsesCompactRequest.model_validate(...).to_payload()` отказал для required B/resultB, ошибочно связав его с большим resultA. Это final request preparation evidence, а singleton helper sets отдельно intermediate. После patch те же inputs проходят FIFO и public subset `[callB,resultB]`; oversized required pair отказывает. Before JUnit сохранён отдельно, старые assertions не запускались на новых sources как «before».

Final LB120 =27 new +93 existing compact cases,0 failed/0 skipped,94 deselected. Новый public final payload проверяет retained first и second occurrence, alternating, ordinary/state/side-effect, required insufficient budget, final fitting/full input wire cap, opaque fitting unchanged и previous_response_id/conversation consumed-pair exclusion. Helper controls отдельно проверяют both directions, protected sets и function/custom/apply_patch протоколы. Existing93 сохраняют suffix/head/state/side-effect/image/framing/type/continuity/insufficient-budget branches. Intermediate after116 прошёл до четырёх дополнительных controls; он пересекается с120 и не суммируется.

### Точный manifest

AgentBridge —7 файлов относительно `D:/Media/User/source/repos/agent-bridge`:

- `Application/CompactFunctionPairInspector.cs` — новый internal FIFO/origin/uniqueness inspector.
- `Application/ContextCompactor.cs` — проверка output перед save/count.
- `Application/AGENTS.md` — устойчивая граница occurrence association/refusal.
- `tests/AgentBridge.Tests/ContextCompactorTests.cs` —20 regression/control cases и no-replay публичный путь.
- `openspec/specs/agent-runtime/spec.md` — два current requirements с различающими scenarios.
- `Documentation/Technical documentation/18-context-compaction.md` — actual association/ambiguity/opaque/refusal границы.
- `Documentation/Plans/AgentBridge Audit Remediation/11-repeated-call-pairing.md` — статус/Results11; исходное задание сохранено.

codex-lb —11 файлов относительно `D:/Media/User/source/repos/codex-lb`:

- `app/core/openai/requests.py`.
- `tests/unit/test_compact_repeated_call_fifo.py`.
- `openspec/specs/responses-api-compat/spec.md`.
- `openspec/specs/responses-api-compat/context.md`.
- `openspec/changes/archive/2026-10-06-fix-compact-repeated-call-fifo/.openspec.yaml`.
- `openspec/changes/archive/2026-10-06-fix-compact-repeated-call-fifo/proposal.md`.
- `openspec/changes/archive/2026-10-06-fix-compact-repeated-call-fifo/design.md`.
- `openspec/changes/archive/2026-10-06-fix-compact-repeated-call-fifo/context.md`.
- `openspec/changes/archive/2026-10-06-fix-compact-repeated-call-fifo/tasks.md`.
- `openspec/changes/archive/2026-10-06-fix-compact-repeated-call-fifo/specs/responses-api-compat/spec.md`.
- `openspec/changes/archive/2026-10-06-fix-compact-repeated-call-fifo/verification.md`.

Coordination, README/Decisions навигация координатора, historical Initial/Quality Audit reports/original AB18, root/project/slnx и соседние библиотеки не изменены. Generated вручную не правились; DLL/TRX/JUnit/Python environment только в игнорируемых AB artifacts.

### Preflight, разрешения и точные команды

AB concrete core/test csproj и reference graph, ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/locks до D:/, actual existing assets/generated NuGet imports и relevant tests/traits проверены чтением. SDK-style net10.0, compile excludes bin/obj/artifacts; source Exec/custom targets/imports отсутствуют, package-generated imports стандартные Test SDK/xUnit/Serilog/options/binder/logging. EF chain не затронута; packaging hook не запускается. Restore/новые NuGet источники/network для .NET не добавлялись. No-build всегда после свежей успешной сборки того же concrete проекта/output/config; failed build не было.

LB preflight: pyproject требует Python>=3.13; найден `D:/Programs/Python/python.exe`. Root tests/conftest.py загружает app и SQLite; он исключён `--noconftest`, plugin autoload отключён. Import graph requests/v1_requests/contracts/message_coercion/tool_call_safety/types/json_guards и package initializers прочитан: pure shaping, `app.main` lazy не вызывается. Системный Python3.13.0 не имел pydantic/pytest/hypothesis; bundled runtime имеет cp312 pydantic и не подходит Python3.13 syntax. Отдельный exact запрос пользователя разрешил venv+pip только трёх pinned пакетов и transitive dependencies, без full uv sync/app/DB.

Команды AB, cwd `D:/Media/User/source/repos/agent-bridge`, все Exit0; builds0 warnings/0 errors:

```powershell
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/before/
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/before/ --filter 'FullyQualifiedName~RepeatedAssociationDifferentiatingBaseline' --logger 'trx;LogFileName=before.trx' --results-directory artifacts/audit-remediation11/results-before
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/after/
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/after/ --filter 'FullyQualifiedName~RepeatedAssociation' --logger 'trx;LogFileName=pairing.trx' --results-directory artifacts/audit-remediation11/results-after
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/after/ --filter 'FullyQualifiedName~ContextCompactorTests|FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ToolExecutorTests|FullyQualifiedName~ContextBudgetGuardTests' --logger 'trx;LogFileName=compact-regression.trx' --results-directory artifacts/audit-remediation11/results-after
dotnet build agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/after/
& 'D:/Media/User/AppData/npm/openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive
```

Before TRX1/0/0; intermediate pairing8/0/0 сохранён в `pairing-initial8.trx`; final pairing20/0/0 и regression183/0/0. Final183 включает20 и существующие opaque/full-budget/provider/expiry/save/lifecycle/cancellation/executor controls; это пересекающиеся наборы. Core/test builds after повторялись после additions, каждый Exit0, без stale DLL. Main strict valid=true/issues=[],1 passed, SHA256 ниже; main после strict не менялась.

Команды LB, cwd `D:/Media/User/source/repos/codex-lb`, exact разрешения пользователя в этом чате, все Exit0:

```powershell
& 'D:/Programs/Python/python.exe' -c "import sys, importlib.util; print(sys.version); print({name: importlib.util.find_spec(name) is not None for name in ('pydantic', 'pytest', 'hypothesis')})"
& 'D:/Programs/Python/python.exe' -m venv D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/lb-python
& 'D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/lb-python/Scripts/python.exe' -m pip install --index-url https://pypi.org/simple pydantic==2.12.5 pytest==9.0.2 hypothesis==6.165.3
$env:PYTEST_DISABLE_PLUGIN_AUTOLOAD='1'
$env:PYTHONPATH='D:/Media/User/source/repos/codex-lb'
& 'D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/lb-python/Scripts/python.exe' -m pytest -p no:cacheprovider --noconftest tests/unit/test_compact_repeated_call_fifo.py -q --junitxml=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/lb-before.xml
& 'D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/lb-python/Scripts/python.exe' -m pytest -p no:cacheprovider --noconftest tests/unit/test_compact_repeated_call_fifo.py tests/unit/test_openai_requests.py -k compact -q --junitxml=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/lb-after.xml
& 'D:/Media/User/AppData/npm/openspec.cmd' new change fix-compact-repeated-call-fifo
& 'D:/Media/User/AppData/npm/openspec.cmd' status --change fix-compact-repeated-call-fifo --json
& 'D:/Media/User/AppData/npm/openspec.cmd' list --specs
& 'D:/Media/User/AppData/npm/openspec.cmd' show responses-api-compat --type spec --json --no-scenarios
& 'D:/Media/User/AppData/npm/openspec.cmd' show responses-api-compat --type spec
& 'D:/Media/User/AppData/npm/openspec.cmd' instructions proposal --change fix-compact-repeated-call-fifo --json
& 'D:/Media/User/AppData/npm/openspec.cmd' instructions specs --change fix-compact-repeated-call-fifo --json
& 'D:/Media/User/AppData/npm/openspec.cmd' instructions design --change fix-compact-repeated-call-fifo --json
& 'D:/Media/User/AppData/npm/openspec.cmd' instructions tasks --change fix-compact-repeated-call-fifo --json
& 'D:/Media/User/AppData/npm/openspec.cmd' instructions apply --change fix-compact-repeated-call-fifo --json
& 'D:/Media/User/AppData/npm/openspec.cmd' validate fix-compact-repeated-call-fifo --type change --strict --json --no-interactive
& 'D:/Media/User/AppData/npm/openspec.cmd' archive fix-compact-repeated-call-fifo --skip-specs --yes
```

В таблице commands сгруппированы по назначению, фактический порядок: preflight→new/status/inventory→proposal→spec/design/context→tasks/apply ready→before2→production patch/regression assertions→after116→four controls→after120→strict/verification/status4/4→exact sync check→отдельное разрешение archive→archive Exit0. Python network только explicitly authorized pip/PyPI. Каждый pytest даёт3 config warnings о deliberately disabled pytest-asyncio options; они не скрыты, app fixtures не включались ради их устранения. LB instructions отмечают pre-existing unknown config rule context_docs, config вне scope; focused strict issues=[].

OpenSpec-first focused change создан до LB behavior code;4/4 tasks выполнены,2 requirements/4 scenarios покрыты. Agent-driven sync добавил только два блока и stable context, exact delta/main comparison passed. Пользователь отдельно разрешил archive после review-ready concrete evidence: archived as `2026-10-06-fix-compact-repeated-call-fifo`, specs skipped потому что уже синхронизированы. Original AB18 и другие changes не архивировались. Strict focused result не является global validation всех старых LB requirements. Postarchive verification — чтение preserved tasks/delta/manifest, без иной CLI-команды.

### Артефакты и остаток

Все artifact paths относительно `D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation11/`:

| Артефакт | SHA256 |
| --- | --- |
| results-before/before.trx | `B5A0C70DF73131FD52EB4EF059C8EBAE01D072683CEB05DFA270824EA215629E` |
| results-after/pairing.trx | `4272557ECE5EFE3A6E03182B24A0EBA09D867B014BBFC1FEA807BE7D0E695272` |
| results-after/compact-regression.trx | `7A96887AD89070FE2428A652E78ECE34D3583D601AB592AA32FEE9502CEE15A2` |
| lb-before.xml | `8FBFF0F04905B620C5D9279A58BFB789FC643A972732443E0F4ED0B9E37B9EA1` |
| lb-after.xml | `E1DACAD0E7BAD6710BB0EA1993FBBD7FB00CF3150758EC03AE0B8109E5D3A5CF` |
| after/Debug/net10.0/AgentBridge.dll | `5140F87A42AF9A5275C0DC0ED9A4B17E26438BF735C380DE172236B805F8E667` |

AB main spec SHA256 `0BEF9F145B581AFEA7EB99A6410A01F0BC70F574F243B8087A1F723C301E351D`; LB requests.py `5C5923186D1F5863D0C5D0293937C61CB5339ED4B527734E1D895A75A4936C2F`, new Python test `FC7877A3110A7D8B98D8B8C5AF3371FBF7EEDC2D62A12D198202683922D0FC19`. Они после соответствующих проверок не менялись.

Actual: production compactor/builder/guard/executor и Python/Pydantic final preparation. Doubles: model gateway, context writer, providers/clock/counter большинства управляемых сценариев; existing actual offline tokenizer opaque controls входят в183. Registry-open observer подтверждает отсутствие invocation scope для закрытых retained pairs, не реальные бизнес side effects. DB CAS/atomicity/restart/live endpoint не проверены. Статические source/UTF-8/EOL/diff проверки не являются B execution.

Final read-only checks: AB7 strict UTF-8/LF без U+FFFD, четырёх вопросительных знаков и проверенных mojibake markers; LB11 strict UTF-8 без повреждённых markers, existing requests.py/main spec/context сохраняют CRLF, new source/docs LF. Оба git diff --check Exit0; postarchive exact delta/main2 blocks passed; actual XML counters подтвердили1/20/183/2/120 и0 failed/skipped. EF/HTTP git status clean; foreign Coordination/.vs сохранены вне manifest. Git warnings о будущем LF→CRLF в AB не означают изменения actual EOL. HEAD обоих repo неизменны. Последняя проверка Results сначала нашла буквальный диагностический marker в описании самой проверки; он заменён словами, повторная проверка прошла.

Несколько read commands с Windows wildcard/неверным cwd и два не совпавших documentary patch hunk завершились ошибкой без изменений; исправлены адресным чтением/точным hunk. Build/test/product failures не было. Runtime/deployed compact, скрытая opaque association и actual upstream budget остаются19; endpoint/модели неизвестны. Локальных бизнес-вопросов или implementation blockers нет. Готово к приёмке A/B; коммиты только после отдельного поручения координатора, manifests7/11, без Coordination/.vs. После передачи shared checkout не изменять до нового допуска.

### Приёмка11

2026-10-06 координатор от имени пользователя принял11 в локальных A/B границах Q-005. Независимо проверены final source diff/uniqueness guard/positive controls, Python120, TRX20/183 с учётом пересечения, fresh build chains, strict AB/focused LB, прямое разрешение пользователя на archive и actual Exit0, archived verification/tasks, UTF-8/EOL/hashes7/11 и diff checks. Live19 и global strict всех старых LB требований не закрыты.

Явно поручены два отдельных локальных English Conventional Commit: AB только exact7-file manifest и LB только exact11-file manifest выше. Перед ними изменены только статус и этот раздел приёмки; code/tests/spec после final checks не менялись. Coordination/.vs исключаются из index; add/commit только адресные manifests, без amend/push/PR/branch. Полные hashes/show manifests/postcommit status передаются координатору отдельно. Исходное before/after evidence и промежуточные checkpoints сохранены.
