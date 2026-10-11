# Evidence библиотечных возможностей диалогов

Дата: 11.10.2026. Реализация AgentBridge для требований Ledger 01A проверена в изолированной границе. Ledger не изменялся; его 01A остаётся **In Progress**, этапы 02–09 не начаты. [Checkpoint](Checkpoint.md) и [Continue-prompt](Continue-prompt.md) описывают историческую паузу 10.10, а не текущий результат.

## Реализация и API

Фактические публичные сигнатуры, DTO, ограничения и обоснованные отличия от предложенного ТЗ: [30-dialog-catalog-and-recovery](<../../Technical documentation/30-dialog-catalog-and-recovery.md>). Extended guard call sites переведены на LoadRunAsync; legacy LoadAsync и public constructor overloads сохранены. Default implementations новых writer overloads отказывают Unsupported, без обхода fencing через прежний API.

Library-owned compact owner/site/agent catalog, immutable profile/policy, canonical writes/root/index/feed выполняются одной сценарной transaction. Readiness/lease/epoch и recovery journal durable; reader строит отдельный effective context. Evidence/quiescence I/O находится между коротким preflight и финальной transaction с повторным CAS/epoch guard. Trusted evidence не принимается как raw client output. Original history/terminal/confirmed outputs сохраняются; прежний handler не вызывается повторно.

Unknown не становится NotStarted/Canceled по отмене, read-only или lease expiry. После expired lease требуется fence; после pending pairs — durable recovery. Продолжение использует прежний DialogId, новый явный TurnId и новый context только после Ready. Signed/search/page-size/expiry cursor и актуальная бизнес-авторизация — host responsibilities, не реализованы в этой библиотечной задаче.

## Свежие compile/test результаты

| Проверка текущих исходников | Результат |
| --- | --- |
| Core Debug build | 0 warnings / 0 errors |
| Core isolated tests | 542 passed, 0 failed/skipped |
| Persistence Debug build | 0 warnings / 0 errors |
| Full isolated persistence, исключён Dependency=Database | 422 passed, 0 failed/skipped |
| Новые catalog / recovery / lifecycle / evidence / provider cases | 12 / 15 / 14 / 16 / 6; всего 63 passed в общем наборе |
| SQL Server Release delivery build: win-x64, linux-x64, linux-arm64 | Все три исходные и три coherent сборки: 0 warnings / 0 errors |
| External binary consumer, новый schema3 bundle, три SQL Server RID | Все три: locked local-cache restore и Release compile, 0 warnings / 0 errors |

Диагностика текущих tests: `artifacts/assistant01a-tests/core-final/AgentBridge.Tests_net10.0_x64_261011024910560.diag`, `artifacts/assistant01a-tests/final3/AgentBridge.Persistence.EfCore.Tests_net10.0_x64_261011024915372.diag`. Это результаты до упаковки, после последних production/evidence правок. Дальнейшие изменения документации не меняют проверенные production inputs.

```powershell
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore --nologo -p:BaseOutputPath=artifacts/assistant01a-compile/ -p:GeneratePackageOnBuild=false
dotnet test --project tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/assistant01a-compile/ -p:GeneratePackageOnBuild=false --diagnostic --diagnostic-output-directory artifacts/assistant01a-tests/core-final
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore --nologo -p:BaseOutputPath=artifacts/assistant01a-compile/ -p:GeneratePackageOnBuild=false
dotnet test --project tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/assistant01a-compile/ -p:GeneratePackageOnBuild=false --filter-not-trait Dependency=Database --diagnostic --diagnostic-output-directory artifacts/assistant01a-tests/final3
```

Release для каждого `<rid>` из win-x64/linux-x64/linux-arm64 выполнен SDK 10.0.401:

```powershell
dotnet restore tests/Delivery/Build/AgentBridge.Delivery.csproj --nologo --source C:/Users/Spike/.nuget/packages -r <rid> -p:DeliveryProvider=SqlServer -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/<rid>/ -p:NuGetAudit=false -p:GeneratePackageOnBuild=false
dotnet build tests/Delivery/Build/AgentBridge.Delivery.csproj -c Release --no-restore --nologo -r <rid> -p:DeliveryProvider=SqlServer -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-release/<rid>/ -p:GeneratePackageOnBuild=false
```

Inputs/hooks проверены до выполнения: library projects, package graph, design-time factories и delivery Build без Exec; metadata preparation hook не запускался. Restore использовал только local package cache. EF runtime/Design 10.0.12 и SqlClient 7.0.2 сохранены. Locks вручную не изменялись.

## Acceptance matrix ТЗ

| Категория | Содержательное evidence | Предел |
| --- | --- | --- |
| Empty/first question/saved dates/snippet, safe text | DialogCatalogCapabilityTests: atomic saved metadata, no tool text, Unicode scalar/NFC/literal search | Host envelope/allowlist projector Ledger не реализован |
| Scope/profile/bounded stable keyset | Catalog tests + ChangedProfileOrNamespaceCannotReadHistoryOrStartRun; production provider query translation | Подпись/search/expiry/PageSize host cursor и реальные query plans/materialization не проверены |
| Root/canonical/profile/index/feed atomicity | Save/projector rollback, create unknown commit before/after apply, stale writes, expiry/delete tombstone | Strict doubles, не SQL transaction acceptance |
| Cancellation/mixed pending outcomes | Recovery tests: partial text/delta-only, saved calls/checkpoints/Started/handler cancellation, repeated call_id по позиции, Succeeded/Unknown/NotStarted | Изолированные public runner/context/executor/persistence API |
| Append-only/effective context/compaction | Original history/terminal/output invariance, Ready gate, old compact recovery revision invalidation, no handler/model replay | Actual provider restart не выполнялся |
| Cross-instance lifecycle/fencing/CAS | Two instances Begin/recovery CAS; active recovery/delete/next turn refusal; renew/expiry/stale epoch; existing turn no replay | Persisted-state doubles, не реальные locks нескольких процессов |
| Recovery failures/unknown commit | Save/read/finalize/OCE failures, before/after apply, full-batch rollback, RecoveryId lookup/idempotency/altered payload | Unknown сохраняется неподтверждённым до authoritative read |
| Trusted evidence/quiescence | Resolver refusal/OCE/exception/mismatch/oversize/missing; no transaction during I/O; idempotency без повтор resolver/verifier; race recovery/delete/fence | Авторизованный host evidence port обязателен; real external evidence не выполнялся |
| Feed/reconciliation | Bounded ordered feed/state, gaps/unknown checkpoints, tombstone/no ID reuse; нет snapshot reads в bounded paths | Host cache checkpoint/application/rebuild orchestration не реализованы |
| Provider schema/query generation | SqlServer/Sqlite/PostgreSql disconnected query translation; strict migration snapshot/designer/current model differ и operations metadata | Не SQL execution, locking, query plans или provider acceptance |

## Generated migrations

[Точные tooling команды](Tooling-commands.md) отдельно разрешены пользователем и выполнены. Первый набор отказал при загрузке Design до генерации; исправленный local additionalprobingpath успешно загрузил проектный Design/runtime 10.0.12 через installed launcher 10.0.11. Получено предупреждение о более старой версии launcher; пакеты не подменялись.

- SqlServer: `20261011024148_AddCatalogAndDurableRecovery`.
- Sqlite: `20261011024150_AddCatalogAndDurableRecovery`.
- PostgreSql: `20261011024151_AddCatalogAndDurableRecovery`.

Up/Down/designer/snapshot согласованы с десятью таблицами current model: четыре новые catalog/clocks/feed/recovery таблицы. Legacy roots остаются CatalogRegistered=false; RuntimeJson/message SavedAtUtc nullable, RecoveryRevision=0; автоматического импорта и invented timestamps нет. Cascade нового FK только для recovery operations; tombstones/feed переживают root delete. Raw SQL operations отсутствуют. Generated файлы вручную не правились, strict assertions не ослаблялись. БД/SQL/apply не запускались.

## Новая поставка

Полный согласованный bundle создан штатными упаковщиками после отдельного разрешения [четырёх точных команд](Packaging-coherent-commands.md):

`D:/Media/User/source/repos/agent-bridge/artifacts/assistant01a-delivery-20261011-coherent/SqlServer`

Manifest SHA256: **`5fd8ddabe234857a68a79d49a74079e384920e7a122af7536dc54b7b97d9206a`**. Schema3 dll-nuget, net10.0, Release, SDK 10.0.401, RIDs win-x64/linux-x64/linux-arm64. 10 local DLL, 9 XML штатного состава (EFCoreLibrary.dll не имеет XML в source build), 3 props; manifest перечисляет 22 файла. Все listed sizes/SHA256, exact-case/portable paths, отсутствие unlisted files и assembly identities проверены отдельно после упаковки. Новые catalog/lifecycle/recovery/evidence сигнатуры присутствуют в AgentBridge.xml и используются binary consumer. Required package roots: 12, включая EF Core/Relational/SqlServer 10.0.12 и SqlClient 7.0.2. NuGet графы всех трёх RID сохранены в sourceKits; local DLL SHA256 совпадают между RID. Guard props не изменены.

Первый разрешённый [набор команд](Packaging-commands.md) создал отдельные SDK kits, но общий bundle был отвергнут mixed guard до создания destination: CodeView PDB paths содержали разные ArtifactsPath. Эти kits не смешивались и не правились. Fresh inputs пересобраны в `artifacts/assistant01a-release-coherent/<rid>/` с ArtifactsPivots=release и PathMap=<absolute variant build root>=/_/delivery-build как в штатном Prepare-DeliveryTests.ps1; сам Prepare-DeliveryTests.ps1 не запускался. После этого все десять local DLL совпали, упаковщик успешно проверил полный source closure. Файлы комплектов вручную не правились.

Точные дополнительные build свойства к ранее указанному local-cache restore/build: `-p:UseArtifactsOutput=true -p:ArtifactsPivots=release -p:ArtifactsPath=D:\Media\User\source\repos\agent-bridge\artifacts\assistant01a-release-coherent\<rid> -p:PathMap=D:\Media\User\source\repos\agent-bridge\artifacts\assistant01a-release-coherent\<rid>=/_/delivery-build`. Outputs: `bin/AgentBridge.Delivery/release`, assets: `obj/AgentBridge.Delivery/project.assets.json`.

Production provenance/алгоритм hash зафиксированы в Packaging-commands.md. Старая поставка `artifacts/ledger-delivery-20261010-ef10.0.12-sql7.0.2/SqlServer` не изменена и не является новой; её manifest SHA256 `00c5515d809e435ac2654b40b1bb82ad3593f227de458ce3289d5931f3504035` не применим к новым binaries.

External binary consumer находится вне всех репозиториев: `C:/Users/Spike/.codex/scratch/agentbridge-assistant01a-20261011-coherent/consumer-matrix`; рядом скопирован целый проверенный bundle в `delivery`. Source .cs/.csproj взяты из tests/Delivery/Consumer, включая AssistantDialogCapabilities.cs; ProjectReference/Exec отсутствуют, методы не исполнялись. Initial local-cache restore создал lock через NuGet; затем каждый RID прошёл --locked-mode restore и Release --no-restore build. Assets/lock включают net10.0 плюс все три RID и только local package source.

```powershell
dotnet restore consumer-matrix/AgentBridge.BinaryConsumer.csproj --locked-mode --nologo --source C:/Users/Spike/.nuget/packages -p:RuntimeIdentifier=<rid> -p:AgentBridgeDeliveryRoot=C:/Users/Spike/.codex/scratch/agentbridge-assistant01a-20261011-coherent/delivery -p:ArtifactsPath=C:/Users/Spike/.codex/scratch/agentbridge-assistant01a-20261011-coherent/artifacts/matrix-<rid> -p:NuGetAudit=false -p:GeneratePackageOnBuild=false
dotnet build consumer-matrix/AgentBridge.BinaryConsumer.csproj -c Release --no-restore --nologo -p:RuntimeIdentifier=<rid> -p:AgentBridgeDeliveryRoot=C:/Users/Spike/.codex/scratch/agentbridge-assistant01a-20261011-coherent/delivery -p:ArtifactsPath=C:/Users/Spike/.codex/scratch/agentbridge-assistant01a-20261011-coherent/artifacts/matrix-<rid> -p:GeneratePackageOnBuild=false
```

Историческая первая scratch попытка использовала dotnet restore -r: CLI ограничил RuntimeIdentifiers одним RID; смена RID в locked mode дала NU1004. Guard не отключали, lock не редактировали. Финальный fresh consumer-matrix использует -p:RuntimeIdentifier, сохраняет все RuntimeIdentifiers из штатного props и успешно проходит три locked restores/builds.

UTF-8 source/docs и новые navigation links проверены без U+FFFD/mojibake/четырёх question marks. SQLite/PostgreSQL свежие binary kits не готовились. Полный пяти-вариантный consumer matrix, native loading/Windows/Linux/ARM64 execution, реальные SQL transactions/locking/rollback/migration/restart/backup, live gateway/handlers, HTTP/widget/hosting/Docker/deployment не проверялись.

## Порядок последующего принятия в Ledger

### Последующая актуализация integration expectations, 11.10.2026

После пользовательского сообщения о `IntegrationDatabase.InitializeAsync: Expected 3 / Actual 4` исправлены оставшиеся исторические assertions в integration fixtures/maintenance. Общий IntegrationSchemaExpectations задаёт exact ordered migration IDs (SQLite/PostgreSQL четыре, SqlServer две) и полный current table set (десять). Initialize/Inspect/Update/history и schema Down/Up/SQL Server backup-restore assertions используют этот контракт; ProviderDesignTimeTests из isolated набора сверяет его с actual generated migration assemblies/current model. Legacy six-table cascade/data tests сохранены отдельно, catalog/feed не включаются в ошибочную root cascade проверку. Правило обязательной синхронизации закреплено в Integration/AGENTS.md и tests/AGENTS.md.

Свежий persistence Debug compile-check: 0 warnings/errors; full isolated run с `--filter-not-trait Dependency=Database`: 422 passed, 0 failed/skipped, diagnostics `artifacts/assistant01a-tests/integration-expectations`. Команда совпадает с указанной выше, изменён только diagnostic-output-directory. Реальные Database tests после правок не запускались; показанная ошибка fixture устранена в исходниках, integration green не заявляется. Production sources/generated migrations/поставленный bundle не менялись, новая упаковка не требуется.

### Дальнейшая приёмка

1. Отдельное разрешение на приёмку целого нового комплекта; verify всех paths/size/hash/assembly/API/XML/manifest и exact roots/RID graph. Не смешивать файлы старой и новой поставок.
2. Сохранить Ledger.Dependencies.props, штатные exact/RID/root/lock guards и импорт AgentBridge.Delivery.props после app PackageReferences. Local-cache locked restore/build; missing package — blocker без сети. Изменившийся graph/locks обновляет только NuGet по отдельному обоснованному разрешению.
3. Actual DLL contract tests и affected Ledger graph, затем adapters/projector/profile gate/catalog/recovery в рамках 01A и свежие fast tests/coverage. Unknown требует durable recovery; прежние turn/handler не replay.
4. Отдельная SQL/provider/migration/restart/native/Linux/runtime приёмка на разрешённых ресурсах. Только после всех обязательных критериев можно оценивать завершение Ledger 01A. Эта библиотечная работа сама его не завершает и не разрешает 02–09.
