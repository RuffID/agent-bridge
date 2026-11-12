# Контекст восстановления

Исходник — родитель cbc6a46. Восстанавливаются только SQL Server части; добавленная тем коммитом PostgreSQL bounded history сохраняется. Значения DatabaseProvider.PostgreSql=1 и SqlServer=2, имена migrations assembly, history table и migration IDs остаются прежними. Generated migrations возвращаются без редактирования содержимого.

Пример: приложение вызывает AddAgentBridgeSqlServer(), задаёт Database.Provider=SqlServer и ConnectionString, затем AddAgentBridge(...). AddAgentBridgeSqlServerBoundedHistory(...) подключает явные лимиты. Maintenance отдельно требует SqlServerBackupDirectory на сервере и положительный BackupRetentionPeriod; параметры pg_dump для SQL Server не нужны. Регистрация не открывает БД и не выполняет обслуживание.

Чтение старой SQL Server БД предполагает прежнюю таблицу __AgentBridgeMigrationsHistory. Реальное соответствие существующей БД и выполнение backup/migrate в этой задаче не проверяются. PostgreSQL migration artifacts не меняются. Поставки требуют пересборки; старые комплекты сами не обновляются.
