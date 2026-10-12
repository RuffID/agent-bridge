# Готовый prompt: принять B03 и завершить только Ledger03

Проверь и прими новую B03-BoundedHistory DLL-поставку в AquaByte Ledger, затем заверши **только полный mandatory этап03 ИИ-помощника**. Library B03 завершён в isolated/DLL delivery границе; Ledger integration ещё не принята. Передача scope не расширяет.

## Репозитории и разрешения

Рабочий репозиторий: `D:\Media\User\source\repos\work\AquaByte-Ledger`. Разрешаю необходимые точечные изменения для проверки/приёмки новой поставки и реализации только03, содержательные isolated tests/Testing TestServer без реальной сети/Kestrel, local-cache-only restore/compile-check/fresh coverage и актуальную документацию03. Принятие kit выполнить после проверки manifest/полной closure/API/props/exact dependencies; затем revalidate affected Ledger consumers. Это отдельная новая приёмка, не переписывание Completed01A/02 history.

`D:\Media\User\source\repos\agent-bridge` — только чтение source/API/документов/нового kit/evidence. Код AgentBridge и другие checkouts в этом чате не менять. Не копировать runner/gateway/executor/compactor/persistence в Ledger; не добавлять собственный SQL reader или truncated model history.

Запрещены Git, сеть для restore, реальные HTTP/model, БД/SQL/backup/migrate, Docker/browser/hosting/Kestrel/dev/watch/publish/deployment/production startup/DI/maintenance activation. Production ordinary Program/ConfigureServices assistant module не активировать. Business code Ledger/migrations/closing plan/04–09 не менять. `AquaByteLedger.Web\Config\config.json` **не читать/копировать/изменять/включать в outputs**. Project scripts/executables отдельно не запускать: при необходимости сначала подготовить reviewable inputs и запросить разрешение на точную команду. Locks вручную не редактировать; нужный restore/update lock выполняется dotnet offline в разрешённой приёмке, с последующей locked verification.

Manual changes через apply_patch, сохранять encoding/newlines/кириллицу, проверять повреждённые строки/U+FFFD. Assertions/thresholds/exclusions не ослаблять. Missing cached dependency/actual mandatory capability — точный blocker, без обхода и расширения scope.

## Сначала прочитать

Все применимые Ledger AGENTS от root до WebAssistant/Web Pages/Services/tests и библиотеки поставки; csharp-project-rules и style/build/AGENTS references, профильные ASP.NET/options/HTTP/ServiceResult/PageModel skills для затрагиваемого кода. `Documentation\Testing.md` задаёт actual commands/guards.

В `Documentation\Plans\ai-assistant-widget-2026-10-10\` прочитать все:
`main.md`, `analysis.md`, `architecture.md`, `00-decisions-and-contracts.md`,
`03-dialog-api-and-authorization.md`, `03-bounded-history-change-request.md`,
`03-preparation-20261011.md`, `01a-acceptance-20261011.md`,
`01a-storage-and-recovery-contract.md`, `02-configuration-and-storage.md`,
`02-setup-and-maintenance.md`, `02-acceptance-20261011.md`.

Изучить actual WebAssistant adapters/contracts, ActualHistoryLoadBoundaryTests и accepted DLL/XML/props/manifest/locks. В AgentBridge прочитать:
`Documentation\Technical documentation\31-bounded-history.md`,
`30-dialog-catalog-and-recovery.md`, `29-dll-and-nuget-delivery.md`,
`Documentation\Plans\B03 Bounded History\README.md`, `Evidence.md`, `Packaging-commands.md`,
и corresponding actual source/query/runner/lifecycle/recovery API при сомнении.
Проектируемые signatures не выдавать за существующие. Compact catalogue и durable recovery01A **уже существуют**.

## Actual состояние Ledger

- 00/01/01A/02/P00–P04 Completed в documented isolated boundaries.
- 03 In Progress: независимая подготовка выполнена, HTTP handlers не подключены; B03 library blocker теперь реализован, но новая DLL-поставка **ещё не принята Ledger**.
- 04–09 Not started. Production activation отсутствует; ordinary Program/ConfigureServices assistant module не активируют.
- Уже есть cookie/session resolver, immutable server-owned Ledger profile gate, bounded JSON parser/safe HTTP mapping и active owner/dialog/turn registry.
- Registry имеет **явную single-instance demo boundary**; он не обеспечивает durable completed-turn dedup, distributed Stop или restart recovery. Frozen generations одного demo instance должны разделять registry; нельзя выдавать active reservation за durable journal.
- Прежняя accepted manifest SHA-256 `libs\AgentBridge.SqlServer\delivery.manifest.json`:
  `5fd8ddabe234857a68a79d49a74079e384920e7a122af7536dc54b7b97d9206a`.
  Все22 entries повторно проверены в AgentBridge task, Ledger kit не заменялся.
- Historical Ledger03 preparation: `artifacts\assistant03-20261011`,1506 passed,0 failed/skipped,68 новых stage03 cases,шесть builds0 warnings/errors,unchanged coverage thresholds passed. Это не acceptance новой DLL или новых handlers.

## Новая полная поставка B03

Kit: `D:\Media\User\source\repos\agent-bridge\artifacts\b03-delivery-20261011\SqlServer`.

Manifest SHA-256:
`fd66709eb625acc138b90ed5da1834bf0a988bb10258630ef43ecb4a84dac633`.

Schema3 `dll-nuget`,net10.0/Release,10 local DLL/9 XML/3 props/12 required package roots; RIDs win-x64/linux-x64/linux-arm64. Все own DLL/XML hash-identical трём source SDK kits. Exact EF10.0.12/SqlClient7.0.2; import `AgentBridge.Delivery.props` **после application PackageReferences**, required roots/RID/lock validation сохранить. Не понижать SqlClient, не удалять native assets/roots и не обходить exact guards. Generated manifest/props/locks вручную не редактировать.

Actual new public contract:
- `AgentBridge.Application.Models.DialogHistoryLoadLimits(int maxRecords, long maxRecordBytes, int maxQueries)`; immutable positive explicit limits без defaults и checked derived MaxPayloadBytes.
- `AgentBridge.Application.Ports.IBoundedDialogReader : IDialogReader`, property `LoadLimits`; inherited actual `ReadAsync(DialogAccess, CancellationToken)` и `ReadCatalogAsync(DialogCatalogAccess, CancellationToken)`, full snapshot либо refusal.
- `AgentBridge.Persistence.SqlServer.SqlServerPersistenceRegistrationExtensions.AddAgentBridgeSqlServerBoundedHistory(IServiceCollection, DialogHistoryLoadLimits)` вместе с existing SqlServer/persistence/facade registration до generation provider build.
- `AgentRunner.RunBoundedAsync(AgentRunRequest, callback?, CancellationToken)` — общая actual RunCoreAsync, strict port/policy до первого history I/O/providers/model.
- `DialogHistoryLoadLimitException.Error` = existing **Rejected** + fixed `history_load_limit_exceeded`; `DialogHistoryLoadConflictException` = `history_load_changed_during_query`. Reader возвращает safe semantic refusal/Data=null; cancellation/неожиданные errors не маскируются. Не отдавать эти raw errors наружу.
- Legacy ReadAsync/RunAsync/constructors ABI сохранены; без явной policy они не получают B03 guarantee. Legacy reader.LoadLimits бросает `bounded_history_policy_required`; не закрывать blocker одним marker/custom port.

Source:
`AgentBridge\Application\AgentRunner.cs`,
`Models\DialogHistoryLoadLimits.cs`, `Ports\IBoundedDialogReader.cs`,
`Results\DialogHistoryLoad{Limit,Conflict}Exception.cs`;
EF `Reading\HistoryLoadBudget.cs`, `HistoryQueryGuard.cs`, `IHistoryRecordSizeProvider.cs`, `DialogReader.cs`,
`UnitOfWork\PersistenceOperationGate.cs`, все seven `Repositories\*Queries.cs`;
SqlServer `SqlServerHistoryRecordSizeProvider.cs`, `SqlServerPersistenceRegistrationExtensions.cs`.
Compile-only actual sample: `tests\Delivery\Consumer\BoundedHistoryRegistration.cs`.

### Гарантия и ограничения

Выбрано bounded чтение, без hard write cap/schema/migrations. Supplied SQL Server query paths используют existing EFCoreLibrary base Query: scalar size preflight Take(remaining+1), independent server payload size predicate/Take(remaining), без SQL/DbContext bypass. В одном gate operation ≤R полных rows, каждая ≤B accounting bytes, суммарно≤R×B; ≤Q query pairs и≤Q×(R+1) scalar long values. Все reports/continuation/errors/tool journals/root/runtime/settings/profile/policy/feed/recovery provenance/contexts/internal state loaders включены.

Accounting SQL Server=256+2×DATALENGTH всех variable columns всех10 mapped tables, включая binary owner/complex reports. Это **per-operation conservative storage payload bound**, не exact CLR heap/whole-run lifetime/server scan/work cap. Supplied generation обязана использовать actual bounded queries/gate/reader; не override/custom-port ради зелёных tests. SQL Server поддержан; другие providers/custom readers/manual legacy constructors не объявлены bounded.

History остаётся полной по protocol/function pairs; overflow отказывает, без UI/model truncation/retry/refill/replay/implicit new chat. Read refusal canonical unchanged. Internal writer loader overflow до save проходит обычный rollback/cleanup. Если runner finalization тоже overflow, TerminalSaved=false; Ready не выдумывается. Unknown/cleanup failures требуют actual fresh authoritative state/recovery, без replay. Existing structural recovery4096 operations/pending1024 validation не ослаблена. Owner/site/agent/profile/root/retention/tombstone/rechecks/CAS/incarnation/fencing сохраняются; B03 legacy read также отказывает по expiry до children. Policy не ограничивает размер новых writes атомарным hard cap: следующая операция может получить честный overflow.

Новые explicit host limits для library load должны быть валидированы и captured frozen generation; они считаются в records со всеми metadata/rechecks, а не UI message count. Обоснуй значения по actual accounting и accepted input/output limits. Не выдавай их за существующие defaults02 и не увеличивай их автоматически после отказа.

## Fresh AgentBridge evidence

`D:\Media\User\source\repos\agent-bridge\artifacts\b03-bounded-history-20261011`:
- Core542/CodexLb414/persistence470 =1426 passed,0 failed/skipped;48 new B03 cases.
- Persistence только Dependency!=Database; bytes/count/query boundary/limit+1,20 малых turns,5000 provenance records,крупные items/reports/compact/root/profile,authorization/races/cancel/rollback/function pairs/Unknown/recovery/old-turn/fencing.
- Fresh coverage `coverage/{core,codexlb,persistence,merged}.cobertura.xml`; только3 current full reports, unrounded `production-rates.json`: core94.99/91.08,EF91.97/78.96,SqlServer91.76/84.62,CodexLb96.06/87.27 line/branch%. AgentBridge threshold config не обнаружен; Ledger thresholds не приписывать этим results.
- Debug3/Release3 RID/raw binary API probe3 builds0 warnings/errors,SDK10.0.401; `sdk-own-hashes.json`, `source-fingerprints.json`, input inventories и `public-api-xml.json`.
- Пользователь отдельно разрешил и выполнены именно4 packaging commands; Prepare-DeliveryTests/Metadata build hooks не запускались.
- External **full kit/props** consumer вне repo: `C:\Users\Spike\AppData\Local\Temp\AgentBridge.B03.20261011`, три offline locked restore/build0 warnings/errors.
- Windows external binary29 passed,0 failed/skipped: compatibility3,actual bounded query15,focused manifest/XML/PE/source closure/native metadata/API11. Семь expected negative exact/duplicate/framework/missing/unsupported RID/lock-disabled/locked-drift guards; locks вручную не редактировались.
- `packaging-results.json`, `negative-guards.json`, `binary-tests.log`, `binary-consumer-<rid>-{restore,build}.log`; reproducible source/independent generated locks `binary-consumer-inputs`, hashes `binary-consumer-input-hashes.json`.
- SQL/provider concurrency/query plans/restart/native loading/Linux runtime/live/production acceptance **не выполнялась**. Metadata reading PE/ELF не native execution.

## Конкретные работы Ledger — весь mandatory03

1. Проверить kit manifest SHA/полные hashes/portable closure/XML/actual API/exact roots. Принять комплект целиком в `libs\AgentBridge.SqlServer` по existing vendor workflow, не смешивать old/new files. Revalidate local-cache-only package/locked graph и затронутые binary adapters/consumers. Сохранить historic01A/02 Completed evidence; новую приёмку B03/03 оформить отдельно.
2. Подключить explicit bounded policy в captured assistant generation, strict IBoundedDialogReader для History и actual RunBoundedAsync для Send. Scope/resources await-ятся, generation не меняется посреди run. Не использовать full legacy reader с проверкой/Take после загрузки. Existing102 legacy-load test сохраняет смысл legacy regression; добавить actual new bounded acceptance до I/O, не переписать его как доказательство UI truncation.
3. Реализовать **Bootstrap, bounded List/Search, Create, History/Status, Send POST NDJSON, Stop, Delete**. Наличие parser/gate/registry не считать готовыми handlers. Использовать cookie owner/session, existing CSRF для POST/stream; Enabled и explicit limits; неизвестные page/object/input/override/duplicate fields закрыты. Owner/site/agent/profile не принимаются от browser.
4. Server-owned immutable Ledger profile gate до catalogue/history/model/disclosure, с переавторизацией после awaits/перед каждым generation/compact step и раскрытием результата. Потеря любого необходимого права блокирует весь чат до восстановления; new chat только explicit user action. Никаких reasoning/canonical reports/tool arguments/results/секретов в JSON/NDJSON/history/title/snippet/logs.
5. Safe JSON/HTTP/stream mapping: fixed allowlist error/DTO, корректный status/envelope до stream, started/textDelta/terminal turnId+sequence после старта, final allowed text/terminal status/terminalSaved. Abort/transport failure/save failure/Unknown не выдавать за success; отсутствующий terminal означает обрыв. B03 overflow — safe understandable refusal, без implicit retry/refill/new chat.
6. Durable completed-turnId dedup через actual persisted library lifecycle; active registry этого не заменяет. Existing turn не запускается повторно; concurrent turn/run явный conflict. После uncertain response Status/History по прежним IDs. Не refresh/retry CAS ради сохранения stale result.
7. RequestAborted/explicit Stop связываются с actual run, без replay/background worker. Stop owner-scoped; не объявляет terminal/save и не освобождает slot до await завершения. Single-instance demo boundary остаётся явной; distributed Stop/restart гарантию не выдумывать.
8. Actual01A terminal/save/Unknown/readiness/lifecycle/recovery/fencing и explicit owner acknowledgement: пока active/pending/recovery не финализирован, next Send получает pending/conflict; confirmed outputs/original terminal не переписываются. Explicit новый вопрос после Ready — тот же DialogId,новый turnId,новый captured page context. Delete/expiry/late callbacks не восстанавливают чат и не обходят active lease/CAS.
9. Fresh содержательные isolated checks всех mandatory endpoints и actual binary library paths: cookie/CSRF/Enabled/revoke/profile до load/model/disclosure,JSON/bytes/count/overflow bounds,UI/history allowlist,no canonical/secrets,full model payload guard,input/NDJSON/rate limits,dedup/concurrency,Stop/RequestAborted/save/Unknown/recovery/function pairs/fencing/delete/expiry,frozen generations/scoped resources/cleanup failures. Testing/TestServer без Kestrel/real network/production config; Docker/SQL/live не запускать. Commands/fast filters/coverage — Documentation/Testing.md; threshold не ослаблять.

Модельный профиль **сохранить**: `gpt-6.1-sol/medium`,
TokenThreshold24000,InputTokenReserve8000,
UnknownModelEstimateEncoding=o200k_base,BudgetPolicy=ServerValidation,
example ModelEncodings пуст. Approximate явно разрешён; exact Sol encoding не подтверждена — не назначать неподтверждённый mapping/не возвращать exact-only blocker. InputContextWindow800000 — transport-declared metadata, live Responses/compact acceptance не выполнялась. Полный model payload продолжает проверять actual library ContextBudgetGuard; probe не заменяет его.

## Завершение и передача

Ledger tools/widget/frontend/instruction catalogue/метрики вне03.04–09 и production activation запрещены.03 Completed **только** после всего перечисленного mandatory объёма и новой содержательной isolated acceptance, целостной поставки, актуальных current docs/owning AGENTS. Source/build/doubles не объявлять SQL/restart/production acceptance.

В конце сообщи actual изменения/API/kit path/manifest hash/tests/evidence/ограничения и статус03. Если возникает missing mandatory capability — работай до точного blocker, не копируй библиотечный код/SQL bypass и не расширяй scope.

**Обязательно выдай обновлённый готовый самодостаточный prompt передачи, если понадобится ещё один чат: actual состояние, files/contracts/kit/hash/evidence, конкретный остаток/blocker, ограничения, разрешённые проверки и критерии завершения. Передача не расширяет scope;04–09 автоматически не разрешаются.**
