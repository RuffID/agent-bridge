# 10 — Глубина проверки вложенных controls

[Навигатор](README.md) · [Принятое решение](Decisions.md). Статус: **принят в локальных A/B границах**. Зависимость: 00. **Q-004 согласован**: malformed/duplicate known nested fields отклоняются до HTTP. Подтверждённой audit находки о дефекте на прежнем неоднозначном контракте нет.

## Цель и область

Реализовать согласованную границу known nested validation до HTTP. Источник — [решение Q-004](Decisions.md), исторический [вопрос](<../AgentBridge Quality Audit/OpenQuestions.md#abqa-q-004>) и controls в [OpenSpec](../../../openspec/specs/agent-runtime/spec.md).

Точки сверки: [ResponseRequestWriter](../../../adapters/AgentBridge.CodexLb/Responses/ResponseRequestWriter.cs), JSON/SSE/compact paths, [requests.py codex-lb](../../../../codex-lb/app/core/openai/requests.py). Чтение исходников codex-lb не разрешает их изменение; локальный source не доказывает deployed contract.

## Работы

1. Подготовить различающие inputs: reasoning.summary=42, duplicate known nested summary, корректный str/null, unknown nested field. Сверить known shape целевой версии и прежнюю top-level validation.
2. Перечислить фактически известные nested controls и проверить types/duplicates по их подтверждённому контракту. Применять pre-HTTP Validation для неверной known формы; локально не угадывать model-specific ограничения.
3. Синхронизировать нормативный текст и техническое описание с принятым решением. Unknown nested fields сохранять без удаления/нормализации; policy unknown top-level остаётся прежней.
4. Точечно реализовать недостающую проверку в request writer и адресных tests с сохранением canonical input/opaque controls. Для части правил, уже выполняемых кодом, добавить различающий контроль без ненужной переписи.
5. Проверить одинаковую agreed validation для JSON, SSE и compact. Если нужен server contract change, вынести отдельную согласованную работу в codex-lb, не имитировать её в адаптере.

## Проверки

B с actual writer/gateway и fake HTTP handler: Validation для malformed/duplicate known nested fields и **HTTP calls=0**, valid known inputs, top-level duplicates, сохранение unknown nested fields и input ordering. Live поведение отдельно19; ошибка fake handler не доказывает server rejection.

## Критерии завершения

Known nested форма отклоняется локально до HTTP, unknown fields сохранены; JSON/SSE/compact используют одинаковую проверку. Есть адресное evidence, нормативное описание и safe Validation. Принятое решение не повышает задним числом достоверность аудита.

## Результаты

### Область и baseline

2026-10-06, Asia/Novosibirsk. Реализован только этап10, исполнение принятого Q-004. Достоверность исторического аудита не изменена; это не ретроспективное подтверждение дефекта. Actual gateway/writers и actual HttpClientLibrary проверены в B через fake HttpMessageHandler и локальные JSON/SSE потоки. Live codex-lb/модель/серверная валидация не проверялись, остаются19.

Входной и конечный HEAD AgentBridge: `ffd636f4ba69e58fd50c481940e91e908d0df88c` (после принятого09 `de201cf2ecfb964b10d82e1922fcb7e7f019cb8a`). На входе чужой modified Coordination.md; он не изменялся исполнителем и исключён из manifest. HTTP HEAD `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`, EF HEAD `5962deceb6ea01306cbbda400040db88ea8e5df9`: clean, read-only. LB local HEAD `f8ffbac2099a113fba54dfd8d77774f5bca80ffa`, foreign untracked `.vs/` сохранён, read-only; local source не доказательство deployed version. Commit/add/push не выполнялись.

Прочитаны задание10, результаты00/04/06/08/09, README/Decisions/OpenSpec workflow, audit Findings/15, main spec и актуальные sources; root/Documentation/Plans/CodexLb/Responses/tests AGENTS. Использован csharp-project-rules с csharp-style/build-validation/agents-maintenance. Persistence, application result API и соседние библиотеки не изменяются: backend UoW/service-result skill не требуется для локального bool validator и существующего mapping.

### Реализация и подтверждённая форма

LB `app/core/openai/requests.py:614–641`: ResponsesReasoning.summary string/null, ResponsesTextControls.verbosity string/null и format object/null; ResponsesTextFormat.type/name string/null, strict boolean/null, schema PassthroughJsonValue, extra=allow. CompactRequest имеет reasoning, без text. Tool_choice string/object/null на сервере не разрешает расширить прежнюю adapter top-level null границу; его объект не имеет подтверждённой вложенной schema. Static enum/model restrictions не добавлены.

Новый NestedControlsValidator проверяет только перечисленные known пути. Оба writer используют одну Reasoning проверку, generation writer JSON/SSE также Text. Повторы summary, verbosity, format, format.type/name/strict/schema отклоняются до HTTP; effort override запрещён независимо значения. Nullable known поля допускаются. Schema принимает любой JSON, но повтор самого известного поля schema запрещён. Unknown JSON и его дубли не обходятся рекурсивно, значения и порядок не нормализуются. Запись по-прежнему выполняется через исходный JsonElement.WriteTo, без serializer normalization. Наружные reasoning/text=null, unknown top-level и compact allowlist не расширены. Existing фиксированные безопасные сообщения Validation не включают raw input.

### Точный собственный manifest

Только `D:/Media/User/source/repos/agent-bridge`,11 файлов:

- `adapters/AgentBridge.CodexLb/Responses/NestedControlsValidator.cs` — новый внутренний validator.
- `adapters/AgentBridge.CodexLb/Responses/ResponseRequestWriter.cs` — два вызова validator.
- `adapters/AgentBridge.CodexLb/Responses/CompactRequestWriter.cs` — общий reasoning validator.
- `adapters/AgentBridge.CodexLb/Responses/AGENTS.md` — устойчивая nested граница.
- `tests/AgentBridge.CodexLb.Tests/ResponsesJsonTests.cs` — адресная общая матрица actual JSON/SSE/compact через существующий fixture; SSE production path действительно выбран callback, это не имитация JSON.
- `openspec/specs/agent-runtime/spec.md` — два current requirements с различающими scenarios.
- `Documentation/Technical documentation/14-responses-json-adapter.md` — known формы, nullable/unknown границы.
- `Documentation/Technical documentation/18-context-compaction.md` — общий reasoning и прежний compact allowlist.
- `Documentation/Plans/AgentBridge Audit Remediation/README.md` — навигация по проверенному10, без приёмки.
- `Documentation/Plans/AgentBridge Audit Remediation/Decisions.md` — датированное исполнение принятого Q-004, не изменение решения.
- `Documentation/Plans/AgentBridge Audit Remediation/10-nested-controls-contract.md` — статус/Results10; исходное задание сохранено.

EF/HTTP/LB: собственного writable manifest нет. Root/project/slnx, исторические Initial/Quality Audit/original18 changes и Coordination не менялись. New helper автоматически включён существующим compile glob адаптера, новый Solution Item не требуется. TRX/DLL находятся только в игнорируемом artifacts, generated не правились вручную.

### Before/after и контроли

До production правки добавлен только различающий test: reasoning.summary=42 и duplicate known summary, каждый в JSON/SSE/compact. Сборка before свежая. Все6 cases подтвердили Success и handler calls1: known malformed inputs проходили до HTTP. После правки те же6 inputs требуют Validation и calls0. Baseline assertions изменены на нормативную regression проверку; before TRX сохранён отдельно и не выдаётся за after regression.

After адресная матрица115:6 differentiating +79 malformed/прежние top-level границы +30 positive controls. Отклоняются wrong types/duplicates всех известных путей, top-level duplicates, effort overrides/null, outer null, unknown top-level, прежние null tool_choice/include/parallel shapes; text/tool_choice/include для compact остаются Unsupported. Positive controls проходят HTTP/calls1 и Completed, сохраняют selected effort, ordered canonical input, opaque reasoning/unknown content, unknown duplicates, строковые пробелы/пустые/неизвестные enum, known null, strict true/false/null и schema object/array/string/number/bool/null. Вложенные known-подобные имена и duplicates внутри arbitrary schema/unknown/tool_choice остаются полными. Explicit include=[] и default include отдельно проверены. Safe errors/logs не содержат payload/instructions/key.

JSON/SSE регрессия313 включает эти115 и прежние lifecycle/continuation/cancellation/disposal/compact checks; наборы пересекаются и не суммируются как independent evidence. Новый fake handler не доказывает upstream rejection, SQL, процессы, native, runtime DLL или реальные ресурсы.

### Preflight и точные команды

Cwd всех commands: `D:/Media/User/source/repos/agent-bridge`. Перед первой сборкой проверены concrete test/adapter/core/HTTP csproj, reference graph, ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/locks до D:/, existing assets четырёх проектов и isolated source/traits. Единственный source props — HTTP Directory.Build.props, меняет paths только своего HTTP test project. Custom source Exec/Target/hooks не обнаружены; core/adapters/test compile исключают outputs. EF graph не используется, packaging false не требуется. Existing NuGet sources — nuget.org и локальный SDK cache, restore/network не выполнялись. Дополнительно после первой сборки прочитаны NuGet generated imports (стандартные Test SDK/xUnit/options/binder, вручную не менялись); первый rg с literal wildcard на Windows дал path error и исправлен на `rg -g`.

Все commands ниже Exit0; builds —0 warnings/0 errors. No-build tests запускались только после успешной свежей сборки того же concrete test project/output/configuration. После последнего production build C# не менялся.

```powershell
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation10/before/
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation10/before/ --filter 'FullyQualifiedName~NestedControlsDifferentiatingRegression' --logger 'trx;LogFileName=before.trx' --results-directory artifacts/audit-remediation10/results-before
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation10/after/
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation10/after/ --filter 'FullyQualifiedName~NestedControls' --logger 'trx;LogFileName=nested-controls.trx' --results-directory artifacts/audit-remediation10/results-after
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation10/after/ --filter 'FullyQualifiedName~ResponsesJsonTests|FullyQualifiedName~ResponsesSseTests' --logger 'trx;LogFileName=responses-regression.trx' --results-directory artifacts/audit-remediation10/results-after
dotnet build adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/audit-remediation10/after/
& 'D:/Media/User/AppData/npm/openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive
```

TRX: `artifacts/audit-remediation10/results-before/before.trx` —6 passed/0 failed/0 skipped; `artifacts/audit-remediation10/results-after/nested-controls.trx` —115/0/0; `artifacts/audit-remediation10/results-after/responses-regression.trx` —313/0/0. Source/output before и after раздельны.

CLI выполнен по постоянному exact разрешению пользователя09, без повторной permission. Wrapper/package прочитаны: package1.14.1 не изменился относительно evidence09; дополнительный version/help не запускался. Actual strict main: valid=true, issues=[],1 passed/0 failed, Exit0; JSON version1.0 — schema вывода, не версия CLI. Original18 не изменены и повторно не валидировались. Main SHA256 после strict: `D774A8F8BFB290A3BAFC57A914F74FAC070B446D5D385C6EBDB2991425315D12`; после run main не менялась. Sync/archive/install не выполнялись.

### Артефакты и итоговые ограничения

SHA256:

| Артефакт | SHA256 |
| --- | --- |
| after/Debug/net10.0/AgentBridge.CodexLb.dll | `0D10A37F44F07C405A260FADA5BF16CC900B0939A9AD3E8F5F98E7E9E2803711` |
| after/Debug/net10.0/AgentBridge.CodexLb.Tests.dll | `CA3532D0A4C3A2385BE07A9B3CE4391916034F07468B36E7DCB82D9517009E30` |
| results-before/before.trx | `2ECAF50A148030309C7B973D44982991A16CCDAB824080A6C1E0AA21CC3EC19B` |
| results-after/nested-controls.trx | `AE1C11A11D0608335A853D39AA98E1B3B906A5A36CA506B57443932E03C94E49` |
| results-after/responses-regression.trx | `9CA7AD713DF69C3CB375BA037A89126F00494E7D18538459001209EC45F69E2F` |

Пути артефактов относительно `artifacts/audit-remediation10/`. Read-only Git status/diff/stat/name-only/diff --check/log/rev-parse, UTF-8 strict decoder и EOL/non-ASCII checks выполнены; actual11 own files UTF-8 без BOM/LF, U+FFFD/четыре вопросительных знака/проверенные mojibake markers отсутствуют. Предупреждения Git о future LF→CRLF не означают изменения actual EOL. Final inline read-only check:11 файлов/72 существующие локальные ссылки/issues0, XML counters трёх TRX подтверждают6/115/313 passed и0 failed/skipped; diff --check Exit0, main hash совпадает. Первый вариант encoding-check получил PowerShell ParserError из-за literal mojibake-pattern с типографской кавычкой, повтор с Unicode regex escapes завершился Exit0; файлы при ошибке не менялись. Новых бизнес-вопросов/блокеров локальной A/B нет.

Application/hosting/Docker/real HTTP/БД/SQL/native/business process/deployment, restore, пользовательские scripts, tooling вне exact разрешённой CLI команды не запускались. Endpoint/deployed source неизвестны, live evidence остаётся19. Этап готов к приёмке A/B; до явной приёмки и поручения commit исполнитель останавливается.

### Приёмка10

2026-10-06 координатор от имени пользователя принял10 в локальных A/B границах Q-004. Независимо сверены full source/helper/writers/test diff, actual local requests.py shape, Results, свежие command outputs/CLI valid, TRX6/115/313 rows/counters/skipped0, UTF-8/LF11 и main SHA256 после strict/diff. Before6 с old Success/calls1 и after тех же inputs Validation/calls0 приняты; пересекающиеся115/313 не суммированы. Known null/unknown duplicates/arbitrary schema/effort/top-level границы сохранены. Live19 остаётся отдельно, историческая достоверность аудита не повышена.

Явно поручен actual local English Conventional Commit только exact11 manifest выше, без Coordination и соседних репозиториев. Перед commit обновлены только статусы/приёмка в трёх документах плана; C#/main после проверки не менялись. Полный hash, postcommit status и финальные docs checks передаются координатору отдельным сообщением; push/PR/amend не выполняются.
