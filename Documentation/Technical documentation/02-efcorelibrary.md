# Доступ к данным через EFCoreLibrary

Текущий write API этапа 10: [сценарные Unit of Work](10-scenario-unit-of-work.md). Реализован и принят; запрещённые проверки пропущены. `AddAgentBridgePersistence` регистрирует все существующие read/write ports, общий scope/session/gate. Исторические разделы 08–09 ниже описывают границы тех этапов, а не отсутствие текущего write path.

## Обязательное решение

Вся работа с БД AgentBridge строится на EFCoreLibrary. Исходники: `D:\Media\User\source\repos\work\EFCoreLibrary`.

Приоритет — готовые базовые операции чтения, создания, изменения и удаления. Custom query используется при недостаточности базовых операций и остаётся внутри той же инфраструктуры. Прямой EF/SQL или собственный параллельный набор репозиториев для обхода библиотеки не применяется.

Если контракт библиотеки недостаточен или противоречит потребностям, вопрос сначала обсуждается с пользователем. Библиотека развивается, поэтому старые примеры не заменяют проверку текущих исходников.

## Проверенные типы библиотеки

Повторно проверено статически на этапе 00, 2026-10-03. `EFCoreLibrary.csproj`: версия `0.0.4`, `net10.0`, EF Core и DI Abstractions `10.0.3`. Это версии исходного проекта, не подтверждение состава ранее собранных DLL.

| Операция | Infrastructure-контракт | Базовый класс |
| --- | --- | --- |
| Чтение по ID | `IContextGetItemByIdRepository<TEntity, TId, TContextKey>` | `GetItemByIdRepository<TEntity, TId, TContextKey>` |
| Чтение по условию | `IContextGetItemByPredicateRepository<TEntity, TContextKey>` | `GetItemByPredicateRepository<TEntity, TContextKey>` |
| Создание | `IContextCreateItemRepository<TEntity, TContextKey>` | `CreateItemRepository<TEntity, TContextKey>` |
| Изменение | `IContextUpdateItemRepository<TEntity, TContextKey>` | `UpdateItemRepository<TEntity, TContextKey>` |
| Удаление | `IContextDeleteItemRepository<TEntity, TContextKey>` | `DeleteItemRepository<TEntity, TContextKey>` |
| Custom query | `IContextQueryRepository<TEntity, TContextKey>` | `QueryRepository<TEntity, TContextKey>` |

Базовые read-контракты поддерживают predicate, tracking-настройку и композицию запроса. CRUD-классы изменяют общий change tracker; сохранение выполняется отдельно.

### Точные операции и ограничения

- [CreateItemRepository](../../../work/EFCoreLibrary/EfCore/Repository/Base/CreateItemRepository.cs#L10), [UpdateItemRepository](../../../work/EFCoreLibrary/EfCore/Repository/Base/UpdateItemRepository.cs#L10) и [DeleteItemRepository](../../../work/EFCoreLibrary/EfCore/Repository/Base/DeleteItemRepository.cs#L10): синхронные `Create/CreateRange`, `Update/UpdateRange`, `Delete/DeleteRange`; внутри `DbSet.Add/Update/Remove`, без `SaveChanges` и собственной транзакции.
- [GetItemByIdRepository.GetItemByIdAsync](../../../work/EFCoreLibrary/EfCore/Repository/Base/GetItemByIdRepository.cs#L13): `Task<TEntity?>`, по умолчанию `asNoTracking=false`, необязательная композиция `include` и `CancellationToken`. Требует `IEntity<TId>` с публичным `Id { get; set; }`; `TId` также реализует `IEquatable<TId>` и `IComparable<TId>`. Для закрытого доменного ID допустим уже существующий predicate-контракт внутри Infrastructure, без ослабления домена.
- [GetItemByPredicateRepository](../../../work/EFCoreLibrary/EfCore/Repository/Base/GetItemByPredicateRepository.cs#L12): `GetItemByPredicateAsync` возвращает первый элемент либо `null`; `GetItemsByPredicateAsync` — `Task<List<TEntity>>`, принимает predicate, skip, take, tracking, include и cancellation. Собственной сортировки нет; `take <= 0` не ограничивает выборку. Порядок истории нельзя выводить из порядка строк БД; доступный `include` имеет тип `Func<IQueryable<TEntity>, IQueryable<TEntity>>` и применяется до skip/take.
- [QueryRepository.Query](../../../work/EFCoreLibrary/EfCore/Repository/Base/QueryRepository.cs#L11) возвращает `IQueryable<TEntity>` с tracking по умолчанию. Это резерв для запросов, которым недостаточно базового чтения, а не обязательный путь для обычного CRUD.

## Контекст и регистрация

`EfDbContextAdapter<TContext, TContextKey>` связывает конкретный DbContext с ключом контекста. `IUnitOfWorkContext<TContextKey>` предоставляет `SaveChangesAsync`, `DatabaseFacade` и технические операции change tracker.

Регистрация строится на `AddEfCoreContext<TContext, TContextKey>()` и `AddEfCoreBaseRepositories<TContextKey>()`. Все репозитории одного сценарного scope используют один экземпляр контекста.

В [ServiceCollectionExtensions](../../../work/EFCoreLibrary/Extensions/ServiceCollectionExtensions.cs#L13) первая операция регистрирует только scoped `IAppDbContext<TContextKey>` → `EfDbContextAdapter<TContext,TContextKey>`; сам `TContext` и provider должно зарегистрировать приложение. Вторая использует `TryAddScoped` для открытых generic `IContext*Repository`, `IUnitOfWorkContext<>` и `IRepositoryContext<>`. Общий контекст обеспечивается корректной scoped-регистрацией одного ключа, а не созданием нового DbContext внутри каждого репозитория.

[IAppDbContext.SaveChanges](../../../work/EFCoreLibrary/Abstractions/Database/IAppDbContext.cs#L11) возвращает `Task<int>`; [UnitOfWorkContext.SaveChangesAsync](../../../work/EFCoreLibrary/EfCore/UnitOfWorkContext.cs#L12) делегирует ему. `Database`, `Entry`, `Attach`, `ClearChangeTracker` доступны через узкий UoW-контекст. Готового сценарного UoW AgentBridge, автоматического commit/rollback или защиты от параллельного использования DbContext библиотека этим не предоставляет. Встречающиеся в старых примерах имена `IGetItemByIdRepository<...,TContext>` не заменяют текущие `IContext*` и `TContextKey`.

Application получает узкие предметные порты AgentBridge. `IQueryable`, EF `Expression`, `Include`, tracking и типы контекста остаются в Infrastructure. Сигнатуры внешних базовых классов не копируются в Application механически.

## Чтение, изменение и удаление

- Чтение диалога и состояния адаптируется к базовым read-репозиториям.
- Создание сообщений и нового состояния контекста использует базовые create-репозитории.
- Обновление статуса обращения и активного состояния использует предусмотренные библиотекой операции изменения.
- Удаление истории и зависимых данных использует `Delete`/`DeleteRange` через сценарный UoW.
- Custom query не выбирается только ради сокращения кода, если подходят базовые операции.

Сетевое обращение к модели не удерживает открытую транзакцию БД. После него сценарий повторно проверяет актуальность диалога и сохраняет результат в короткой транзакционной границе.

SQLite и PostgreSQL используют собственные EF-провайдеры. Общие сценарии и базовые репозитории остаются одинаковыми; схема и миграции должны учитывать выбранный provider.

Очистка истёкших диалогов выполняется теми же базовыми read/delete-операциями. На этапе 05 в EFCoreLibrary отдельно реализованы `IDatabaseMaintenance<TKey>`, общий Relational coordinator и optional SQLite/PostgreSQL/SQL Server/MySQL модули. Основной CRUD-проект остаётся `0.0.4` с EF `10.0.3`; maintenance использует Relational `10.0.11`. Этап 08 согласует graph EF-адаптера на Microsoft EF `10.0.11` и Npgsql `10.0.3`, не подключая maintenance. Startup относится к этапу 12. API, SingleInitializer, scope backup и ограничения проверки: [обслуживание БД](06-database-maintenance.md).

## Реализация этапа 08

Статус: **реализован и принят; запрещённые проверки пропущены**. Локальный ProjectReference ведёт на `../../../work/EFCoreLibrary/EFCoreLibrary.csproj` из EF-адаптера. Ядро/Application не получают EF-ссылки. Restore подтверждает Microsoft.EntityFrameworkCore, Abstractions, Relational, Sqlite/Core и Analyzers `10.0.11`; Npgsql.EntityFrameworkCore.PostgreSQL `10.0.3` допускает EF `[10.0.4, 11.0.0)`. Тестовый DI обновлён на `10.0.11` из-за NU1605; это не изменение options/API ядра. Любая сборка с библиотекой требует `-p:GeneratePackageOnBuild=false`.

| Модель в AgentBridge.Persistence.EfCore.Models | Ключ и содержимое |
| --- | --- |
| `DialogRecord` | Id; OwnerId, IncarnationId/Revision, fixed CreatedAtUtc/ExpiresAtUtc, LastChangedAtUtc, ContentBytes |
| `DialogTurnRecord` | DialogId/Id; Sequence начала, Status, StartedAtUtc/FinishedAtUtc |
| `CanonicalItemRecord` | DialogId/TurnId/Sequence; полный ContentJson, включая function_call_output/call_id, reasoning и неизвестные поля |
| `ModelStepRecord` | DialogId/TurnId/Id; Sequence выполнения и отдельный полный Response |
| `DialogContextRecord` | DialogId/Version; ThroughTurnSequence, CreatedAtUtc и отдельный полный Compaction |
| `ModelResponseRecord` | Required complex-значение внутри шага/compact: FormatVersion, Status, OutputJson, EnvelopeJson, ContinuationJson, ErrorType/Message |

Persistence DTO изменяемы, но не являются Domain. Доменные setters/конструкторы не менялись; восстановление агрегата остаётся следующим этапам. `FromModelResponse`/`ToModelResponse` выполняют только преобразование формата в памяти, без CRUD. Все lifecycle сохраняют известный output; envelope и continuation не превращаются в items. Несовместимый FormatVersion или повреждённая форма отчёта отклоняются явно. Per-call ModelAccess/API keys отсутствуют в моделях/DI хранения; чувствительные JSON и ошибки не логируются этим кодом.

Composite FK item/step → DialogId/TurnId не позволяет связать данные с turn другого диалога. Turn ID не глобален; step ID локален обращению, поэтому будущий base predicate обязан включать родителей. Unique индексы DialogId/Sequence и DialogId/TurnId/Sequence поддерживают порядок; ключ items и ключ context поддерживают чтение окна/истории. Индекс ExpiresAtUtc/Id нужен ограниченной детерминированной выборке очистки. Индекса OwnerId нет: текущий reader читает по PK и проверяет владельца; не вводится дополнительный размерный предел неограниченного OwnerId. BINARY (SQLite) / C (PostgreSQL) сохраняют точное сравнение без trim/case folding.

Максимальная принятая Version по DialogId определяет активный compact; прежние версии и исходная история сохраняются. Check metadata допускает только Completed compact и неотрицательный ThroughTurnSequence. Непрерывность terminal prefix, неубывание покрытия и atomic guards требуют будущего сценария 09–10; текущий FK/check этого не доказывает и не отбрасывает items по metadata. Каскады диалог → turns/items/steps и диалог → contexts описывают весь набор удаления.

UTC DateTimeOffset хранится точными ticks через `UtcTicksConverter`, в INTEGER/bigint. Это обходит неподдержанную сортировку SQLite DateTimeOffset и округление PostgreSQL timestamp до микросекунд; сравнения expiry не теряют 100-нс границу. Ненулевое смещение запрещено. JSON сохраняется text, без jsonb-нормализации. AfterSaveBehavior.Throw защищает fixed owner/incarnation/created/expiry в EF; concurrency metadata включает incarnation/revision/owner/expiry. Ни создание новой incarnation, ни атомарность existence/owner/expiry/accessID/token не исполняются этим этапом.

Фактическая регистрация (значения подключения задаёт приложение):

```csharp
using AgentBridge.Persistence.EfCore.Configuration;

services.AddDatabaseConfiguration(options =>
{
    options.Provider = DatabaseProvider.PostgreSql; // либо DatabaseProvider.SQLite
    options.ConnectionString = applicationConnectionString;
});
services.AddAgentBridgePersistence();
```

`AddAgentBridgePersistence` регистрирует scoped `AgentBridgeDbContext`, точный `AddEfCoreContext<AgentBridgeDbContext, AgentBridgeContextKey>` и `AddEfCoreBaseRepositories<AgentBridgeContextKey>`. Ошибки обязательных options проверяются до выбора provider; fallback в SQLite отсутствует. SensitiveDataLogging выключен. Контекст не открывает БД при регистрации/разрешении metadata. На этапе 08 Application ports/UoW/migrations/startup не регистрировались; этап 09 добавляет только read ports и adapters, описанные ниже.

Проверены metadata обоих providers, составные ключи/FK/каскады/checks, UTC converter, сериализация полного payload и scoped DI: **34 passed / 0 failed / 0 skipped**, 25 новых тестов; production и test builds — 0 warnings/errors. Это не проверка relational enforcement, restart на БД или атомарности. БД, SQL, migrations, backup/restore, hosting и внешние процессы — **«Пропущено по указанию пользователя»**. OpenSpec CLI отсутствует в PATH, CLI validation не выполнялась. [Точные команды и файлы](<../Plans/AgentBridge Initial Implementation/08-persistence-models.md>).

## Адаптеры этапа 09

Статус: **Реализован и принят; запрещённые проверки пропущены**. Актуальные base API повторно сверены с исходниками EFCoreLibrary; соседняя библиотека не менялась. Domain не ослаблен; новые Application repository ports не добавлены.

| Тип в EF-адаптере | Фактическая операция |
| --- | --- |
| `Repositories.DialogRecordQueries` | Base ById для глобального dialog ID; base predicate + include OrderBy expiry/ID + take для кандидатов |
| `Repositories.TurnRecordQueries` | Predicate DialogId/turnId; вся история по Sequence начала |
| `Repositories.ItemRecordQueries` | Predicate DialogId/TurnId для одного обращения либо весь DialogId; порядок items по Sequence внутри turn |
| `Repositories.ModelStepRecordQueries` | Predicate DialogId/TurnId/stepId; полные отчёты по Sequence выполнения |
| `Repositories.ContextRecordQueries` | Активный max Version через descending include; все прежние версии по возрастанию |
| `Repositories.RecordStaging<TEntity>` | StageCreate/Update/Delete и Range делегируют base CRUD, возвращают void, без SaveChanges/transaction |
| `Reading.DialogReader : IDialogReader` | Owner ordinal, полный DTO истории и active compact, повторная primitive root-проверка |
| `Reading.ExpiredDialogReader : IExpiredDialogReader` | Положительно ограниченная выборка token кандидатов, включая точную границу expiry == now |

`AddAgentBridgePersistence` теперь регистрирует эти scoped adapters и только два Application read ports. ById без parent predicate не используется для локальных turn/step IDs. Сортировка выполняется через проверенный base include до take, поэтому `IContextQueryRepository` не требуется. Read adapters передают no-tracking/cancellation; root/turn Find допускают tracking для будущего Infrastructure write-сценария. Приложение не получает EF/query-типы через Application.

Перед чтением детей `DialogReader` проверяет owner и фиксирует primitive incarnation/revision/owner/metadata, после детей повторно читает root. Удаление/смена владельца/жизни/версии возвращает NotFound/Forbidden/Conflict без snapshot и без retry. Mutable record не подменяет исходную версию. История не отбрасывается по ThroughTurnSequence; JSON/report mapping сохраняет все lifecycle/output/envelope/continuation/error. Orphan items/steps и незавершённый принятый compact отклоняются явно. Максимальная Version определяет active независимо от CreatedAtUtc; прежние версии не изменяются.

Пример доступного чтения после существующей регистрации:

```csharp
IDialogReader reader = scope.ServiceProvider.GetRequiredService<IDialogReader>();
ServiceResult<DialogSnapshot> result = await reader.ReadAsync(
    new DialogAccess(DialogId.From(dialogGuid), DialogOwnerId.From(applicationUserId), nowUtc), cancellationToken);
```

Истёкший диалог до physical delete читается владельцем; это не разрешение продолжения. Повторная root-проверка не является транзакционным snapshot. `RecordStaging<TEntity>` предназначен только инфраструктуре сценария после Domain/guards, не возвращает готовый успех write port. Saving/transactions/atomic guards/Domain rehydration и реализации `IDialogCreator`/writers/deletions остаются этапу 10, не зарегистрированы. Cleanup orchestration остаётся этапу 22.

Проверки: **71 passed / 0 failed / 0 skipped**, 37 новых; production/test builds **0 warnings/errors**. Base fakes доказывают делегирование, фильтры/порядок, full snapshot mapping и управляемые delete/recreate/revision interleavings, но не provider translation, relational enforcement, atomicity или restart. **Пропущено по указанию пользователя:** все БД/SQL/реальные интеграции/hosting/migrations/backup/процессы. CLI validation не выполнена: OpenSpec отсутствует в PATH; change не архивирован. [Точные команды, изменённые файлы и ограничения](<../Plans/AgentBridge Initial Implementation/09-base-repository-adapters.md>).

## Источники

- [EFCoreLibrary.csproj](../../../work/EFCoreLibrary/EFCoreLibrary.csproj)
- [Регистрация](../../../work/EFCoreLibrary/Extensions/ServiceCollectionExtensions.cs)
- [Базовые репозитории](../../../work/EFCoreLibrary/EfCore/Repository/Base/)
- [IUnitOfWorkContext](../../../work/EFCoreLibrary/Abstractions/Database/IUnitOfWorkContext.cs)

Бизнес-сценарий: [хранение и удаление](<../Business logic/04-storage-and-retention.md>).
