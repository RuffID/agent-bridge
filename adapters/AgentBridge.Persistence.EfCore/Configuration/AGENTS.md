# Настройка реляционного хранения

- Database.Provider задаётся явно как PostgreSql=1 или SqlServer=2; SQLite=0, неизвестные имена/числа и отсутствие модуля отклоняются до I/O. Общая конфигурация не зависит от Npgsql; приложение регистрирует выбранный модуль.
- Maintenance подключается отдельно. Для PostgreSQL обязательны BackupDirectory, retention, pg_dump path, major и cleanup timeout; для SQL Server — SqlServerBackupDirectory на сервере и retention; проверка формы не открывает файлы/БД и не раскрывает значения. Автоматического backup, migration или fallback нет.
