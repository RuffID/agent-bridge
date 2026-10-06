# 17 — Проверка provider maintenance и сохранения

[Навигатор](README.md) · [Решения](Decisions.md). Статус: **подготовка A/B; заблокирован C**. Зависимости: 01,02,05,07,12–16; конкретные ресурсы **Q-002**, отдельное разрешение C. Область: собственные временные SQL Server ресурсы и адресная регрессия existing providers.

## Цель и основание

Подтвердить actual MSSQL migrations/CRUD/CAS/journal и исправленные maintenance/Restore пути. Источники — [решение об основной БД](Decisions.md), [аудит05](<../AgentBridge Quality Audit/05-migrations-and-maintenance.md>), [Integration/AGENTS](../../../tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md). Старые Integration правила разрешают лишь SQLite/PostgreSQL: при реализации MSSQL их нужно явно расширить, а не считать старое разрешение подходящим.

Исторический общий DB00–13 был на EFCoreLibrary0.0.4, а compatibility0.0.5 не был его полным повтором. Этот этап даёт новое адресное evidence; не требуется заново исполнять весь исторический аудит.

## Подготовка и работы

1. Согласовать SQL Server version/edition/runtime/auth/TLS, собственные БД, server backup destination, команды запуска и очистки. SQL не выполнять в рамках этого плана; отдельное будущее разрешение должно явно охватывать соответствующие операции. Определить opt-in и фильтр `Dependency=Database` по актуальным tests; старый PostgreSQL opt-in не включать автоматически.
2. Проверить post-CREATE failure/OCE до migration и запрет следующего entrant на том же root gate. Реальное состояние созданной БД зафиксировать; очистка не считается автоматическим recovery приложения.
3. Проверить backup primary + connection cleanup secondary с actual SQL Server provider, safe codes и poison. Existing PostgreSQL process/SQLite native проявления ABQA-007 проверить адресно либо оставить их явным незакрытым остатком; переход на MSSQL не опровергает исходную находку.
4. Провести разрешённый SQL Server migration/backup/restore round-trip с отдельными test target и destination; проверить schema и данные после restore. Проверить серверный receipt/verification по EFCoreLibrary; путь и права принадлежат серверу БД. Down допускается только для собственных уничтожаемых ресурсов и отдельно согласованной команды.
5. Проверить корректный Dialog Restore round-trip с append/context. Некорректное состояние не подготавливать прямой SQL-правкой без разрешения; если соответствующий scenario остаётся только в B, явно сохранить границу.
6. Адресно проверить root/settings races, journal+outputs rollback и неизвестный commit в затронутом persistence contract через EFCoreLibrary и существующие сценарные UoW. Не заменять библиотеку прямым EF/SQL.
7. После проверки выполнить только разрешённую очистку собственных ресурсов с проверкой абсолютных путей/идентичности; сохранить sanitized evidence и manifests без секретов.

## Проверки

C: основной SQL Server сценарий, версии/DDL/backup/restore receipts/exit codes, подтверждённые scope outcomes и absence of replay. Адресные existing SQLite/PostgreSQL проверки выполняются только по отдельному разрешению ресурсов. Реальная потеря ack не доказывается подставным исключением после commit; crash/network scenarios относятся к19.

Не включать MySQL, чужие БД, production data, произвольный destructive Down или очистку shared directories. Наличие integration tests/env flag не заменяет разрешение. Linux ARM64 app client не требует размещения SQL Server Engine на той же машине.

## Критерии завершения

У основного SQL Server сценария есть текущее evidence и подтверждённая очистка собственных ресурсов; existing providers проверены в согласованной затронутой области. Для ABQA-006/007/005 различены B и C conclusions. Невоспроизведённые native/process отказы перечислены, а не закрыты общим успешным MSSQL backup.

## Результаты

### Выполненная часть и полномочия

2026-10-06, Asia/Novosibirsk. Исходное задание выше сохранено, изменён только статус. После запроса ресурсов пользователь ответил: **«Подготовь fixtures, прямые тесты будут позже»**. Выполнена только подготовка A/B; реальные MSSQL/SQLite/PostgreSQL, SQL, native/process, backup/restore/cleanup и C/D не запускались. Это не завершение17 и не provider readiness.

Before: IntegrationDatabase поддерживает SQLite/PostgreSQL с общим opt-in и собственной automatic cleanup; отдельного MSSQL fixture нет. After: отдельные MSSQL Settings/Database/Fact, fail-fast configuration checks и один prepared C-case. Production/dependencies/migrations/public API не менялись; оснований для изменения EFCoreLibrary не обнаружено. Decisions Q-003–005 не переоткрывались. Прочитаны текущие root/tests/Integration, Documentation/Plans и adapter/library AGENTS, README/Decisions, Results01/02/05/07/12–16, итог аудита15 и соответствующие main spec clauses. Применены csharp-project-rules/style/build-validation/agents-maintenance, backend-uow-repositories/reference и ef-core-migrations; migration tooling не запускался.

MSSQL constructor проверяет отдельный opt-in, точный TCP endpoint с портом, согласованное уникальное имя `abverify_<GUID N>`, явные Encrypt/TrustServerCertificate/ConnectRetryCount=0, Pooling=false и отсутствие failover/read-only/attach. Серверный backup path проверяется как серверный путь без обращения к локальной файловой системе; budget явно задаётся и ограничен пятью минутами. Configuration parser ошибки не раскрывают исходную строку/inner exception. Constructor создаёт только DI, не соединение/файлы/каталоги; InitializeNewAsync отдельно вызывает existing maintenance/CREATE/migration через EFCoreLibrary. BuildRoot допускает будущую адресную подстановку failure boundary, но сам её не создаёт.

Один prepared `SqlServerPersistenceIntegrationTests.InitializeAppendContextRestartAndExpiry` помечен `Dependency=Database` и отдельным `AGENTBRIDGE_SQLSERVER_INTEGRATION=1`; старый `AGENTBRIDGE_INTEGRATION` его не включает. Case подготовлен для actual migration → public create/begin/append/finish/context → новый root/read, full envelope/continuation/context, stale token и exact expiry. Ordinal controls работают на **той же stored row**: каждый из `Owner`, `owner`, `Owner `, `Owner\0`, `Owner\0\0`, `Owner\ud800`, `Owner\ud801` сохраняется и читается точно; все остальные значения отвергаются при read и BeginAsync. Таким образом проверяются обе стороны prefix/suffix/case/NUL/trailing-zero/unpaired UTF-16, а не только независимые строки. Это подготовленные assertions, не исполненное C evidence; application guard сам по себе не доказывает серверное сравнение/CAS.

MSSQL DisposeAsync освобождает **только DI**. DROP/Down/restore/backup-file cleanup не реализованы и не подразумеваются. При будущем run собственная БД останется даже после failure; её имя/фактическая identity должны быть сохранены в защищённом журнале и сверены перед отдельно согласованной очисткой. Prefix/GUID не доказывает владение. Фикстура не переносит legacy destructive cleanup на неизвестный сервер и не сбрасывает root gate.

### Точный writable manifest и baseline

```text
tests/AgentBridge.Persistence.EfCore.Tests/Integration/SqlServerIntegrationSettings.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/SqlServerIntegrationDatabase.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/SqlServerIntegrationFactAttribute.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/SqlServerPersistenceIntegrationTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/SqlServerIntegrationSettingsTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md
Documentation/Plans/AgentBridge Audit Remediation/17-provider-verification.md
```

AgentBridge HEAD `cc69ecc57d061fb22357de908af682cd4c10bc57`. Принятый16 `b1e966445c70f30cad63cc5ba690808c75c7fd35`; source snapshot16 содержит474 inputs, из них сейчас отличается только собственный Integration/AGENTS. Новый compile snapshot479 добавляет пять C# файлов; production source сохраняется. Соседи read-only, manifests пусты: EF `5962deceb6ea01306cbbda400040db88ea8e5df9`, HTTP `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4` чисты; codex-lb `f2b8e042c4ce012ae703bc939413bf9961b00032` сохраняет foreign untracked `.vs/`. Чужой Coordination.md сохранён и исключён из manifest. Index/add/commit/push не менялись.

Все пять kits16 заново сверены по каждому manifest entry size/SHA256: mismatch0. SQLServer/win-x64140 entries, linux-x64138, linux-arm64137; Sqlite/PostgreSql win-x64 по140. Manifest SHA совпадают с Results16 и записаны в `artifacts/stage17/verification.json`. Kits не пересобирались/не загружались, это continuity A, не runtime evidence18.

### Preflight, команды и результаты B

Проверен recursive ProjectReference graph конкретного persistence test csproj:14 проектов,82 csproj/ancestor/generated/package inputs с SHA256 в `artifacts/stage17/preflight-inputs.json`. Directory.Build.props HTTP задаёт override только своему test project; новых source hooks/imports/Exec нет. Assets присутствуют, package imports прочитаны и проверены на Exec. Existing NuGet sources в assets включают nuget.org/SDK packages, но **restore не выполнялся**, builds/tests используют --no-restore; network sources/downloads не добавлялись. EF packaging подавлен `GeneratePackageOnBuild=false`, только concrete Build, не solution/Rebuild. Traits исключают все actual Integration cases: `Dependency!=Database`; новые isolated21 cases проверяют только SqlConnectionStringBuilder/локальные args без DI, соединений/native.

SDK `10.0.401`, Windows, Debug/net10.0. Current graph: EF10.0.11, Npgsql10.0.3, SqlClient6.1.6, SQLitePCLRaw2.1.12, EFCoreLibrary0.0.5, HTTP FileVersion0.0.0.5, Test.Sdk18.0.1/xUnit2.9.3/VS3.1.5; полные assets/deps связаны hashes preflight/output snapshot. Actual команды из cwd AgentBridge:

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/stage17/compile/ -flp:logfile=D:/Media/User/source/repos/agent-bridge/artifacts/stage17/build.log -v:minimal
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/stage17/compile/ --filter 'Dependency!=Database' --logger 'trx;LogFileName=persistence-isolated.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage17/results
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/stage17/compile/ --filter 'Dependency!=Database&FullyQualifiedName~ActualPinCleanupPreservesPrimary' --logger 'trx;LogFileName=pin-focused.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage17/results
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/stage17/compile/ -flp:logfile=D:/Media/User/source/repos/agent-bridge/artifacts/stage17/build-final.log -v:minimal
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/stage17/compile/ --filter 'Dependency!=Database' --logger 'trx;LogFileName=persistence-isolated-final.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage17/results
```

| Run | Exit | Passed/failed/skipped | Граница |
| --- | --- | --- | --- |
| First Build | 0 | warnings0/errors0 | Fresh source до уточнения same-row C controls |
| First isolated | 1 | 309/1/0 | Existing ActualPinCleanupPreservesPrimary(deadline,false): expected Closes1, actual0 |
| Focused pin | 0 | 8/0/0 | Успешно собранный first binary; pin source не менялся. C controls уже редактировались, поэтому это не final-source evidence |
| Final Build | 0 | warnings0/errors0 | Fresh final source с same-row ordinal pairs |
| Final isolated | 0 | 310/0/0 | Final binary; новые21 isolated cases включены, все C исключены |

Первый failure сохранён, не объявлен product/provider defect: failing assertion подтверждает отсутствие pin cleanup до deadline, но не устанавливает его причину. Existing test имеет1s deadline, холодная EF инициализация ранее описана Results02; focused8 и final310 прошли без изменений этого теста/production. Это наблюдаемый timing-sensitive результат B, не исправление его устойчивости. First/focused/final suites пересекаются и не суммируются. First outputs не закреплены отдельным immutable snapshot до final overwrite; первые TRX/log остаются диагностикой, definitive source/binary hashes принадлежат final run. После failed **test** использовался успешно собранный binary; failed build/stale DLL не было.

Generated evidence только ignored `artifacts/stage17/`. XML TRX rows сверены с counters, mismatch0; start/finish и SHA каждого TRX — verification.json. Final suite15:30:28–15:30:32 UTC+07:00. Final source479 и final managed/XML/deps/PDB outputs — отдельные snapshots, без secrets.

| Artifact относительно artifacts/stage17 | SHA256 |
| --- | --- |
| preflight-inputs.json | cc6a0b19baffc395928a5a51c260cee825f01a14b32a3a103f6afd50ab3a194b |
| source-snapshot.json | 2a862a874427ec1e5c7030cf886d2f4239b5a2c018367ad7ac0ca3b48a348c41 |
| output-snapshot.json | eb5f6a8db7ec954058cd4c735db9af9b61287aa36a56464b485debdd0e606524 |
| verification.json | f5f76cd97be9e3cc97d19120ed7716c45d4199f325c205ad134df6bc0354f6be |
| build.log | 24901854cf616b8b120f0716f071270d9d06fbed67f440066157d83904afca52 |
| build-final.log | bf3a51bcb01e728db4709650d37ea9819b95b7ab93fe795cd6e5cc81db1d8421 |
| results/persistence-isolated.trx | 049cd23a28ce0bc7569a5e30452f4ebc27ed303cef8a1ad9808e6e85fd959341 |
| results/pin-focused.trx | 5115324c944740882db58703877740f1d3e0cecc1d3213e52885f75696fb0373 |
| results/persistence-isolated-final.trx | 789e078d407263970d21649b99b1873793a5b0e120ac65f256c33d1bd56910d1 |

### Незапущенная матрица C и форма ресурсов

| Обязательная область | Подготовка / фактическая граница |
| --- | --- |
| MSSQL migration, basic CRUD/create/read, stale root, append/context/restart/expiry | Один prepared case, только compile; delete/cascade не подготовлены этим case |
| Ordinal owner case/space/NUL/trailing-zero/unpaired UTF16 | Same-dialog read/write pair controls подготовлены; actual SQL equality/CAS не проверены |
| Root/settings CAS, first insert/update races, mixed races | Existing B и legacy SQLite/PG tests; адресный MSSQL C-case ещё не подготовлен/не запущен |
| Journal+outputs real rollback и scope outcomes | Legacy tests/doubles не перенесены автоматически; MSSQL case ещё не подготовлен/не запущен |
| Post-CREATE failure/OCE, poison до next entrant на том же root | Fixture сохраняет Root и допускает customization; fault case/actual созданная БД/очистка ещё не подготовлены/не проверены. Results01 B не заменяет C |
| Backup primary+connection cleanup secondary / safe codes / poison | Results02 B; actual SQLServer fault fixture/case ещё не подготовлен/не запущен |
| Server backup receipt/header/VERIFYONLY/restore roundtrip schema+data | Existing provider source проверен, fixture имеет server directory; backup/restore case/отдельная target/SQL/cleanup ещё не подготовлены/не запущены |
| Domain Dialog Restore/append/context | Public restart/read/write prepared case; backup restore/полный восстановленный journal/settings и malformed-state case не проверены |
| ABQA-007 PostgreSQL process и SQLite native primary/OCE+cleanup | Отдельный незакрытый C остаток; новый MSSQL successful path его не опровергает |
| Unknown commit/real ack loss/crash | Synthetic throw не реальная авария; реальные C/D scenarios относятся19, не запускались |
| EngineEdition2/3/4, explicit TLS/auth/stable endpoint/ConnectRetryCount0 | Локальный parsing и provider metadata guard; actual server/edition/TLS identity не проверены. Azure/MI/Synapse и SQL Engine ARM64 не обещаны |
| Down/cleanup | Только будущие согласованные собственные объекты с actual identity check; подготовленной destructive команды нет |

Для будущего запуска владелец ресурсов отдельно заполняет sanitized карточку: SQL Server version/edition/EngineEdition, OS/архитектура **сервера**, exact endpoint, authentication mode/права и локальный secret source, Encrypt/TrustServerCertificate/сертификат, source и отдельная restore target, server backup destination/служебные права, operation budget и список разрешённых create/migrate/backup/restore/Down/cleanup операций. Указать server/database GUID identity check, конкретные собственные backup filenames/paths, внешнего владельца cleanup и подтверждение итогового удаления. Значения secrets не включать в карточку/чат/TRX/evidence. SQLite/PG ресурсы отдельно; старое разрешение не покрывает неизвестный MSSQL.

Prepared fixture читает только:

| Environment key | Значение / владение |
| --- | --- |
| AGENTBRIDGE_SQLSERVER_INTEGRATION | 1 только при отдельном разрешённом запуске C |
| AGENTBRIDGE_SQLSERVER_CONNECTION | Локальный secret source; не сохранять строку в evidence |
| AGENTBRIDGE_SQLSERVER_ENDPOINT | Согласованный exact tcp:host,port; без defaults |
| AGENTBRIDGE_SQLSERVER_DATABASE | Exact собственное уникальное abverify_<GUID N>; fresh отсутствующая БД для InitializeNew |
| AGENTBRIDGE_SQLSERVER_BACKUP_DIRECTORY | Абсолютный путь на сервере, fixture не создаёт/не очищает |
| AGENTBRIDGE_SQLSERVER_BUDGET_SECONDS | Явный целый положительный бюджет, не более300s |

Команда будущего C и destructive cleanup намеренно не выдаются как готовые разрешённые операции: реальные ресурсы/права/identity/restore path ещё не заданы. Наличие env/config/fixture не разрешает запуск.

### Передача

Final source snapshot479 повторно совпал с текущими inputs; final test DLL SHA256 `937b862c6895905b6903c44275c13ac993070206e01c8701e905d11298a498eb`. Проверены семь manifest files: strict UTF-8 без BOM/LF, без U+FFFD/четырёх question marks/проверенных mojibake markers; scoped diff --check Exit0, index пуст. Исходное задание до Results совпадает с HEAD кроме статуса. Первая read-only encoding команда имела PowerShell parser error из-за unicode quote в mojibake pattern; исправленная проверка использовала codepoints и завершилась Exit0, файлы при ошибке не менялись. Per-file manifest SHA и encoding evidence — `artifacts/stage17/manifest-validation.json`; после этой итоговой записи checksum документа обновляется отдельной финальной сверкой, build source не меняется.

Доступная подготовка A/B готова к независимому review координатором; **этап17 заблокирован C по решению пользователя**. Fixtures не составляют полную исполняемую матрицу17. Production/API, schema и соседние репозитории сохранены. Writes останавливаются; add/commit возможны только после явной приёмки и отдельного поручения exact manifest. Поздние ресурсы/ответ не возобновляют writes после handoff без нового допуска координатора. Commit и общая приёмка17–20 не заявляются.
