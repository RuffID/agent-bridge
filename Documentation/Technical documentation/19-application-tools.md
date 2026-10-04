# Инструменты приложения — этап19

Источник проверяемых правил: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). [Контекст и граница durable recovery](../../openspec/changes/application-tools/context.md).

## Подключение и обязанности приложения

`AddAgentBridgeTools()` явно подключает singleton `IToolRegistry`/`IToolExecutor` и default `TimeProvider.System` через TryAdd. `AddAgentBridgeTool<THandler,TValidator>(ModelToolDefinition)` регистрирует полный независимый snapshot exact name/description/schema/strict и **scoped** типы приложения. Повторное exact имя отклоняется. Нет host, discovery, операций при регистрации или options новых сервисов. `AgentOptions.MaxToolSteps` приложение передаёт в `ToolExecutionLimits.MaxSteps` при создании сессии.

`IToolInvocationValidator.ValidateAsync(definition, invocation, ct)` обязателен: приложение проверяет **всю schema**, ограничения бизнес-данных, текущего owner и agent. Executor проверяет canonical shape и complete JSON object, но не реализует общий JSON Schema engine. Регистрация/selected names не являются доказательством прав. Validator не выполняет побочных действий. `IToolHandler.Definition` должен совпадать с регистрацией до действия; `ExecuteAsync` продолжает самостоятельно защищать бизнес-операцию. Каждый invocation создаёт отдельный async DI scope для handler/validator и их scoped сервисов. Нет общей EF-сессии нескольких handler tasks; нельзя регистрировать зависимый DbContext singleton.

Все прикладные DB/HTTP действия handler остаются ответственностью приложения и должны использовать принятые EFCoreLibrary/UoW и HttpClientLibrary. Executor не открывает БД, сеть или write transaction AgentBridge.

## Фактический API

```csharp
// definition, GetOrderStatusHandler и GetOrderStatusValidator предоставляет приложение.
services.AddAgentBridgeTool<GetOrderStatusHandler, GetOrderStatusValidator>(definition);
IToolExecutor executor = serviceProvider.GetRequiredService<IToolExecutor>();
ToolExecutionLimits limits = new(agentOptions.MaxToolSteps, maxCallsPerStep: 16,
    maxConcurrency: 2, timeout: TimeSpan.FromSeconds(30));
ToolExecutionSession session = executor.CreateSession(call, snapshot.Token,
    snapshot.ExpiresAtUtc, [definition.Name], limits);
ServiceResult<ToolExecutionBatch> report = await executor.ExecuteAsync(session, storedModelStep, cancellationToken);
```

Числа в примере выбирает приложение; новых глобальных defaults нет. `storedModelStep` — actual `StoredModelStep(Guid stepId, ModelResponse response)`, а не gateway envelope или потоковый fragment. `CreateSession` также принимает optional `IToolExecutionCheckpoint checkpoint`; без него durable начала нет.

`ExecuteAsync` возвращает ожидаемый preflight отказ через `ServiceResult.Fail` (malformed/non-Completed response, занятая/остановленная session, bounds/time). Успех `ServiceResult` означает получение отчёта; проверяются `ToolExecutionBatch.Results`, `Outputs`, `Error`, `CanContinue`. Ни CanContinue, ни наличие tool output не заменяет full `ContextBudgetGuard` перед generation.

## Идентичность, шаги и результаты

Session фиксирует ApplicationCallContext (owner/dialog/turn/agent), incarnation исходного DialogWriteToken, выбранные exact names, expiry и limits. Token не является авторизацией. `ToolExecutionIdentity` добавляет `StepId` и `OutputIndex` во всём canonical output. call_id связывает результат с call, но не является ключом дедупликации. FIFO pairing допускает repeated IDs и `call(x), call(x), output(x), output(x)`; закрытые calls не выполняются, pending calls одинакового ID получают разные позиции. Исходные calls/opaque/envelope/continuation не изменяются.

MaxSteps считает модельные шаги с pending calls; MaxCallsPerStep проверяется до действий. Один StepId с действиями не запускается повторно в той же session. Execute одной session не выполняется одновременно. MaxConcurrency ограничивает scopes, результаты возвращаются в исходном порядке. Общий monotonic timeout начинается с создания session, включая промежутки между шагами; UTC expiry проверяется до действия и после validator/checkpoint. Шаги срок не продлевают.

Статусы попытки:

| Status | Смысл | Canonical output |
| --- | --- | --- |
| Succeeded | Получен полный подтверждённый ToolOutput | function_call_output, исходный call_id, output = JSON string полного Content |
| Rejected | Unknown/not-selected/validator refusal либо подтверждённый handler Fail | Явный JSON error внутри output; только safe semantic type/message |
| Unknown | Handler начат, результат не подтверждён: exception/cancellation/Timeout | Отсутствует; не выдумывать результат |
| NotStarted | Действие не начато, например остановка/checkpoint failure | Отсутствует |

Handler `ServiceResult.Fail` означает подтверждённый отказ. Для uncertain side effects приложение должно бросить исключение либо вернуть Timeout, а не выдать обычный Fail как подтверждённый outcome. Неожиданные exceptions распространяются после ожидания workers; `session.LastResult` хранит ordered частичный отчёт. Primary+Dispose failure возвращаются вместе в AggregateException. Ошибка cleanup не удаляет уже подтверждённый output. Caller cancellation сохраняет исходный caller token; поздний подтверждённый результат остаётся в LastResult, session остановлена. Raw validator/handler error message не попадает в report/canonical error. Args и успешные outputs чувствительны; не логировать report.

Timeout — cooperative cancellation, не принудительное убийство handler. Все tasks/scopes ожидаются. Игнорирующий cancellation handler может превысить срок; если он позже вернёт достоверный output, тот сохраняется в остановленном отчёте.

## Сохранение и передача этапу20

В19 executor не пишет историю. Приложение сохраняет canonical calls/step и `batch.Outputs` через короткий `IDialogTurnWriter.AppendAsync`, принимая successful-write token и свежий UTC без refresh/retry. Outputs не заменяют полный model response, envelope/continuation не становятся input. Проверка с GetOrderStatus подтверждает actual ContextBuilder с ранее сохранённой canonical парой и следующим вопросом «Когда доставят?», а также отказ для чужого owner. Snapshot fixture не доказывает реальную запись или atomicity БД.

Пользователь явно согласовал **durable recovery в этапе20**. Идентичности19 можно использовать для журнала. `IToolExecutionCheckpoint.BeforeExecuteAsync(identity, invocation, ct)` awaited после validator и до handler; failure/exception/cancellation запрещает действие и останавливает session без fake output/retry. Порт может вызываться параллельно: реализация20 обязана выделять scope/UoW на вызов и сериализовать короткие записи/обновление version token одного диалога. Transaction должна завершиться до возврата callback; нельзя держать её во время handler. Unknown save outcome не позволяет повторять checkpoint/dействие со свежим token. Finish journaling, восстановление после restart и сохранение canonical outputs координирует будущий AgentRunner20. При восстановленном «начато, исход неизвестен» действие автоматически не повторяется. Null checkpoint/session-memory/new session не являются restart protection.

## Проверки и ограничения

Этап20 уже реализует [AgentRunner и durable journal](20-agent-turn-orchestration.md), отдельные Start/outcomes UoW и restart refusal. Ниже границы isolated19 сохранены как исторический отчёт; actual SQLite/PostgreSQL evidence20 находится в его плане. Standalone executor/null checkpoint по-прежнему не защищают restart.

[Команды, TRX и manifest19](<../Plans/AgentBridge Initial Implementation/19-application-tools.md>). Public DI/registry/executor, actual ContextBuilder и affected tokenizer/guard проверяются изолированно. ScopedState — test double, не настоящий DbContext; real persistence/checkpoint recovery и бизнес-authorizer приложения не проверены. Production persistence/transport, migrations и соседние библиотеки не изменялись. Пропущено по указанию пользователя: live HTTP/codex-lb/OpenAI/Telegram, hosting/application/demo и произвольные scripts. OpenSpec CLI отсутствует; CLI validation не выполнена, change не архивирован. Текущие [runner20](20-agent-turn-orchestration.md), [settings21](21-settings-and-dialog-status.md), [cleanup22](22-expired-dialog-cleanup.md) и [поставка/руководство24–25](25-usage-guide.md) реализованы. Их отдельное evidence не расширяет изолированный набор19.
