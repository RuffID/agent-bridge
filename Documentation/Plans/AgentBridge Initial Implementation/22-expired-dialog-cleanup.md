# 22 — Реализовать вызываемую приложением очистку просроченных диалогов

Статус: **Реализован, проверен и принят координатором; локальный коммит разрешён**. Дата: **2026-10-04, Asia/Novosibirsk**. Зависимости: **10, 20, 21**. Этапы23–25 этим исполнителем не начаты.

Baseline21: `6482d595cb1f89eeb30ec33696026aa6c1bde1cc`, parent20 `95c28fae3773efa9e8e4d0281282340f5ac9e6c0`, ветка master. При начале tree/index чистые; до приёмки HEAD не менялся. Работа только в agent-bridge, соседние исходники read-only. Прочитаны root/local AGENTS, plan/evidence21, SSOT, actual ports/UoW и обязательные библиотеки EFCoreLibrary0.0.5/HttpClientLibrary0.0.0.5. Все обязательные пути доступны.

## Цель

Удалять просроченные диалоги и связанные данные через базовые операции EFCoreLibrary.

## Задачи

- [x] Предоставить явную операцию очистки, за вызов и расписание которой отвечает приложение.
- [x] Читать просроченные диалоги базовыми операциями репозитория и обрабатывать пакетами ограниченного размера.
- [x] Согласованно удалять сообщения, результаты инструментов и состояния контекста через сценарный UoW.
- [x] Использовать настроенный срок от момента создания; активность и compact не продлевают его.
- [x] Предотвратить восстановление удалённого состояния результатом выполняющегося запроса.
- [x] Возвращать результат очистки без представления ошибки удаления как успеха.

## Проверка и завершение

Проверить границы срока хранения, удаление связанных данных, частичный сбой и одновременное выполнение очистки и хода агента. Этап завершён, когда один лишь мягкий порог байтов не может запустить очистку.

Источник: [хранение данных](<../../Business logic/04-storage-and-retention.md>).

## Фактическая реализация

Public `AddAgentBridgeDialogCleanup()`/scoped `ExpiredDialogCleanup.CleanupAsync(int limit, CancellationToken)` обрабатывает один snapshot из existing IExpiredDialogReader. Read и каждый sequential delete имеют отдельный awaited async scope. UTC свежий перед каждым портом; ни drain-loop, ни refresh/retry, ни scheduler/background service, ни новые options не добавлены. Existing base CRUD/Serializable DialogDeletionUnitOfWork/guards/cascade сохранены без правок schema/migrations.

Immutable report различает batch Completed/Partial/Failed/Canceled/Interrupted и candidates Deleted/Failed/Unknown/NotAttempted. Expected отказ продолжается со следующими кандидатами, source ServiceError/token сохраняются. Cancel прекращает следующие операции и сохраняет подтверждённые удаления. Unexpected exception распространяется, LastResult содержит прогресс без raw exception. Success порта фиксируется до DisposeAsync; primary+cleanup aggregate и отдельная OCE cleanup не маскируются caller cancellation. Concurrent вызов одного экземпляра fail-fast до второго read.

ContentBytes/soft threshold не участвуют в выборке или DI-регистрации. Existing ExpiresAtUtc не пересчитывается по новой конфигурации. Equality считается expiry. Новая incarnation не удаляется исходным кандидатом; active run сохраняет pinned settings21, но его поздние response/compact/settings writes после удаления не восстанавливают историю. [API/примеры/ограничения22](<../../Technical documentation/22-expired-dialog-cleanup.md>), [main SSOT](../../../openspec/specs/agent-runtime/spec.md), [change22](../../../openspec/changes/expired-dialog-cleanup/proposal.md).

## Проверки

SDK10.0.401/net10.0, actual EFCoreLibrary0.0.5, EF Core/SQLite10.0.11, Npgsql10.0.3, PostgreSQL18/Docker Engine29.8.1. Microsoft.ML.Tokenizers2.0.0 используется actual runner integration; HTTP не запускается. Existing restore assets пригодны, restore не потребовался. Прочитаны конкретные projects и действующие import/build files всей build chain; executable hooks не обнаружены. Generated schemas21 не менялись.

| Финальный TRX в artifacts/test-results/stage22 | Passed | Failed | Skipped | Evidence |
| --- | ---: | ---: | ---: | --- |
| cleanup-core-final.trx | 79 | 0 | 0 | 19 новых public cleanup cases + AgentRunnerTests/ApplicationPortsTests |
| cleanup-persistence-final.trx | 61 | 0 | 0 | existing read/delete/UoW/scope/model/settings/base registration, без БД |
| cleanup-db-after-cancellation-fix.trx | 16 | 0 | 0 | 8 SQLite +8 PostgreSQL actual cleanup risks |

**156 distinct адресных cases**, промежуточные overlapping запуски не суммируются. Latest concrete builds tests/core и persistence/tests/двух migrations с их actual EFCoreLibrary chain:0 warnings/0 errors. После добавления только XML summaries повторный compile-check также0/0; это не новый test run.

Real DB16: equality и один bounded пакет среди трёх expired, следующая порция отдельным app call, unexpired ContentBytes выше soft threshold сохраняется; cascade шести таблиц включая settings; stale revision/recreate/NotFound между read/delete; partial failure/cancel после настоящего SQL SaveChanges до commit второго удаления (первое остаётся принято, второе rollback, третье не начато); separate scopes/restart; cleanup во время actual AgentRunner ожидания controlled gateway с actual offline counter; late response/compact/settings отказ NotFound/Conflict и отсутствие revival.

**Ограничение interleaving:** stale revision case передаёт исторический NOW до expiry для моделирования принятой ранее мутации между snapshot и delete. Он проверяет исходный token guard, а не возможность real late write. Gateway/settings в DB runner cases — управляемые Application doubles, не HTTP. Изолированные ports/scopes не доказывают SQL atomicity. Unknown остаётся неизвестным, даже когда конкретный fault injection доказал rollback. SQLite/PostgreSQL не подтверждают SQL Server/MySQL.

## Точные команды сборки и тестов

Все commands из `D:\Media\User\source\repos\agent-bridge`, без solution build/Rebuild/pack/publish:

```powershell
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~ExpiredDialogCleanupTests|FullyQualifiedName~AgentRunnerTests|FullyQualifiedName~ApplicationPortsTests' --logger 'trx;LogFileName=cleanup-core-final.trx' --results-directory artifacts\test-results\stage22 --verbosity minimal
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'Dependency!=Database&(FullyQualifiedName~ExpiredDialogReaderTests|FullyQualifiedName~DialogWritePortsTests|FullyQualifiedName~UnitOfWorkScopeTests|FullyQualifiedName~PersistenceModelTests|FullyQualifiedName~DialogSettingsMappingTests|FullyQualifiedName~PersistenceRegistrationTests)' --logger 'trx;LogFileName=cleanup-persistence-final.trx' --results-directory artifacts\test-results\stage22 --verbosity minimal

# Только собственный временный контейнер; record/credentials после тестов удалены.
$taskRecord=Get-Content -LiteralPath 'artifacts\integration-abverify-stage22-4159eec503cf40bd9aee04673ee046ca\run.json' -Raw -Encoding UTF8 | ConvertFrom-Json
$env:AGENTBRIDGE_INTEGRATION='1'
$env:AGENTBRIDGE_INTEGRATION_ROOT=$taskRecord.root
$env:AGENTBRIDGE_POSTGRES_CONNECTION="Host=127.0.0.1;Port=59349;Database=postgres;Username=$($taskRecord.user);Password=$($taskRecord.password);SSL Mode=Disable;Pooling=false"
$env:AGENTBRIDGE_PG_DUMP='D:\Programs\PostgreSQL\18\bin\pg_dump.exe'
$env:AGENTBRIDGE_PG_RESTORE='D:\Programs\PostgreSQL\18\bin\pg_restore.exe'
$env:AGENTBRIDGE_PG_MAJOR='18'
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~ExpiredDialogCleanupIntegrationTests' --logger 'trx;LogFileName=cleanup-db-after-cancellation-fix.trx' --results-directory artifacts\test-results\stage22 --verbosity minimal
```

Initial core test использовал тот же command с filter `FullyQualifiedName~ExpiredDialogCleanupTests` и logger `cleanup-core-first.trx`:16/0/0 до добавления трёх cancellation/read-scope cases. Initial/follow-up DB command тот же с logger `cleanup-db-first.trx` (0 passed/16 failed/0 skipped) и `cleanup-db-final.trx` (16/0/0). После production cancellation fix адресный DB16 повторён и финальный TRX указан выше. Исторические30 runner/settings16/maintenance39 предыдущих этапов не выдаются за повторённые22; backup/restore не повторялся.

## Первоначальные ошибки и исправления

1. Core build CS0117: synthetic fixture использовал несуществующий DialogId.New. Исправлено на actual DialogId.From(Guid.NewGuid()); до исправления тесты не запускались на старом binary.
2. Initial Docker env-file был сформирован PowerShell выражением concat/array без скобок и не получил отдельной строки POSTGRES_PASSWORD. Контейнер d9d0304c45314bdbceb8338adc221107c9f1d4adfafb7371f4df18f46d018e3a завершился exit1. Исправлены три runtime env lines, проверены exact own name/label/mounts, собственный failed container удалён и создан заново. Secrets не печатались.
3. Первый pg_isready сразу после запуска вернул no response. Следующий явный check подтвердил accepting connections до DB tests; readiness не выдавалась за успешные тесты.
4. Initial DB16 failed: тестовый CountAsync ошибочно использовал DialogId для таблицы DialogSettings, actual PK/FK — Id. Source mapping проверен, observer исправлен; production/UoW/schema не менялись. Initial TRX сохранён. Координатор отдельно указал тот же дефект; к его сообщению исправление и successful DB16 уже выполнены.
5. Прочитан disposal/cancel риск: OCE из DisposeAsync при canceled caller могла ошибочно классифицироваться обычной отменой. До сдачи tracked operation-cancellation отделена от cleanup exception; добавлены19th-suite cases, финальные core79/persistence61/DB16 прошли.
6. rg discovery с Windows wildcard `tests/.../*Tests.cs` дал os error123. Повторено через каталог + `-g '*Tests.cs'`; это не failure validation.

## Собственные ресурсы и их удаление

Official postgres:18 отсутствовал до этапа, скачан digest `sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722`. Final own container `abverify-stage22-4159eec503cf40bd9aee04673ee046ca`, ID `736f91649eadf2b47419f6b0270b85810d6dc9a118a0d6151bbbbcfc58ad23a6`, exact label agentbridge.stage22 с тем же именем. Port127.0.0.1:59349→5432, tmpfs /var/lib/postgresql:rw,size=1073741824, Mounts[] без volumes. Existing pg_dump/pg_restore18 и Docker Desktop не устанавливались.

Runtime ресурсы создавались уникальными Guid именами; user/password генерировались и хранились только в ignored env-file/run.json. Команды после исправления:

```powershell
docker pull postgres:18
docker run -d --name $taskRecord.name --label "agentbridge.stage22=$($taskRecord.name)" --env-file (Join-Path $taskRecord.root 'container.env') -p 127.0.0.1::5432 --tmpfs /var/lib/postgresql:rw,size=1073741824 postgres:18
docker exec $taskRecord.name pg_isready -U $taskRecord.user -d postgres
docker inspect abverify-stage22-4159eec503cf40bd9aee04673ee046ca --format '{{.Id}} {{.Name}} {{index .Config.Labels "agentbridge.stage22"}} {{json .Mounts}} {{json .HostConfig.Tmpfs}} {{json .NetworkSettings.Ports}}'
docker ps -a --filter ancestor=postgres:18 --format '{{.ID}} {{.Names}}'
docker stop abverify-stage22-4159eec503cf40bd9aee04673ee046ca
docker rm abverify-stage22-4159eec503cf40bd9aee04673ee046ca
docker image rm postgres:18
Remove-Item -LiteralPath 'D:\Media\User\source\repos\agent-bridge\artifacts\integration-abverify-stage22-4159eec503cf40bd9aee04673ee046ca\container.env','D:\Media\User\source\repos\agent-bridge\artifacts\integration-abverify-stage22-4159eec503cf40bd9aee04673ee046ca\run.json' -ErrorAction Stop
Remove-Item -LiteralPath 'D:\Media\User\source\repos\agent-bridge\artifacts\integration-abverify-stage22-4159eec503cf40bd9aee04673ee046ca' -ErrorAction Stop
```

Fixtures удалили собственные SQLite directories и PostgreSQL DB. Перед cleanup проверены exact resolved absolute root, два credential files без дочерних directories, container name/label/port/mounts и отсутствие других containers этого image. Удалены только собственные контейнеры, newly pulled image и пустой own root; Test-Path false, label container list/image list пусты. Docker Desktop не останавливался; ignored TRX/compile outputs сохранены.

## Пропуски и ограничения

**Пропущено по указанию пользователя:** real HTTP/codex-lb/OpenAI/upstream/accounts/tokens, HTTP server/hosting/TestServer/WebApplicationFactory/application/Telegram/dev server, рабочие БД/SQL/secrets/volumes, SQL Server/MySQL, произвольные project/user scripts, pack/publish, remote Git/branch changes/PR/uploads, этапы23–25. Local add/commit до приёмки не выполнялись; после приёмки координатор отдельно разрешил коммит ровно30 файлов manifest. Соседние исходники и generated migrations не менялись.

**Недоступная проверка:** OpenSpec CLI не найден при initial/final Get-Command и repository filename discovery; не устанавливался. CLI validation не выполнена. Main/delta/context просмотрены статически и синхронизированы; это не CLI validation. Change не архивирован.

Ручные правки apply_patch, UTF-8 без BOM/LF сохранены. Полный manifest30 проверен strict decoder: U+FFFD, четыре вопросительных знака и mojibake отсутствуют. Проверены254 локальные ссылки до добавления последней ссылки отчёта в tasks,255 после неё; main/delta requirement совпадают. Compare-Object manifest против modified/untracked пуст. При сдаче Git diff --check прошёл, index был пуст, HEAD совпадал с baseline21. Core/autocrlf warnings не меняют файлы или Git config. Documentation по-прежнему исключён из None в root csproj; новый technical doc добавлен solution item без переноса каркаса.

## Приёмка и локальный коммит

Координатор принял этап22 после просмотра core cleanup/report/DI, actual integration и isolated tests, docs/specs/plan. Независимо подтверждены TRX79/61/16, exact manifest30, diff --check, пустой index, HEAD21 и отсутствие собственных ресурсов22. Разрешён English Conventional Commit `feat: add bounded expired dialog cleanup`, только30 явно перечисленных файлов, parent `6482d595cb1f89eeb30ec33696026aa6c1bde1cc`. Перед коммитом проверяются ordinary/staged diff и состав, после — full hash/parent/count и чистые staged/tree. Hash передаётся координатору в сообщении после коммита. Этап23 этому исполнителю не поручен.

## Полный manifest — 30 файлов

Включает modified/untracked при сдаче, этот отчёт и solution item. Artifacts, credential files и build outputs не входят. До приёмки index был пуст и stage/commit не выполнялись; после неё локальный коммит разрешён только для этого manifest.

```text
AGENTS.md
Application/AGENTS.md
Application/ExpiredDialogCleanup.cs
Application/Models/ExpiredDialogCleanupResult.cs
Application/Models/ExpiredDialogCleanupStatus.cs
Application/Models/ExpiredDialogDeletionResult.cs
Application/Models/ExpiredDialogDeletionStatus.cs
Configuration/AGENTS.md
Configuration/AgentBridgeDialogCleanupExtensions.cs
Documentation/Business logic/04-storage-and-retention.md
Documentation/Plans/AgentBridge Initial Implementation/22-expired-dialog-cleanup.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/README.md
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/06-database-maintenance.md
Documentation/Technical documentation/22-expired-dialog-cleanup.md
Documentation/Technical documentation/README.md
README.md
adapters/AgentBridge.Persistence.EfCore/AGENTS.md
agent-bridge.slnx
openspec/changes/expired-dialog-cleanup/context.md
openspec/changes/expired-dialog-cleanup/proposal.md
openspec/changes/expired-dialog-cleanup/specs/agent-runtime/spec.md
openspec/changes/expired-dialog-cleanup/tasks.md
openspec/specs/agent-runtime/context.md
openspec/specs/agent-runtime/spec.md
tests/AGENTS.md
tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md
tests/AgentBridge.Persistence.EfCore.Tests/Integration/ExpiredDialogCleanupIntegrationTests.cs
tests/AgentBridge.Tests/ExpiredDialogCleanupTests.cs
```
