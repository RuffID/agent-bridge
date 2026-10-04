# 20 — Организовать полный ход агента

Статус: **Принят координатором; локальный commit ровно58 файлов manifest разрешён**. Зависимости: **10, 15, 18, 19**. После завершения20 работа **приостановлена по прямой просьбе пользователя**;21–25 не начаты, продолжение только новым поручением.

## Цель

Объединить операции хранения, контекста, модели и инструментов в сценарий агента, доступный вызывающему приложению.

## Задачи

- [x] Фиксировать принадлежность диалога пользователю, актуальную версию и настройки в начале хода.
- [x] Загружать контекст, рассчитывать бюджет, выполнять сжатие при необходимости и запускать цикл модели и инструментов.
- [x] Передавать потоковые события с явным итоговым результатом.
- [x] Сохранять завершённый результат протокола и необходимое состояние операций в коротких границах сценарного UoW.
- [x] Обрабатывать отмену, частичные сбои и прерывание без сообщения об успехе.
- [x] Отклонять запоздалые записи после истечения срока диалога, его удаления или очистки.

## Проверка и завершение

Проверить полный сценарий через точку входа агента с подставными хранилищем, моделью и инструментами, включая сбои после успешного выполнения инструмента. Этап завершён, когда весь сценарий сохраняет принадлежность данных владельцу и состояние, доступное после перезапуска.

Источник: [сценарий работы агента](<../../Business logic/02-dialogs-and-tools.md>).

## Реализация и граница

2026-10-04 реализован только20 в существующем master, без worktree/подагентов/других этапов. Baseline19=de58342f5c80e46279e1d7b59fe0665c893c5d4b, parent73045a151b6b48accd0fece98f75858c5336a16c; исходные tree/index чистые. [Actual public API и пример подключения](<../../Technical documentation/20-agent-turn-orchestration.md>), [change](../../../openspec/changes/agent-turn-orchestration/proposal.md), [нормативная delta](../../../openspec/changes/agent-turn-orchestration/specs/agent-runtime/spec.md). Change создан до кода, main синхронизирован; rationale/context отдельно. CLI не найден, установка/CLI validation/archive не выполнялись.

Public AgentRunner/AddAgentBridgeRunner, AgentRunRequest/Result/Status, immutable StoredToolAttempt/state и IDialogToolAttemptWriter реализованы. Owner/version/settings/instructions/selection/limits фиксируются на run, ModelAccess resolves один раз и используется новым actual ReadWithAccessAsync для проверки каталога и generation/compact. Старый пользовательский reader получает Unsupported без fallback. Providers один раз на сценарий; initial input после Begin не дублируется. Null callback → JSON, non-null → SSE. Existing builder/compactor/guard/executor используются без дублирования правил; полный guard перед каждой generation, compact failure не маскируется.

Журнал — отдельная nullable text ModelSteps.ToolAttemptsJson/version1, без hidden canonical metadata. Root owner/incarnation + parent-aware dialog/turn/step + AgentId/output position задают identity. Full model step/calls сохраняются до tools; Started commit завершается до handler. Checkpoint callbacks сериализованы, каждый read/write имеет свой short scope/UoW, transaction не удерживается на время model/action. Outcomes и confirmed outputs атомарны. Repeated completed call_id допускаются. Unknown не получает output. Legacy null не разрешает replay: existing TurnId всегда Interrupted/Conflict до model/handler, включая restart без journal.

Successful compact tokens/window фиксируются в runner writer boundary до возвращения в compactor; exception/cancel следующего прохода сохраняет факт принятого окна. LastResult учитывается только matching StepId после всех awaited workers/scopes. Primary+partial-save+read/write scope DisposeAsync failures сохраняются вместе; после любого refused/unknown write дальнейшие writes блокируются. Fresh UTC, original/successful-save token, без refresh/retry. LastTools/LastResponse сохраняют reports при ожидаемом storage refusal. При late cancellation во время terminal save возможен Canceled run с TerminalSaved=true и accepted Turn.Completed; второй finish не выполняется. Unexpected exceptions распространяются после попытки honest finalization.

## Зависимости, tooling и builds

Обязательные EFCoreLibrary/HttpClientLibrary доступны, прочитаны применимые AGENTS и actual base contracts/csproj: Version0.0.5/FileVersion0.0.0.5. Соседние исходники этой задачей не менялись. Root csproj не менялся, Documentation None Remove сохранён; slnx добавляет только новый technical solution item. Новых package/project references нет. Concrete csproj/build/import boundaries проверены, executable custom hooks не обнаружены; restore не понадобился, использованы existing assets. Все сборки — Build, не Rebuild/pack/publish; GeneratePackageOnBuild=false.

Точные compile команды (workdir везде D:\Media\User\source\repos\agent-bridge):

```powershell
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet build tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
```

Последние три builds: **0 warnings/errors**; включают core, EF adapter, обе migrations assemblies, transport, libraries и integration source. Первый test build новых runner fixtures: **CS0121** из-за target-typed new ServiceProviderOptions, **2 xUnit2031 warnings** для Assert.Single(Where). Исправлены только fixtures, последующие builds без warnings/errors. Production compile ошибок не было.

Пользователь разрешил обе точные команды новых AddDurableToolAttempts. Dotnet-ef10.0.11 уже установлен, ничего не устанавливалось. Для поиска ранее собранных DLL env BaseOutputPath настроен на existing compile-check; команды с --no-build выполнены SQLite → PostgreSQL:

```powershell
$env:BaseOutputPath='artifacts\compile-check\'
$env:GeneratePackageOnBuild='false'
dotnet ef migrations add AddDurableToolAttempts --project adapters\AgentBridge.Persistence.Migrations.Sqlite\AgentBridge.Persistence.Migrations.Sqlite.csproj --startup-project adapters\AgentBridge.Persistence.Migrations.Sqlite\AgentBridge.Persistence.Migrations.Sqlite.csproj --context AgentBridgeDbContext --output-dir Migrations --no-build --configuration Debug
dotnet ef migrations add AddDurableToolAttempts --project adapters\AgentBridge.Persistence.Migrations.PostgreSql\AgentBridge.Persistence.Migrations.PostgreSql.csproj --startup-project adapters\AgentBridge.Persistence.Migrations.PostgreSql\AgentBridge.Persistence.Migrations.PostgreSql.csproj --context AgentBridgeDbContext --output-dir Migrations --no-build --configuration Debug
```

Успех обеих команд. Новые20261004074344/20261004074347 migrations/designer и обновлённые snapshots созданы tooling, вручную не редактировались. Up добавляет nullable text ToolAttemptsJson, Down удаляет её; initial migrations не регенерированы. Nullable historical rows сохраняются, отсутствие journal после Down/Up не разрешает replay.

## Точные test команды и TRX

```powershell
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~AgentRunnerTests|FullyQualifiedName~ToolExecutorTests|FullyQualifiedName~ContextCompactorTests|FullyQualifiedName~ContextBuilderTests|FullyQualifiedName~ApplicationPortsTests|FullyQualifiedName~ContextTokenCounterTests|FullyQualifiedName~ContextBudgetGuardTests' --logger 'trx;LogFileName=runner-core-verified.trx' --results-directory artifacts\test-results\stage20 --verbosity minimal
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter FullyQualifiedName~ModelCatalogTests --logger 'trx;LogFileName=pinned-access-verified.trx' --results-directory artifacts\test-results\stage20 --verbosity minimal
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'Dependency!=Database' --logger 'trx;LogFileName=persistence-final.trx' --results-directory artifacts\test-results\stage20 --verbosity minimal
```

Real DB env перед каждой соответствующей командой задавался только из собственного temporary run.json (секрет не публикуется):

```powershell
$taskRecord=Get-Content -Raw -Encoding UTF8 'artifacts\integration-stage20-87b677779fad489f893856e9c6e978d9\run.json' | ConvertFrom-Json
$env:AGENTBRIDGE_INTEGRATION='1'
$env:AGENTBRIDGE_INTEGRATION_ROOT=$taskRecord.root
$env:AGENTBRIDGE_POSTGRES_CONNECTION="Host=127.0.0.1;Port=55045;Database=postgres;Username=$($taskRecord.user);Password=$($taskRecord.password);SSL Mode=Disable;Pooling=false"
$env:AGENTBRIDGE_PG_DUMP='D:\Programs\PostgreSQL\18\bin\pg_dump.exe'
$env:AGENTBRIDGE_PG_RESTORE='D:\Programs\PostgreSQL\18\bin\pg_restore.exe'
$env:AGENTBRIDGE_PG_MAJOR='18'
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter FullyQualifiedName~AgentRunnerIntegrationTests --logger 'trx;LogFileName=runner-db-verified.trx' --results-directory artifacts\test-results\stage20 --verbosity minimal
```

Все предыдущие runner-команды имеют тот же project/options/results-directory; только точные filter/TRX отличались:

| TRX | Filter | Passed | Failed | Skipped |
| --- | --- | ---: | ---: | ---: |
| runner-first.trx | FullyQualifiedName~AgentRunnerTests | 24 | 0 | 0 |
| persistence-first.trx | Dependency!=Database | 164 | 0 | 0 |
| pinned-access-first.trx / pinned-access-final.trx | FullyQualifiedName~ModelCatalogTests | 42 каждый | 0 | 0 |
| runner-db-first.trx | FullyQualifiedName~AgentRunnerIntegrationTests | 26 | 2 | 0 |
| runner-db-repaired.trx | FullyQualifiedName~ParallelPartialFailurePersistsConfirmedNeighbor | 2 | 0 | 0 |
| runner-core-final.trx | тот же combined core filter выше | 268 | 0 | 0 |
| runner-db-final.trx | FullyQualifiedName~AgentRunnerIntegrationTests | 28 | 0 | 0 |
| runner-scope-final.trx | FullyQualifiedName~AgentRunnerTests | 31 | 0 | 0 |
| runner-db-commit-final.trx | FullyQualifiedName~UnknownStartCommitPersistsBarrierWithoutExecutingAction | 2 | 0 | 0 |
| **runner-core-verified.trx** | combined core filter выше | **272** | **0** | **0** |
| **pinned-access-verified.trx** | FullyQualifiedName~ModelCatalogTests | **42** | **0** | **0** |
| **persistence-final.trx** | Dependency!=Database | **164** | **0** | **0** |
| **runner-db-verified.trx** | FullyQualifiedName~AgentRunnerIntegrationTests | **30** | **0** | **0** |

Последние четыре TRX — **508 distinct cases**, без суммирования пересекающихся прошлых runs/исторических этапов. Core включает31 новых public runner cases +241 related actual tools/context/compactor/offline BPE/guard/ports; transport42 включает1 новый pinned-access case; real DB30 =15 SQLite+15 PostgreSQL. Isolated gateway/store/counter doubles не считаются real persistence/live HTTP.

Первые2 real failures были в parallel fixture observer: multi-read DialogReader законно вернул Conflict при соседнем checkpoint update. Исправлен только observer на один parent-aware base query committed step, без production retry или ослабления guards. Адресный повтор2/2 и финальные30/30 успешны. Real tests проверяют Start отдельному scope до handler, no transaction в handler, repeated call_id, atomic SQL rollback journal+outputs, lost acknowledgement после actual commit (управляемый test wrapper), restart legacy/Started, root guards, expiry/delete/cleanup и migration Down/Up historical rows. Исторический maintenance набор39 без новых рисков не повторялся; его fixtures обновлены только для двух migrations.

## Собственные ресурсы и cleanup

Docker Desktop Linux29.8.1 доступен, native pg_dump/restore18 paths существовали, установка программ не выполнялась. Для actual PostgreSQL tests скачан official postgres:18 (до задачи отсутствовал), digest sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722. Уникальный контейнер abverify-stage20-87b677779fad489f893856e9c6e978d9, label agentbridge-verification=stage20-87b677779fad489f893856e9c6e978d9, binding127.0.0.1:55045, tmpfs /var/lib/postgresql, без volume/mount. Пользователь/БД abverify_<GUID>, временный случайный пароль только своей задачи; SQLite файлы — под собственным artifacts/integration-stage20-87b677779fad489f893856e9c6e978d9. Каждый fixture удалил свои БД/каталоги.

```powershell
docker version --format '{{.Server.Os}} {{.Server.Version}}'
docker image ls postgres:18 --format '{{.Repository}}:{{.Tag}} {{.ID}}'
docker pull postgres:18
docker run --detach --name $taskContainer --label "agentbridge-verification=$taskRunId" --env-file $taskEnvFile --publish '127.0.0.1::5432' --tmpfs '/var/lib/postgresql:rw,size=1073741824' postgres:18
docker inspect abverify-stage20-87b677779fad489f893856e9c6e978d9 --format '{{index .Config.Labels "agentbridge-verification"}} {{json .NetworkSettings.Ports}}'
docker exec abverify-stage20-87b677779fad489f893856e9c6e978d9 pg_isready
```

Readiness подтвердил accepting connections. Перед cleanup сверены точные name/label/mounts/loopback port и отсутствие других контейнеров ancestor postgres:18. Только свои ресурсы остановлены/удалены:

```powershell
docker stop abverify-stage20-87b677779fad489f893856e9c6e978d9
docker rm abverify-stage20-87b677779fad489f893856e9c6e978d9
docker image rm postgres:18
```

Resolve-Path подтвердил точный собственный absolute каталог, затем Remove-Item -LiteralPath удалил только container.env/run.json и уже пустой каталог, без recursive delete. Test-Path=False; Docker filters по task label и postgres:18 пусты. Рабочие DB/secrets/data/volumes и чужие containers не изменялись. Compile/TRX оставлены в игнорируемых artifacts, не входят в manifest.

## Пропуски и ограничения

- **Пропущено по указанию пользователя:** live HTTP/codex-lb/OpenAI/upstream/accounts/tokens, local HTTP server/hosting/TestServer/WebApplicationFactory, Telegram/application/demo, произвольные project/user scripts, remote Git/PR/публикации/uploads/deployment. Add/commit не выполнялись до отдельной приёмки.
- Actual HttpClientLibrary использовалась с fake handler только для pinned catalog; live compatibility не подтверждена. Real SQLite/PostgreSQL не доказывают SQL Server/MySQL. Управляемый lost acknowledgement после настоящего commit не является network fault injection. Test gateway/counter реальных DB cases не доказывают server token budget.
- OpenSpec CLI не найден; CLI validation не выполнена, static main/delta checks не её замена, change не архивирован.
- Tool deadlines кооперативны: начатые handlers/scopes всегда awaited. External action и DB commit не имеют общей transaction; при невозможности сохранить confirmed output durable Started остаётся барьером, а не доказательством результата.
- Existing turn не возобновляется автоматически. Interrupted state доступен приложению через read/result; не вводились UI/status usecases21 или cleanup orchestration22. После20 пользователь поручил паузу.

## Статический аудит и состояние при сдаче

2026-10-04 перед приёмкой проверены фактические working tree/index/HEAD: **master**, HEAD=de58342f5c80e46279e1d7b59fe0665c893c5d4b, **35 modified +23 untracked =58 файлов**. Staged diff был пуст, `git diff --check` успешен. До приёмки add/commit не выполнялись; после проверки координатор принял20 и разрешил локальный commit этого manifest. После20 — остановка по прямой просьбе пользователя; этапы21–25 не начаты. Старт21 разрешается только новым поручением, приёмка20 сама по себе его не разрешает.

Все58 файлов декодированы strict UTF-8; U+FFFD, четыре подряд вопросительных знака и проверенные признаки mojibake не обнаружены. Для существующих файлов ручных правок index/worktree сохраняют LF. Два generated snapshots записаны EF tooling с CRLF; generated migrations/designer также с CRLF, вручную не изменялись. Предупреждения Git о будущей LF→CRLF conversion относятся к существующей настройке, config не менялся.

XML slnx разобран, все его Path существуют. Проверены211 локальных Markdown-ссылок в изменённых/новых документах: отсутствующих targets нет (anchors этой проверкой не валидируются). Нормативный added block change совпадает с main spec дословно. Tasks и stable context синхронизированы; это статическая проверка, **не OpenSpec CLI validation**, change остаётся неархивированным. Production diff/new files просмотрены; новые package/project references и ручные изменения initial/generated migrations отсутствуют. Build/TRX/bin/obj, temporary env/run.json и признаки private keys/tokens не входят в manifest. Последние четыре TRX повторно прочитаны:272+42+164+30 passed,0 failed/notExecuted. Собственный temporary resource directory отсутствует.

## Полный manifest этапа20

`M` — изменённый tracked файл, `??` — новый untracked файл на момент сдачи до staging. Перечень включает сам отчёт и все новые файлы; координатор отдельно разрешил локальный add/commit ровно этого manifest после приёмки20.

| Статус | Файл |
| --- | --- |
| M | `AGENTS.md` |
| M | `Application/AGENTS.md` |
| M | `Application/Models/StoredModelStep.cs` |
| M | `Application/Ports/IModelSettingsReader.cs` |
| M | `Configuration/AGENTS.md` |
| M | `Documentation/Business logic/02-dialogs-and-tools.md` |
| M | `Documentation/Plans/AgentBridge Initial Implementation/20-agent-turn-orchestration.md` |
| M | `Documentation/Plans/AgentBridge Initial Implementation/README.md` |
| M | `Documentation/README.md` |
| M | `Documentation/Technical documentation/01-architecture.md` |
| M | `Documentation/Technical documentation/19-application-tools.md` |
| M | `Documentation/Technical documentation/README.md` |
| M | `README.md` |
| M | `adapters/AgentBridge.CodexLb/AGENTS.md` |
| M | `adapters/AgentBridge.CodexLb/Models/CodexLbModelSettingsReader.cs` |
| M | `adapters/AgentBridge.Persistence.EfCore/AGENTS.md` |
| M | `adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs` |
| M | `adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs` |
| M | `adapters/AgentBridge.Persistence.EfCore/Models/ModelStepRecord.cs` |
| M | `adapters/AgentBridge.Persistence.EfCore/Reading/DialogReader.cs` |
| M | `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/AGENTS.md` |
| M | `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/TurnContentStaging.cs` |
| M | `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AGENTS.md` |
| M | `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge/Persistence/Migrations/PostgreSql/Migrations/AgentBridgeDbContextModelSnapshot.cs` |
| M | `adapters/AgentBridge.Persistence.Migrations.Sqlite/AGENTS.md` |
| M | `adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge/Persistence/Migrations/Sqlite/Migrations/AgentBridgeDbContextModelSnapshot.cs` |
| M | `agent-bridge.slnx` |
| M | `openspec/specs/agent-runtime/context.md` |
| M | `openspec/specs/agent-runtime/spec.md` |
| M | `tests/AGENTS.md` |
| M | `tests/AgentBridge.CodexLb.Tests/ModelCatalogTests.cs` |
| M | `tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md` |
| M | `tests/AgentBridge.Persistence.EfCore.Tests/Integration/IntegrationDatabase.cs` |
| M | `tests/AgentBridge.Persistence.EfCore.Tests/Integration/MaintenanceIntegrationTests.cs` |
| M | `tests/AgentBridge.Persistence.EfCore.Tests/ProviderDesignTimeTests.cs` |
| ?? | `Application/AgentRunScope.cs` |
| ?? | `Application/AgentRunSession.cs` |
| ?? | `Application/AgentRunner.cs` |
| ?? | `Application/Models/AgentRunRequest.cs` |
| ?? | `Application/Models/AgentRunResult.cs` |
| ?? | `Application/Models/AgentRunStatus.cs` |
| ?? | `Application/Models/StoredToolAttempt.cs` |
| ?? | `Application/Models/ToolAttemptState.cs` |
| ?? | `Application/Ports/IDialogToolAttemptWriter.cs` |
| ?? | `Configuration/AgentBridgeRunnerExtensions.cs` |
| ?? | `Documentation/Technical documentation/20-agent-turn-orchestration.md` |
| ?? | `adapters/AgentBridge.Persistence.EfCore/Mapping/ToolAttemptMapping.cs` |
| ?? | `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogToolAttemptUnitOfWork.cs` |
| ?? | `adapters/AgentBridge.Persistence.Migrations.PostgreSql/Migrations/20261004074347_AddDurableToolAttempts.Designer.cs` |
| ?? | `adapters/AgentBridge.Persistence.Migrations.PostgreSql/Migrations/20261004074347_AddDurableToolAttempts.cs` |
| ?? | `adapters/AgentBridge.Persistence.Migrations.Sqlite/Migrations/20261004074344_AddDurableToolAttempts.Designer.cs` |
| ?? | `adapters/AgentBridge.Persistence.Migrations.Sqlite/Migrations/20261004074344_AddDurableToolAttempts.cs` |
| ?? | `openspec/changes/agent-turn-orchestration/context.md` |
| ?? | `openspec/changes/agent-turn-orchestration/proposal.md` |
| ?? | `openspec/changes/agent-turn-orchestration/specs/agent-runtime/spec.md` |
| ?? | `openspec/changes/agent-turn-orchestration/tasks.md` |
| ?? | `tests/AgentBridge.Persistence.EfCore.Tests/Integration/AgentRunnerIntegrationTests.cs` |
| ?? | `tests/AgentBridge.Tests/AgentRunnerTests.cs` |

## Приёмка и передача

2026-10-04 координатор принял20 по public runner, durable journal, short scopes, migrations Up/Down/snapshots, документации/main-delta, фактическим TRX и cleanup. Разрешён непустой локальный Conventional Commit ровно58 файлов manifest на master, без remote операций и дополнительных файлов. [HEAD master после локального коммита20](../../../.git/refs/heads/master) содержит фактический hash; полный hash/parent и состояние tree/index возвращаются в итоговом сообщении. Самореферентный hash в этот документ не записывается.

Передача:272 core+164 persistence+42 transport isolated и30 real SQLite/PostgreSQL passed,0 failed/skipped; финальные builds0 warnings/errors. Исходные fixture failures и исправления описаны выше. Собственные test DB/container/image/directory удалены. Live HTTP/upstream/hosting/Telegram не выполнялись, SQL Server/MySQL не проверены; OpenSpec CLI validation недоступна, change не архивирован. После завершения20 работа приостановлена по прямой просьбе пользователя;21–25 не начаты. Продолжение — только новым поручением.
