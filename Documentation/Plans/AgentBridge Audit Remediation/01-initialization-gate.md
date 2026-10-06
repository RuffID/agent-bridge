# 01 — Gate после начавшейся первой установки

[Навигатор](README.md). Статус: **проверен с ограничениями**. Зависимость: 00. Находка: **ABQA-006, S2, подтверждена статически**.

## Цель и область

После отказа начавшейся установки блокировать последующее обслуживание через тот же SingleInitializerGate, независимо от текущего диагностического Stage. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), требование запрета обслуживания после неуспешной первой установки.

Причина принадлежит EFCoreLibrary: [DatabaseMaintenance](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs), [SingleInitializerGate](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/SingleInitializerGate.cs). Изменять эту библиотеку только отдельным поручением. AgentBridge не вводит собственный gate или SQL recovery.

## Работы

1. Добавить изолированный контрпример: Missing → начало CREATE → успешные create/binding → ошибка или OCE в PendingAsync при успешном cleanup → следующий scope на том же gate.
2. Точечно сохранять факт начала необратимой initialization отдельно от меняющегося Stage. Защита должна охватывать отказ CREATE и последующие discovery/recheck/verification, а не только успешный CREATE.
3. Poison выполнять до release lease; ожидающий и последующий entrant не должны добраться до provider/migration boundaries после такого отказа.
4. Сохранить нормальную установку, update existing, ранний отказ до начала установки, caller/deadline classification и safe logging. Не добавлять автоматический reset/retry gate.
5. В AgentBridge добавить адресный сценарий через существующую maintenance-регистрацию и actual coordinator с подставными provider/migration ports. Синхронизировать ближайшие инструкции, если меняется устойчивое правило.

## Проверки

- B, после проверки проекта и правил запуска: тесты `EFCoreLibrary.Maintenance.Tests`; адресные `DatabaseMaintenanceTests` AgentBridge с `Dependency!=Database`.
- Отдельные контролируемые отказы до CREATE, внутри CREATE, в pending после CREATE, в final recheck, на migration/verification. Проверять последовательность poison/release и отсутствие повторных вызовов provider.
- Успешная initialization и допустимое обслуживание existing DB не должны получить ложный poison. Gated задачи освобождать и await в finally.
- Compile-check затронутых конкретных проектов с `GeneratePackageOnBuild=false`. Реальные provider effects относятся к17.

## Критерии завершения

Контрпример воспроизведён до правки и проходит после неё; все контроли сохранены. Защита не зависит от текущего Stage. Есть библиотечное и адаптерное evidence; scope/lease завершаются. Статус C пока не заявляется.

## Результаты

### Область, baseline и изменение

2026-10-06, Asia/Novosibirsk. Выполнено отдельное поручение реализации01; результаты00 приняты в A и сохранены commit `bfd09fda45d5d36f97feec2ee0f3af3a18912acd`. Прочитаны задание01, результаты00 (manifest/контрпример/controls), README/Decisions плана, Findings/итог15, current main spec, root/ближайшие AGENTS обеих областей и C# / backend-UoW skills с references. Исходное задание выше сохранено, кроме статуса. Исторический аудит не переписывался.

Повторный входной Git status/rev-parse: AB HEAD `d68055727a80b0460d016a623616ee32e0d04a18`, изменён только чужой `Documentation/Plans/AgentBridge Audit Remediation/Coordination.md`; EF HEAD `3a8a53187af3c5df049770dfd6727b5065159d1f`, чисто; HTTP HEAD `6d0528d940d1d8494c722c22464051dd961d6bf7`, чисто; LB HEAD `f8ffbac2099a113fba54dfd8d77774f5bca80ffa`, чужая untracked `.vs/`. Эти HEAD не изменялись исполнителем. Coordination принадлежит координатору и может обновляться независимо; исключён из manifest. Новых веток/worktree/чатов/субагентов нет.

Первопричина: после successful CREATE/binding Stage менялся на Discovery, а перед final recheck — на Inspection. Catch отравлял gate только по текущей Stage либо CleanupUnconfirmed. Pending fault/OCE с successful unpin оставлял gate доступным. Теперь private Operation фиксирует `InitializationStarted=true` непосредственно перед provider.CreateAsync; catch использует этот факт независимо от диагностической Stage и выполняет Poison до finally/release. Failure внутри CREATE также покрыт, успешность CREATE не является условием. Public API, schema, provider/CRUD и classification/logging не менялись. `SingleInitializerGate.cs` не изменён: actual EnterAsync уже проверяет poison после wait.

Exact writable manifest, без расширения:

| Repo | Файл | Что изменено |
| --- | --- | --- |
| EFCoreLibrary | `maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs` | Факт начала initialization и poison независимо от Stage |
| EFCoreLibrary | `maintenance/EFCoreLibrary.Maintenance/Coordination/AGENTS.md` | Устойчивое правило начала CREATE, poison до release и pre-CREATE граница |
| EFCoreLibrary | `tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs` | Regression/стадийные controls, success/pre-CREATE controls, finally/await existing fatal waiter |
| EFCoreLibrary | `tests/EFCoreLibrary.Maintenance.Tests/MaintenanceFixture.cs` | Только fake Create/Pending/Pin fault hooks и pin/unpin counters |
| AgentBridge | `tests/AgentBridge.Persistence.EfCore.Tests/DatabaseMaintenanceTests.cs` | Actual DI/coordinator regression, fresh scopes, success/pre-CREATE controls, finally/await existing waiter |
| AgentBridge | `tests/AgentBridge.Persistence.EfCore.Tests/FakeMaintenanceBoundary.cs` | Только fake Pending fault hook |
| AgentBridge | `Documentation/Plans/AgentBridge Audit Remediation/01-initialization-gate.md` | Этот результат |

HTTP/LB production и собственные инструкции не изменялись. Transitive HTTP/provider compilation не разрешает их I/O. Coordination/README/project/generated файлы вне manifest.

### Before/after и controls

До production-правки добавлены EF staged regression и AB Pending regression. Оба выполнены после successful fresh Debug builds, без restore, с отдельными TRX. Сценарий: Missing → successful CREATE → post-create inspection → pin/binding → Pending typed ConnectionFailed / caller OCE / deadline OCE; cleanup успешен. Waiter начат, пока first удерживает gate. Это различает006 от existing migration/cleanup cases, уже отравлявших gate.

| Actual run | Exit | Passed / failed / skipped | Evidence |
| --- | --- | --- | --- |
| EF before, первая версия assertion | 1 | 6 / 4 / 0 | `EF/artifacts/stage01/ef-before.trx` |
| EF before-v2, assertion после awaited waiter | 1 | 6 / 4 / 0 | `EF/artifacts/stage01/ef-before-v2.trx` |
| AB before | 1 | 0 / 3 / 0 | `AB/artifacts/stage01/ab-before.trx` |
| EF after, до дополнительного raw-error control | 0 | 49 / 0 / 0 | `EF/artifacts/stage01/ef-after.trx` |
| AB after | 0 | 40 / 0 / 0 | `AB/artifacts/stage01/ab-after.trx` |
| EF final | 0 | 50 / 0 / 0 | `EF/artifacts/stage01/ef-final.trx` |
| AB final | 0 | 40 / 0 / 0 | `AB/artifacts/stage01/ab-final.trx` |

EF первая версия иногда раньше waiter assertion обнаруживала второй unpin (Expected1/Actual2), поскольку waiter уже прошёл. Assertion перенесён после await waiter; production ещё не изменён. Before-v2 все4 падения непосредственно показывают отсутствие ожидаемого исключения waiter: Pending typed/caller/deadline и final recheck. Остальные6 controls проходили на старом coordinator: CREATE/post-create inspection/pin/binding/migration/verification. AB before все3 Pending cases падали на «No exception was thrown» от waiter. Before свежие DLL были построены actual командами ниже, старые TRX не использовались.

После fix EF final проверяет11 вариантов отказа: CREATE, post-create inspection, pin, binding, Pending typed/raw/caller/deadline, final recheck, migration и verification. Во всех waiter и следующий новый coordinator получают GatePoisoned, Calls и PinCalls не растут, pin освобождён. AB final проходит public maintenance registration → actual coordinator/root singleton gate → fake ports; waiter имеет отдельный DI scope, последующий entrant создаётся в новом scope после отказа. Expected Calls заканчиваются единственным successful unpin, новый scope не добавляет ни provider, ни migration boundary calls. Caller OCE содержит original caller token, deadline — DeadlineExceeded; logger сохраняет Discovery и safe Code без raw exception/secret. Raw Pending control добавлен после before runs и не заявляется отдельным before reproduction.

Positive controls в обоих наборах: successful initialization с pending и без pending, existing update с pending и без pending → subsequent inspection тем же gate разрешён. EF negative controls до CREATE: authentication, permission, connection, invalid target, already existing → gate остаётся доступен; AB permission-before-CREATE → fresh scope может явно initialize. Existing classification/typed-failure priority/safe diagnostics/backup/no fallback controls выполнены в адресных наборах целиком. Все новые gated задачи и затронутые existing gated owner/waiter освобождаются и await в finally, включая ранний assertion/timeout; Record.ExceptionAsync в finally наблюдает expected failures, основные assertions остаются в try. Existing AB successful owner дополнительно await после finally.

### Preflight и точные команды

SDK actual `dotnet --version`: `10.0.401` (Exit0). Проверены конкретные четыре целевых csproj и их transitive ProjectReference graph, source imports/ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json/lock files до корня диска. Source custom Exec/Target hooks в этой цепочке не найдены; HTTP Directory.Build.props перенаправляет только свой test project. Existing assets для net10.0 присутствуют; package sources — nuget.org и existing SDK local source. EF root имеет GeneratePackageOnBuild=true, подавлен явным false во всех командах. DefaultItemExcludes присутствует. Package/config/source references не изменены; restore/network/install не выполнялись. Это не аудит installed SDK/package-generated hooks. Шесть AB Integration classes имеют Dependency=Database; actual filter ниже исключает их. Прочитанные адресные tests используют только doubles/local tasks; no SQLite in-memory.

Ниже **EF cwd** = `D:/Media/User/source/repos/work/EFCoreLibrary`, **AB cwd** = `D:/Media/User/source/repos/agent-bridge`. Относительный BaseOutputPath создаёт output отдельно в каждом project directory; пути DLL берутся из actual build/test output. Все build команды Exit0, warnings0/errors0. Каждому test с --no-build предшествовал successful build того же project/config/output.

EF cwd, before production fix:

```powershell
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ef-before-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ --filter 'FullyQualifiedName~CoordinatorTests.Initialization_failure_blocks_waiter_and_next_coordinator' --logger 'trx;LogFileName=ef-before.trx' --results-directory artifacts/stage01 --diag artifacts/stage01-ef-before-test.log
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ef-before-v2-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ --filter 'FullyQualifiedName~CoordinatorTests.Initialization_failure_blocks_waiter_and_next_coordinator' --logger 'trx;LogFileName=ef-before-v2.trx' --results-directory artifacts/stage01 --diag artifacts/stage01-ef-before-v2-test.log
```

AB cwd, before production fix:

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ab-before-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ --filter 'Dependency!=Database&FullyQualifiedName~DatabaseMaintenanceTests.InitializationDiscoveryFailureBlocksWaitingAndFreshScopes' --logger 'trx;LogFileName=ab-before.trx' --results-directory artifacts/stage01 --diag artifacts/stage01-ab-before-test.log
```

EF cwd, after production fix:

```powershell
dotnet build maintenance/EFCoreLibrary.Maintenance/EFCoreLibrary.Maintenance.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ef-core-after-build.log
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ef-after-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ --filter 'FullyQualifiedName~EFCoreLibrary.Maintenance.Tests.CoordinatorTests' --logger 'trx;LogFileName=ef-after.trx' --results-directory artifacts/stage01 --diag artifacts/stage01-ef-after-test.log
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ef-final-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ --filter 'FullyQualifiedName~EFCoreLibrary.Maintenance.Tests.CoordinatorTests' --logger 'trx;LogFileName=ef-final.trx' --results-directory artifacts/stage01 --diag artifacts/stage01-ef-final-test.log
```

AB cwd, after production fix:

```powershell
dotnet build adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ab-adapter-after-build.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ab-after-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ --filter 'Dependency!=Database&FullyQualifiedName~AgentBridge.Persistence.EfCore.Tests.DatabaseMaintenanceTests' --logger 'trx;LogFileName=ab-after.trx' --results-directory artifacts/stage01 --diag artifacts/stage01-ab-after-test.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ -flp:logfile=artifacts/stage01-ab-final-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage01/ --filter 'Dependency!=Database&FullyQualifiedName~AgentBridge.Persistence.EfCore.Tests.DatabaseMaintenanceTests' --logger 'trx;LogFileName=ab-final.trx' --results-directory artifacts/stage01 --diag artifacts/stage01-ab-final-test.log
```

Final binary SHA256 (Get-FileHash, Exit0):

| Absolute file | SHA256 |
| --- | --- |
| `D:/Media/User/source/repos/work/EFCoreLibrary/tests/EFCoreLibrary.Maintenance.Tests/artifacts/compile-check/stage01/Debug/net10.0/EFCoreLibrary.Maintenance.Tests.dll` | `8B547B694B677BFEED5B95DA5991756DDC305E0874827F8967D392944CEF59B3` |
| `D:/Media/User/source/repos/agent-bridge/tests/AgentBridge.Persistence.EfCore.Tests/artifacts/compile-check/stage01/Debug/net10.0/AgentBridge.Persistence.EfCore.Tests.dll` | `6FEB8CD50A8AFA58785EE1EB8C667FD229888339788F624916968B5937AAD68D` |
| `D:/Media/User/source/repos/work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/artifacts/compile-check/stage01/Debug/net10.0/EFCoreLibrary.Maintenance.dll` | `1529BDDC1AF03DEDAD12B6263F88A39B079E61828C3E0B66C7798DDD7ABF09B2` |

Logs/TRX/DLL только в игнорируемых artifacts; before binaries в том же output затем обновлены fresh after builds. Before build logs/TRX сохранены отдельно, final hashes не выдаются за before binary identity. Suites before/after/final пересекаются; counts не суммируются как independent coverage. Не запускались остальные EF/AB suites, приложение, host, real provider/SQL/DDL, migrations tooling, БД, HTTP, Docker, native/process business operations, deployment, scripts/CLI или C/D.

### Передача и ограничения

Actual: maintenance coordinator/gate/budget и AB registration/scopes/logging. Doubles: provider CREATE/inspection/backup и migration/pin/disposal ports. B подтверждает порядок и запрет вызовов, но не actual CREATE/DDL, provider atomicity, реальную частичную БД или восстановимость. C остаётся17; это не полная приёмка20. Отдельных вопросов/блокеров B нет.

Ручные изменения через apply_patch, UTF-8 без BOM и LF сохранены; strict UTF-8/U+FFFD/четыре question marks проверены без находок. Git diff --check обеих областей Exit0. Собственные файлы ограничены manifest; Coordination и чужая LB .vs/ сохранены, HTTP source status чистый. Этап готов к приёмке координатором. До явной приёмки/поручения add/commit не выполнялись.

**Приёмка01, 2026-10-06:** координатор от имени пользователя принял этап в локальной A/B-границе после независимой проверки production/test diffs, spec733, gate/catch/finally, fresh build/test commands, before-v2 и final TRX, сохранности задания, UTF-8/LF семи файлов и diff --check обеих repo. Поручены отдельные локальные commits по manifest: EF — fix, AB — test; Coordination исключён. Реальные provider effects остаются17. Адресная очистка existing gated tests допустима здесь, но не закрывает весь ABQA-010/этап07: оба исходных места и early-exit evidence проверяются отдельно.
