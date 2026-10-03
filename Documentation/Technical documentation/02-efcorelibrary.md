# Доступ к данным через EFCoreLibrary

## Обязательное решение

Вся работа с БД AgentBridge строится на EFCoreLibrary. Исходники: `D:\Media\User\source\repos\work\EFCoreLibrary`.

Приоритет — готовые базовые операции чтения, создания, изменения и удаления. Custom query используется при недостаточности базовых операций и остаётся внутри той же инфраструктуры. Прямой EF/SQL или собственный параллельный набор репозиториев для обхода библиотеки не применяется.

Если контракт библиотеки недостаточен или противоречит потребностям, вопрос сначала обсуждается с пользователем. Библиотека развивается, поэтому старые примеры не заменяют проверку текущих исходников.

## Проверенные типы библиотеки

Проверено статически 2026-10-03. `EFCoreLibrary.csproj` использует `net10.0` и EF Core `10.0.3`.

| Операция | Infrastructure-контракт | Базовый класс |
| --- | --- | --- |
| Чтение по ID | `IContextGetItemByIdRepository<TEntity, TId, TContextKey>` | `GetItemByIdRepository<TEntity, TId, TContextKey>` |
| Чтение по условию | `IContextGetItemByPredicateRepository<TEntity, TContextKey>` | `GetItemByPredicateRepository<TEntity, TContextKey>` |
| Создание | `IContextCreateItemRepository<TEntity, TContextKey>` | `CreateItemRepository<TEntity, TContextKey>` |
| Изменение | `IContextUpdateItemRepository<TEntity, TContextKey>` | `UpdateItemRepository<TEntity, TContextKey>` |
| Удаление | `IContextDeleteItemRepository<TEntity, TContextKey>` | `DeleteItemRepository<TEntity, TContextKey>` |
| Custom query | `IContextQueryRepository<TEntity, TContextKey>` | `QueryRepository<TEntity, TContextKey>` |

Базовые read-контракты поддерживают predicate, tracking-настройку и композицию запроса. CRUD-классы изменяют общий change tracker; сохранение выполняется отдельно.

## Контекст и регистрация

`EfDbContextAdapter<TContext, TContextKey>` связывает конкретный DbContext с ключом контекста. `IUnitOfWorkContext<TContextKey>` предоставляет `SaveChangesAsync`, `DatabaseFacade` и технические операции change tracker.

Регистрация строится на `AddEfCoreContext<TContext, TContextKey>()` и `AddEfCoreBaseRepositories<TContextKey>()`. Все репозитории одного сценарного scope используют один экземпляр контекста.

Application получает узкие предметные порты AgentBridge. `IQueryable`, EF `Expression`, `Include`, tracking и типы контекста остаются в Infrastructure. Сигнатуры внешних базовых классов не копируются в Application механически.

## Чтение, изменение и удаление

- Чтение диалога и состояния адаптируется к базовым read-репозиториям.
- Создание сообщений и нового состояния контекста использует базовые create-репозитории.
- Обновление статуса обращения и активного состояния использует предусмотренные библиотекой операции изменения.
- Удаление истории и зависимых данных использует `Delete`/`DeleteRange` через сценарный UoW.
- Custom query не выбирается только ради сокращения кода, если подходят базовые операции.

Сетевое обращение к модели не удерживает открытую транзакцию БД. После него сценарий повторно проверяет актуальность диалога и сохраняет результат в короткой транзакционной границе.

SQLite и PostgreSQL используют собственные EF-провайдеры. Общие сценарии и базовые репозитории остаются одинаковыми; схема и миграции должны учитывать выбранный provider.

Очистка истёкших диалогов выполняется теми же базовыми read/delete-операциями. Check/backup/migrate использует библиотечный adapter и развиваемый контракт обслуживания EFCoreLibrary. Порядок и анализ AquaByte-Ledger: [обслуживание БД](06-database-maintenance.md).

## Источники

- [EFCoreLibrary.csproj](../../../work/EFCoreLibrary/EFCoreLibrary.csproj)
- [Регистрация](../../../work/EFCoreLibrary/Extensions/ServiceCollectionExtensions.cs)
- [Базовые репозитории](../../../work/EFCoreLibrary/EfCore/Repository/Base/)
- [IUnitOfWorkContext](../../../work/EFCoreLibrary/Abstractions/Database/IUnitOfWorkContext.cs)

Бизнес-сценарий: [хранение и удаление](<../Business logic/04-storage-and-retention.md>).
