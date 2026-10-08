# Вызываемая приложением очистка истёкших диалогов

Этап22 реализует `ExpiredDialogCleanup` в Application. Он координирует существующие `IExpiredDialogReader`/`IExpiredDialogDeletion`; EF-адаптер по-прежнему использует base predicate/read/delete EFCoreLibrary0.0.5 и `DialogDeletionUnitOfWork`. Схема и generated migrations не менялись.

## Подключение и вызов

После конфигурации выбранного SQLite/PostgreSQL и `AddAgentBridgePersistence()` приложение явно регистрирует:

```csharp
services.AddAgentBridgeDialogCleanup();
```

Регистрация сохраняет custom TimeProvider через TryAdd и не читает БД. Приложение авторизует системную очистку и задаёт расписание; AgentBridge не запускает hosted/background service. Отдельный вызывающий scope позволяет хранить LastResult до обработки исключения:

```csharp
await using AsyncServiceScope caller = root.CreateAsyncScope();
ExpiredDialogCleanup cleanup = caller.ServiceProvider.GetRequiredService<ExpiredDialogCleanup>();
ExpiredDialogCleanupResult result = await cleanup.CleanupAsync(100, cancellationToken);
// Приложение проверяет result.Status и индивидуальные result.Candidates.
```

`100` здесь — явный пример приложения, не default. Limit должен быть положительным. Один экземпляр отклоняет concurrent CleanupAsync; для каждого вызова приложение выделяет собственный caller scope. Неожиданные exceptions распространяются; после catch приложение может прочитать `cleanup.LastResult`, сохранить безопасные counts/status и передать исключение своей стандартной обработке. Raw exceptions в DTO отсутствуют.

## Граница пакета и времени

Один вызов выполняет одну ограниченную выборку по текущему cutoff CreatedAtUtc <= now-period в порядке CreatedAtUtc/Id. Null период даёт пустой пакет без чтения кандидатов. Scope чтения освобождается до deletion. Каждый кандидат получает новый scope и fresh UTC; deletion атомарно повторяет проверку текущей политики. Общей длинной transaction и внешнего I/O внутри UoW нет.

Истечение включает `nowUtc == ExpiresAtUtc`. Порт удаления повторно проверяет root incarnation/revision/expiry в Serializable transaction и каскадно удаляет Dialogs, DialogTurns, ModelSteps, CanonicalItems, DialogContexts и DialogSettings. Guard активного AgentRunner отказывает поздней записи без восстановления удалённого root или обновления stale token. Новая incarnation того же ID сохраняется.

Положительный RetentionPeriod действует на все даты создания; новая конфигурация применяется в новых scopes, отсутствие/null отключает expiry и автоматическое удаление. Активность, compact и выбор модели срок не продлевают. Legacy NOT NULL ExpiresAtUtc сохраняется для совместимости схемы и CAS; MaxValue кодирует новый бессрочный диалог, public nullable expiry вычисляется из текущей политики. ContentBytes не запускает очистку. Внутри пакета нет refresh/retry даже при Conflict/NotFound.

## Отчёт

`ExpiredDialogCleanupResult` содержит Status, Limit, CandidatesRead, immutable Candidates, DeletedCount и optional Error чтения. CandidatesRead различает успешный пустой snapshot и отсутствие snapshot. Токены — исходные кандидаты, а не права записи.

| Batch status | Значение |
| --- | --- |
| Completed | Все прочитанные кандидаты Deleted либо пакет пуст; другие истёкшие строки ещё могут существовать |
| Partial | Пакет обработан, хотя один или несколько delete вернули ожидаемый отказ |
| Failed | Reader вернул ожидаемый отказ; Error сохранён |
| Canceled | Наблюдалась caller cancellation; следующие операции остановлены |
| Interrupted | Неожиданное exception распространяется; LastResult сохраняет прогресс |

| Candidate status | Значение |
| --- | --- |
| NotAttempted | Delete port не вызывался |
| Deleted | Port подтвердил атомарное удаление |
| Failed | Port вернул semantic ServiceError; исходная error сохранена |
| Unknown | Port вызван, но acknowledgement не получен; commit/rollback не предполагается |

Например, три кандидата с итогом Deleted/Failed(Conflict)/Deleted дают Partial и DeletedCount2. Ожидаемый отказ не останавливает соседние кандидаты; unexpected exception останавливает пакет. Принятый результат записывается в отчёт до DisposeAsync, поэтому cleanup failure не стирает Deleted/Failed. Primary и Dispose errors сохраняются вместе. OCE освобождения scope не маскируется caller cancellation даже если caller token уже отменён. Сам ServiceResult read/delete success не подменяет итог всего вызова.

## Проверки и ограничения

19 новых isolated cases проверяют public DI/limits/time/scopes, partial/unknown/cancel, fail-fast port contract, primary+Dispose failures, read failure и concurrent call. Они не доказывают БД. Actual SQLite/PostgreSQL16 проверяют equality, ограниченность нескольких app calls, каскад шести таблиц, неожиданный отказ/отмену после настоящего SQL SaveChanges до commit, новые scopes/restart и delete-vs-active-run.

Stale-revision case использует исторический NOW до expiry как контролируемую принятую мутацию между read и delete. Это проверяет отказ старому token, а не реальную запись после истечения. Реальные тесты используют управляемую local gateway response, actual AgentRunner и offline BPE; HTTP/server не запускаются. SQLite/PostgreSQL evidence не распространяется на SQL Server/MySQL. Backup/restore maintenance не повторялся без нового риска.

Нормативный SSOT: [agent-runtime](../../openspec/specs/agent-runtime/spec.md), [контекст](../../openspec/specs/agent-runtime/context.md), [change22](../../openspec/changes/expired-dialog-cleanup/proposal.md). [Полный отчёт и команды22](<../Plans/AgentBridge Initial Implementation/22-expired-dialog-cleanup.md>).
