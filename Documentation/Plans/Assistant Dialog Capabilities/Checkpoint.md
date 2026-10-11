# Checkpoint приостановленной работы

Дата: 10.10.2026. Работа остановлена по прямой просьбе пользователя. **Исходники сейчас не подтверждены успешной сборкой; новая DLL-поставка отсутствует.** Git не выполнялся, поэтому это описание фактических правок, а не git diff/status.

## Поручение и ограничения

Рабочий репозиторий: `D:/Media/User/source/repos/agent-bridge`.

Требования только читаются из `D:/Media/User/source/repos/work/AquaByte-Ledger/Documentation/Plans/ai-assistant-widget-2026-10-10/`:

1. `01a-agentbridge-change-request.md`.
2. `01a-storage-and-recovery-contract.md` — нормативный контракт.
3. `01a-agentbridge-dialog-capabilities.md` — прежнее состояние и evidence.

Разрешены точечные изменения через apply_patch, необходимые restore только из `C:/Users/Spike/.nuget/packages`, compile-check и изолированные тесты после проверки inputs/hooks. Git, внешняя сеть, реальные БД/SQL/backup/migrate, Docker, browser, hosting/dev/watch, publish/deployment запрещены. Проектные скрипты/исполняемые файлы требуют отдельного разрешения точной команды. Generated migrations/snapshots вручную не редактировать. Locks вручную не редактировать.

Ledger код/vendor/locks/статусы/исторические Completed-планы не менялись. EF 10.0.12 / SqlClient 7.0.2 и штатные exact/RID/root/lock guards сохранены. Соседние EFCoreLibrary/HttpClientLibrary исходники не изменялись; compile-check создавал только build outputs их ProjectReference.

Прочитаны применимые root/Application/Domain/persistence/UoW/tests/Delivery/Documentation/migrations AGENTS, скиллы csharp-project-rules, backend-uow-repositories, ef-core-migrations, runner/context/executor, transaction guards и штатные поставщики.

## Реализовано в исходниках до последних непроверенных правок

### Core: AgentBridge/Application

- Immutable catalog scope/profile/policy/metadata/cursor/feed DTO, copied collections и cloned JSON.
- Public ports: IDialogCatalogCreator, IDialogCatalogReader, IDialogCatalogChangeReader, IDialogCatalogProjector, IDialogContinuationReader, IDialogRecovery, IDialogRunLifecycle.
- Projector имеет явные Key/Version для exact registry. Не существует default projector или Ledger envelope reader в production; приложение регистрирует чистый синхронный bounded projector.
- `IDialogRecovery.ReadAsync(DialogAccess, Guid recoveryId, CancellationToken)` — authoritative operation lookup после unknown commit.
- Full-profile history read назван `IDialogReader.ReadCatalogAsync(DialogCatalogAccess, CancellationToken)`. Overload ReadAsync с другим request приводил к source ambiguity существующих target-typed `new(...)`; отдельное имя сохраняет старые вызовы.
- Epoch-aware overloads AppendAsync/StartAsync/SaveOutcomesAsync/SaveWithModelAsync принимают DialogRunWriteAccess. Legacy default implementations возвращают Unsupported.
- AgentRunRequest overload с DialogRunOptions; runner/session получают durable Begin/Finalize и lease/epoch, короткие scopes и existing TurnId no replay сохранены.
- DialogSnapshot содержит originals Turns, отдельные EffectiveTurns, Catalog/Continuation; reader не выдаёт model permission для active run. OwnedRunLease устанавливает только библиотечная сессия.
- ContextBuilder/compactor/model guard используют effective context, readiness и recovery revision. Старое compact окно не применяется поверх новой recovery.
- Pure DialogCatalogText: whitespace/scalar limit/NFC/invariant search key, без Ledger dependencies.

### Persistence: adapters/AgentBridge.Persistence.EfCore

- Четыре новые таблицы в current EF model: DialogCatalog, DialogCatalogClocks, DialogCatalogChanges, DialogRecoveryOperations. Всего current model — 10 таблиц; migrations ещё отражают прежние шесть.
- Root CatalogRegistered/RuntimeJson concurrency, server saved message timestamps, compact RecoveryRevision.
- DialogCatalogQueries: один bounded keyset slice 1..256, sortTime DESC + RFC UUID hex DESC, bounded feed/checkpoint/reset и compact state, recovery lookup/history budget.
- DialogCatalogStaging и Creation UoW: atomic root/canonical/immutable profile/index/feed, first saved question title, «Новый чат», safe saved user/assistant dates/snippet; tool/delta/recovery не меняют message metadata.
- DialogLifecycleUnitOfWork: atomic Begin/input/lease/epoch, renew, terminal finalization, fence, append-only recovery/idempotency.
- DialogRuntimeState: bounded versioned lifecycle, максимум 1024 calls и 2 MiB; Started без outcome после финализации/fence читается Unknown, original attempt не меняется.
- Effective pairs привязаны к original turn/step/output/item позиции, включая повторные call_id и immutable confirmed outputs. Original items/reports/terminal не переписываются; handler не replay.
- Registered roots защищены от legacy writes; stale lease/epoch отвергаются. Expired lease остаётся Active до explicit fence. Delete/expiry atomic tombstone/feed; старый dialogId не переиспользуется.
- Все queries/staging используют existing EFCoreLibrary base repositories, transactions — Serializable UnitOfWorkScope без retry. Только exact CAS/PK conflicts классифицируются; unknown commit остаётся exception и poison scoped gate.
- AddAgentBridgePersistence регистрирует новые scoped ports/services и TimeProvider. Нет зависимостей от Ledger/WebAssistant/claims.

## Последние правки после успешных тестовых запусков — НЕ ПРОВЕРЕНЫ

- Добавлены DialogRecoveryKind.KnownCanceled / ConfirmedExternalOutcome, DialogRunFenceReason.ConfirmedQuiescence.
- Добавлены host ports IDialogRecoveryEvidenceResolver.ResolveAsync(DialogRecoveryEvidenceRequest) и IDialogRunQuiescenceVerifier.VerifyAsync(DialogRunFence).
- Lifecycle выполняет отдельный короткий preflight до evidence I/O, затем повторно проверяет CAS/epoch в финальной transaction. Existing operation lookup должен исключать повтор evidence I/O при idempotent retry.
- Evidence output принимается только от trusted resolver: exact function_call_output/call_id/string output, bounded bytes. Client raw output не принимается. KnownCanceled требует отдельного evidence, ordinary cancel/read-only/expiry не доказывают исход.
- Recovery journal сохраняет resolution kind/reference; synthetic error содержит recovery/original identity provenance. `DialogRecoveryRecord` теперь имеет `Output`, `Kind`, `EvidenceReference` (прежнее новое поле ErrorOutput переименовано до поставки).
- CatalogWriteFixture принимает optional evidence/quiescence doubles; существующие assertions переведены на Output. Новых evidence acceptance tests ещё нет.
- DialogReader сохраняет primitive RuntimeJson перед children reads, чтобы mutable double не скрывал изменение.
- Добавлены явные прежние constructor overloads DialogReader, DialogCreation/Turn/ToolAttempt/Context/DeletionUnitOfWork для бинарных сигнатур.

**Непосредственная незавершённая правка:** добавление прежней сигнатуры DialogWriteGuard.LoadAsync вызвало CS0121 в DialogSettingsUnitOfWork. Затем расширенный метод переименован в `LoadRunAsync`, legacy wrapper оставлен `LoadAsync`, но остальные call sites ещё НЕ переведены на LoadRunAsync. Запущенная до паузы apply_patch завершилась. После переименования сборка не запускалась; named lease/catalogControl вызовы сейчас требуют исправления.

При продолжении обновить только расширенные guard calls в DialogTurnUnitOfWork, DialogToolAttemptUnitOfWork, DialogContextUnitOfWork, DialogDeletionUnitOfWork и DialogLifecycleUnitOfWork. Legacy DialogSettingsUnitOfWork должен сохранить LoadAsync. Затем проверить overload resolution/DI/бинарные сигнатуры и новые evidence paths.

## Выполненные проверки и точные пределы

Все результаты ниже относятся к исходникам ДО последних непроверенных evidence/constructor/guard изменений.

| Проверка | Результат |
| --- | --- |
| Core isolated suite | 542 passed, 0 failed/skipped |
| Новые catalog acceptance cases | 12 passed |
| Новые recovery acceptance cases | 15 passed |
| Новые lifecycle failure cases | 14 passed |
| Всего новых cases, отдельные запуски | 41 passed; финального общего свежего запуска нет |
| Full isolated persistence до добавления последних 14 cases | 386 total: 383 passed, 3 failed, 0 skipped |
| Три persistence failures | ProviderDesignTimeTests.GeneratedSnapshotAndDesignerMatchCurrentSchema для SQLite/PostgreSql/SqlServer: current model отличается от старых generated snapshots; assertions не ослаблены |
| Последняя сборка после evidence/constructor additions | Failed: 1 CS0121 в DialogSettingsUnitOfWork из-за двух optional guard overloads, 0 warnings |
| После переименования расширенного guard в LoadRunAsync | Build/test не запускались; call sites не обновлены |

Тесты подключают actual public runner/builder/executor/persistence API через isolated base repositories/transaction/gateway doubles с раздельными committed/staged копиями и двумя instances. Это не provider/SQL atomicity или production restart acceptance.

Успешные прежние compile-check были 0 warnings / 0 errors. Restore выполнялся только local cache, с NuGetAudit=false / GeneratePackageOnBuild=false. Параметры штатных build/test:

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore --nologo -p:BaseOutputPath=artifacts/assistant01a-compile/ -p:GeneratePackageOnBuild=false
dotnet test --project tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/assistant01a-compile/ -p:GeneratePackageOnBuild=false --filter-not-trait Dependency=Database --diagnostic --diagnostic-output-directory artifacts/assistant01a-tests/final
```

Для адресного запуска добавить `--filter-class '*DialogRecoveryCapabilityTests'` или другой класс. Не применять старые VSTest --filter. Попытки --report-trx/--report-trx-filename дали 0 executed / exit5; diagnostic mode работает. Core project: `tests/AgentBridge.Tests/AgentBridge.Tests.csproj` — перед запуском проверить фактический путь и inputs.

Новые тестовые файлы: CatalogWriteFixture.cs, CatalogRunnerFixture.cs, DialogCatalogCapabilityTests.cs, DialogRecoveryCapabilityTests.cs, DialogLifecycleFailureTests.cs. Existing metadata/retention tests скорректированы под 10 таблиц и bounded registered expiry, historical migration tests не ослаблены.

## Migrations и DLL-поставка

Migrations tooling/упаковщики НЕ запускались; отдельное разрешение команд ещё НЕ запрашивалось.

- Installed global dotnet-ef 10.0.11; runtime/Design EF 10.0.12. Exact tool 10.0.12 в локальном NuGet cache не найден. Нельзя загружать его из сети или менять runtime version.
- Driver найден: `C:/Users/Spike/.dotnet/tools/.store/dotnet-ef/10.0.11/dotnet-ef/10.0.11/tools/net8.0/any/tools/net8.0/any/ef.dll`.
- Три design-time factories принимают только empty args, synthetic connection и не открывают БД/host. Target/startup — соответствующая migrations library. Build outputs доступны в `adapters/AgentBridge.Persistence.Migrations.<Provider>/artifacts/assistant01a-compile/Debug/net10.0/` с DLL/deps/runtimeconfig.
- Возможный путь генерации — approved exact `dotnet exec --depsfile ... --runtimeconfig ... <ef.dll> migrations add AddCatalogAndDurableRecovery --assembly ... --startup-assembly ... --project-dir ... --root-namespace ... --language C# --nullable --context AgentBridgeDbContext --output-dir Migrations` для каждого provider. Это только идея: проверить options/совместимость driver и запросить точные три команды; не считать разрешёнными.
- Нужны новые Up/Down/designer/snapshot. Существующие roots legacy не импортировать автоматически: новые registration/runtime поля должны безопасно сохранять старые данные.
- Штатная поставка: tests/Delivery/Build/AgentBridge.Delivery.csproj, Assemble-Delivery.ps1 (BuildOutput/AssetsFile/Destination/Provider/Rid/Configuration/provenance/SDK), затем Assemble-NuGetDelivery.ps1 (SourceRoot/Destination).
- Требуются новые согласованные Release SQL Server SDK kits для win-x64/linux-x64/linux-arm64 из local cache и полная DLL/XML/props/manifest schema3 поставка. Для каждого packaging script получить разрешение точной команды.
- Prepare-DeliveryTests.ps1 как есть НЕ запускать: restore с default sources нарушит запрет сети. Metadata project имеет такой Exec hook; при безопасном compile использовать AgentBridgeSkipDeliveryPreparation=true, не объявляя fresh delivery validation.
- Старая поставка `artifacts/ledger-delivery-20261010-ef10.0.12-sql7.0.2/SqlServer`, manifest SHA256 `00c5515d809e435ac2654b40b1bb82ad3593f227de458ce3289d5931f3504035` не обновлялась и НЕ содержит новую работу.

## Остаток после возобновления

1. Завершить LoadRunAsync call sites и подтвердить compile; проверить семантику/границы последних evidence additions.
2. Добавить meaningful actual API tests evidence/quiescence: refusal/OCE/exception/mismatch/oversize, trusted external output без изменения originals, idempotency без повтор verifier, CAS/race после awaited evidence, partial batch rollback, missing resolver, active/recovery/delete/next-turn races.
3. Проверить compaction recovery revision и unresolved calls. Fenced original InProgress остаётся original; compactor останавливает terminal prefix на таком turn и сохраняет suffix. Не переписывать terminal ради compact.
4. Завершить owning AGENTS, technical/business documentation/OpenSpec/API mapping и acceptance matrix. Эти обязательные обновления до паузы НЕ выполнены; добавлены только checkpoint, навигация и промпт.
5. Согласовать точные migrations/tooling команды, сгенерировать/проверить артефакты без SQL execution; strict snapshot tests должны пройти.
6. Финальные fresh affected/core tests, UTF-8/line endings, отсутствие U+FFFD, mojibake и последовательностей четырёх вопросительных знаков. Проверить binary compatibility по actual API, не по номерам версии.
7. Согласовать точные packaging commands и создать новый coherent kit/hash через штатный механизм; exact roots/RID/lock guards не обходить.
8. Передать реализованные сигнатуры/evidence/kit либо точный blocker, непроверенные SQL/provider/native/Linux/live категории и отдельный порядок принятия в Ledger. Ledger 01A остаётся In Progress.

Работа не продолжается автоматически: нужна новая явная просьба пользователя о возобновлении.
