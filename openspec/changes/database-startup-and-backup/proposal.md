# Явное обслуживание БД AgentBridge

Этап 12 подключает существующий relational coordinator EFCoreLibrary к AgentBridgeContextKey и выбранному SQLite/PostgreSQL provider. Приложение явно выбирает SingleInitializer и вызывает InspectAsync, UpdateExistingAsync либо InitializeNewAsync в отдельном scope. Регистрация и разрешение сервисов не запускают обслуживание.

Добавляются локально валидируемые настройки backup с обязательным явным сроком хранения без default. Retention и восстановление принадлежат приложению; библиотека не удаляет копии. Production maintenance/backup/migration операции делегируются EFCoreLibrary. Domain/Application, generated migrations и соседние библиотеки не меняются.
