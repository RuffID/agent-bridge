# Интеграционные проверки этапов 00–13

- Запуск только с явным разрешением пользователя на реальные тестовые БД и процессы. Основная реализация приостановлена после этапа 13; последующие этапы здесь не проверяются.
- `Dependency=Database` отделяет проверки от изолированного набора. `AGENTBRIDGE_INTEGRATION=1` включает их явно; отсутствующая обязательная конфигурация после включения — ошибка, не skip.
- Windows runner использует SQLite-файлы в явно выделенном каталоге и PostgreSQL на 127.0.0.1 в отдельном временном Docker-контейнере. Имена БД и тестового пользователя начинаются с `abverify_`. Рабочие секреты и существующие БД запрещены.
- Операции данных идут через публичные порты AgentBridge и реальные base repositories/scope EFCoreLibrary. SQL для наблюдения схемы и создания отказных условий — через IDatabaseCommands этой библиотеки; не заменять production pipeline.
- Миграции применяются настоящим maintenance API. Для проверки Down используется штатный IMigrator из DatabaseFacade библиотечного IUnitOfWorkContext, поскольку maintenance API не предоставляет downgrade.
- Backup выполняет реальный provider EFCoreLibrary. SQLite восстанавливается её native backup API, PostgreSQL — штатным pg_restore через её process runner. Restore всегда в отдельной тестовой БД, с сопоставлением схемы и данных.
- Сверять API с EFCoreLibrary 0.0.5: contracts — `.Maintenance.Abstractions`, models — `.Models`, budget — `.Coordination`, errors — `.Errors`, workspace — `.Backup`, commands — `.Database`, SQLite native API — `.Sqlite.Backup`, PostgreSQL provider — `.PostgreSql.Providers`. Integration должна компилироваться; сборка не разрешает её запуск.
- Каждый fixture владеет только созданными им файлами/БД и удаляет их при DisposeAsync. Контейнером владеет внешний вызывающий; тесты Docker не запускают. Restore/build/test всегда с GeneratePackageOnBuild=false, outputs и credentials не коммитить.
