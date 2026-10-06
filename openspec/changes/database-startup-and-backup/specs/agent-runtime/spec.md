## ADDED Requirements

### Requirement: Явное подключение обслуживания AgentBridge

EF-адаптер AgentBridge MUST регистрировать scoped `IDatabaseMaintenance<AgentBridgeContextKey>` через общий coordinator EFCoreLibrary и выбирать только SQLite/PostgreSQL по DatabaseOptions. SingleInitializer MUST задаваться явно.

#### Scenario: Подключение без обслуживания

- **WHEN** приложение регистрирует и разрешает maintenance сервис в отдельном scope
- **THEN** доступен API InspectAsync/UpdateExistingAsync/InitializeNewAsync выбранного provider
- **AND** никакая операция обслуживания не начинается до явного вызова приложения.

#### Scenario: Проверка правила — Явное подключение обслуживания AgentBridge

- **WHEN** приложение выбрало SQLite или PostgreSQL и SingleInitializer
- **THEN** scoped maintenance зарегистрирован через общий coordinator.

### Requirement: Регистрация maintenance без побочных операций

Регистрация и разрешение сервисов MUST NOT открывать соединение, создавать файлы, запускать backup, migrations, процессы или фоновые задачи. Singleton gate MUST оставаться общим на root container и MUST NOT захватывать scoped context.

#### Scenario: Проверка правила — Регистрация maintenance без побочных операций

- **WHEN** root разрешает сервис обслуживания
- **THEN** соединения, backup и фоновые задачи не запускаются, общий gate не захватывает scoped context.

### Requirement: Единый контракт maintenance результатов

Результаты, безопасные ошибки, отмена и диагностика стадий MUST сохранять контракт EFCoreLibrary без автоматического retry или fallback initialization.

#### Scenario: Проверка правила — Единый контракт maintenance результатов

- **WHEN** coordinator возвращает безопасный отказ
- **THEN** адаптер сохраняет error/cancellation контракт без retry или fallback initialization.

### Requirement: Явные настройки backup

AgentBridge MUST требовать абсолютный backup directory и явно заданный положительный срок хранения backup без значения по умолчанию. Для PostgreSQL MUST требоваться абсолютный путь pg_dump, явный major сервера 10+ и конечный положительный cleanup timeout. Настройки MUST проверяться локально до maintenance I/O без раскрытия значений в ошибках.

#### Scenario: Срок backup не выбран

- **WHEN** приложение не задало срок хранения backup
- **THEN** локальная валидация отклоняет настройки до обслуживания
- **AND** срок хранения диалогов не используется как запасное значение.

#### Scenario: Проверка правила — Явные настройки backup

- **WHEN** backup retention не задан или pg_dump settings недопустимы
- **THEN** локальная проверка отклоняет настройки до I/O без раскрытия значений.

### Requirement: Владение backup retention и артефактом

Backup retention MUST оставаться обязанностью приложения и MUST NOT запускать purge или подменяться expiry диалогов. Format, scope, receipt, private workspace и защита от перезаписи MUST делегироваться EFCoreLibrary.

#### Scenario: Проверка правила — Владение backup retention и артефактом

- **WHEN** приложение завершило backup
- **THEN** retention остаётся приложению, receipt и защита артефакта делегируются EFCoreLibrary без автоматического purge.
