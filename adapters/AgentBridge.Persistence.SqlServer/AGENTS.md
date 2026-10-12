# Провайдер SqlServer

- AddAgentBridgeSqlServerBoundedHistory принимает обязательные DialogHistoryLoadLimits и регистрирует frozen singleton policy/size provider, scoped budget/guard и strict IBoundedDialogReader. Вызывать до построения DI вместе с existing SqlServer/persistence/facade. Duplicate policy/provider или legacy/custom reader без capability отклоняются; app не override supplied queries/gate. Отсутствие policy не получает скрытые defaults.
- SqlServerHistoryRecordSizeProvider строит server expression десяти mapped tables: 256 + 2 × DATALENGTH каждой variable column, включая binary owner/complex reports. Новые reference properties требуют явной поддержки; client evaluation/SQL bypass запрещены. Scope rows/bytes/query bounds — Technical documentation/31-bounded-history.md; SQLite/PostgreSQL здесь не обещаются. EF10.0.12/SqlClient7.0.2 exact roots сохраняются.

- Модуль зависит от общего AgentBridge.Persistence.EfCore, EF provider 10.0.12 и существующего EFCoreLibrary.Maintenance.SqlServer. Другие провайдеры и migrations assemblies не подключать.
- AddAgentBridgeSqlServer явно и идемпотентно регистрирует модуль до AddAgentBridge/AddAgentBridgePersistence. Общая конфигурация выбирает провайдера; отсутствие или неоднозначность отклоняется до операций.
- Configure задаёт existing migrations assembly и __AgentBridgeMigrationsHistory. CreateMaintenance использует библиотечный provider в текущем scope; не выполняет backup/migrations/SQL и не владеет orchestration.
- Общий DbContext, модели, repositories и UoW остаются в общем адаптере. Схема и generated migrations не изменяются.
- Проверки — isolated persistence tests и compile-check конкретных проектов с GeneratePackageOnBuild=false. Не открывать БД/HTTP, не запускать приложение/Docker/tooling.
