# Провайдер PostgreSql

- Модуль зависит от общего AgentBridge.Persistence.EfCore, EF provider 10.0.3 и существующего EFCoreLibrary.Maintenance.PostgreSql. Другие провайдеры и migrations assemblies не подключать.
- AddAgentBridgePostgreSql явно и идемпотентно регистрирует модуль до AddAgentBridge/AddAgentBridgePersistence. Общая конфигурация выбирает провайдера; отсутствие или неоднозначность отклоняется до операций.
- Configure задаёт existing migrations assembly и __AgentBridgeMigrationsHistory. CreateMaintenance использует библиотечный provider в текущем scope; не выполняет backup/migrations/SQL и не владеет orchestration.
- Общий DbContext, модели, repositories и UoW остаются в общем адаптере. Схема и generated migrations не изменяются.
- Проверки — isolated persistence tests и compile-check конкретных проектов с GeneratePackageOnBuild=false. Не открывать БД/HTTP, не запускать приложение/Docker/tooling.

- PostgreSQL — единственный поддерживаемый модуль AgentBridge. AddAgentBridgePostgreSqlBoundedHistory регистрирует opt-in policy с общим gate; PostgreSqlHistoryRecordSizeProvider использует 256 + 4 × сумму length nullable text полей, без изменения модели. Trailing spaces сохраняются; это консервативный payload bound, не CLR heap или размер БД. Translation проверяется через ToQueryString без SQL execution.
