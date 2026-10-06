# 02 — Сохранение primary error при maintenance cleanup

[Навигатор](README.md). Статус: **принят в A/B-границе**. Зависимость: 00; 01 для совместной регрессии coordinator. Находка: **ABQA-007, S3, подтверждена статически**.

## Цель и область

Сохранить первичную безопасную причину при вторичном отказе cleanup. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), контракт primary/secondary maintenance errors.

Владелец — EFCoreLibrary: DatabaseMaintenance/connection pin, BackupProcessRunner и SqliteBackupStepper. Работы в соседней библиотеке требуют отдельного поручения. Raw exception text, пути/подключения и секреты не становятся public errors или логами.

## Работы

1. Создать различающий тест: BackupNotConfirmed → ошибка CloseConnectionAsync → наружу CleanupUnconfirmed с сохранённым primary code. Использовать actual координацию, а не fake, заранее возвращающий нужный PrimaryError.
2. Проверить и исправить capture primary вокруг владения pin, сохраняя действующую классификацию и poison. Не подменять причину новым generic кодом.
3. Раздельно проверить process primary failure + unknown stop и native copy primary failure/OCE + Finish failure. Внести точечные исправления сохранения причины во всех связанных путях записи ABQA-007.
4. Для отмены и deadline сверить существующий контракт: успешный cleanup сохраняет прежнюю семантику; неподтверждённый cleanup не скрывается штатной отменой. Не придумывать новый error contract внутри реализации.
5. Проверить projection в AgentBridge на actual библиотечной границе. Если изолированная проверка process/native пути требует нового injectable boundary, сначала согласовать его в EFCoreLibrary; реальный процесс не запускать под видом B.

## Проверки

- B: primary-only, cleanup-only, primary+cleanup и successful path для каждого из трёх путей, включая caller/deadline контроли.
- Проверять safe code/PrimaryError, gate blocking, отсутствие начала DDL при неподтверждённом backup; primary не исчезает и secondary не считается успехом.
- Compile-check конкретных maintenance/provider/test проектов, которые действительно изменены; `GeneratePackageOnBuild=false`.
- Если native/process boundary нельзя проверить изолированно, записать незакрытую часть и передать её17. Переход основного сценария на MSSQL не закрывает исходные process/native проявления автоматически. Одного coordinator test недостаточно для закрытия всей записи.

## Критерии завершения

Комбинации ошибок подтверждены адресным evidence; все три проявления учтены. Primary/secondary доступны в действующем безопасном контракте. Интеграционные ограничения явны; код AgentBridge не обходит библиотеку.

## Результаты

### Область и baseline

2026-10-06, Asia/Novosibirsk. Выполнено отдельное поручение02 в EFCoreLibrary maintenance/tests и адресных AB maintenance tests. Принятые00/01 не переписаны:00 `bfd09fda45d5d36f97feec2ee0f3af3a18912acd`; EF01 `be05cc94b5fd7426699e12ea29e8814b361c4e5d`; AB01 `5ced2104a996c066da62d17ab51f09fc84201885`. Прочитаны задание02 полностью, результаты00/01, README/Decisions, актуальные maintenance-требования main OpenSpec и ABQA-007/связанные записи Findings/итог15. Root/ближайшие AGENTS и C# style/build/agents-maintenance, backend-UoW skills использованы для правил области и проверок. Исходное задание выше сохранено, кроме статуса.

Actual вход: AB HEAD `36ca57ca3514d681b27ba7c650701e9665b6cf2d`, чужой modified `Documentation/Plans/AgentBridge Audit Remediation/Coordination.md`; EF HEAD `be05cc94b5fd7426699e12ea29e8814b361c4e5d`, clean. HTTP HEAD из поручения `6d0528d940d1d8494c722c22464051dd961d6bf7`, status clean; LB local HEAD из поручения `f8ffbac2099a113fba54dfd8d77774f5bca80ffa`, прежняя чужая untracked `.vs/`. Coordination и .vs исключены, не изменялись исполнителем. Нет новых веток/worktrees/чатов/субагентов, restore или установки.

### Изменение и exact manifest

Pin раньше освобождался через await using внутри action; secondary CloseConnectionAsync заменял exception до внешнего catch. Теперь private Operation владеет pin, ExecuteAsync фиксирует безопасный primary до DisposeAsync и освобождает pin под gate до классификации/poison/release. InitializationStarted01 и текущие стадии сохранены. EfMigrationOperations не изменён: actual lease по-прежнему безопасно преобразует throwing CloseConnectionAsync в CleanupUnconfirmed, а capture выполняет coordinator.

Runner фиксирует primary до StopAsync; unknown stop возвращает CleanupUnconfirmed + PrimaryError, удерживая handle/private cleanup для явного recovery. Cleanup-only не получает фиктивный primary. При successful stop прежний exception/OCE contract сохраняется. Stepper фиксирует primary до Finish; non-OK return остаётся действующим BackupNotConfirmed + PrimaryError. Throwing Finish означает неизвестный cleanup и даёт существующий CleanupUnconfirmed + PrimaryError. Finish вызывается один раз, receipt и DDL при любом таком отказе отсутствуют.

Важно различие native: возврат non-OK из actual sqlite3_backup_finish сообщает ошибку копирования, но native adapter всё равно освобождает соединения в finally; это известный BackupNotConfirmed, он сам по себе не poisons gate. Exception Finish не подтверждает cleanup, poisons gate через coordinator и блокирует последующий entrant. Классификация return-code пути не заменена новым кодом ради poison. Ни unknown cleanup, ни cancellation не разрешают automatic retry; explicit resource recovery не сбрасывает gate.

Для точного caller/deadline snapshot потребовались private caller token и internal CapturePrimary в существующем MaintenanceBudget. Linked token сам по себе не различает эти источники; Remaining не использован как догадка. SQLite получает friend access через csproj InternalsVisibleTo. Это внутреннее взаимодействие двух maintenance-модулей: новых public error/lifecycle contracts, injectable boundaries или I/O interfaces нет. Existing factory/handle/session/migration interfaces достаточны, поэтому отдельное согласование boundary не потребовалось.

| Repo | Файл | Причина включения |
| --- | --- | --- |
| EF | `maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs` | Capture primary, owned pin cleanup до gate release |
| EF | `maintenance/EFCoreLibrary.Maintenance/Coordination/MaintenanceBudget.cs` | Internal snapshot safe primary, caller/deadline attribution |
| EF | `maintenance/EFCoreLibrary.Maintenance/Coordination/AGENTS.md` | Pin/capture/gate инварианты |
| EF | `maintenance/EFCoreLibrary.Maintenance/Processes/BackupProcessRunner.cs` | Primary при unknown stop |
| EF | `maintenance/EFCoreLibrary.Maintenance/Processes/AGENTS.md` | Ownership и primary/cleanup-only правило |
| EF | `maintenance/EFCoreLibrary.Maintenance.Sqlite/Backup/SqliteBackupStepper.cs` | Primary при Finish return/exception |
| EF | `maintenance/EFCoreLibrary.Maintenance.Sqlite/Backup/AGENTS.md` | Native code/unknown cleanup граница |
| EF | `maintenance/EFCoreLibrary.Maintenance/EFCoreLibrary.Maintenance.csproj` | Friend access internal budget helper, не новый package/API |
| EF | `maintenance/EFCoreLibrary.Maintenance/AGENTS.md` | Карта межмодульного internal доступа |
| EF | `tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs` | Pin матрица, actual runner/stepper projection, no DDL/poison, cleanup OCE |
| EF | `tests/EFCoreLibrary.Maintenance.Tests/MaintenanceFixture.cs` | Fake unpin fault hook/counter |
| EF | `tests/EFCoreLibrary.Maintenance.Tests/ProcessOwnershipTests.cs` | Actual runner, fake completion/stop/disposal/recovery controls |
| EF | `tests/EFCoreLibrary.Maintenance.Tests/NativeBackupTests.cs` | Actual stepper, fake Step/Finish controls |
| AB | `tests/AgentBridge.Persistence.EfCore.Tests/DatabaseMaintenanceTests.cs` | Actual EF pin/CloseConnectionAsync и actual process/native public registration projection |
| AB | `tests/AgentBridge.Persistence.EfCore.Tests/FakeMaintenanceBoundary.cs` | Existing migration port получает actual EF pin через test-only delegate |
| AB | `tests/AgentBridge.Persistence.EfCore.Tests/FakePinnedConnection.cs` | Fake DbConnection open/close; команды/SQL запрещены |
| AB | `Documentation/Plans/AgentBridge Audit Remediation/02-maintenance-primary-errors.md` | Этот отчёт |

CRUD/UoW, schema/generated, provider SQL, HTTP/LB, root README/spec, другие этапы и Coordination вне manifest. AB production adapter не исправлялся: его public registration уже делегирует actual EF coordinator; projection подтверждена tests.

### Before/after evidence

Регрессии добавлены до production fix и выполнены после successful fresh builds. EF before-v2: primary typed/raw/caller/deadline × secondary failure для каждого из трёх путей. Все12 отказов непосредственно показывают ожидаемый PrimaryError vs actual null. Остальные18 positive/negative controls прошли на старом коде. EF первый before использовал raw IOException fake unpin, поэтому пять pin cases падали уже на outer code (BackupFailed/ConnectionFailed вместо CleanupUnconfirmed); затем fake unpin приведён к реальному safe lease contract, before-v2 сохранён отдельно. Это уточнение test boundary, не подмена первичной причины.

AB before использовал actual EfMigrationOperations.PinAsync → EF DatabaseFacade.CloseConnectionAsync → fake DbConnection.CloseAsync. Typed receipt failure + close failure и caller/deadline + close failure потеряли primary. Четвёртое падение — positive deadline control: холодная инициализация EF израсходовала60ms до pin, Closes был0. Для целевого deadline-path бюджет увеличен до1s; этот первый cold-start failure не является product дефектом. Остальные четыре controls прошли. После fix все восемь actual-pin cases прошли, Closes=1. Более поздние projection/late-cleanup controls не заявляются отдельным before reproduction.

| Run / TRX относительно cwd | Exit | Passed / failed / skipped |
| --- | --- | --- |
| EF `artifacts/stage02/ef-before.trx` | 1 | 17 / 13 / 0 |
| EF `artifacts/stage02/ef-before-v2.trx` | 1 | 18 / 12 / 0 |
| AB `artifacts/stage02/ab-before.trx` | 1 | 4 / 4 / 0 |
| EF `artifacts/stage02/ef-final.trx` | 0 | 106 / 0 / 0 |
| AB `artifacts/stage02/ab-after.trx` | 0 | 48 / 0 / 0 |
| EF `artifacts/stage02/ef-final-v2.trx` | 0 | 108 / 0 / 0 |
| AB `artifacts/stage02/ab-final.trx` | 0 | 51 / 0 / 0 |

Final EF108 включает existing Coordinator50 этапа01 и новые pin/process/native controls; отдельно direct ProcessOwnership и NativeBackup. Final AB51 включает existing maintenance40, actual EF pin8 и actual backup projection3. Матрицы каждого direct пути включают primary-only, cleanup-only, both и success, caller/deadline. Actual coordinator дополнительно проверяет process unknown/native throwing Finish, caller/deadline primary, отсутствие migrate и последующего boundary entry. Native non-OK code control сохраняет пригодность gate к inspection, без автоматического повторного backup. Cleanup OCE с late canceled caller проверена с primary и без primary: наружу CleanupUnconfirmed, а не штатная caller cancellation.

Process controls подтверждают retained handle без Dispose до explicit confirmed recovery, затем одно Dispose и UnconfirmedCount0. Новые tests завершают fake-owned work/recovery в finally; gated regressions01 с finally/release/await повторены. Existing ABQA-010 early-exit evidence/UnitOfWorkScopeTests остаётся задачей07, не объявлено закрытым здесь. Primary-only raw exceptions при successful cleanup direct runner/stepper сохраняют прежний contract; coordinator классифицирует их безопасно, error.ToString/InnerException/logging не раскрывают synthetic secret.

### Preflight и точные команды

SDK `dotnet --version`:10.0.401, Exit0. Проверены конкретные EF maintenance/SQLite/tests и AB adapter/tests csproj, transitive source ProjectReference/Imports/Exec/Target, ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json/lock names до D:/, existing net10.0 assets/package sources. Source executable hooks в этой graph не найдены; HTTP Directory.Build.props меняет только output собственного HTTP test project. Existing sources nuget.org и SDK local NuGetPackages, restore/network не выполнялись. DefaultItemExcludes присутствует. EF root GeneratePackageOnBuild=true подавлен явным false. Шесть actual AB integration classes имеют Dependency=Database и исключены фильтром. Нет SQLite in-memory, native engine initialization или real process start. Installed SDK/package generated hooks отдельно не аудитировались.

Ниже EF cwd = `D:/Media/User/source/repos/work/EFCoreLibrary`, AB cwd = `D:/Media/User/source/repos/agent-bridge`. BaseOutputPath относительный и создаёт отдельный output в каждом project directory. Все build команды Exit0. Единственная промежуточная EF after сборка имела два xUnit2031 warning; assertions исправлены, все final builds warnings0/errors0. Failed build/stale DLL/no-build evidence отсутствует.

EF before и before-v2 (каждому test предшествует собственный successful build):

```powershell
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ef-before-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ --filter 'FullyQualifiedName~Pin_cleanup_preserves_primary|FullyQualifiedName~Unknown_stop_preserves_primary|FullyQualifiedName~Finish_preserves_primary' --logger 'trx;LogFileName=ef-before.trx' --results-directory artifacts/stage02 --diag artifacts/stage02-ef-before-test.log
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ef-before-v2-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ --filter 'FullyQualifiedName~Pin_cleanup_preserves_primary|FullyQualifiedName~Unknown_stop_preserves_primary|FullyQualifiedName~Finish_preserves_primary' --logger 'trx;LogFileName=ef-before-v2.trx' --results-directory artifacts/stage02 --diag artifacts/stage02-ef-before-v2-test.log
```

AB before:

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ab-before-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ --filter 'Dependency!=Database&FullyQualifiedName~ActualPinCleanupPreservesPrimary' --logger 'trx;LogFileName=ab-before.trx' --results-directory artifacts/stage02 --diag artifacts/stage02-ab-before-test.log
```

EF после fix, первый адресный run:

```powershell
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ef-after-build.log
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ef-final-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ --filter 'FullyQualifiedName~CoordinatorTests|FullyQualifiedName~ProcessOwnershipTests|FullyQualifiedName~NativeBackupTests' --logger 'trx;LogFileName=ef-final.trx' --results-directory artifacts/stage02 --diag artifacts/stage02-ef-final-test.log
```

AB после fix, первый адресный run:

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ab-after-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ --filter 'Dependency!=Database&FullyQualifiedName~DatabaseMaintenanceTests' --logger 'trx;LogFileName=ab-after.trx' --results-directory artifacts/stage02 --diag artifacts/stage02-ab-after-test.log
```

Финальные concrete builds и повтор адресных наборов после последних tests:

```powershell
# EF cwd
dotnet build maintenance/EFCoreLibrary.Maintenance/EFCoreLibrary.Maintenance.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ef-core-final-build.log
dotnet build maintenance/EFCoreLibrary.Maintenance.Sqlite/EFCoreLibrary.Maintenance.Sqlite.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ef-sqlite-final-build.log
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ef-final-v2-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ --filter 'FullyQualifiedName~CoordinatorTests|FullyQualifiedName~ProcessOwnershipTests|FullyQualifiedName~NativeBackupTests' --logger 'trx;LogFileName=ef-final-v2.trx' --results-directory artifacts/stage02 --diag artifacts/stage02-ef-final-v2-test.log
# AB cwd
dotnet build adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ab-adapter-final-build.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ -flp:logfile=artifacts/stage02-ab-final-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage02/ --filter 'Dependency!=Database&FullyQualifiedName~DatabaseMaintenanceTests' --logger 'trx;LogFileName=ab-final.trx' --results-directory artifacts/stage02 --diag artifacts/stage02-ab-final-test.log
```

Binary SHA256 после final (Get-FileHash, Exit0):

| Файл относительно cwd | SHA256 |
| --- | --- |
| EF `tests/EFCoreLibrary.Maintenance.Tests/artifacts/compile-check/stage02/Debug/net10.0/EFCoreLibrary.Maintenance.Tests.dll` | `417DAAB3372FF9C54735A7D3EF6584323467D906963003F0FB4C96B63B37AA7C` |
| EF `maintenance/EFCoreLibrary.Maintenance/artifacts/compile-check/stage02/Debug/net10.0/EFCoreLibrary.Maintenance.dll` | `61584331CF6C412EDCC9E26A52496F72AF3462FE77F9C47EC1C9D180F39397C0` |
| EF `maintenance/EFCoreLibrary.Maintenance.Sqlite/artifacts/compile-check/stage02/Debug/net10.0/EFCoreLibrary.Maintenance.Sqlite.dll` | `0CCC30472B56683AE6A49341871E7135DE907C2F84668ED9E6FDE283EE275811` |
| AB `tests/AgentBridge.Persistence.EfCore.Tests/artifacts/compile-check/stage02/Debug/net10.0/AgentBridge.Persistence.EfCore.Tests.dll` | `E6FF35C27B4AADEA43C722DF8808D2CB1D822E992AA615EEB3F329EA4635BC55` |

Before/after используют один stage02 output, обновлённый fresh builds; отдельные before logs/TRX сохранены, final hashes не являются before binary identity. Suites пересекаются и не суммируются как independent coverage. Outputs/TRX/logs только в игнорируемых artifacts.

### Граница и передача

Actual: DatabaseMaintenance/SingleInitializerGate/MaintenanceBudget, BackupProcessRunner, SqliteBackupStepper; AB public registration/root scopes/logging и actual EfMigrationOperations/EF DatabaseFacade pin/close. Doubles: provider inspection/backup/receipt/migration ports, DbConnection без команд, process factory/handle/Completion/stop, native Step/Finish session. Actual algorithm evidence получено отдельно для pin/process/native; fake не возвращает заранее построенную комбинацию PrimaryError. Это не real process termination, reader OS lifecycle, sqlite3_backup_finish, SQL/DDL или provider atomicity.

ABQA-007 исправлен и подтверждён в локальной A/B-границе всех трёх проявлений; полное C-закрытие не заявлено.17 должен проверить actual CloseConnection/provider errors, process/pipe stop + retained credentials/recovery, native step/Finish/release и caller/deadline на выделенных ресурсах. MSSQL-переход не заменяет эти process/native проверки. C17/DDL/БД/SQL/native/process, приложение/hosting/Docker/deployment/real HTTP/CLI/restore/pack/publish не запускались. Отдельных B-blockers или вопросов boundary нет.

Ручные правки — apply_patch. Финальный контроль17 собственных файлов: strict UTF-8 без BOM/LF, без U+FFFD/четырёх вопросительных знаков/проверенных mojibake markers, issues0. Diff --check обеих repo Exit0; сравнение исходного задания02 до раздела результатов с HEAD, кроме статуса, совпало. Принятые00/01 не менялись. HTTP/LB HEAD повторно подтверждены указанными выше значениями; HTTP clean, LB сохраняет чужую .vs. Чужой Coordination не включён. Add/commit не выполнялись. Этап готов к независимой приёмке координатором; остановка после отчёта, commits только по отдельному поручению после review.

**Приёмка02, 2026-10-06:** координатор от имени пользователя принял этап в A/B после независимой проверки production/test diffs, fresh builds/точных команд, TRX108/108 и51/51, UTF-8/LF17 manifest files и diff checks. Поручены отдельные локальные commits: EF fix только13 файлов manifest, AB test только4 файла manifest. Coordination исключён. C17 real provider/process/native остаётся открытым; приёмка B не заменяет реальное evidence.
