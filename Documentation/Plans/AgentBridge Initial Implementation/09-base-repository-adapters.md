# 09 — Адаптеры базовых репозиториев EFCoreLibrary

Статус: **Реализован и принят; запрещённые проверки пропущены**. Зависимости: **08**.

## Цель

Реализовать прикладные контракты данных через базовые операции общей библиотеки.

## Задачи

- [x] Адаптировать чтение диалогов и контекста к базовым репозиториям по ID/условию.
- [x] Использовать базовые репозитории создания, изменения и удаления для соответствующих операций как staging без сохранения.
- [x] Сохранить стабильный порядок диалога и фильтры владения пользователем.
- [x] Использовать custom query только тогда, когда нужная операция не выражается проверенными базовыми средствами: predicate/include достаточны, custom query не добавлен.
- [x] Не обходить недостаточное/спорное поведение библиотеки через EF/SQL: недостаточности base API для задач 09 не обнаружено, соседние исходники не изменены.

## Проверка и завершение

Адаптеры проверены изолированными заглушками точных base API. Общий CRUD делегируется библиотеке, а не копируется в AgentBridge. Этап реализован и принят; это не подтверждение работы реальной БД.

## Реализовано

- Пять узких record query adapters: глобальный DialogRecord.Id через base ById; локальные turn/step IDs через predicate с родителями; items/steps/turns/versions сортируются по сохранённому порядку. Active compact — max Version, не время. Include композиция применяется до take, custom query не требуется.
- `RecordStaging<TEntity>` делегирует Create/Update/Delete и все Range-варианты, возвращает void и не вызывает SaveChanges/transaction. Никакого несохранённого успеха `IDialogCreator`/writers/deletions нет; эти порты не реализованы и не зарегистрированы. Staging не разрешает row mutations в обход Domain. Новые Application repository ports/UoW не вводились.
- `IDialogReader`: owner ordinal до детей; primitive owner/incarnation/revision/metadata фиксируются до await; после всех последовательных child reads root перечитывается. Delete/recreate другим владельцем, same-owner новая incarnation и изменение revision дают NotFound/Forbidden/Conflict без snapshot/token замены/retry. Orphan items/steps отклоняются явно. Вся история и полные lifecycle/output/envelope/continuation/error сохраняются; prefix не отбрасывает items. Незавершённый принятый compact/повреждённый JSON явно отклоняются.
- `IExpiredDialogReader`: положительный limit, точный UTC expiry <= now, порядок expiry/ID до take, только token кандидатов. Empty — успех; удаление не выполняется.
- Scoped DI регистрирует adapters и два read ports в existing `AddAgentBridgePersistence`. Один context-key/shared session; нет параллельных запросов к одному контексту, нового DbContext или mega-UoW. Domain не изменён; Application изменён только XML-описанием ожидаемого Conflict чтения.

## Фактические проверки

Рабочий каталог build/test: `D:\Media\User\source\repos\agent-bridge`. До запуска проверены production/test/root/EFCoreLibrary csproj, применимые AGENTS, ancestor Directory.Build/NuGet/lock и package-generated imports. Пользовательских Exec/custom executable hooks не обнаружено. Зависимости восстановлены предыдущим этапом; restore на 09 не потребовался. Pack/publish отключены явно, outputs — `artifacts/compile-check` каждого проекта.

```powershell
dotnet build adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=./artifacts/compile-check/ -p:GeneratePackageOnBuild=false
```

Итог: production/test builds — **0 warnings / 0 errors**; **71 passed / 0 failed / 0 skipped**, **37 новых** и 34 существующих persistence-теста. Первоначальная production-сборка обнаружила CS1501 в пяти вызовах несуществующей перегрузки Fail; исправлено под actual `Fail(ServiceError)`, последующие сборки успешны. После XML/doc-only правок выполнен финальный compile-check; поведение после успешных тестов не менялось.

Новые проверки: все пять row types делегируют одиночный/пакетный CRUD без commit; root/turn tracking и cancellation передаются base API; parent filters выдерживают collisions локальных IDs; unordered данные сортируются по Sequence/Version; expiry точен до ticks и ограничивается после сортировки. Public read path проверяет четыре lifecycle, tool call_id/unknown/opaque JSON, envelope/continuation/error, независимость DTO от изменяемых records, историю до prefix и InProgress turn, owner без trim/casefold, пустую/истёкшую историю, отказ без чтения детей/staging, caller cancellation, unexpected errors и corruption. Управляемые fake interleavings проверяют delete/recreate/новую incarnation/revision; stable case проходит две root reads. DI обоих providers разрешает adapters/read ports без connection; write ports отсутствуют.

Статически проверены отсутствие нового прямого EF query/DbSet CRUD/SQL/SaveChanges/transaction в adapters09, отсутствие persistence leakage в Application, один верхнеуровневый тип на файл, английские пути и русские XML docs, UTF-8/LF без U+FFFD/mojibake/четырёх вопросительных знаков, ссылки документации, `git diff --check` и точный список файлов.

## Пропуски и ограничения

- **Пропущено по указанию пользователя:** любая БД/provider execution (включая SQLite in-memory/EF InMemory), SQL, relational enforcement/query translation/реальные CRUD/concurrency/restart, migrations/apply, backup/restore/native/dump процессы, hosting/TestServer/WebApplicationFactory, реальные HTTP/codex-lb/OpenAI, Docker, приложения, project/user scripts и внешние процессы. Эти пропуски не входят в 0 skipped runner.
- OpenSpec CLI отсутствует в PATH (`Get-Command openspec -ErrorAction SilentlyContinue`); CLI validation **не выполнялась** и не считается успешной. Change `base-repository-adapters` не архивирован. Main spec/context синхронизированы по фактическим границам.
- Повторная root-проверка не объявляется транзакционным snapshot. Полные saving/transactions/atomic write guards и Domain rehydration остаются этапу 10; модели/metadata и fakes не доказывают их на БД. Точный Responses mapping/cutoff — этапы 14–18; cleanup orchestration — 22.
- Соседние EFCoreLibrary/HttpClientLibrary, ветка и исходный каркас не изменены. После приёмки разрешён локальный коммит только 35 перечисленных файлов; точный hash — в истории Git. Remotes/publication не выполняются.

## Изменённые файлы

Пути относительно `D:\Media\User\source\repos\agent-bridge`:

- `AGENTS.md`
- `Application/AGENTS.md`
- `Application/Ports/IDialogReader.cs`
- `README.md`
- `adapters/AgentBridge.Persistence.EfCore/AGENTS.md`
- `adapters/AgentBridge.Persistence.EfCore/Configuration/PersistenceRegistrationExtensions.cs`
- `adapters/AgentBridge.Persistence.EfCore/Reading/DialogReader.cs`
- `adapters/AgentBridge.Persistence.EfCore/Reading/ExpiredDialogReader.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/ContextRecordQueries.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/DialogRecordQueries.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/ItemRecordQueries.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/ModelStepRecordQueries.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/RecordStaging.cs`
- `adapters/AgentBridge.Persistence.EfCore/Repositories/TurnRecordQueries.cs`
- `tests/AGENTS.md`
- `tests/AgentBridge.Persistence.EfCore.Tests/BaseRepositoryAdapterTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/DialogReaderTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/ExpiredDialogReaderTests.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeBaseRepository.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/FakeDialogByIdRepository.cs`
- `tests/AgentBridge.Persistence.EfCore.Tests/PersistenceRegistrationTests.cs`
- `Documentation/README.md`
- `Documentation/Plans/README.md`
- `Documentation/Plans/AgentBridge Initial Implementation/README.md`
- `Documentation/Plans/AgentBridge Initial Implementation/09-base-repository-adapters.md`
- `Documentation/Technical documentation/README.md`
- `Documentation/Technical documentation/01-architecture.md`
- `Documentation/Technical documentation/02-efcorelibrary.md`
- `Documentation/Technical documentation/09-application-ports.md`
- `openspec/specs/agent-runtime/spec.md`
- `openspec/specs/agent-runtime/context.md`
- `openspec/changes/base-repository-adapters/proposal.md`
- `openspec/changes/base-repository-adapters/tasks.md`
- `openspec/changes/base-repository-adapters/context.md`
- `openspec/changes/base-repository-adapters/specs/agent-runtime/spec.md`

Источник: [приоритеты репозиториев](<../../Technical documentation/02-efcorelibrary.md>).
