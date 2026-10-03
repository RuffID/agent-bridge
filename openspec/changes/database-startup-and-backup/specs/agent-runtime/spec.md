## ADDED Requirements

### Requirement: Явное подключение обслуживания AgentBridge

EF-адаптер AgentBridge MUST регистрировать scoped `IDatabaseMaintenance<AgentBridgeContextKey>` через общий coordinator EFCoreLibrary и выбирать только SQLite/PostgreSQL по DatabaseOptions. SingleInitializer MUST задаваться явно. Регистрация и разрешение сервисов MUST NOT открывать соединение, создавать файлы, запускать backup, migrations, процессы или фоновые задачи. Singleton gate MUST оставаться общим на root container и MUST NOT захватывать scoped context. Результаты, безопасные ошибки, отмена и диагностика стадий MUST сохранять контракт EFCoreLibrary без автоматического retry или fallback initialization.

#### Scenario: Подключение без обслуживания

- **WHEN** приложение регистрирует и разрешает maintenance сервис в отдельном scope
- **THEN** доступен API InspectAsync/UpdateExistingAsync/InitializeNewAsync выбранного provider
- **AND** никакая операция обслуживания не начинается до явного вызова приложения.

### Requirement: Явные настройки backup

AgentBridge MUST требовать абсолютный backup directory и явно заданный положительный срок хранения backup без значения по умолчанию. Для PostgreSQL MUST требоваться абсолютный путь pg_dump, явный major сервера 10+ и конечный положительный cleanup timeout. Настройки MUST проверяться локально до maintenance I/O без раскрытия значений в ошибках. Backup retention MUST оставаться обязанностью приложения и MUST NOT запускать purge или подменяться expiry диалогов. Format, scope, receipt, private workspace и защита от перезаписи MUST делегироваться EFCoreLibrary.

#### Scenario: Срок backup не выбран

- **WHEN** приложение не задало срок хранения backup
- **THEN** локальная валидация отклоняет настройки до обслуживания
- **AND** срок хранения диалогов не используется как запасное значение.
