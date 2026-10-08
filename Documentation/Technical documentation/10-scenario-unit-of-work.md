# Сценарные Unit of Work

Статус: **Реализован и принят; запрещённые проверки пропущены**. Требования: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). [Результаты и точные команды этапа 10](<../Plans/AgentBridge Initial Implementation/10-scenario-unit-of-work.md>).

## Фактические границы

`AddAgentBridgePersistence` регистрирует один scoped контекст через EFCoreLibrary и следующие реализации существующих Application ports:

| Реализация в AgentBridge.Persistence.EfCore.UnitOfWork | Порты и операция |
| --- | --- |
| `DialogCreationUnitOfWork` | `IDialogCreator.CreateAsync`: новый incarnation, фиксированные даты, revision 0 |
| `DialogTurnUnitOfWork` | `IDialogTurnWriter.BeginAsync/AppendAsync/FinishAsync`: input, новые items, полные отчёты и статус обращения |
| `DialogContextUnitOfWork` | `IDialogContextWriter.SaveAsync`: Completed compact, следующая версия и вся прежняя история |
| `DialogDeletionUnitOfWork` | `IDialogDeletion` и `IExpiredDialogDeletion`: один root и FK-каскад зависимых строк |

Сценарные порты уже определяют минимальные операции; дополнительный глобальный Application UoW и контейнер всех репозиториев не добавлены. Каждая реализация делегирует техническую границу `UnitOfWorkScope`. `IDialogReader` и `IExpiredDialogReader` остаются независимыми от него.

`EfUnitOfWorkSession` использует текущий `IUnitOfWorkContext<AgentBridgeContextKey>`: `DatabaseFacade.BeginTransactionAsync(Serializable)`, `SaveChangesAsync` и `ClearChangeTracker`. Base CRUD остаётся в EFCoreLibrary. Внутри одного DI scope все сценарии имеют одну session, context и `PersistenceOperationGate`. Параллельные/вложенные операции отклоняются сразу. Ambient, enlisted и существующие transactions, а также автоматическая retry strategy запрещены. Приложение не должно выполнять произвольный staging или использовать инфраструктурный контекст между вызовами ports.

## Запись и ошибки

После begin tracking read root фиксирует исходные EF concurrency values. Проверяются access/token ID, существование, ordinal owner, incarnation/revision и срок текущей scoped политики от CreatedAtUtc. При null срока истечения нет. Явное удаление допускает истёкший диалог; системное удаление одного кандидата повторно проверяет истечение (включая равенство) и token, при отключённом сроке отказывает. Время передаётся приложением для текущей операции; скрытых часов и продления срока активностью нет.

`DialogStateLoader` читает всю историю turns/contexts базовыми запросами. `Dialog.Restore` валидирует revision, LastChangedAtUtc, последовательности, статусы/времена и каждую версию terminal prefix без фиктивных mutations. `TryAppendTurn` повышает revision выполняющегося обращения. Доменный snapshot остаётся локальным экземпляру; его получение после Restore не заменяет проверку исходного persisted token.

Подготовленный пакет items/reports добавляется вместе с root revision update. Идентичности step проверяются внутри родительского turn. Уникальные последовательности продолжаются без дыр; прошлые данные не перезаписываются. Full output/envelope/continuation/error остаются отдельными от input-items. `ContentBytes` увеличивается на UTF-8 фактически сохранённых текстовых колонок (ContentJson, OutputJson, EnvelopeJson, ContinuationJson, ErrorMessage). Отдельно сохраняемые копии учитываются отдельно; числовые метаданные и overhead БД не входят в размер. Мягкий лимит здесь не блокирует запись; предупреждение доступно в [DialogStatus21](21-settings-and-dialog-status.md).

Все изменения сохраняются одной transaction. Только root concurrency exception на save даёт ожидаемый Conflict; создание отдельно распознаёт точное нарушение primary key. PostgreSQL serialization failure, SQLite busy и неизвестные driver/commit/cleanup ошибки не превращаются в Conflict. Повторов нет. При отказе или исключении до commit выполняются rollback/dispose/clear, включая отменённый caller token: обязательный cleanup использует CancellationToken.None. Commit failure может означать уже состоявшуюся запись, поэтому success не возвращается и scope блокируется. Ошибки rollback/dispose/clear также блокируют scope; несколько исключений сохраняются в AggregateException. Begin failure блокирует gate, но не очищает чужую transaction/tracker.

## Использование после внешнего ожидания

Модель и инструмент вызываются вне write port. Приложение может уничтожить предыдущий DI scope до внешнего ожидания и открыть новый для следующей операции. Исходный token результата сохраняется без обновления при конфликте:

```csharp
using AgentBridge.Application.Ports;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using Microsoft.Extensions.DependencyInjection;

// applicationServices, access, originalToken, turnId, newItems и modelSteps уже подготовлены приложением.
using IServiceScope saveScope = applicationServices.CreateScope();
IDialogTurnWriter writer = saveScope.ServiceProvider.GetRequiredService<IDialogTurnWriter>();
ServiceResult<DialogWriteToken> result = await writer.AppendAsync(access, originalToken, turnId, newItems, modelSteps, cancellationToken);
// Успех содержит следующий token. Conflict не разрешает переписать old result со свежим token.
```

Technical callbacks доступны только в Infrastructure и используются собственными сценариями. Application ports не принимают делегат сети, инструмента или произвольной работы в transaction. Отдельный scoped gate не является распределённой блокировкой; межконтекстная защита опирается на provider transaction и root concurrency predicates.

## Проверка и ограничения

Expiry возвращает существующий `ServiceErrorType.Expired` до stale revision, согласно принятому контракту этапа 07. После финальной сверки этого mapping повторены только 6 guard-тестов (6/6); полный набор без причины повторно не запускался.

Прошли 107 тестов ядра (14 новых) и 108 persistence-тестов (37 новых); runner: 0 failed / 0 skipped. Fake committed/staged copies проверяют последовательности и partial failures, stale/concurrent root changes, reincarnation, payload и prefix. Actual `UpdateItemRepository` и настоящий EF tracker проверены без connection/SaveChanges: original guard values сохранены, новая revision current, immutable properties не modified.

Evidence10 выше не доказывает actual relational atomicity, locking, SQL translation, FK-каскады или restart. Последующие actual SQLite/PostgreSQL проверки отдельно перечислены в [карте00–25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>); они не расширяют старый isolated набор. На этапе10 реальные БД, SQL, integrations, hosting и процессы **пропущены по указанию пользователя**. Migrations/startup и batch cleanup не реализованы этим этапом. OpenSpec CLI validation не выполнена; change не архивирован.
