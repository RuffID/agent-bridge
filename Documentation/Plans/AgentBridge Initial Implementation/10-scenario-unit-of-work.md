# 10 — Границы сценарных Unit of Work

Статус: **Реализован и принят; запрещённые проверки пропущены**. Зависимости: **09**.

## Дополнительная интеграционная проверка 2026-10-04

SQLite/PostgreSQL actual Serializable UoW через EFCoreLibrary подтверждены: owner/existence/expiry/incarnation/revision, root PK collision, stale original-value CAS с rollback детей, fixed-field запрет, удаление/пересоздание и два competing writers одного token. SQLite BEGIN IMMEDIATE сериализует writers; PostgreSQL serialization SQLSTATE 40001 может приходить во вложенной EF exception-обёртке. Driver failures не подменяются ожидаемым Conflict, автоматического retry нет.

Тестовый session декоратор вызывает **настоящий SaveChanges**, подтверждает минимум три сохранённые записи, затем выбрасывает ошибку до commit. Public BeginAsync откатывает root/turn/item, fingerprint БД не меняется, tracker очищен. Это реальные SQL/transactions; ошибка внедрена тестом, production и библиотека не заменены. Неизвестный commit/cleanup при реальном сетевом сбое остаётся вне этого запуска, изолированные regressions сохранены.

**121 core / 164 isolated persistence / 39 integration passed**, 0 failed/skipped; [точные команды](README.md#дополнительный-интеграционный-запуск-2026-10-04). Первоначальные fake-only результаты ниже сохранены; внешняя модель, инструменты и оркестрация не запускались.

## Цель

Обеспечить атомарность связанных изменений без глобального контейнера репозиториев.

## Задачи

- [x] Определить минимальные Unit of Work изменяющих сценариев и общий технический scope сохранения/транзакций.
- [x] Использовать один scoped-контекст EFCoreLibrary для участвующих репозиториев.
- [x] Не связывать сценарии только чтения с ненужными UoW-контейнерами.
- [x] Отделить ожидание внешней модели/инструмента от коротких транзакций БД.
- [x] Проверять версию и доступность диалога перед сохранением итогового результата.

## Проверка и завершение

Частичные ошибки и попытки сохранения неактуального диалога проверены управляемыми заглушками. Связанные изменения используют общий технический scope, параллельное использование одного контекста отклоняет scoped gate. Реальная relational atomicity не проверена. Этап принят; запрещённые проверки пропущены.

## Реализовано

- Четыре минимальных сценарных UoW: создание, Begin/Append/Finish, принятие compact, явное/системное удаление одного диалога. Они реализуют ранее определённые Application ports и используют общий `UnitOfWorkScope`; глобального контейнера репозиториев нет. Read ports остаются отдельными.
- Техническая `EfUnitOfWorkSession` использует actual `IUnitOfWorkContext<AgentBridgeContextKey>` EFCoreLibrary: Serializable transaction, save и clear. Один scoped context/key/session/gate через существующую регистрацию; все CRUD делегированы base API. Соседняя библиотека не изменена, gap контракта не обнаружен.
- Внутри transaction проверяются ID/existence/owner ordinal/явный expiry/incarnation/revision. Root читается tracking, child writes сопровождаются revision update; старый token не подменяется свежим. Сохранение owner/fixed dates не изменяет. Только подтверждённый root CAS failure на save/точный PK collision создания имеет ожидаемый Conflict; неизвестные driver/serialization/commit/cleanup failures остаются исключениями, без retry.
- После собственной transaction выполняются rollback при отказе, dispose и clear до возврата. Ошибки begin/commit/cleanup блокируют текущий scope; несколько ошибок сохраняются в AggregateException. При отказе begin чужой tracker/transaction не затрагивается. Shared gate охватывает также read ports и не разрешает concurrent/nested операции. Сеть/инструмент не передаются в порты или Infrastructure callbacks.
- `Dialog.Restore` проверяет реальные даты/revision/LastChangedAtUtc, ID/порядок turns и все terminal prefixes без replay mutations. Добавлен `TryAppendTurn`; локальная идентичность жизни не превращена в persistent token. Persistence DTO остаются отдельными, EF в Domain/Application не добавлен.
- Items и полные model reports сохраняются отдельно, включая все lifecycle/unknown/opaque/envelope/continuation/error. IDs step локальны turn; все context versions и история сохраняются, prefix не обрезает items. ContentBytes считает UTF-8 сохраняемых payload-колонок, без overhead БД. Мягкий лимит запись не блокирует; UI warning и будущая оркестрация не реализуются этим этапом.
- Обновлены архитектурные AGENTS, README/навигация, technical docs и main/delta OpenSpec. Change `scenario-unit-of-work` не архивирован.

## Фактические проверки

Все команды выполнялись из `D:\Media\User\source\repos\agent-bridge`, ветка `master`. До сборок прочитаны применимые AGENTS, root/adapter/test/EFCoreLibrary csproj, проверены ancestor Directory.Build/Directory.Packages/NuGet/lock и package-generated imports. Пользовательских executable hooks не обнаружено. Restore не потребовался; pack/publish отключены явно, outputs — `artifacts/compile-check` соответствующих проектов.

```powershell
dotnet build adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
```

Итог: **107 core passed / 0 failed / 0 skipped** (14 новых), **108 persistence passed / 0 failed / 0 skipped** (37 новых). Production и итоговые test builds — **0 warnings / 0 errors**. Первоначальная test build дала 2 EF1001 из test-only internal EF entries: заменены публичным override Entries в fake exception. Первый persistence run: 100 passed / 4 failed — assertions сравнивали семантически одинаковый JSON с разным Unicode escaping; исправлено на JsonElement.DeepEquals, production serialization не менялась. Следующий полный run 108/108 прошёл.

Дополнительная concern ревью проверена адресно после полного run: actual `RecordStaging → UpdateItemRepository → EfDbContextAdapter → EF tracker`, без БД/SaveChanges. OriginalValue Revision/incarnation/owner/expiry сохраняются; CurrentValue Revision новая; fixed properties не modified. После усиления assertions выполнены только test build (0 warnings/errors) и:

```powershell
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~BaseUpdatePreservesOriginalConcurrencyAndFixedFields
```

Результат: **1 passed / 0 failed / 0 skipped**. Полные наборы повторно без причины не запускались.

При финальной сверке ServiceErrorType и принятого contract fake этапа 07 исправлена классификация expiry на `Expired` (с приоритетом перед stale revision), вместо общего Conflict. Повторная test build прошла с 0 warnings/errors; выполнены только затронутые guards:

```powershell
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false --filter FullyQualifiedName~GuardsRejectWithoutReadingChildren
```

Результат: **6 passed / 0 failed / 0 skipped**. Полные 108/107 запускались до этой адресной правки; остальные paths не менялись. Финальная статическая проверка: `git diff --check` без ошибок, 60 изменённых/новых файлов UTF-8/LF без U+FFFD/mojibake/четырёх вопросительных знаков. В отчёте перечислены все 60 файлов, включая untracked; EFCoreLibrary clean, ветка AgentBridge master.

Проверены partial save/rollback/commit/cleanup failures, сохранение первичной и вторичных ошибок, caller cancellation с обязательным cleanup, gate для read/write через управляемое незавершённое await без фоновых задач. Production write ports проверены на stale ID/owner/expiry/incarnation/revision, delete/recreate, fake CAS interleavings, duplicate turns/steps, terminal state, полный payload/UTF-8 bytes, все compact versions и сохранение прежнего состояния при отказе. Fakes имеют отдельные committed/staged копии; настоящую БД не заменяют.

## Пропуски и ограничения

- **Пропущено по указанию пользователя:** любые реальные БД (включая SQLite in-memory/EF InMemory), SQL, relational locking/atomicity/cascades/query translation/restart, migrations/apply, backup/restore/native/dump processes, реальные HTTP/codex-lb/OpenAI, hosting/TestServer/WebApplicationFactory, Docker, приложения и произвольные project/user scripts/external processes. Эти пропуски не входят в 0 skipped runner.
- `Get-Command openspec -ErrorAction SilentlyContinue` не находит CLI. CLI validation **не выполнялась**, не считается успехом; change не архивирован.
- Provider serialization/busy errors не нормализуются как ожидаемый Conflict. Неизвестный исход commit не обещает отсутствие записи. Новый DI scope не разрешает автоматический retry внешнего действия; согласование результата остаётся вызывающему коду.
- Запросы полной истории метаданных для восстановления последовательны и находятся в короткой transaction без сетевого ожидания; оптимизация объёма запросов не входит в этап. Migrations/startup/agent orchestration/Responses mapping/cutoff/cleanup scheduling остаются этапам 11–22.
- Исходники EFCoreLibrary/HttpClientLibrary, корневые проекты и ветка не изменены. Разрешён локальный коммит только перечисленных 60 файлов; remotes/publication не выполняются. **Этап принят; запрещённые проверки пропущены**.

## Изменённые файлы

Точный список относительно `D:\Media\User\source\repos\agent-bridge` приведён ниже; build outputs в него не входят.

- `AGENTS.md`
- `Application/AGENTS.md`
- `Application/Ports/IDialogContextWriter.cs`
- `Application/Ports/IDialogTurnWriter.cs`
- `Documentation/Plans/AgentBridge Initial Implementation/10-scenario-unit-of-work.md`
- `Documentation/Plans/AgentBridge Initial Implementation/README.md`
- `Documentation/Plans/README.md`
- `Documentation/README.md`
- `Documentation/Technical documentation/01-architecture.md`
- `Documentation/Technical documentation/02-efcorelibrary.md`
- `Documentation/Technical documentation/05-configuration-and-lifecycle.md`
- `Documentation/Technical documentation/08-dialog-domain-state.md`
- `Documentation/Technical documentation/09-application-ports.md`
- `Documentation/Technical documentation/README.md`
- `Domain/AGENTS.md`
- `Domain/Dialogs/Dialog.cs`
- `README.md`
- `adapters/AgentBridge.Persistence.EfCore/AGENTS.md`
- `adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Reading/DialogReader.cs`
- `adapters/AgentBridge.Persistence.EfCore/Reading/ExpiredDialogReader.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/ModelStepRecordQueries.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/RecordStaging.cs`
- `openspec/specs/agent-runtime/context.md`
- `openspec/specs/agent-runtime/spec.md`
- `tests/AGENTS.md`
- `tests/AgentBridge.Persistence.EfCore.Tests/DialogReaderTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/ExpiredDialogReaderTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeBaseRepository.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/PersistenceRegistrationTests.cs`
- `Documentation/Technical documentation/10-scenario-unit-of-work.md`
- `Domain/Dialogs/DialogContextSnapshot.cs`
- `Domain/Dialogs/DialogTurnSnapshot.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/AGENTS.md`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogContextUnitOfWork.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogCreationUnitOfWork.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogDeletionUnitOfWork.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogStateLoader.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogTurnUnitOfWork.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogWriteGuard.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/DialogWriteResults.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/EfUnitOfWorkSession.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/IUnitOfWorkSession.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/PersistenceOperationGate.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/PreparedTurnContent.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/StoredContentSize.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/TurnContentStaging.cs`
- `adapters/AgentBridge.Persistence.EfCore/UnitOfWork/UnitOfWorkScope.cs`
- `openspec/changes/scenario-unit-of-work/context.md`
- `openspec/changes/scenario-unit-of-work/proposal.md`
- `openspec/changes/scenario-unit-of-work/specs/agent-runtime/spec.md`
- `openspec/changes/scenario-unit-of-work/tasks.md`
- `tests/AgentBridge.Persistence.EfCore.Tests/DialogWritePortsTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeRootConcurrencyException.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeUnitOfWorkSession.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeWriteFixture.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeWriteTransaction.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/UnitOfWorkScopeTests.cs`
- `tests/AgentBridge.Tests/DialogRestorationTests.cs`

Источник: [решения по жизненному циклу](<../../Technical documentation/05-configuration-and-lifecycle.md>).
