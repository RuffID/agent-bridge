# Конфигурация ядра

- AddAgentBridgeTools явно подключает singleton registry/executor через TryAdd без исполнения. AddAgentBridgeTool фиксирует полный definition, отклоняет duplicate exact name и регистрирует handler/обязательный validator scoped. Executor создаёт отдельный async scope invocation; не подменять scoped business state singleton. Выбор имён и immutable ToolExecutionLimits задаёт приложение для session; MaxSteps берётся из существующего AgentOptions, новых options/defaults нет.

- AddAgentBridgeCompaction регистрирует scoped ContextCompactor и default TimeProvider.System через TryAdd. ContextBuilder с explicit ordered providers, counter/gateway/writer/options предоставляет приложение. Регистрация не выполняет compact и не расширяет permissions источников. Новых options нет: ContextCompactionOptions.MaxPasses и CodexLbOptions.CompactTimeout уже существовали.

- Здесь находятся options агента, полной истории и рабочего контекста, а также их групповая регистрация. Приложение выбирает источник конфигурации; `IConfiguration` используется только при binding в расширении composition root.
- Период хранения применяется к времени создания будущего диалога через `CalculateExpiresAtUtc`; область не создаёт доменные сущности и не переносит уже сохранённые сроки.
- Валидация защищает локальные диапазоны при получении options и через стандартный `IStartupValidator`. Проверка модельного бюджета, доступности модели и effort принадлежит каталогу адаптера, не статическому списку ядра.
- Источники контекста и обработчики инструментов регистрируются программно на соответствующих этапах; их имена не являются списком прав в options.
- Options — входные настройки приложения, не безопасный снимок для UI и не данные для журнала. Сервис безопасных settings реализуется отдельно.
- Публичная граница проверки — DI-регистрация и получение `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>` в `tests/AgentBridge.Tests`, без hosting и интеграций.
- AddAgentBridgeTokenization явно регистрирует singleton IContextTokenCounter и transient ContextBudgetGuard через TryAdd, сохраняя выбор приложения. DI не токенизирует запрос и не выполняет I/O; новые options не вводятся, guard принимает ModelSettingsSnapshot с проверенными threshold/reserve из существующего каталога.
