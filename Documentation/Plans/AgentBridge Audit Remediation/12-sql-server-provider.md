# 12 — Подключение Microsoft SQL Server

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **принят в A/B; реальные проверки17/18 ожидаются**. Зависимости: 00,01,02,05,07. Область: новый provider AgentBridge; EFCoreLibrary без обхода.

## Цель и исходное состояние

Основной сценарий — MSSQL, .NET10, приложение на Windows/Linux. Сейчас AgentBridge enum/registration/migrations поддерживают только SQLite/PostgreSQL. В EFCoreLibrary уже существует [SQL Server maintenance](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.SqlServer/AGENTS.md); его существование не доказывает реальную MSSQL-проверку AgentBridge.

## Работы

1. Сверить actual EFCoreLibrary CRUD/context/maintenance API и supported SQL Server editions. Использовать существующие base repositories и сценарные UoW; не добавлять собственный coordinator, raw SQL или обходной persistence layer.
2. Добавить явный provider SQL Server, соответствующее конфигурирование EF, зависимости и migrations identity. Не переключать provider автоматически при ошибке подключения; не менять значения existing enum несовместимым образом.
3. Создать отдельный library target/startup migrations проект по существующим правилам решения, с собственными AGENTS/design-time factory. Генерацию выполнить tooling только после согласования точной команды; generated migration/snapshot/designer вручную не править.
4. Проверить model mapping шести таблиц, composite keys/FK/cascades, JSON/bytes, UTC ticks, revision/settings CAS и journal atomicity на provider-specific metadata. Устранить реальные SQL Server особенности каскадов/типов, сохранив контракт; schema integration evidence отдельно17.
5. Подключить actual SQL Server maintenance module EFCoreLibrary. Settings для backup — серверный путь, receipt/verification и privileges по текущему API. Путь на Windows/Linux app host не подменяет server backup destination.
6. Согласовать и документировать поддержку конкретной редакции/версии/TLS/auth; Linux ARM64 app может подключаться к отдельному поддержанному серверу БД. Azure SQL/MI/Synapse и локальный SQL Server Engine ARM64 не объявлять поддержанными по этой работе.
7. Обновить карту проектов/ближайшие AGENTS, `.slnx`, technical/provider docs и binary consumer для существующего API. Устаревший PostgreSQL example заменить MSSQL только после реальной реализации/compile-check16.

## Проверки

B: provider/options/DI metadata, mapping, history identity, actual EFCoreLibrary registrations и безопасные errors без открытия соединения. Собирать конкретные новые/затронутые проекты с `GeneratePackageOnBuild=false`. Для изолированного persistence набора сохранять фильтр `Dependency!=Database`.

C в17: собственный SQL Server, migrations/CRUD/CAS/journal/backup/restore. SQL и DB-команды отдельно разрешаются; отсутствие сервера не позволяет считать интеграцию успешной.

## Критерии завершения

Есть actual SQL Server provider/migrations/maintenance и адресное B evidence. SQLite/PostgreSQL не сломаны и не выбираются как fallback. Область MSSQL-поддержки документирована; сведения о реальных provider outcomes относятся только к выполненному17.

## Результаты

### Область, дата и baseline

2026-10-06, Asia/Novosibirsk. Реализован только Audit Remediation12 в локальной A/B-границе, ожидает приёмки координатором. Прочитаны задание12, принятые Results00/01/02/05/07, README/Decisions, Findings/итог15, current main OpenSpec, root/EF adapter/UoW/tests/migrations/Delivery/Documentation AGENTS. Использованы csharp-project-rules (style/domain/build/agents references), backend-uow-repositories и ef-core-migrations. Принятые решения не переоткрывались. Исходное задание этого этапа сохранено, изменён только статус и Results.

AgentBridge входной HEAD `aa2641e1a053650a88f8b348e6a1e7cb2ba0e1b2`; исходное единственное изменение — чужой Coordination.md. Финальный HEAD тот же, index не изменялся. Соседи только read-only: EFCoreLibrary `5962deceb6ea01306cbbda400040db88ea8e5df9` (0.0.5), HttpClientLibrary `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4` (0.0.0.5), codex-lb `f2b8e042c4ce012ae703bc939413bf9961b00032`. На выходе EF/HTTP чистые; LB сохраняет чужую untracked .vs/. Coordination не входит в manifest.

### Exact manifest

Единственный writable repo — AgentBridge (37 файлов). Соседние manifests пусты; artifacts/obj/build outputs не входят в исходный manifest.

- `AGENTS.md`
- `README.md`
- `agent-bridge.slnx`
- `openspec/specs/agent-runtime/spec.md`
- `Documentation/Plans/AgentBridge Audit Remediation/README.md`
- `Documentation/Plans/AgentBridge Audit Remediation/12-sql-server-provider.md`
- `Documentation/Technical documentation/05-configuration-and-lifecycle.md`
- `Documentation/Technical documentation/06-database-maintenance.md`
- `Documentation/Technical documentation/11-provider-migrations.md`
- `Documentation/Technical documentation/12-sql-server-provider.md`
- `Documentation/Technical documentation/README.md`
- `adapters/AgentBridge.Persistence.EfCore/AGENTS.md`
- `adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj`
- `adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/AgentBridgeMigrationsAssemblies.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseBackupOptions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseBackupOptionsValidator.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseConfigurationExtensions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseMaintenanceRegistrationExtensions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseProvider.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Mapping/ModelResponseMapping.cs`
- `adapters/AgentBridge.Persistence.EfCore/Mapping/OrdinalOwnerConverter.cs`
- `adapters/AgentBridge.Persistence.EfCore/Mapping/ProviderModelMapping.cs`
- `adapters/AgentBridge.Persistence.Migrations.SqlServer/AGENTS.md`
- `adapters/AgentBridge.Persistence.Migrations.SqlServer/AgentBridge.Persistence.Migrations.SqlServer.csproj`
- `adapters/AgentBridge.Persistence.Migrations.SqlServer/SqlServerAgentBridgeDbContextFactory.cs`
- `adapters/AgentBridge.Persistence.Migrations.SqlServer/Migrations/20261006060948_InitialAgentBridgeSchema.cs`
- `adapters/AgentBridge.Persistence.Migrations.SqlServer/Migrations/20261006060948_InitialAgentBridgeSchema.Designer.cs`
- `adapters/AgentBridge.Persistence.Migrations.SqlServer/AgentBridge/Persistence/Migrations/SqlServer/Migrations/AgentBridgeDbContextModelSnapshot.cs`
- `tests/AGENTS.md`
- `tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj`
- `tests/AgentBridge.Persistence.EfCore.Tests/DatabaseConfigurationTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/ProviderDesignTimeTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/SqlServerProviderTests.cs`
- `tests/Delivery/AGENTS.md`
- `tests/Delivery/Consumer/SqlServerRegistration.cs`

### Before/current и реализация

До12 enum/DI/runtime/factories/migrations поддерживали только SQLite/PostgreSQL; common checks использовали quoted identifiers/length(), payload — text, owner — BINARY/C. Current SqlServer=2 задан явно при сохранении SQLite=0/PostgreSql=1. Options/DI не выбирают fallback. UseSqlServer10.0.11 выбирает SQLSERVER assembly и __AgentBridgeMigrationsHistory, SensitiveDataLogging=false. Existing scoped context/base CRUD/scenario UoW и EFCoreLibrary coordinator/gate не заменены; ядро не получило provider dependencies.

SQL Server payload/journal/settings/provenance — nvarchar(max), ticks — bigint, trusted checks — brackets/DATALENGTH. Owner — varbinary(max) из little-endian UTF-16 code units без trim/replacement/MaxLength, с fail-fast при нечётном byte payload. Case/trailing spaces/NUL/unpaired surrogate и длинная строка5000 сохраняются. Actual EF parameter mapping и disconnected Where(OwnerId==owner).ToQueryString подтверждают binary conversion/translation: короткий параметр varbinary(8000), длинный varbinary(max). Это только translation, SQL не отправлялся; server equality/CAS ещё17.

Шесть таблиц, parent-local composite keys/FK/order, immutable root/concurrency, settings Version и nullable snapshot/provenance/journal сохранены. К каждой дочерней таблице один путь cascade; DATALENGTH nonempty сохраняет trailing-space semantics. Generated snapshot/current и designer/snapshot relational models одинаковы у всех трёх providers. Existing SQLite/PostgreSQL migrations/generated не менялись. ContentBytes остаётся прикладным UTF-8 счётчиком, не размером DB.

Maintenance использует actual SqlServerMaintenanceProvider EFCoreLibrary с тем же scoped migration adapter. SqlServerBackupDirectory обязателен и проверяется как server Unix/Windows drive/UNC path независимо от app host; локальный BackupDirectory MSSQL не требуется. Retention положительный/app-owned, SingleInitializer явный. EngineEdition2/3/4, ConnectRetryCount=0/direct endpoint/без failover и receipt COPY_ONLY/CHECKSUM/HEADERONLY/VERIFYONLY — existing source contract, не новая реализация SQL в AgentBridge. У приложения остаются secrets/TLS/auth, writes/DDL/all-instance stop и cleanup/retention.

### Tooling и generated identity

Пользователь ответил **«Разрешаю точную команду»** на async вопрос `call_k5DRyLKtuwEu6i4dpvkTiZrZ` этого чата. Pinned file `C:/Users/Spike/.dotnet/tools/.store/dotnet-ef/10.0.11/dotnet-ef/10.0.11/tools/net8.0/any/dotnet-ef.dll`, nuspec10.0.11 и DotnetToolSettings прочитаны; installation/version CLI не выполнялись. Новый target=startup уже был успешно собран; MSBuild property evaluation с тем же BaseOutputPath подтвердил artifacts/compile-check/stage12/Debug/net10.0, runtimeconfig=true. Выполнена ровно разрешённая команда, cwd AgentBridge:

```powershell
$stage12PriorOutput = $env:BaseOutputPath
$stage12PriorPack = $env:GeneratePackageOnBuild
try {
    $env:BaseOutputPath = 'artifacts/compile-check/stage12/'
    $env:GeneratePackageOnBuild = 'false'
    dotnet 'C:/Users/Spike/.dotnet/tools/.store/dotnet-ef/10.0.11/dotnet-ef/10.0.11/tools/net8.0/any/dotnet-ef.dll' migrations add InitialAgentBridgeSchema --project adapters/AgentBridge.Persistence.Migrations.SqlServer/AgentBridge.Persistence.Migrations.SqlServer.csproj --startup-project adapters/AgentBridge.Persistence.Migrations.SqlServer/AgentBridge.Persistence.Migrations.SqlServer.csproj --context AgentBridge.Persistence.EfCore.AgentBridgeDbContext --configuration Debug --framework net10.0 --no-build --output-dir Migrations --namespace AgentBridge.Persistence.Migrations.SqlServer.Migrations
} finally {
    $env:BaseOutputPath = $stage12PriorOutput
    $env:GeneratePackageOnBuild = $stage12PriorPack
}
```

Exit0, migration `20261006060948_InitialAgentBridgeSchema`. Tooling создал **три**, а не четыре generated файла; четвёртый hash ниже — ручной factory. Migration Up/Down/designer/snapshot прочитаны, generated вручную не редактировались. UpOperations описывают6 CreateTable/5 cascade FK/23 checks/3 indices, SqlOperation отсутствует; Down удаляет детей до родителей. EF history не входит в initial Up. SQL generator/apply/DB не вызываются.

| Файл внутри SQLServer проекта | SHA256 |
| --- | --- |
| Migrations/20261006060948_InitialAgentBridgeSchema.cs | B28DE509613D5B2AE2C9A991449332249EB8E3D268BA4972C8C11B668E5545C9 |
| Migrations/20261006060948_InitialAgentBridgeSchema.Designer.cs | 09AB8F891F9B1EC6EB6663B9163EDCF564600F33CBDCD6E14F17C50B7EDCA3DB |
| AgentBridge/Persistence/Migrations/SqlServer/Migrations/AgentBridgeDbContextModelSnapshot.cs | 3486A03C11D8C0D0743E603F6D11AF78727B90A061E2511226F1CAE8207B2D6F |
| SqlServerAgentBridgeDbContextFactory.cs | CF784F386EAFE06E8AC00D6B578E55F572EB8A199D2AD3CFC23B50C3336F71A1 |

### Preflight, команды и результаты

Recursive source graph содержит13 проектов (test/core/adapters/три migrations/EF CRUD+четыре maintenance/HTTP), SDK-style net10.0; HTTP также net8.0. Конкретные csproj, ancestor props/targets/packages/NuGet/global/lock names и existing assets проверены. Source Exec/custom executable targets отсутствуют; HTTP Directory.Build.props меняет outputs только своего test project. EF packaging=true подавлен false. Это не полный аудит installed SDK/package generated hooks.

Все6 integration classes имеют Dependency=Database и исключены verified filter. Выбранные tests используют metadata/DI/converters либо существующие storage/gate/process/native doubles, без реальной БД/HTTP/process/native. SQLite in-memory/EF InMemory отсутствуют. Для новых references заранее объявлен restore только из existing local package cache с NuGetAudit=false, без сети:

```powershell
dotnet restore tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false
```

Exit0. Build/test cwd — AgentBridge, относительный BaseOutputPath создаёт separate output внутри каждого project directory. Все builds Exit0/warnings0/errors0; solution/Rebuild/install/pack/publish не выполнялись.

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ -flp:logfile=artifacts/stage12-test-build.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ -flp:logfile=artifacts/stage12-test-corrected-build.log
dotnet build adapters/AgentBridge.Persistence.Migrations.SqlServer/AgentBridge.Persistence.Migrations.SqlServer.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ -flp:logfile=artifacts/stage12-sqlserver-build.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ -flp:logfile=artifacts/stage12-test-pretool-build.log
dotnet build adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ -flp:logfile=artifacts/stage12-adapter-final-build.log
dotnet build adapters/AgentBridge.Persistence.Migrations.SqlServer/AgentBridge.Persistence.Migrations.SqlServer.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ -flp:logfile=artifacts/stage12-sqlserver-final-build.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ -flp:logfile=artifacts/stage12-tests-final-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage12/ --filter 'Dependency!=Database' --logger 'trx;LogFileName=stage12-isolated-final.trx' --results-directory artifacts/stage12
```

Предшествующие test команды совпадают с последней, кроме LogFileName=stage12-isolated-initial.trx/stage12-isolated-corrected.trx; pretool — filter 'Dependency!=Database&FullyQualifiedName~SqlServerProviderTests', LogFileName=stage12-sqlserver-pretool.trx. Каждый test имел свежую успешную сборку того же project/config/output; stale --no-build после failed build нет.

| TRX в artifacts/stage12 | Exit | Passed / Failed / Skipped |
| --- | --- | --- |
| stage12-isolated-initial.trx | 1 | 206 / 2 / 0 |
| stage12-isolated-corrected.trx | 1 | 205 / 3 / 0 |
| stage12-sqlserver-pretool.trx | 0 | 11 / 0 / 0 |
| stage12-isolated-final.trx | 0 | 211 / 0 / 0 |

Initial failures: прежний UnknownProviderNameFailsBinding использовал теперь допустимое SqlServer и заменён на UnsupportedProvider; новое assertion length( ошибочно совпало с DATALENGTH(. Corrected failures: тест ожидал varbinary(max) даже для короткого EF parameter, actual mapping использует varbinary(8000); assertion уточнён и добавлен long5000 control. Эти ошибки tests не объявляются production defects. Final TRX counters/211 result rows совпадают; overlapping runs не суммируются. Full211 включает existing maintenance chronology/UoW cleanup regressions01/02/05/07; это AB/doubles compatibility, не новый actual EF maintenance SQLServer run.

Final DLL SHA256: adapter `3A70735FD60A8446B2C2E63DBF962DA500538D45C659952F6C164928D8A8B144`; SQLServer migrations `981801912B06EE64611BAF9590AA241C9B578766149AA0839369EB8880ABCAD4`; persistence test DLL `FFD16B5A40F3610F4354A146C33C6A81DFEC813DD52F6EDE59A8765795D1CC45`. Пути — corresponding project/artifacts/compile-check/stage12/Debug/net10.0.

Main strict выполнен exact постоянно разрешённой командой из поручения09 (userMessage01a10fa0-7b81-70e1-a47e-5fc67440131d):

```powershell
& 'D:/Media/User/AppData/npm/openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive
```

Первый run Exit1 из-за двух >500-character requirement warnings; provider mapping/server backup выделены в отдельные требования со scenarios без ослабления normative смысла. Повтор Exit0, valid=true/issues=[],1 passed/0 failed. Wrapper/package прочитаны, actual CLI1.14.1, JSON schema version1.0 не выдаётся за CLI version. Original18 changes не менялись и заново не валидировались; archive/sync отсутствуют.

### Граница передачи и обязательный остаток17

Actual: current EF options/factory/provider metadata/history, library DI/coordinator registration, converter/parameter/query translation, generated operations/model differ, compile-only linked consumer source. Doubles: existing CRUD/session/transaction/process/native fault boundaries. Registration/resolution сохраняют Closed connection. Metadata не доказывает relational enforcement, server binary equality/trailing zero/CAS, journal atomicity, locking/PK race outcomes или restore. SQL Server driver PK/serialization/network failures не маскируются как typed Conflict по тексту; actual classification/behaviour проверяются17.

Не заданы версия/редакция SQL Server, endpoint/TLS/auth, собственная test DB, права metadata/CREATE/backup/restore, service-account access/server backup destination и cleanup policy: обязательный ресурсный блокер C17. EngineEdition2/3/4 описаны по source, AzureSQL/MI/Synapse/EngineARM64 не обещаны. DB/SQL script/apply/backup/restore, app/hosting/Docker/live HTTP/native/business process/restart/deployment не запускались. CLI tool generation разрешена отдельно; это не разрешение операций БД.

Новые win-x64/linux-x64/linux-arm64 kits и external binary consumers относятся15/16, actual Ubuntu/ARM64 load —18. Root PostgreSQL server example не заменялся неподтверждённым MSSQL app: новый actual API показан source compile-only примером, его методы не исполнялись. IConfiguration strict/facade13/14 не реализованы.

Финальный контроль37 manifest files: strict UTF-8, без U+FFFD/четырёх question marks; ручные файлы LF, три tooling generated — исходный CRLF. Original stage scope до Results совпадает с HEAD после нормализации только статуса, slnx XML корректен, git diff --check Exit0, staged diff пуст. Ранняя проверка diff обнаружила дополнительную пустую строку EOF отчёта, она удалена точечно. Source code/tests после final211 не менялись; документальные правки не требовали повторного suite. Незнакомые mojibake markers проверены отдельно без case-insensitive regex.

Готово к review в A/B12. До явной приёмки и отдельного поручения координатора add/commit не выполняются; после передачи checkout не возобновляется самостоятельно. Historical audit/Initial reports и Coordination не менялись.

**Приёмка12, 2026-10-06:** координатор от имени пользователя принял A/B после независимой сверки provider/DI/maintenance/converter/parameter/EF metadata, generated Up/Down/history/model differ и predecessor preservation; подтверждены userMessage01a10fd4-e417-7872-b1ab-412ccc6acb74/tooling Exit0, три final concrete builds и211 TRX rows/counters/0skip, strict main valid, generated3+factory hashes, UTF-8/EOL37,150 local links и122 slnx paths без missing/duplicates, diff checks. Поручен один local feat(persistence) commit только exact37 manifest; Coordination/outputs/соседи исключены. Source/tests/spec/generated после final evidence не менялись. Реальные SQL equality/CAS/journal/races/backup/restore17, kits15/16 и runtime18 остаются открытыми.
