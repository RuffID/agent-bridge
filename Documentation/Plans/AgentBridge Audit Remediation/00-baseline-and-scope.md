# 00 — Исходное состояние и границы исправлений

[Навигатор](README.md). Статус: **принят в A-границе**. Зависимости: нет. Область: документирование подготовки.

## Цель и основание

Зафиксировать актуальную отправную точку перед реализацией. Источник — [итог аудита15](<../AgentBridge Quality Audit/15-final-reconciliation.md>), [Findings](<../AgentBridge Quality Audit/Findings.md>) и [OpenQuestions](<../AgentBridge Quality Audit/OpenQuestions.md>). Audit HEAD41072fc и версии соседей — исторические сведения, которые нельзя автоматически принять за состояние будущего исполнения.

## Работы

1. Сверить текущие AGENTS, нормативные требования и исходники для ABQA-001–010; составить карту «ID → владеющий компонент → этап → проверки». Сохранить различие дефекта, подозрения, пробела и неоднозначности.
2. Зафиксировать версии/хеши исходников AgentBridge, EFCoreLibrary, HttpClientLibrary; для Q-005 — локального и целевого codex-lb. Чужие изменения отметить и исключить из manifest. Git использовать только по разрешению конкретных read-only операций.
3. Для очередного этапа согласовать область: ядро/адаптер/тесты AgentBridge либо конкретная соседняя библиотека. Разрешение одной области не переносится на другую.
4. Составить список конкретных production/test `.csproj`, проверить build imports, packaging hooks и trait-фильтры. Недоступные assets/CLI не восстанавливать и не устанавливать автоматически.
5. Для будущих17–19 уточнить оставшиеся Q-002 параметры по [принятым решениям](Decisions.md): .NET10/Windows11/Ubuntu, три RID и основной MSSQL уже выбраны; ещё нужны версии/редакция сервера, runtime environments, server backup path, permissions/TLS, endpoint/deployed commit/exact models, небольшой бюджет и cleanup. Live подключение отложено. Подготовка списка не разрешает запуски.
6. Передать09–11 согласованные Q-003–005: main spec актуальна, known nested controls валидируются до HTTP, compact сохраняет FIFO/явно отклоняет неопределимую связь. Решения не означают выполненные CLI/tests. Q-001 не открывать повторно без нового факта.

## Проверки

Только A: пути, локальные ссылки, версии по доступным файлам, соответствие ID и источников. Отдельно отметить непроверенные runtime свойства. Не запускать код, CLI, scripts, restore, БД или сеть ради baseline.

## Результат и критерии завершения

- Есть актуальный manifest ближайшего этапа и карта всех ABQA/Q без потерь и новых требований.
- Для каждой соседней области известны владелец и необходимое поручение.
- Отсутствующее окружение записано блокером соответствующей проверки; локальные исправления остаются доступными.
- Следующий этап получает проверяемый контрпример и положительные контроли.

## Результаты

### Дата, полномочия и actual baseline

2026-10-06, Asia/Novosibirsk. Выполнен только уровень **A**: чтение текущих требований, инструкций, проектов, исходников, локального Git и статическая проверка файлов. Единственный собственный manifest записи — `agent-bridge/Documentation/Plans/AgentBridge Audit Remediation/00-baseline-and-scope.md`: статус и этот раздел. Исходное задание сохранено. На момент первичной передачи приёмка и коммит ожидались; последующая приёмка записана в конце раздела.

Поручение пользователя, переданное координатором, разрешает необходимые исправления этапов00–20 в четырёх явно названных репозиториях и отдельные локальные commits после приёмки. **На00 это разрешение ограничено документацией/A**; следующие этапы здесь не начаты. Запреты запуска00 имеют приоритет над обычным compile-check/test workflow скиллов. Coordination, README, slnx и исторические отчёты не входят в собственный manifest.

| Репозиторий / ветка | HEAD, повторно прочитанный на00 | Состояние на входе / версия из исходников |
| --- | --- | --- |
| `D:/Media/User/source/repos/agent-bridge`, `master` | `686a0dbb42e7d6baba24b71277b636f182550c3c` | Уже изменён `Documentation/Plans/AgentBridge Audit Remediation/Coordination.md` координатором; index пуст. План отдельным commit `3c316f046d028c955d7a1ea057b42847f98899f5`, проверен через local show. SDK library `net10.0` |
| `D:/Media/User/source/repos/work/EFCoreLibrary`, `master` | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Чисто; `EFCoreLibrary.csproj:9` Version `0.0.5`, `net10.0`; только чтение |
| `D:/Media/User/source/repos/work/HttpClientLibrary`, `master` | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Чисто; `HttpClientLibrary.csproj:7` FileVersion `0.0.0.5`, TFM `net8.0;net10.0`; только чтение |
| `D:/Media/User/source/repos/codex-lb`, `main` | `f8ffbac2099a113fba54dfd8d77774f5bca80ffa` | Чужой untracked `.vs/`; исключён целиком, не изменялся. Это **local**, не deployed HEAD |
| Целевой codex-lb | Не установлен | Endpoint/deployed commit/exact models не предоставлены: блокер D19, не основание заменять их local HEAD |

Audit HEAD `41072fc37bb718dd9f4bb0473912802260bb9164` и Initial evidence остаются историей. Совпадение HEAD соседних библиотек с аудитом не превращает прежние TRX в свежие проверки. Текущие исходники сверены отдельно.

Прочитаны root/Documentation/Plans AGENTS, применимые Domain/Application/CodexLb/Responses/EF adapter/UnitOfWork/tests/Delivery правила; root обеих библиотек, EF maintenance/Coordination/EfCore/Errors/Processes/Sqlite/Backup/tests и HTTP Clients, root codex-lb. Использованы `csharp-project-rules` с build-validation и `backend-uow-repositories` с backend-uow для статической оценки границ. Основание: README/Decisions этого плана, Findings/OpenQuestions/итог15 аудита и текущая `openspec/specs/agent-runtime/spec.md`. Main не переписана на00: нормативное согласование MSSQL/configuration/known nested/FIFO выполняется в соответствующих09–15; Decisions уже приняты, повторный выбор не требуется.

### Карта ID → источник → владелец → этап → проверки

В путях ниже **AB** — корень AgentBridge, **EF** — корень EFCoreLibrary, **HTTP** — корень HttpClientLibrary, **LB** — корень local codex-lb. Номера строк относятся к actual source на указанных HEAD. A подтверждает control flow, но не исполнение контрпримера.

| ID / категория и состояние | Actual source evidence на00 | Владелец / этапы | Требуемое различающее evidence |
| --- | --- | --- | --- |
| ABQA-001, расхождение документации, **сохраняется актуальный статический контрпример** | `AB/Documentation/Plans/README.md:7–9` уже различает завершённую Initial implementation, частичный A-аудит и новый план; навигация частично обновлена. Однако `Documentation/Technical documentation/01-architecture.md:52` объявляет названия без явного статуса реализации будущими, а строка66 содержит `AgentSettingsService` без такого статуса, хотя `AB/Application/AgentSettingsService.cs:10` определяет существующий класс. Это остающееся stale description того же ABQA-001 | Документация AB;08,20 | A08: устранить конкретное расхождение статуса AgentSettingsService и сверить current descriptions/API/checkpoints по затронутым страницам; old reports сохранить. Частичная актуализация навигации не снимает finding; архитектурный документ на00 не изменён |
| ABQA-002, **подозрение**, S2 предварительно | `HTTP/Models/HttpStreamResponseResult.cs:22–32`: Body.Dispose/DisposeAsync перед response.Dispose без finally; `AB/adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs:95–111` await using | HTTP владеет wrapper; AB владеет propagation adapter;06,16,19 | B06: throwing Body disposal в обоих путях, наблюдаемый response, read/callback primary + cleanup; обычный cleanup контроль. Утечка/достижимость на реальном потоке не доказаны; D19 отдельно |
| ABQA-003, подтверждённый **пробел CLI evidence** | В `AB/openspec/changes/`18 текущих `tasks.md`; Initial25 и audit15 фиксируют невыполненную CLI validation, не syntax failure | OpenSpec AB;09,20 | B09: actual CLI/version, main + все18 исходных changes; отсутствие run на00 явно сохранено. Validation не разрешает archive автоматически |
| ABQA-004, расхождение old delta/main; Q-003 **решён** | `openspec/changes/responses-json-adapter/specs/agent-runtime/spec.md:21` сохраняет промежуточный Unsupported compact; main содержит отдельный JSON compact transport и принятие compact; `Decisions.md`, Q-003 устанавливает приоритет main | Workflow/spec AB;09 | A/B09: current workflow, точный main/delta mapping и CLI evidence; old deltas история, запрет compact не переносится повторно; реализации09 ещё нет |
| ABQA-005, дефект **подтверждён статически**, S3 | `AB/Domain/Dialogs/Dialog.cs:73–128` принимает no-turn/context-only revision1, context=t0+1min, LastChanged=t0+2min; `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogStateLoader.cs:14–23` передаёт persisted даты в Restore | Domain AB;05,16,17 | B05: фабрика отклоняет контрпример; valid append round-trip, same-time mutations, repeated compact сохранены. C17 actual corrupt input отдельно; штатные writes не названы генератором corrupt state |
| ABQA-006, дефект **подтверждён статически**, S2 | `EF/maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs:46–56,107–113`: CREATE → Discovery; Pending failure + successful unpin обходят poison, lease освобождается. `SingleInitializerGate.cs:9–26` проверяет poison после wait | EF coordinator/gate; AB adapter regression;01,16,17 | B01: post-CREATE Pending failure/OCE, waiter и next scope заблокированы до boundary calls; controls ниже. C17 actual provider effects отдельно |
| ABQA-007, дефект **подтверждён статически**, S3 | EF `Coordination/DatabaseMaintenance.cs:53–79,111`: await using не сохраняет primary; `EfCore/EfMigrationOperations.cs:40–43` cleanup даёт null PrimaryError; `Processes/BackupProcessRunner.cs:29–35` и `../EFCoreLibrary.Maintenance.Sqlite/Backup/SqliteBackupStepper.cs:32–35` заменяют первичную причину | EF pin/process/native lifecycle;02,16,17 | B02: safe primary+secondary во всех трёх путях, typed failure vs caller/deadline, unknown cleanup poisons. Doubles не actual OS/native/C17 |
| ABQA-008, дефект **подтверждён статически**, S3 | `AB/adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs:112–114` после EOF не проверяет HasData, catch127–130 проверяет; `ResponseSseState.cs:121–137` может создать empty Canceled | CodexLb AB;04,16,19 | B04: HTTP2xx/empty EOF/successful disposal cancels caller → original-token OCE, callbacks0; empty/no-cancel Incomplete, partial/terminal/explicit failure контроли; D19 отдельно |
| ABQA-009, дефект **подтверждён статически**, S3 | `AB/Application/AgentRunScope.cs:12–20` cleanup-only OCE распространяется; `AgentRunSession.cs:104–113` уже приняла step/token и блокирует writes; `AgentRunner.cs:155–175` при canceled caller маскирует cleanup OCE как Canceled/Error=null | Application AB;03,16,19 | B03: accepted append + caller cancel + отдельная cleanup OCE → exception/LastResult без потери подтверждённого step; caller-only и primary+cleanup контроли, no replay. `ExpiredDialogCleanup.cs:68–98`/одноимённые tests — независимый контроль |
| ABQA-010, **test cleanup gap**, S4 | `AB/tests/AgentBridge.Persistence.EfCore.Tests/UnitOfWorkScopeTests.cs:125–140` assertions до release/await first; `EF/tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs:144–166` аналогичный fail.Task без finally | Тесты AB/EF, без production fix;07,16 | B07: normal и forced early assertion/timeout paths завершают и await все started tasks; `ContextCompactorTests.ActivationWaitsForSuccessfulSave` использует finally как контроль. Не выдавать за production deadlock |
| Q-002, **частично определён** | Decisions: .NET10, Windows11 → Ubuntu ASP.NET Core, основной MSSQL, сохранение SQLite/PG, RID win-x64/linux-x64/linux-arm64 | Владелец внедрения;00,12–19 | A00 ресурсы перечислены ниже; C/D17–19 заблокированы ресурсами/точными permissions. Локальные01–16 доступны независимо |
| Q-003, **решение принято, implementation/CLI evidence нет** | Decisions + main/old transport delta из строки004 | Workflow AB;09,20 | Закрепить main как действующий контракт; проверить18 changes; не переоткрывать политику compact |
| Q-004, **решение принято, implementation evidence нет** | `AB/adapters/AgentBridge.CodexLb/Responses/ResponseRequestWriter.cs:59–61,84–94` копирует reasoning properties и проверяет лишь outer object/no effort; `LB/app/core/openai/requests.py:614–618` summary=str/null | CodexLb AB/контракт;10,19 | B10: reasoning.summary=42 и duplicate known summary → Validation, HTTP calls0; valid string/null и unknown nested data полностью сохранены; JSON/SSE/compact одна граница; D19 отдельно |
| Q-005, **решение принято, final subset/live evidence нет** | `AB/Application/ToolExecutor.cs:192–224` Queue/Dequeue FIFO, ContextBuilder:133–167 баланс. `LB/app/core/openai/requests.py:1348–1370,1624–1671` последний unmatched/pop и output cutoff следующим call | AB pairing/compact + LB OpenSpec-first;11,19 | B11: callA(x),callB(x),outA(x),outB(x), разные args/results; FIFO0→2/1→3, выбор каждого occurrence/final subset. Невосстановимая связь → отказ compact, accepted window/history сохранены, no replay. Local helper trace не live loss/replay |

Q-001 уже закрыт организационным evidence, не открывается. Карта содержит10 находок и Q-002–005; новые product требования из test gaps не введены. Переданное разрешение соседних репозиториев действует только в явно порученном этапе: EF01/02/07, HTTP06, LB11 по OpenSpec-first. На00 они read-only; обход библиотек через SQL/EF/HTTP запрещён.

### Конкретные проекты, imports, packaging и фильтры

В AB статически разобраны11 `.csproj`; все literal ProjectReference существуют. Все — SDK-style libraries `net10.0`, nullable; потребитель/build delivery сейчас `win-x64`. Полный список:

- Production: `agent-bridge.csproj`; `adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj`; `adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj`; `adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj`; `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj`.
- Tests: `tests/AgentBridge.Tests/AgentBridge.Tests.csproj`; `tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj`; `tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj`.
- Delivery: `tests/Delivery/Build/AgentBridge.Delivery.csproj`; `tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj`; `tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj`.
- EF chain: `EFCoreLibrary.csproj`; `maintenance/EFCoreLibrary.Maintenance/EFCoreLibrary.Maintenance.csproj`; optional `maintenance/EFCoreLibrary.Maintenance.{Sqlite,PostgreSql,SqlServer,MySql}/EFCoreLibrary.Maintenance.{Sqlite,PostgreSql,SqlServer,MySql}.csproj` (четыре существующих проекта); `tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj`. Core maintenance → EF root; каждый optional → core; EF tests → все четыре optional.
- HTTP: `HttpClientLibrary.csproj`; `HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj`, оба `net8.0;net10.0`. LB не имеет C# project для Python pairing: `pyproject.toml`, `uv.lock`, `app/core/openai/requests.py`; его CLI/tests на00 не запускались.

AB core исключает adapters/tests/test и nested bin/obj/artifacts. CodexLb → core+HTTP, EF adapter → core+EF root+SQLite/PG maintenance; migrations → EF adapter. Persistence tests дополнительно ссылаются на CodexLb и обе migrations, поэтому цепочка будущего build включает HTTP и оба existing providers, даже при isolated filter. SQLServer migrations/integration project в AB пока отсутствует: это planned12, не missing existing reference.

Проверены source `.csproj/.props/.targets` и наличие `Directory.Build.props/targets`, `Directory.Packages.props`, `NuGet.config`, `global.json` на ancestor-путях трёх C# репозиториев до корня диска, а также nested конфиги/lock-файлы. Единственный найденный общий source import-файл — `HTTP/Directory.Build.props`: только для HttpClientLibrary.Tests перенаправляет output/intermediate в `HTTP/artifacts/{bin,obj}/HttpClientLibrary.Tests/`. Custom source `<Target>`/`<Exec>` не найден; это не аудит installed SDK или package-generated build hooks.

**Packaging hook:** `EF/EFCoreLibrary.csproj:7` задаёт `GeneratePackageOnBuild=true`, поэтому **во всей future EF chain нужен `-p:GeneratePackageOnBuild=false`**; optional/core maintenance/tests уже задают false. AB IsPackable=false; явные pack/publish/Rebuild не нужны. `tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj:12` импортирует `$(AgentBridgeDeliveryRoot)/AgentBridge.Delivery.props`; actual props содержит binary lib DLL/XML и native `win-x64/e_sqlite3.dll`, без source references/Exec. Build выбирает `DeliveryProvider` в ProjectReference; смена варианта требует отдельного restore, она не делалась. Иной переданный consumer props перед future build надо проверять заново.

AB/EF isolated tests: Test SDK18.0.1, xUnit2.9.3/runner3.1.5; HTTP Test SDK17.14.1, xUnit.v3 3.0.1/runner3.1.1. В AB persistence шесть Integration-классов помечены `[Trait("Dependency", "Database")]`: PersistenceIntegrationTests, MaintenanceIntegrationTests, AgentRunnerIntegrationTests, DialogSettingsIntegrationTests, ExpiredDialogCleanupIntegrationTests, CrossComponentIntegrationTests. **Обязательный future filter `Dependency!=Database`**; для адресного01 дополнительно `FullyQualifiedName~AgentBridge.Persistence.EfCore.Tests.DatabaseMaintenanceTests`. EF maintenance tests используют fake DB/native/process/command границы; Database traits не найдены. Адресный EF01 filter: `FullyQualifiedName~EFCoreLibrary.Maintenance.Tests.CoordinatorTests`. Сам filter не заменяет чтение нового теста на отсутствие real I/O.

`project.assets.json` найдены для19 из20 перечисленных C# проектов (HTTP tests — в redirected artifacts/obj); у AB binary Consumer assets отсутствует. Это presence-only: SDK/version/TFM/RID compatibility, package cache, свежесть assets/DLL не подтверждены. SDK/CLI не запускались, restore/download/install не выполнялись. PackageReference версии и источники не менялись. `--no-build` в будущем допустим только после successful fresh build **того же** проекта/configuration; failed build не оправдывает старую DLL. Metadata/compile consumer не доказывают runtime/DI/native/C/D.

### Manifest и передача ближайшему01

Это **плановый адресный manifest01**, а не выполненные изменения00. Исполнитель01 получает отдельное поручение координатора; expansion обосновывает по actual source до записи. Общий gate остаётся в EF, core AB/SQL/provider implementations не исправляются обходом.

| Repo | Exact prospective writable paths01 | Причина |
| --- | --- | --- |
| EFCoreLibrary | `maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs`; `maintenance/EFCoreLibrary.Maintenance/Coordination/AGENTS.md` | Факт начала initialization независимо от диагностического Stage; устойчивое правило poison до release |
| EFCoreLibrary | `tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs`; `tests/EFCoreLibrary.Maintenance.Tests/MaintenanceFixture.cs` | Различающий post-CREATE Pending case и controls; current fixture CreateAsync/PendingAsync не имеют fault hooks, нужны минимальные fake-only hooks |
| AgentBridge | `tests/AgentBridge.Persistence.EfCore.Tests/DatabaseMaintenanceTests.cs`; `tests/AgentBridge.Persistence.EfCore.Tests/FakeMaintenanceBoundary.cs` | Actual registration/coordinator, общий root gate и отдельные scopes; fake PendingAsync пока без fault hook |
| AgentBridge | `Documentation/Plans/AgentBridge Audit Remediation/01-initialization-gate.md` | Actual before/after B evidence и ограничения |
| HttpClientLibrary / codex-lb | Нет writable files01 | Transitive build/reference не является поручением менять transport/server |

`SingleInitializerGate.cs` — read-only reference для01: EnterAsync уже проверяет poison после ожидания, lease release идемпотентен. Отдельная правка gate не требуется из доказанного source trace. EF/tests AGENTS и AB/tests AGENTS уже требуют no real I/O/await всех tasks; изменять их только при actual смене устойчивого правила. Coordination/README/slnx, `.vs/`, generated outputs/migrations, csproj не входят в manifest01. Отчёт01 не даёт автоматического разрешения tooling/скриптам.

**Минимальный контрпример01 до правки (только trace A, не исполнен):** shared gate; initialize=true; first Inspect подтверждает Missing; Create начинается и завершается; последующие existence/target binding успешны; first PendingAsync бросает `MaintenanceException(ConnectionFailed)`; pin.DisposeAsync успешен. На строках55–56 Stage=Discovery, catch107–108 не poisons; finally113 освобождает lease. Waiter либо next Inspect через тот же gate получает доступ к fake provider. Нужный B assertion: waiter и fresh scope получают `GatePoisoned`, счётчики provider/pin/pending/create/migrate после failure не растут. Первый failure code остаётся safe; caller OCE содержит исходный caller token, deadline отдельно.

**Различающая матрица01:** fault до CREATE (Inspect/auth/permissions/invalid target/wrong mode) не poisons только из-за initialize=true; fault при начале CREATE poisons; success CREATE → post-create discovery/Pending fault, final recheck fault, migration/verification fault poisons независимо от текущего Stage. Pending fault/OCE при успешном cleanup обязательно отдельный case: CleanupUnconfirmed или migration failure уже отравляют gate и не различают006. Waiter стартует пока первый operation удерживает gate; poison должен случиться **до release**, отдельный future scope также блокируется. Все TaskCompletionSource освобождаются TrySetResult/cancel в finally, все started tasks await также при раннем assertion/timeout (не переносить010 в новый тест).

**Положительные контроли01 из actual methods, пока лишь прочитаны:** EF CoordinatorTests `Missing_initialization_has_no_backup`, `Existing_database_backs_up_then_rechecks_then_migrates`, `No_pending_is_unchanged`, `Explicit_modes_reject_wrong_database_state`, `Typed_failure_is_not_overwritten_by_cancellation`, `Actual_cancellation_is_attributed`, `Cancelled_waiter_does_not_release_owner`, `Raw_error_is_not_exposed`. AB DatabaseMaintenanceTests `NewDatabaseIsInitializedExplicitly`, `ExistingDatabaseBacksUpBeforeMigration`, `NoPendingMeansNoBackup`, `ModesDoNotFallback`, `ConnectionFailureNeverMeansMissing`, `InitializationCollisionDoesNotContinue`, `CallerCancellationIsPreserved`, `DeadlineIsReportedWithoutCallerCancellation`, `DiagnosticsPreserveSafeStagesAndErrors`. Successful initialize/no pending и successful existing update должны допускать последующее Inspect тем же gate; expected pre-CREATE failure — последующую разрешённую операцию. Safe logging без raw exception/secrets сохраняется.

Future B01 требует fresh builds конкретных EF maintenance и EF maintenance tests, AB EF adapter и persistence tests с GeneratePackageOnBuild=false; actual test runs с указанными filters, exact commands/config/output/TRX и before/after evidence записывает01 после preflight. На00 команды build/test намеренно не исполнялись. C17 подтверждает actual CREATE/provider effects отдельно; B fake CREATE не является DDL evidence.

### Остаточные ресурсы Q-002 и сила evidence

- **17:** версия/редакция SQL Server, server OS/архитектура, отдельные disposable DB/schema/accounts, credentials/TLS/trust/permissions, доступный SQL Server service account серверный backup path, квоты/verification/cleanup owner, точные разрешённые commands. Для адресного existing SQLite/PG — собственные файлы/DB, runtime/provider версии, PG dump tools/права, backup/restore/cleanup границы. MSSQL основной уже согласован; local Engine на ARM64 не обещан.
- **18:** конкретные win-x64/linux-x64/linux-arm64 машины/контейнеры или иные явно разрешённые runtime resources, версия Windows11/Ubuntu/.NET10, dependency graph потребителя, native/DI loading scenarios, placement/cleanup owner и exact запускаемые commands. Windows compile не заменяет Linux ARM64 run. Hosting/Docker не разрешены.
- **19:** endpoint, deployed codex-lb commit, exact model IDs/capabilities/access/TLS, предел token/money budget и операции, отдельные данные/права приложения/tools, disposable processes/network fault points и cleanup owner для crash/ack/disconnect. Индивидуальные/shared keys не печатать и не сохранять в evidence. Пока live отложен; local LB не deployment.

Это список блокеров **соответствующих C/D17–19**, а не повторный вопрос о принятых Q-003–005 и не блокер A00/локальных исправлений. Реальные DB/SQL/migrations/backup/native/process/HTTP/hosting/deployment, project scripts/CLI/restore/build/test не запускались. Не доказаны runtime disposal leak, actual SQL atomicity/DDL, server FIFO final payload, target DI/native compatibility или crash recovery.

### A-проверки, before/after и артефакт

Actual read-only команды выполнялись через PowerShell в указанном cwd; ExitCode0 у повторных успешных проверок:

```powershell
git status --short
git rev-parse HEAD
git rev-parse --abbrev-ref HEAD
git -C ../work/EFCoreLibrary status --short
git -C ../work/EFCoreLibrary rev-parse HEAD
git -C ../work/EFCoreLibrary rev-parse --abbrev-ref HEAD
git -C ../work/HttpClientLibrary status --short
git -C ../work/HttpClientLibrary rev-parse HEAD
git -C ../work/HttpClientLibrary rev-parse --abbrev-ref HEAD
git -C ../codex-lb status --short
git -C ../codex-lb rev-parse HEAD
git -C ../codex-lb rev-parse --abbrev-ref HEAD
rg --files openspec/changes -g tasks.md
rg -n 'Stage =|CreateAsync|PendingAsync|gate.Poison|finally|class Operation' ../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs
rg -n 'Trait\("Dependency"' tests/AgentBridge.Persistence.EfCore.Tests/Integration
git diff --name-only
git diff --check
```

Чтение файлов — `Get-Content -Raw -Encoding UTF8 <exact path>` либо `Get-Content -Encoding UTF8 <exact path> | Select-Object -Skip N -First M`; source search — `rg -n ... <existing file/root> -g '*.csproj' -g '*.props' -g '*.targets' -g '!**/obj/**' -g '!**/bin/**'`. XML/path проверка использовала `[xml](Get-Content -Raw -Encoding UTF8 ...)`, `[IO.Path]::GetFullPath` и `Test-Path`;11 AB csproj распарсены, missing literal ProjectReference0. Ancestor checks использовали DirectoryInfo.Parent/Test-Path/Get-Content, без запуска build tooling. Assets проверены Test-Path,20 проектов/19 presence, consumer отсутствует. Проверено18 tasks, не CLI валидность.

До правки свой00 — UTF-8 без BOM, LF31/CR0; исходный текст «Работы и проверки ещё не выполнялись». После — документ содержит актуальные baseline/map/manifest/контрпример/controls/блокеры, source production/tests **не изменены**, поэтому runtime before/after результата нет. Ручная запись — apply_patch. После записи strict `[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes(...))` подтвердил UTF-8 без BOM/CR; поиск U+FFFD/четырёх question marks/трёх mojibake markers дал0, relative Markdown path checks5/5, сравнение исходного задания с `git show HEAD:<own path>` (кроме статуса) совпало. Карта ABQA имеет10 строк/10 unique IDs; Q-002–005 присутствуют. Git diff --check ExitCode0, index пуст. Собственный diff только00, общий diff также содержит входной Coordination. Артефакт только этот Markdown; новых TRX/DLL/manifests/build logs нет.

Read-only `Get-FileHash -Algorithm SHA256 <exact path>` до/после дал одинаковые значения: Coordination `D21D4648124BE9FAA85535A761B3EBEFA41DCDF93F091BD24C859CC813621FF2`; EF `maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs` `3619C9013AA66A59D13B1526BFD72C8C7CE55716EC8816C38C003F2C5E87C97C`; AB `tests/AgentBridge.Persistence.EfCore.Tests/DatabaseMaintenanceTests.cs` `215CBD88E1B0E6804B4C9BC6B3DA91650CDCC63B0BFDF9DAA51C222232D71D8B`. Повторный status соседей сохраняет чистые EF/HTTP и чужую `.vs/` LB. Это контроль неизменности опорных источников/чужой записи, не assertion runtime поведения.

Во время discovery два предполагаемых имени01 и split `DatabaseMaintenance.Initialization.cs` оказались отсутствующими; повторно найдены actual `01-initialization-gate.md` и единый `DatabaseMaintenance.cs`. Windows rg не раскрыл wildcard в позиционном пути; повторные поиски использовали concrete files/root + glob. Первый inline документный check получил ParserError из-за literal Unicode quote в mojibake marker; повтор с числовыми Unicode chars завершён ExitCode0/issues0. Эти read/check errors не failures продукта, не замаскированы fallback и не меняли файлов. Git предупреждает LF→CRLF при будущем касании own00/Coordination; actual own00 EOL сохраняется LF.

**Передача:**00 готов к приёмке в A-границе; код не исправлен,01 не исполнен, B/C/D не пройдены. Карту/manifest/сценарии можно использовать для отдельного поручения01. README/Coordination/общие статусы синхронизирует координатор; локальный commit00 — только после его явной приёмки/поручения.

**Приёмка00, 2026-10-06:** координатор от имени пользователя принял результат в A-границе после проверки уточнения ABQA-001, исходного задания, UTF-8/пяти ссылок, diff --check и source006/manifest01. Явно поручен отдельный локальный docs-коммит только этого файла; Coordination исключён. Приёмка не означает исполнение01 или прохождение B/C/D.
