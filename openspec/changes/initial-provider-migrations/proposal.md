# Первые provider-specific миграции

Этап 11 подготавливает отдельные сборки миграций SQLite/PostgreSQL для общего AgentBridgeDbContext. Контекст и persistence DTO сохраняют принятые границы; Domain/Application и соседние библиотеки не изменяются.

Design-time создание не использует приложение, DI host, реальные credentials или соединение. После явного разрешения пользователя выполнены ровно две согласованные команды dotnet-ef 10.0.11 с --no-build: SQLite, затем PostgreSQL. InitialAgentBridgeSchema, designer и snapshot обоих providers созданы и проверены через static review/relational metadata, без применения схемы. Этап реализован и принят; запрещённые проверки пропущены.
