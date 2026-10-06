# 20 — Итоговая приёмка исправлений и подключения

[Навигатор](README.md) · [Решения](Decisions.md). Статус: **принят координатором как итог частичной приёмки доступного локального объёма**. Зависимости: фактический отчёт или явный блокер каждого00–19. Область: документация итогов.

## Цель и основание

Сопоставить все исходные находки с принятыми изменениями и evidence, сохранив честные остаточные риски. Источники — [Findings](<../AgentBridge Quality Audit/Findings.md>), [OpenQuestions](<../AgentBridge Quality Audit/OpenQuestions.md>), результаты нового плана. Число passing tests не заменяет это сопоставление.

## Работы

1. Для каждого ABQA-001–010 записать исходную категорию, решение, владеющий repository/component, изменённые файлы, before/after сценарий, evidence A/B/C/D, ограничения и фактический статус.
2. Для Q-002–005 сопоставить принятые решения с реализацией/evidence; Q-002 остаётся частичным до определения real resources. Q-001 сохранить закрытым исторически. Согласованные Q-003–005 не считать выполненной CLI validation или исправлением кода без соответствующего run.
3. Разделить «исправлено, адресно проверено локально», «проверено в целевом окружении», «опровергнуто в указанной границе», «частично», «открыто». Для ABQA-007 учесть pin/process/native; для ABQA-010 — оба репозитория.
4. Сверить public API/DLL/XML/examples, основной MSSQL сценарий и три RID, strict IConfiguration boundary/отсутствующие настройки, app-owned logger/schedule, fixed expiry, CAS/short scopes и unknown/no-replay. Новая обязательность settings — явная несовместимость прежним defaults; документировать migration path и не принимать как скрытый рефакторинг.
5. После приёмки добавить датированные remediation notes со ссылками в исходные Findings/OpenQuestions; не удалять старые доказательства/ID. Синхронизировать status/navigation нового плана и текущие документы, сохранив исторические отчёты.
6. Проверить UTF-8/EOL/links, manifest всех затронутых репозиториев и отсутствие manual generated changes. Git status/diff/history доступны только при явно разрешённых read-only операциях; stage/commit/push — отдельное поручение.

## Итоговая матрица

При исполнении заполнить таблицу, не проставляя заранее «исправлено»:

| ID или проверка | Решение/изменение | Evidence и версия | Граница/остаток | Приёмка |
| --- | --- | --- | --- | --- |
| ABQA-001–010, отдельная строка на каждый ID | Не заполнено | Не выполнено | Требует результатов этапов | Не принят |
| Q-002–005, отдельная строка на каждый вопрос | Не заполнено | Не выполнено | Требует решения/проверки | Не принят |
| MSSQL/strict config/facade/три RID12–15 | Не заполнено | Не выполнено | Новое согласованное подключение | Не принят |
| Изолированная регрессия/compile-only16 | Не заполнено | Не выполнено | B | Не принят |
| Provider/runtime/live17–19, отдельные области | Не заполнено | Не выполнено | C/D | Не принят |

## Критерии завершения

Для всех источников есть конкретный исход и ссылка на evidence или блокер. Полностью закрытые записи не имеют скрытого незапущенного обязательного случая. Если включённые проверки17–19 не завершены, итог прямо назван частичной приёмкой; локальные исправления принимаются в собственной границе. ARM64 compilation без actual ARM64 run не закрывает runtime поддержку.

Планирование, наличие нового теста, successful build, historical TRX, textual spec comparison и подготовка DLL не подменяют соответственно реализацию, run, runtime, fresh evidence, CLI validation и native readiness. Deployment, archive OpenSpec и Git-коммит не входят в итог автоматически.

## Результаты

### Вывод и область

2026-10-06, Asia/Novosibirsk. **Частичная приёмка доступного локального объёма**: принятые исправления00–16 и доступные части17–19 сопоставлены с исходными ABQA-001–010 и Q-001–005. Полный план не завершён: C17 отложен пользователем;18 имеет только Windows runtime evidence;19 содержит подготовку A, все31 C/D случая не запущены, executable live/crash consumer, fault controllers и durable counter отсутствуют. Документация20 принята координатором от имени пользователя в частичной границе; полное закрытие17–19 не заявляется.

Поручение человека, переданное координатором, разрешает именно документацию20 и текущую навигацию, датированные append notes в Findings/OpenQuestions, тестовые проверочные команды и read-only Git. Соседние репозитории read-only. После независимого review координатор поручил адресный commit exact7 docs; push не разрешён. Исходные работы/критерии/матрица-шаблон выше сохранены. Coordination принадлежит координатору и исключён из manifest.

Прочитаны отчёты00–19, актуальный журнал приёмок, README/Decisions, Findings/OpenQuestions/исторический итог аудита15 и main OpenSpec. Историческая memory была только указателем к классификации, её сведения об отсутствии исправлений не использованы как текущий статус. Текущие факты перепроверены по source/artifacts/Git. Применены root/Documentation/Plans и относящиеся к проверяемым контрактам AGENTS, csharp-project-rules, backend-uow-repositories/reference, aspnetcore-project-rules и ef-core-migrations. Новых продуктовых требований/решений Q-003–005 не введено.

### Находки: исходная категория и локальный исход

AB/EF/HTTP/LB означают корни AgentBridge/EFCoreLibrary/HttpClientLibrary/codex-lb. Полные адресные manifests и full hashes каждого принятого изменения находятся в таблице этапов ниже и в [проверенном inventory Git](../../../artifacts/stage20/source-evidence-verification.json). В колонке файлов указаны владеющие source/tests/docs; полные списки включают связанные AGENTS и отчёты. Один ID не переиспользуется и не повышается задним числом до runtime-дефекта.

| ID / исходная категория / серьёзность | Причина, владелец и изменённые файлы | Before → after и различающий положительный контроль | Evidence / локальный статус / обязательный остаток |
| --- | --- | --- | --- |
| ABQA-001 / расхождение документации / S3 | AB docs: текущий AgentSettingsService и реализованные runner/transport/storage были названы будущими. Exact12 docs manifest [08](08-documentation-alignment.md#результаты), текущая навигация20 | Старый статус реализации → actual API и явные checkpoints.12 C# blocks сохранены,243 local links/21 fragments проверены08;20 исправляет новые stale строки текущих навигаторов | **Исправлено в A**08/20; runtime из этого не следует. Historical Initial/audit15 не переписаны |
| ABQA-002 / подозрение на дефект зависимости / S2 предварительно | HTTP `Models/HttpStreamResponseResult.cs`, `Clients/HttpApiClient.cs`, новые disposal tests; AB gateway/SSE tests. Body cleanup мог пропустить response, owning cleanup заменял primary | Wrapper before2/4 на каждой TFM: response attempts0; gateway3/6: primary заменён. After HTTP58/58 обеTFM, gateway198/198: remaining cleanup attempted, original identity/stack и immutable secondary. Successful cached-body/repeat, primary-only, cleanup-only и same-object stack controls | **Воспроизведено/исправлено в локальной A/B ownership/error границе**06/16. Попытка не равна confirmed release; deployment connection leak не доказана, S2 остаётся предварительной. C/D cleanup/соединения —19 T10 |
| ABQA-003 / пробел CLI проверки / серьёзность не назначена | AB OpenSpec/workflow/evidence, exact24 manifest09 | Отсутствие evidence → CLI1.14.1 final main+original18:19 Exit0/valid, ERROR0/WARNING0; исходные460 clauses/167 scenarios сохранены. Промежуточные4/15 и5/14 не скрыты | **Закрыт в B CLI**09. Повтор текущих19 strict20:19/19 Exit0. JSON1.0 — schema, не версия CLI. INFO2 archive advisory/readiness отдельно; AB changes не архивировались |
| ABQA-004 / расхождение OpenSpec, нормативная применимость требовала уточнения / S3 предварительно | AB `openspec/README.md`, main/old deltas и clause map09; владелец workflow принял Q-003 | Промежуточный Unsupported compact в deltas → сохранён как история; main authoritative, старый запрет нельзя повторно применить. Matching names/operations и460 clauses/167 scenarios не потеряны; strict pass не означает безопасный sync | **Принят в A workflow**09; Q-003 согласован. Автоматический sync/archive AB не выполнен; runtime CompactAsync дефект не заявлен |
| ABQA-005 / дефект реализации / S3 | AB `Domain/Dialogs/Dialog.cs`, DialogRestorationTests и DialogWritePortsTests05: LastChanged принимался без возможной mutation | Before core24/28, fake-loader1/2; after68/68+24/24. Context-only повреждение отклонено без staging, Begin/terminal/active append/repeated compact/same-time/independent settings controls сохранены | **Исправлено, адресно проверено A/B**05/16. Corrupt real row origin не установлен. C17 persistence restore/backup/journal/settings остаются; фабрика не доказывает SQL atomicity |
| ABQA-006 / дефект зависимости / S2 | EF `Coordination/DatabaseMaintenance.cs` + CoordinatorTests; AB DatabaseMaintenanceTests01. Stage после CREATE скрывала начавшуюся installation | Before EF6/10 и AB0/3; after50/50+40/40: InitializationStarted фиксируется до CREATE, poison до release. Post-CREATE pending/final recheck failure/OCE и waiter/next-scope отказывают; pre-CREATE/nonfatal/success controls допускают правильную работу | **Исправлено A/B**01/16, actual coordinator/gate с fake provider. C17 post-CREATE fault/состояние БД/cleanup ещё не подготовлены и не запущены; real loss не заявлена |
| ABQA-007 / дефект зависимости / S3 | EF coordinator/EfMigrationOperations pin, BackupProcessRunner, SqliteBackupStepper и safe error contracts/tests02; AB DatabaseMaintenanceTests | Before EF18/30:12 primary-loss; AB4/8:3 primary-loss и1 positive deadline cold-start failure до pin (Closes0 при60ms, не product defect; бюджет уточнён до1s). After108/108+51/51. **Pin:** actual EF DatabaseFacade с fake DbConnection; **process:** actual runner с fake handles/stop; **native:** actual stepper с fake Step/Finish. Typed primary/caller/deadline + secondary сохраняют safe PrimaryError; primary-only/success/unknown poison controls | **Исправлено A/B во всех трёх проявлениях**02/16. C17 actual connection close, OS process/pipe stop, sqlite3_backup_finish/release отдельно открыты; MSSQL success или native Load/Free18 не закрывают их |
| ABQA-008 / дефект реализации / S3 | AB gateway GenerateStreamAsync и ResponsesSseTests04; empty EOF+successful disposal cancellation не учитывал HasData | Before0/2: No exception; after62/62: original caller-token OCE/callbacks0. Empty no-cancel Incomplete, comments/DONE no-data, partial/terminal canonical reports, explicit Failed/deadline/callback controls сохранены | **Исправлено A/B**04/06/16. Real controlled pre-data/late cancellation —19 T08/T09; plain live timing не различающий контроль |
| ABQA-009 / дефект реализации / S3 | AB AgentRunScope/Session/Runner и AgentRunnerTests03. Cleanup-only OCE origin терялся при canceled caller, включая normalization executor | Before-v2 11/14; final-v2 66/66: original cleanup OCE identity/stack, accepted append/checkpoint state и no subsequent writes/replay. Ordinary caller, primary+cleanup aggregate, read cleanup и ExpiredDialogCleanup controls сохранены | **Исправлено A/B**03/16. Storage/model doubles не real ack loss/provider disposal/process restart;19 R08–R10 открыты. Unknown/no-replay не external exactly-once |
| ABQA-010 / пробел проверки, cleanup тестов / S4 | AB UnitOfWorkScopeTests и EF CoordinatorTests07, оба исходных места | AB before5/8 → final17/17; EF before8/8 → final8/8 (finally уже исправлен01, искусственный повтор дефекта не делался). Normal/Assert.Fail/timeout/cancel завершают и await source tasks; safety cleanup стоит после terminal assertions | **Закрыт в B test lifecycle обоих repo**07/16. Test-only gap не production deadlock/leak; secondary bounded-cleanup error branch статическая, навсегда зависшая production task не создавалась |

### Вопросы и согласованное подключение

| Вопрос / область | Реализация и проверенный исход | Остаток / статус |
| --- | --- | --- |
| Q-001 | [Исторически закрыт](<../AgentBridge Quality Audit/OpenQuestions.md#abqa-q-001>) 2026-10-05 22:04:57 UTC+07:00 организационным evidence; отдельный проект/чаты созданы | Сохранён закрытым; не переоткрывался |
| Q-002 | .NET10/MSSQL primary, SQLite/PG сохранены, app kits три RID; Windows11 Pro26200/.NET10.0.12/SDK10.0.401 receipts18. Pi5 Ubuntu24.04.3/kernel6.8.0-1060-raspi/aarch64/SSH LAN по сообщению пользователя; dotnet command not found | **Частично определён**. MSSQL версия/edition/EngineEdition/endpoint/TLS/auth/server paths/own DB/restore target/operations/cleanup не заданы; Linux x64 машина не задана; Pi provisioning/copy/run не выполнялись; endpoint/models/cost budget19 отложены |
| Q-003 | Authority main/history workflow09, actual strict19/19 и сохранённые clauses/scenarios | Решение и A/B09 приняты; безопасное AB sync/archive отдельно, не автоматически закрыто validation |
| Q-004 | NestedControlsValidator общий JSON/SSE/compact reasoning10: known malformed/duplicates Validation до HTTP; nullable/unknown/duplicates unknown/arbitrary schema сохранены. Before6 Success/calls1; final115/115 внутри313/313, те же bad inputs calls0 | **Принят A/B10**. Local exact boundary реализована, deployed counterpart/model acceptance19 T11/T12 не проверены; исходная неоднозначность не названа прежним дефектом |
| Q-005 | AgentBridge FIFO occurrence association и codex-lb final request preparation11. Before AB1 passing assertion исходного wrong association, Python2 различающих; finalAB20 внутри183, LB120=27new+93existing | **Принят A/B11 в обоих repo**. Wrong/ambiguous subset отказ до save, accepted window/history/no-replay controls; deployed/final wire/fitting19 T13–T16 открыты. Focused LB archive выполнен11 по отдельному разрешению; global LB strict не заявлен |
| MSSQL12 | SqlServer=2 без fallback; generated initial six-table schema/UpDown/designer/snapshot/factory, EF10.0.11/SqlClient6.1.6. Owner обратимые UTF-16 code units varbinary(max), payload nvarchar(max), UTC bigint, composite FK/cascade/root-settings CAS metadata. Existing EFCoreLibrary module EngineEdition2/3/4, direct endpoint/ConnectRetryCount0, server backup destination/receipt semantics. B211/211 | **Принят A/B**. Metadata/converter/parameter/translations не actual SQL equality/CAS/rollback/backup/restore. Generated tooling12 реально исполнялся по отдельному permission; apply/SQL20 не выполнялись. C17 открыт, SQL Engine ARM64 не обещан |
| Breaking configuration13 | Missing required scalar/source modes без defaults; IConfiguration presence отдельно от prior Configure; safe Binder path/code без values/unsafe inner. Standard options/reload/PostConfigure/IStartupValidator сохранены. InstructionsSource=Configuration/PerRequest и KeySource=Shared/Individual; individual error без shared fallback; before missing принимался default, after fail до I/O | **Принят A/B**, breaking commit13 с `!`. Migration: явно задать прежние желаемые limits/Retention/Compaction/reserve/timeouts/provider и source modes; explicit zero reserve разрешён, optional maintenance settings лишь при подключении. [Таблица settings/migration](<../../Technical documentation/05-configuration-and-lifecycle.md>) authoritative; core502/transport403/persistence23213, без filesystem/logger runtime claim |
| Facade14 | Actual `AddAgentBridge(IServiceCollection, IConfiguration, Func<IServiceProvider,HttpClient>)` в отдельном Integration net10.0. Required app ILoggerFactory/Individual source до facade; borrowed HTTP app-owned; custom contracts TryAdd/ordered providers/async scoped tools сохраняются, config не runtime dependency. B289/289 с57 facade cases | **Принят A/B**. DI без host/HTTP/DB/maintenance/logger/file/scheduler. App владеет logging sinks/path/rotation, schedule/batch, authorization/keys/business handlers/HTTP lifecycle. App production ASP graph не исполнялся |
| Kits15/16 | SQL Server win-x64/linux-x64/linux-arm64 + SQLite/PG win-x64, SDK closure/manifests/XML/API/13 satellites/PE-ELF/RID guards, offline external binary compile всех5 examples без project/package references | **Принят B**, actual assets/hash/compile. Methods не исполнялись, Windows cross-build не Linux runtime. Release/AOT/trimming/single-file и SQLite/PG Linux не добавлены в scope |
| Fixed expiry/CAS/short UoW/durable unknown | Actual Domain/runner/write ports сохраняют срок от создания, fresh UTC, независимую settings version, per-turn atomic snapshot, Start commit barrier и unknown без retry/replay; короткие scopes вне model/tools, base EFCoreLibrary repositories | **A/B16**, C/D не повышены. Даже успешное acknowledged write с failed disposal не разрешает новые writes. Реальные journal rollback/ack loss/process death/expiry across P2 относятся17/19 |

### Fresh evidence16 и доступные17–19

Fresh означает фактический run соответствующего этапа на pinned source/binary, **не новый test run20**. Source snapshot16:474 inputs, текущая сверка20 нашла только последующие изменения `tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md`17 и `tests/Delivery/AGENTS.md`18. Production inputs16 сохранили SHA; новые opt-in C tests17 и runtime probe18 проверяются их собственными receipts, не автоматически suite16.

| Run / source | Фактическая проверка | Проверка сохранённого evidence20 |
| --- | --- | --- |
| [16](16-isolated-regression-and-delivery.md#результаты), AB baseline `a874c346c288c6b5b6343a59dea4a1220bd785f8` | Fresh core502, Codex403, persistence289 с Dependency!=Database; EF159 address filter; HTTP58 net8 и58 net10; metadata27. Все Exit0/failed0/skipped0; no Integration rows.53 receipts:20 restore/20 Build/7 tests/5 helper/1 expected wrong-RID Build Exit1 | Перечитаны counters=rows и7 TRX/test DLL hashes, build→test timestamps,53 command log hashes. Exact argv/cwd/start/end/exits — [commands.jsonl](../../../artifacts/stage16/commands.jsonl); hashes и sources/packages — [Results16](16-isolated-regression-and-delivery.md#результаты) и verification.json. Outcomes не суммируются |
| Five fresh kits16 |140/138/137 entries SQLServer win/Linuxx64/ARM64,140 SQLite/PG each; managed64/63/63, XML57 each, satellites13 each, native3/2/1. Win PE AMD64; Linux ELF x86-64/AArch64; ARM64 MSAL native не выбран SDK и не придуман | Все695 manifest entries SHA перепроверены; пять external consumer snapshots и пять runtime18 output snapshots вместе2265 files совпали. Full manifest SHA в [inventory20](../../../artifacts/stage20/source-evidence-verification.json). Generator/source/SDK provenance сохраняется |
| [17](17-provider-verification.md#результаты), A/B подготовка | Final isolated310/310, новые21 parsing cases; first309/1deadline test сохранён, focused8/8 overlap. Один basic MSSQL C-case + same-row owner pairs лишь compiled; C отложен пользователем | Final TRX SHA `789e078d407263970d21649b99b1873793a5b0e120ac65f256c33d1bd56910d1` совпал. First failure не назван установленным product defect; focused/final не исправление timing stability. **Не вся Cматрица17 подготовлена** |
| [18](18-runtime-delivery.md#результаты), final run3 | Five binary builds; SqlServer/SQLite/PG Windows runtime каждый managed64/native3 Load/Free, BPE cl100k/o200k, facade/scopes/app logger, sends0/noDB. Linux только builds. Missing resource/mixed hash/wrong provider/RID отказы ожидаемые |17 command log hashes и output snapshots сверены. Native Load/Free — ограниченная Cloader граница; exports/auth/TLS/provider/SQL/process не проверены. Run1 BadImageFormat был fixture enumeration error, run2/run3 overlap не суммирован |
| [19](19-live-contracts-and-recovery.md#результаты), A preparation |31 not_run =19 transport+12 recovery,35 sourceRefs,8 raw fixtures/8 pair candidates/2 alternating. Нет executable live/crash harness/fault controllers/durable counter |35 current source hashes совпали; fixture/readiness prep receipt прочитан. Отсутствует новый B/C/D run19, нет TRX. Windows runtime18 и compile-only17 не доказательство crash/live |

SDK/package receipts16–18: SDK10.0.401, .NET10.0.12 (HTTP также net8.0/.NET8.0.31); EF10.0.11, Npgsql10.0.3, SqlClient6.1.6, SQLitePCLRaw2.1.12, tokenizers/data2.0.0; EFCoreLibrary0.0.5, HTTP FileVersion0.0.0.5. Extensions10.0.11, Configuration/Binder/Options.ConfigurationExtensions10.0.3; Test.Sdk18.0.1/xUnit2.9.3/VS3.1.5, HTTP Test.Sdk17.14.1/xUnit.v3 3.0.1/VS3.1.1, metadata Roslyn5.0.0. Per-run assets/deps authoritative; единой версии всех dependency graphs не придумано.20 не выполнял новый runtime/SDK inventory, restore или compilation; current CLI package source1.14.1 прочитан отдельно.

### Незавершённые обязательные проверки и что требуется

**17 C — отложен пользователем, не completed.** Каждая незапущенная область из [матрицы17](17-provider-verification.md#незапущенная-матрица-c-и-форма-ресурсов) сохраняется отдельно:

- MSSQL migrations/basic CRUD/create/read/stale root/append/context/restart/expiry — один prepared compile-only case; actual run отсутствует, delete/cascade не подготовлены им.
- Ordinal owner case/space/NUL/trailing zero/unpaired UTF16 — same-row pairs prepared, actual SQL equality/CAS отсутствует.
- Root/settings CAS, first insert/update/mixed races — адресный MSSQL C-case не подготовлен/не запущен.
- Journal+outputs rollback/scope outcomes — MSSQL C-case не подготовлен/не запущен.
- Post-CREATE failure/OCE/poison до waiter — fault case/actual created DB/cleanup не подготовлены/не запущены.
- Backup primary+connection cleanup/safe codes/poison — actual SQL fault fixture не подготовлена/не запущена.
- Server receipt/header/VERIFYONLY/restore schema+data — отдельный restore target/backup case/SQL/cleanup не подготовлены/не запущены.
- Domain Restore/recovered journal/settings/malformed state и backup restore — basic prepared case не закрывает полную область.
- ABQA-007 PostgreSQL actual process/pipe stop и SQLite actual native Step/Finish/release при primary/OCE+cleanup — отдельные открытые C области.
- Unknown commit/ack loss/crash — реальные случаи19, synthetic exceptions не замена.
- EngineEdition2/3/4, endpoint/TLS/auth/retry policy — metadata/parsing не actual server evidence.
- Down/cleanup — только future собственные объекты после проверки identity; готовой destructive команды нет.

Для17 нужны согласованные SQL Server version/edition/EngineEdition/OS/endpoint, TLS/auth/secret source, собственная source DB и отдельная restore DB, server backup destination/служебные права, budgets, перечень create/migrate/backup/restore/Down/cleanup и владелец удаления. Credentials не публикуются. Existing provider process/native ресурсы согласуются отдельно. Test-command permission не задаёт эти ресурсы.

**18 runtime — Windows часть принята, Linux/production остаток открыт:** Ubuntu x64 actual machine/access/.NET10/test directory/app dependency graph не заданы; Pi5 доступен по сообщению пользователя, .NET отсутствует, installation/copy/run не выполнены. Нужны согласованное provisioning и выделенный workspace обеих Linux машин; затем actual loader/DI/BPE/native runs и app graph. Windows production ASP.NET Core/package conflicts/IDE readiness и native exports/provider operations/TLS/auth не проверены. Release/AOT/trimming/single-file/SQLite-PG Linux — не расширенный scope. ARM64 клиент не SQL Engine ARM64.

**19 C/D — все31 строки not_run.** Полная матрица сценариев и readiness/варианты находятся в [readiness19](19-live-recovery-readiness.md); это документы/данные без executable harness. Здесь каждый ID сохранён:

| Незапущенные transport случаи | Незапущенные recovery случаи |
| --- | --- |
| T01 catalog exact key/route/model/capabilities; T02 canonical JSON/explicit completion; T03 indexed SSE/callbacks/terminal | R01 до Begin commit; R02 Begin/model step подтверждены до Started; R03 durable Started подтверждён до action |
| T04 compact/save/expiry; T05 JSON↔SSE continuation/no-ID anchor; T06 individual rejection без shared fallback | R04 action confirmed до outcomes; R05 outcomes staging/SaveChanges до commit, actual persisted oracle/atomicity без обещания rollback; R06 outcomes commit до ack/terminal |
| T07 safe errors/401/403/64KiB; T08 pre-data caller cancel/EOF/disposal; T09 partial/terminal/deadline/Failed priority | R07 terminal acknowledged до return; R08 real disconnect/ack loss Started/outcomes/terminal; R09 operation/cleanup OCE origin/accepted writes |
| T10 throwing cleanup/attempt-vs-release; T11 malformed known raw duplicates/send0; T12 nullable/unknown/schema preservation | R10 parallel partial neighbor/hard crash; R11 exact owner/agent/incarnation/revision/output-position/FIFO authorization; R12 expiry/delete/settings snapshot |
| T13 FIFO final payload/closed pairs handler0; T14 required pair/fitting cap; T15 wrong/ambiguous candidate refusal; T16 second-pass fault preserves first window | Для всех R: P1/P2 actual process identities, real storage oracle и per-occurrence durable counter ещё не реализованы |
| T17 full input guard после compact; T18 whole-request guard после confirmed tool outcome, oversized output сохранён без следующей generation; T19 opaque UnknownBudget/estimate=null, отдельный Unsupported guard/generation0 | Fault controller, hard stop/network gates и observable before/after commit/ack не созданы |

Endpoint/deployed commit/model/capabilities/TLS/accounts, per-case и общий requests/tokens/cost/wall-time/actions budgets, process/storage identities, fault mechanisms, isolated durable counter, закрытый evidence directory и cleanup должны быть определены владельцем. Пользователь отложил endpoint; повторный вопрос не задан. После допуска координатора требуется сначала реализовать конкретный reviewable harness и preflight, потом согласованные C/D runs. Отсутствие средства/ресурса — блокер evidence, не подтверждённый дефект продукта. Авторизация/side effects приложения не становятся API AgentBridge.


### Этапы, чаты и фактические коммиты

Источник статуса приёмок — read-only Coordination; full hashes независимо разрешены через git show. Full hash коммита20 после его создания записывает координатор в отдельном журнале; self hash здесь заранее не задаётся. Границы каждой строки являются частью приёмки.

| Этап / Results | Чат / host | Фактический статус | Проверки / ограничения | Полные локальные hashes по repo |
| --- | --- | --- | --- | --- |
| [00](00-baseline-and-scope.md#результаты) | [чат](codex://threads/01a10f3f-ed96-7031-9941-e7372b0e45be) / local | Принят в A-границе | Baseline/map/manifest01; исходное задание/UTF8/LF/5 ссылок/diff check независимо сверены | AB `bfd09fda45d5d36f97feec2ee0f3af3a18912acd` |
| [01](01-initialization-gate.md#результаты) | [чат](codex://threads/01a10f47-8332-7fa3-b0cd-b8cdd07dcaa5) / local | Принят в A/B | EF50/50, AB40/40; before EF4/10 fail, AB3/3 fail; C17 открыт | EF `be05cc94b5fd7426699e12ea29e8814b361c4e5d`; AB `5ced2104a996c066da62d17ab51f09fc84201885` |
| [02](02-maintenance-primary-errors.md#результаты) | [чат](codex://threads/01a10f4f-96c8-7080-b4ff-7b03aec66cbf) / local | Принят в A/B | EF108/108, AB51/51; before primary-loss12/30 и3/8; C17 открыт | EF `bdb0e36bc1d52fb4b5a028559cd65373b4bad704`; AB `edd168a5360c2bda81066bd8317811c94d081316` |
| [03](03-runner-cleanup-cancellation.md#результаты) | [чат](codex://threads/01a10f5d-55d5-7e41-875f-f837d68b75bf) / local | Принят в A/B | Before-v2 11/14, final-v2 66/66; caller/cleanup/checkpoint origin, C/D открыты | AB `9d5df55ad5d3bd0e62a961c92f99bdfd2b2cbbed` |
| [04](04-empty-sse-cancellation.md#результаты) | [чат](codex://threads/01a10f64-ccd7-7141-b98b-16c28c307891) / local | Принят в A/B | Before0/2, after62/62; empty EOF/caller/known data/Failed/callback controls; live19 открыт | AB `1e9e2c07655636876ea5dc304aa2f3ff633c8ba2` |
| [05](05-restore-chronology.md#результаты) | [чат](codex://threads/01a10f68-f6c2-7772-8f3c-b81a67956926) / local | Принят в A/B | Before core24/28,persistence1/2; after68/68+24/24; C17 открыт | AB `c103b60cf98711a2da3fc7e34c04a9437dee549b` |
| [06](06-http-disposal.md#результаты) | [чат](codex://threads/01a10f6f-10e2-7181-9640-8b39117c2bb8) / local | Принят в A/B | Before wrapper2/4 на net8/net10; gateway3/6; final58/58 обе TFM+198/198; S2 предварительно | HTTP `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`; AB `de5d2a96e7fe8cf94e62e10d31b1ff1b31586b22` |
| [07](07-gated-test-cleanup.md#результаты) | [чат](codex://threads/01a10f81-4d87-7073-81a1-d9c30a91d0a2) / local | Принят в A/B | Before AB5/8, EF8/8; final AB17/17, EF8/8; normal/early/timeout/cancel source tasks | EF `5962deceb6ea01306cbbda400040db88ea8e5df9`; AB `c91c965ff8efddd2db9645822bc34a04e25317aa` |
| [08](08-documentation-alignment.md#результаты) | [чат](codex://threads/01a10f8a-12c8-7320-ae97-126cb3c66df3) / local | Принят в A |12 Markdown/243 local file links/21 fragments/12 C# blocks сохранены; current docs/actual API | AB `452d0512e16476d9ea5683c8aef8d867c4d103ce` |
| [09](09-openspec-reconciliation.md#результаты) | [чат](codex://threads/01a10f93-66f8-7103-b1a0-38d1f6c598fb) / local | Принят в A workflow/B CLI | Actual CLI1.14.1 final19/19 Exit0/ERROR0/WARNING0;460 original clauses/167 original scenarios сохранены; INFO2 archive отдельно | AB `de201cf2ecfb964b10d82e1922fcb7e7f019cb8a` |
| [10](10-nested-controls-contract.md#результаты) | [чат](codex://threads/01a10fae-a475-7e60-ade4-15acf4fee31a) / local | Принят в A/B | Before6 Success/calls1; after115/115+313/313 (overlap), Validation/calls0, unknown passthrough; live19 открыт | AB `e10529752513a9330b81069ddb080d0ea5914173` |
| [11](11-repeated-call-pairing.md#результаты) | [чат](codex://threads/01a10fb8-19d5-7752-bc8d-12ccc23e85d9) / local | Принят в A/B | Before AB1/Python2 различают wrong pairing; after20/20+183/183 overlap, Python120/120; strict AB/focused LB; live19 открыт | AB `18d11b0b546cd97c9157a0befd20ff38f4186bda`; LB `f2b8e042c4ce012ae703bc939413bf9961b00032` |
| [12](12-sql-server-provider.md#результаты) | [чат](codex://threads/01a10fce-62e8-76a3-8eec-8fe13f276b05) / local | Принят в A/B | MSSQL provider/DI/model/converter/generated consistency; final211/211; strict valid; C17 открыт | AB `1c508a3dc3ac50d9a25487e968540b695759c07b` |
| [13](13-strict-configuration.md#результаты) | [чат](codex://threads/01a10fe2-70b0-7f61-9106-872ae72fb500) / local | Принят в A/B | Explicit required presence/source modes/safe binding; final502/403/232 без пропусков; strict valid; runtime17–19 открыт | AB `9ed71c9e893a4ddc6148ccd09ea3508ce100e681` |
| [14](14-simplified-registration.md#результаты) | [чат](codex://threads/01a11000-0820-75a2-a41e-82af991d066a) / local | Принят в A/B | Actual facade/full DI graph/options/scopes/overrides; final289/289,57facade; kits15–16/runtime17–19 открыты | AB `4354106234488d039f805aecbfd24cfbc18f2162` |
| [15](15-multiplatform-delivery.md#результаты) | [чат](codex://threads/01a11010-a2bc-7a81-b1a0-f618a3969b7d) / local | Принят в A/B | Five kits/resources/PE/ELF/XML/API,27/27; five external consumers; runtime18 открыт | AB `b0c81321b1a9cfe0b4c640a1413419088daae88d` |
| [16](16-isolated-regression-and-delivery.md#результаты) | [чат](codex://threads/01a11040-5ad5-7d70-8347-cec38abd0ab2) / local | Принят в B | Fresh502/403/289;EF159;HTTP58 обеTFM;metadata27;fivekits/consumers | AB `b1e966445c70f30cad63cc5ba690808c75c7fd35` |
| [17](17-provider-verification.md#результаты) | [чат](codex://threads/01a11050-269b-7aa0-b7ff-858753f157d8) / local | Подготовка A/B принята; C отложен пользователем | Opt-in fixtures/same-row ordinal controls; final310/310, first309/1 сохранён | AB `9ba43d7ffb449099948a846824d53cd6c9fee171` |
| [18](18-runtime-delivery.md#результаты) | [чат](codex://threads/01a1105d-7671-7950-b7a9-4a060e70d30d) / local | Windows часть принята; Linux runtime открыт | Three Windows runtime64managed/3native each; five builds; negative guards | AB `801d9c2bfd694cc411747ed8003ca4cf5237e6b5` |
| [19](19-live-contracts-and-recovery.md#результаты) | [чат](codex://threads/01a1106a-1a93-7f02-b67b-59eabeea8258) / local | Подготовка A принята; C/D заблокирован | Matrix31notrun/35refs/rawduplicates/FIFO/resource card; harness отсутствует | AB `075c8e806967095a287be0ae3a724e6e5871f60b` |
| [20](20-final-acceptance.md#результаты) | [чат](codex://threads/01a11076-3834-7740-935d-2853986862c0) / local | Принят координатором в частичной границе | Частичная приёмка; обязательные C/D17–19 открыты | — |

Coordinator docs chronology по actual git log (от newest к oldest, source/code stage commits находятся в таблице): `673b14f6d66e0aea28324f3d6905d7eb0ede880c`, `2d5bd40f60996d11b75cd7de896ee3654d795307`, `d22f322580dcecc18026bea3a297ef543609f89f`, `cc69ecc57d061fb22357de908af682cd4c10bc57`, `a874c346c288c6b5b6343a59dea4a1220bd785f8`, `3286b12886c34ac4b2ba9d4523fee22c3949ab0a`, `ef2edf2728fddb6e957b711954a191a7461487ee`, `7668d7a320c16ec791935e0d9a1e0c3cc77e965c`, `aa2641e1a053650a88f8b348e6a1e7cb2ba0e1b2`, `fc0fe215031f777089f95cecac0dfe7c9f8ff76a`, `ffd636f4ba69e58fd50c481940e91e908d0df88c`, `8525601e07be5ead657fe5794674ed8c7c91f658`, `9b46d3479c514828b1910251b0f1d8690c102d9f`, `9224f1e8ec74f1f4e0fefc06988f1ec88b150c08`, `4c68efe20b447344f34a6bcdee6552898e64f9e9`, `6b8a01b2f64ffda382ca9a3f1fc437e28ca83252`, `d15a4d0b1bcdaf0d8bf29885051a0ba5f1a01902`, `b3844884cc9bfd09bdda86eddeb5b29feec42dd9`, `36ca57ca3514d681b27ba7c650701e9665b6cf2d`, `d68055727a80b0460d016a623616ee32e0d04a18`, `686a0dbb42e7d6baba24b71277b636f182550c3c`. Initial plan docs commit `3c316f046d028c955d7a1ea057b42847f98899f5`. Они документируют приёмки/полномочия/navigation, не новые независимые tests. Current HEAD20 на входе `673b14f6d66e0aea28324f3d6905d7eb0ede880c`; незакоммиченный Coordination изменяется координатором, не этим исполнителем.

### Manifest20, команды и передача

Exact tracked manifest только AgentBridge:

```text
Documentation/Plans/AgentBridge Audit Remediation/20-final-acceptance.md
Documentation/Plans/AgentBridge Audit Remediation/README.md
Documentation/Plans/AgentBridge Audit Remediation/Decisions.md
Documentation/Plans/AgentBridge Quality Audit/Findings.md
Documentation/Plans/AgentBridge Quality Audit/OpenQuestions.md
Documentation/Plans/README.md
Documentation/README.md
```

Findings/OpenQuestions — только датированные append notes, принятые координатором в частичной границе; прежний body/IDs/evidence сохранены. Их адресный commit разрешён после независимой приёмки. Decisions получает новый checkpoint, старые checkpoints не переписаны. Navigation before называла17–20 не начатыми, общая docs navigation ещё называла facade/kits будущими; after отражает actual partial statuses. API/example code не изменён, новые подписи не придуманы. Соседние tracked manifests пусты; EF/HTTP clean, LB foreign `.vs/` сохранена. Исторические Initial и audit15, source/tests/tooling/generated outputs и slnx не изменены.

Проверочные helpers созданы apply_patch только в ignored `artifacts/stage20`, прочитаны до запуска: file/JSON/XML parsing/hash и разрешённый Git show; CLI helper запускает только постоянные exact19 validate commands. Нет imports/MSBuild/restore/packages/HTTP/DB/host/process faults. Точные запуски из cwd `D:/Media/User/source/repos/agent-bridge`:

```powershell
& './artifacts/stage20/Verify-Acceptance.ps1'
& './artifacts/stage20/Validate-OpenSpec.ps1'
& './artifacts/stage20/Verify-Documents.ps1'
```

Verifier first Exit1: пять ложных helper exit mismatches из-за отсутствующего expectedExit в helper receipts; commands16 success=0 сохранились. Исправлена схема verifier, не source/evidence; final Exit0:25 commits,53 commands16,7 suites,5 kits,2265 copies,17 commands18,19 historical CLI09,issues0. Ошибочное отображение hashtable через Select-Object исправлено, JSON receipt был полным. [Source evidence receipt](../../../artifacts/stage20/source-evidence-verification.json) содержит full commit manifests, TRX/kit hashes и source drift. Strict CLI20 Exit0:current main+original18 valid19/19, errors/warnings0; [полные exact commands/output/start/end/exits](../../../artifacts/stage20/openspec-strict.json). Это новый B CLI20, не archive/sync/product test. Ранее read-only поиск трёх предполагаемых путей artifact/source дал missing path; actual filenames найдены rg, проверки повторены по существующим файлам, missing artifact blocker не остался.

Current strict20 сохранил **6 INFO archive collision**, отдельно от historical INFO2 этапа09: это advisory о повторных ADDED main requirements после дальнейшей реализации12–15, не ERROR/WARNING и не permission archive. Readiness archive не закрыта. Полные diagnostics с exact IDs/headers находятся в receipt; main/current18 validation подтверждена, sync/archive не запускались.

| Evidence20 (ignored artifacts/stage20) | SHA256 |
| --- | --- |
| source-evidence-verification.json | CA475782D2A525C53872C4BDC27A0A328BFAA4E734D6EE7913F3D28ECB5E85CE |
| openspec-strict.json | DECBF456EC4C4BEAD0CE8AFE920BF6E140E283CEEAD1E470EEED0E45E04AB899 |
| Verify-Acceptance.ps1 | 1F3E4639802CFA621A513FF96A5696061ECDF4E95E8B893D73C1CAC6D7A56EFD |
| Validate-OpenSpec.ps1 | 76C4789460571C86F2B9778FA04BDC8B82F8331054F487659E2FFEB385C7EF6B |
| Verify-Documents.ps1 | CDD62B13AD97DF4BAF1ED6100B5A3EE9DE2510F064B4A85D81CD14D7626F5553 |

**Приёмка20, 2026-10-06:** координатор от имени пользователя независимо проверил полный diff7 docs/append notes, исходные категории ABQA/границы/решения/full hashes, corrected31 ID summary, actual strict19 valid/ERROR0/WARNING0/INFO6, UTF-8/manifest hashes, exact historical prefixes обоих реестров и diff check. Принят только документальный итог **частичной приёмки доступного локального объёма**; C/D17–19 и весь план не закрыты. После этой записи разрешены docs-only verifier/scoped diff check и фактический English Conventional docs commit exact7 manifest, Coordination/соседи/outputs исключены. Product/CLI повтор не требовался; результаты прежних команд не изменены. Full hash/actual commit manifest/final status передаются координатору после commit; self hash в отчёте не выдумывается.

Финальный `Verify-Documents.ps1` Exit0:7 tracked files strict UTF-8/no BOM/LF,254 local links/78 fragments,132 slnx paths без missing/duplicates, оба historical bodies unchanged, исходное задание20 неизменно кроме статуса, current19 strict valid/ERROR0/WARNING0/INFO6. [Manifest SHA/encoding/link receipt](../../../artifacts/stage20/document-verification.json) обновляется после последней записи отчёта; hash самого final документа не выдумывается до его сохранения. `git diff --check` Exit0; Git LF→CRLF warning относится к future conversion, actual файлы LF. Source/test/build outputs вручную не менялись; product suites не запускались повторно без новой причины. Координатор принял **итог частичной приёмки доступного локального объёма**, сохранив перечисленные обязательные C/D17–19, и поручил адресный commit7 docs. После него writes останавливаются. Новые ответы о ресурсах сами по себе не разрешают возобновление writes после передачи.
