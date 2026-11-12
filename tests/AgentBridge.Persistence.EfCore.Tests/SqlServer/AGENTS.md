# Изолированные проверки SQL Server

- Восстановленные provider/factory/model/owner/maintenance проверки работают без соединения, SQL execution, Docker, host или файлов backup.
- SQL translation проверяется через ToQueryString; snapshot/designer — через IMigrationsModelDiffer. Это не проверка SQL enforcement или существующей БД.
- Провайдер регистрируется в этих тестах явно; общий PostgreSQL fixture не изменяется. Consumer SQL Server Compile/Link только компилируется.
