# Полный ход агента

Этап20 реализует `Application.AgentRunner` и `Configuration.AddAgentBridgeRunner`. [Нормативные требования](../../openspec/specs/agent-runtime/spec.md), [обоснование](../../openspec/changes/agent-turn-orchestration/context.md), [проверки и полный manifest](<../Plans/AgentBridge Initial Implementation/20-agent-turn-orchestration.md>).

## Подключение и public API

Приложение подключает existing configuration, persistence, model adapter, offline tokenization и инструменты. Оно регистрирует scoped `ContextBuilder` с явно выбранными ordered providers и вызывает `AddAgentBridgeRunner()`. Эти регистрации не запускают операций. Scoped `AgentRunner` получает `IServiceScopeFactory`: каждый read/write открывает отдельный короткий async persistence scope, который завершается до вызова модели или handler. Runner управляет только создаваемыми им persistence scopes; lifetime произвольных providers/handlers приложения определяет приложение. Авторизация выбранного AgentId принадлежит приложению; обязательный tool validator проверяет текущие права и полную schema.

```csharp
services.AddScoped<ContextBuilder>(provider => new ContextBuilder(
    provider.GetServices<IContextProvider>())); // Только выбранные приложением ordered providers.
services.AddAgentBridgeRunner();

AgentRunRequest request = new(call, newInput, selectedToolNames,
    new ToolExecutionLimits(8, 16, 2, TimeSpan.FromMinutes(1)),
    model: selectedModel, effort: selectedEffort, instructions: agentInstructions);
AgentRunResult result = await runner.RunAsync(request, onUpdate, cancellationToken);
```

`call` — `ApplicationCallContext`; `newInput` содержит только новые canonical items, `selectedToolNames` — exact registry names. `AgentOptions.MaxToolSteps` дополнительно ограничивает request limits. Null callback сохраняет JSON путь existing gateway, non-null — SSE с awaited updates. Envelope/continuation сохраняются в полных reports, но не попадают в input. Runner строит полный input и не использует server continuation/compact id для generation.

`AgentRunResult.Status` отличает Completed/Failed/Canceled/Incomplete/Interrupted. `TerminalSaved` подтверждает terminal запись этого run; `Token` — только исходный/успешно записанный token, не permission. `Turn` содержит подтверждённые items/steps/journal. `LastResponse` и `LastTools` сохраняют полученные reports, в том числе при ожидаемом отказе их записи. Не логировать содержимое этих полей.

## Неизменяемый снимок run

Owner/dialog/incarnation/revision/expiry проверяются в начале и атомарно при каждом write. `AgentOptions` instructions/MaxToolSteps и `ContextCompactionOptions.MaxPasses` фиксируются до I/O. `ModelAccess` resolves один раз; `IModelSettingsReader.ReadWithAccessAsync` проверяет каталог тем же экземпляром доступа, что generation/compact. Actual codex-lb reader реализует новый метод. Старый user reader, реализующий только `ReadAsync`, получает Unsupported в run без fallback; существующий standalone ReadAsync сохраняется.

Модель, effort, threshold/reserve, инструкции, tools и их limits не перечитываются между шагами. ModelAccess не сохраняется. Providers вызываются один раз с actual call и исходным new input; их вклад фиксируется на весь model/tool/compact loop. После Begin new input уже находится в InProgress turn и не добавляется повторно. Перед каждой generation используются existing ContextCompactor и отдельный полный ContextBudgetGuard. Compact failure не маскируется продолжением generation; UnknownBudget/opaque не разрешает отправку без полной оценки.

## Durable журнал и restart

`StoredModelStep.ToolAttempts` — явные `StoredToolAttempt` с OutputIndex/AgentId/ToolAttemptState. EF сохраняет их отдельной nullable text-колонкой `ModelSteps.ToolAttemptsJson`, format version1, без скрытых полей в canonical output или metadata. Parent-aware keys задают dialog/turn/step, root — owner/incarnation. JSON state Started/Succeeded/Rejected/Unknown/NotStarted не хранит секрет доступа или аргументы повторно. Байты журнала входят в ContentBytes.

`IDialogToolAttemptWriter.StartAsync` проверяет root guards, InProgress turn, Completed stored step и исходный function_call. Повтор позиции запрещён. Start commit/cleanup завершаются до handler. Параллельные checkpoints одного run сериализуют short writes и token updates; DbContext/scopes между workers не разделяются. `SaveOutcomesAsync` атомарно меняет состояния попыток и добавляет только confirmed outputs, не выдумывает результат для Unknown. Все операции идут через base CRUD EFCoreLibrary0.0.5 и сценарный UoW.

Например, Started для stepA/position2/call(x) после crash запрещает повтор действия. Completed новая пара call(x) в stepB/position1 допустима. Legacy null означает отсутствие журнала, **не разрешение replay**. Run с уже существующим TurnId всегда возвращает Interrupted/Conflict до settings/providers/model/handler и отдаёт сохранённое состояние. Runner не возобновляет ни turn, ни model step автоматически. Новый turn не проходит ContextBuilder при известном незакрытом call, включая partial arguments. История не удаляется ради продолжения, fake output не создаётся.

## Partial, cancellation и ошибки

Полный model report/calls сохраняется до tools. После exception/cancel `ToolExecutionSession.LastResult` принимается только для текущего StepId; начатые workers/scopes сначала awaited. Подтверждённый соседний/late success сохраняется, Unknown остаётся без output. Неожиданные exceptions распространяются после попытки finalization; primary+partial-save+scope-cleanup ошибки сохраняются вместе. При gateway exception со stream callback сохраняются только реально полученные canonical Item updates как Incomplete/Canceled report; text delta не превращается в искусственный canonical output. Иначе доступен полный lifecycle-report gateway.

Finalization использует CancellationToken.None, fresh UTC и только последний подтверждённый token. Любой write refusal/exception/unknown commit блокирует дальнейшие writes: нет refresh, повторной загрузки для принятия stale результата или retry. Delete/cleanup/expiry не восстанавливают диалог. Успешный compact save обновляет token и окно в runner boundary до возврата в compactor; cancellation/exception следующего прохода сохраняет этот факт. Ошибка DisposeAsync scope после accepted save также сохраняет принятую проекцию и блокирует дальнейшие записи.

Поздняя отмена во время успешного terminal save может вернуть **Status=Canceled, TerminalSaved=true, Turn.Status=Completed**: отмена caller и уже принятый БД результат — разные факты. Terminal статус не переписывается второй записью.

## Схема и границы проверки

Согласованные новые `AddDurableToolAttempts` migrations SQLite/PostgreSQL добавляют только nullable ToolAttemptsJson; existing initial migrations не регенерировались. Down удаляет журнал, поэтому downgrade может потерять recovery сведения; canonical history остаётся, а существующий TurnId всё равно не replay. Нужны новые migrations DLL; runtime сам их не применяет.

Isolated public run проверяет реальные builder/compactor/guard/executor, но gateway/storage doubles не доказывают live HTTP или atomic persistence. Адресные SQLite/PostgreSQL tests используют actual EFCoreLibrary0.0.5 и real transactions. Test counters/gateways не доказывают tokenizer/server compatibility; actual BPE проверяется отдельными related tests. SQL Server/MySQL не проверены. OpenSpec CLI отсутствует, change не архивирован. [Settings/status API21](21-settings-and-dialog-status.md) и [cleanup22](22-expired-dialog-cleanup.md) реализованы; UI/расписание остаются обязанностью приложения. Пауза после20 историческая, актуальный checkpoint — в [карте00–25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>).
