# Настройка хранения PostgreSQL

- Database.Provider задаётся явно как PostgreSql=1; другие имена/числа и отсутствие модуля отклоняются до I/O. Общая конфигурация не зависит от Npgsql; приложение регистрирует PostgreSQL-модуль.
- Maintenance подключается отдельно. BackupDirectory, retention, pg_dump path, major и cleanup timeout обязательны; проверка формы не открывает файлы/БД и не раскрывает значения. Автоматического backup, migration или fallback нет.
