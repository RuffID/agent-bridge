# 08 — Модели хранения и контекст

Статус: **Реализован и принят; запрещённые проверки пропущены**. Зависимости: **05, 06, 07**. Дата проверки: **2026-10-03**. Коммит этапа разрешён после приёмки; точный hash локального коммита — в истории Git.

## Цель

Настроить маппинг данных диалога через EF-контекст с выбираемым провайдером на основе EFCoreLibrary.

## Задачи

- [x] Определить модели хранения диалогов, обращений, результатов инструментов и состояний контекста.
- [x] Сохранить канонические данные протокола и стабильные поля последовательности/версии.
- [x] Настроить владение, срок истечения, обязательные связи и индексы для реальных операций чтения.
- [x] Зарегистрировать адаптер контекста и репозитории по актуальному контракту ключа контекста EFCoreLibrary.
- [x] Добавить конфигурацию провайдера SQLite/PostgreSQL без обязательного выбора SQLite.
- [x] Описать обязанности хранения в ближайшем `AGENTS.md`.

## Проверка и завершение

Статически и изолированно проверены EF metadata и различия маппинга providers. Реальные ограничения БД и атомарность этим не подтверждены. Migrations не создавались и не применялись: они принадлежат этапу 11. Этап принят координатором; запрещённые проверки пропущены.

## Реализованное

- Общий `AgentBridgeDbContext`, отдельные persistence DTO без изменений Domain/Application: диалог, turn, canonical item (включая tool results), model step и compact.
- Сохраняемые IncarnationId/Revision и fixed owner/created/expiry, LastChangedAtUtc, ContentBytes; metadata concurrency/immutable fields не подменяет atomic guards будущего сценария.
- Локальные composite turn/step keys и FK, порядок Sequence/Version, cascade deletion. Индексы истории/шагов/expiry; ненужный owner-индекс не добавлен, искусственный предел OwnerId отсутствует.
- Полный lifecycle/output/envelope/continuation/expected error в общем complex payload; JSON text без provider-нормализации, FormatVersion=1 и fail-fast преобразование. Items отдельно от envelope. Все принятые compact сохраняются; активен max Version, Completed constraint. Prefix metadata не удаляет историю и не реализует cutoff.
- UTC ticks с точными сравнениями для SQLite/PostgreSQL; выбор через существующие DatabaseOptions без default SQLite. Scoped DI через актуальные AddEfCoreContext/AddEfCoreBaseRepositories, sensitive logging выключен.
- Обязательный локальный ProjectReference EFCoreLibrary; Microsoft EF/Relational/SQLite 10.0.11, Npgsql provider 10.0.3. Test DI 10.0.11 устраняет обнаруженный NU1605. Соседние исходники не менялись.

## Фактические проверки

Рабочий каталог всех команд: `D:\Media\User\source\repos\agent-bridge`. Перед restore/build/test прочитаны csproj, применимые AGENTS, проверены ancestor Directory.Build/NuGet/lock и generated imports; пользовательских Exec/custom hooks не найдено. Compile outputs игнорируются Git. Pack/publish отключены явно для ProjectReference EFCoreLibrary.

```powershell
dotnet restore tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -p:GeneratePackageOnBuild=false
dotnet build adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
```

Результат: restore успешен после согласования DI; production и test builds — **0 warnings / 0 errors**; тесты — **34 passed / 0 failed / 0 skipped**, включая **25 новых** и 9 существующих. Первоначальный test build обнаружил CS8602 в nullable metadata имени constraint; исправлено, повторная сборка без предупреждений. Проверены настоящие provider metadata без открытия connection: keys/FK/cascades/checks, required complex response, UTC converter, scoped adapter/base repository регистрации. Payload-сериализация проверяет все lifecycle, tool call_id, unknown/opaque fields, отдельные envelope/continuation, повреждённые формы и стабильный token DTO. Это не persistence/restart на настоящей БД.

Статические проверки: фактический NuGet graph в project.assets.json, отсутствие EF в ядре/Application, отсутствие DB/SQL/application/process вызовов в тестах, один верхнеуровневый тип на файл, английские пути/русские XML docs, UTF-8 без U+FFFD/mojibake/четырёх вопросительных знаков, `git diff --check` и итоговый список файлов.

## Пропуски и границы

- **Пропущено по указанию пользователя:** БД любого provider, SQL, SQLite in-memory/EF InMemory, реальные CRUD/relational constraints/concurrency/restart, migrations/apply, backup/restore/native processes, hosting/TestServer/WebApplicationFactory, реальные HTTP, Docker, приложение и проектные скрипты. Эти пропуски не входят в 0 skipped runner.
- OpenSpec CLI отсутствует в PATH (`Get-Command openspec -ErrorAction SilentlyContinue`); CLI validation **не выполнялась**, не считается успешной. Change `openspec/changes/persistence-models` принят и остаётся неархивированным: CLI validation и реальные интеграции не подтверждены.
- CRUD-adapters 09, сценарные UoW/atomic guards 10, migrations 11, startup 12 и AgentRunner 20 не реализованы. Восстановление Domain не добавлено: моделей и формата достаточно для 08; private lifetime не переносится в persistent token. Prefix/monotonic coverage проверит будущий сценарий.
- После приёмки разрешён локальный коммит только 34 перечисленных файлов; точный hash — в истории Git. Remotes/publication не выполняются.

## Изменённые файлы

Пути относительно `D:\Media\User\source\repos\agent-bridge`:

- `adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj`
- `adapters/AgentBridge.Persistence.EfCore/AGENTS.md`
- `adapters/AgentBridge.Persistence.EfCore/AgentBridgeContextKey.cs`
- `adapters/AgentBridge.Persistence.EfCore/AgentBridgeDbContext.cs`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Mapping/UtcTicksConverter.cs`
- `adapters/AgentBridge.Persistence.EfCore/Mapping/ModelResponseMapping.cs`
- `adapters/AgentBridge.Persistence.EfCore/Models/DialogRecord.cs`
- `adapters/AgentBridge.Persistence.EfCore/Models/DialogTurnRecord.cs`
- `adapters/AgentBridge.Persistence.EfCore/Models/CanonicalItemRecord.cs`
- `adapters/AgentBridge.Persistence.EfCore/Models/ModelStepRecord.cs`
- `adapters/AgentBridge.Persistence.EfCore/Models/DialogContextRecord.cs`
- `adapters/AgentBridge.Persistence.EfCore/Models/ModelResponseRecord.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj`
- `tests/AgentBridge.Persistence.EfCore.Tests/PersistenceModelTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/PersistencePayloadTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/PersistenceRegistrationTests.cs`
- `tests/AGENTS.md`
- `AGENTS.md`
- `README.md`
- `Documentation/README.md`
- `Documentation/Plans/README.md`
- `Documentation/Technical documentation/01-architecture.md`
- `Documentation/Technical documentation/02-efcorelibrary.md`
- `Documentation/Technical documentation/09-application-ports.md`
- `Documentation/Technical documentation/README.md`
- `Documentation/Plans/AgentBridge Initial Implementation/08-persistence-models.md`
- `Documentation/Plans/AgentBridge Initial Implementation/README.md`
- `openspec/specs/agent-runtime/spec.md`
- `openspec/specs/agent-runtime/context.md`
- `openspec/changes/persistence-models/proposal.md`
- `openspec/changes/persistence-models/tasks.md`
- `openspec/changes/persistence-models/context.md`
- `openspec/changes/persistence-models/specs/agent-runtime/spec.md`

Источник: [интеграция EFCoreLibrary](<../../Technical documentation/02-efcorelibrary.md>).
