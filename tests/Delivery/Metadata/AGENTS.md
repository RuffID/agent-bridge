# Метаданные provider-поставки

- PE/XML/manifest/SDK closure читаются без загрузки или исполнения AgentBridge/native DLL. Ready fixture требует явно подготовленные PostgreSQL комплекты трёх RID; отсутствие комплекта не превращается в Skip или поиск старой поставки.
- Проверять Npgsql/provider/maintenance, отсутствие SQLite/SQL Server/SqlClient, точные case/hash/size и переносимые references. PostgreSQL closure не требует SQL Server native/resources. ELF controls синтетические; metadata не доказывает runtime.

- SqlServerDeliveryTests отдельно требует SqlServer комплекты трёх RID, проверяет выбранный provider/migrations/maintenance/SqlClient и managed closure без Npgsql/PostgreSQL/SQLite. Отсутствие свежего комплекта — ошибка, не Skip; тесты не подготавливают поставку автоматически.
