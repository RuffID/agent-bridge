# Диагностика

## Граница

- `AddAgentBridgeDiagnostics` подключает стандартный Microsoft.Extensions.Logging и одну singleton-службу с `ILogger<AgentBridgeDiagnostics>`. Serilog logger, провайдеры, уровни, обогащение, sinks и их жизненный цикл принадлежат приложению.
- Область только наблюдает операции. Она не запускает AgentRunner, HTTP, БД, таймеры, не создаёт linked tokens и не перехватывает или заменяет исключения сценариев.
- `BeginOperation` принимает закрытый enum операции, непустой GUID корреляции и два отдельных исходных токена. Имена будущих операций не означают реализации соответствующих сценариев.
- Владелец явно вызывает `Complete` или `Fail` один раз. Автоматическое успешное завершение через Dispose запрещено. Длительность измеряется монотонно; связанные операции имеют общий CorrelationId и разные OperationId.

## Безопасность и отмена

- Итоговое событие содержит только Operation, OperationId, CorrelationId, Status, ErrorCode и DurationMs. Нельзя добавлять произвольные строки, URL, options, сообщения диалогов или объекты исключений.
- Не читать и не выводить `Exception.Message`, `ToString`, Data, InnerException и имена пользовательских типов. `Fail` использует только проверку типа OperationCanceledException.
- Caller cancellation классифицируется по исходному caller-токену и имеет приоритет над локальным deadline. Оба токена должны быть раздельными, не linked. Неатрибутированный OperationCanceledException остаётся ошибкой, не выдуманным таймаутом; обычная ошибка не становится отменой только из-за токена.
- Безопасность относится к данным самой библиотеки. Приложение отвечает за свои ambient scopes, enrichers и события других компонентов, включая будущую интеграцию HttpClientLibrary.
- Проверки — публичная DI/diagnostics-граница в `tests/AgentBridge.Tests`, собирающий ILogger и Serilog provider приложения с in-memory sink; без host и файлов.
