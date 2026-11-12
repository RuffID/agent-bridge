# B03: ограниченная materialization истории

## Фактический public API

Ограниченная загрузка поддерживается отдельными PostgreSQL и SQL Server модулями. Исходные API reader/runner и per-operation limits сохранены; свежий binary kit и PostgreSQL runtime в этой задаче не проверены.

- `DialogHistoryLoadLimits(int maxRecords, long maxRecordBytes, int maxQueries)` в `AgentBridge.Application.Models`: положительные явные значения, immutable properties, проверка переполнения произведения. Рабочих defaults нет.
- `IBoundedDialogReader : IDialogReader` в `AgentBridge.Application.Ports`: `LoadLimits`; actual inherited `ReadAsync(DialogAccess, CancellationToken)` и `ReadCatalogAsync(DialogCatalogAccess, CancellationToken)` возвращают полный `ServiceResult<DialogSnapshot>` либо безопасный отказ.
- `AddAgentBridgePostgreSqlBoundedHistory(IServiceCollection, DialogHistoryLoadLimits)` в `AgentBridge.Persistence.PostgreSql`: общий scoped budget/query guard и SQL size provider для всех библиотечных read/write queries. Регистрация не выполняет I/O. Duplicate policy/size-provider registration — fail-fast.
- `AddAgentBridgeSqlServerBoundedHistory(IServiceCollection, DialogHistoryLoadLimits)` в `AgentBridge.Persistence.SqlServer`: тот же контракт с SQL Server size provider. Одновременно регистрировать две history policies в одном container нельзя.
- `AgentRunner.RunBoundedAsync(AgentRunRequest, Func<ModelStreamUpdate, CancellationToken, ValueTask>?, CancellationToken)`: actual shared orchestration требует bounded port/policy до history I/O/providers/model; контекст, executor, budget guard, lifecycle и короткие scopes остаются библиотечными.
- `DialogHistoryLoadLimitException` в `AgentBridge.Application.Results`: message `history_load_limit_exceeded`, `Error` = existing `ServiceErrorType.Rejected` с той же фиксированной message. `DialogHistoryLoadConflictException`: message `history_load_changed_during_query`.

`IDialogReader`, legacy `RunAsync` и прежние repository/reader/gate constructors сохранены. Без явной регистрации legacy путь не получает B03 guarantee. Совпадение marker interface недостаточно: actual `LoadLimits` legacy reader бросает `bounded_history_policy_required`. Strict runner не превращает custom persistence port в bounded implementation. Поддерживаются supplied PostgreSQL и SQL Server DI generations; custom readers и ручные legacy constructors вне этой границы не объявляются защищёнными.

## Выбранный подход и граница

Выбрано bounded чтение с отказом. Existing EFCoreLibrary `IContextQueryRepository<TEntity, AgentBridgeContextKey>.Query` позволяет поставить scalar projection, server predicate и Take до `ToListAsync`; full snapshot для preflight не нужен. Hard storage cap потребовал бы новый атомарный накопленный accounting для всех writer/provenance путей и миграцию existing roots. SQL-reader, DbContext bypass, копия runner или host history truncation не добавлены.

За один `PersistenceOperationGate` operation:

| Ограничение | Гарантия |
| --- | --- |
| MaxRecords = R | Суммарно не более R полных rows, включая повторные root/settings/catalog reads |
| MaxRecordBytes = B | Каждая полная строка удовлетворяет SQL accounting ≤ B |
| MaxQueries = Q | Не более Q query pairs: scalar preflight и bounded payload |
| MaxPayloadBytes | R × B, консервативные storage payload bytes |
| Scalar I/O | ≤ R+1 long values за query, всего ≤ Q × (R+1); canonical payload отсутствует |

Accounting PostgreSQL: `256 + 4 × SUM(length(variable text columns))`, null даёт ноль. PostgreSQL length считает Unicode-символы; каждый требует до четырёх байт UTF-8 или UTF-16. Trailing spaces учитываются. Expression builder включает все variable properties десяти таблиц и complex ModelResponseRecord; неизвестный reference column/type отклоняется. Это консервативная оценка payload, не точный CLR heap, физический размер БД или token budget.

Accounting SQL Server: `256 + 2 × SUM(DATALENGTH(variable columns))`, включая binary owner и complex reports; null даёт ноль. DATALENGTH сохраняет trailing spaces/Unicode; коэффициент 2 ограничивает UTF-8 для UTF-16 storage. SQL translation проверяется без подключения; фактическое выполнение queries на сервере требует отдельной проверки.

Preflight читает только size scalars с Take(remaining+1). Count/byte overflow отклоняется до payload этой query. Payload независимо содержит byte predicate и Take(remaining), поэтому изменение между queries не снимает bounds. Несовпадение числа rows даёт conflict; root/revision/incarnation/runtime и settings/catalog/profile/expiry rechecks не раскрывают изменившийся snapshot. Transaction guards/CAS/fencing сохраняются. Server scan/work/план/latency отдельно не доказаны; preflight не является SQL snapshot isolation.

Бюджет не сбрасывается между queries одной операции; lease освобождает его для следующего независимого сценария. Run состоит из нескольких ограниченных short operations и existing explicit model/tool/compact step limits; это не единый lifetime heap cap run. Scope нельзя использовать параллельно/вложенно, с app-owned tracked state или через прямые base repositories.

## Какие данные охвачены

Turns/settings snapshots, canonical items, model steps/output/envelope/continuation/errors/tool journals, active context и все версии contexts во внутренних state loaders; root/runtime, settings, profile/policy/metadata/feed clocks/changes и recovery operations/PairsJson/ResultJson. Internal Begin/Append/Start/Outcomes/Finalize/Renew/Fence/Recovery/Compact/Delete loaders используют те же query adapters/gate. Bound относится к чтению existing state; после большой новой записи следующий read/write может честно получить overflow.

История не обрезается по compact prefix. B03 recovery query читает все необходимые rows либо отказывает по явной policy; existing structural recovery limit4096 и pending format limit1024 не снимаются. Нарушение structural format остаётся ошибкой целостности, без продолжения с усечёнными pairs. Unresolved calls, repeated call_id, original reports/terminal и append-only resolutions сохраняются. Old TurnId не переисполняется; новая отправка требует Ready и нового explicit TurnId.

Owner проверяется до детей. Registered history требует exact owner/site/agent/profile/root/tombstone/retention до recovery/canonical load и повторно перед выдачей. B03 legacy read также отказывает по текущему retention до children; legacy без B03 сохраняет прежний metadata read expired root. Host авторизует актуальные права до **любой** catalogue/history/model/disclosure операции; library scope/profile не принимаются от browser/model.

## Подключение и ошибки

1. Host захватывает frozen generation и задаёт все три limits из проверенной configuration. Не назначать скрытые defaults и не увеличивать policy автоматически после отказа.
2. Зарегистрировать `AddAgentBridgePostgreSql()`, `AddAgentBridgePostgreSqlBoundedHistory(limits)` и existing `AddAgentBridgePersistence()` либо Integration facade; Database/options/ports/authorization остаются прежними. Policy регистрируется до построения provider. Не override supplied queries/gate/reader.
   Для SQL Server использовать `AddAgentBridgeSqlServer()` и `AddAgentBridgeSqlServerBoundedHistory(limits)` с `Database.Provider=SqlServer`.
3. History использует scoped `IBoundedDialogReader`, Send — actual `RunBoundedAsync`; async scopes/resources await-ятся. Compile-only пример — `tests/Delivery/Consumer/BoundedHistoryRegistration.cs`.
4. Reader overflow: Rejected + `history_load_limit_exceeded`, Data=null, canonical unchanged. Query race: Conflict + `history_load_changed_during_query`. Cancellation/provider/JSON/cleanup ошибки распространяются.
5. Internal writer overflow — typed exception после rollback/cleanup. Runner переводит только чистый typed overflow в Failed и пытается честную finalization. Если она тоже не может прочитать state под policy, TerminalSaved=false; Ready не выдумывается. Unknown/cleanup failures не маскируются overflow. Новый scope и actual lifecycle/recovery определяют durable состояние без replay.

Host выбирает фиксированный safe HTTP/stream outcome: raw library message/exception/canonical payload не выдаются пользователю. Новый чат только явно. Read отказ не изменяет state; retry/refill/replay/refresh CAS/implicit chat creation отсутствуют. Administrative смена policy требует отдельного решения host.

## Совместимость и поставка

Схемы PostgreSQL и SQL Server, migration IDs и public SQL Server API сохранены. Потребитель явно выбирает provider, соответствующую bounded registration и пересобирается со свежим комплектом. ServiceErrorType и stored response constraint0–8 не менялись. EF/Npgsql версии задают csproj и штатный manifest; свежие binary/runtime/provider/restart/live проверки выполняются отдельно.
