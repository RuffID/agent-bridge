# Подготовка PostgreSQL-поставки

- SDK library собирает Integration closure и PostgreSQL migrations, без Exec/hooks, pack/publish или запуска DLL. DeliveryProvider=PostgreSql и явный win-x64/linux-x64/linux-arm64 проверяются до сборки.
- Prepare-DeliveryTests готовит три последовательных PostgreSQL SDK variants, один DLL/NuGet bundle и shared bundle. Скрипты запускаются только по отдельному разрешению точной команды; build тестов их не запускает.
- Сохранять manifest/provenance/hash/case/RID проверки, эксклюзивный lock, fresh staging и fail-fast. Generated outputs находятся только в ignored artifacts; ready публикуется после успешной подготовки.
