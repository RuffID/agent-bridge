# Подготовка provider-поставки

- SDK library собирает Integration closure и выбранные PostgreSQL либо SQL Server migrations, без Exec/hooks, pack/publish или запуска DLL. DeliveryProvider=PostgreSql|SqlServer и явный win-x64/linux-x64/linux-arm64 проверяются до сборки.
- Prepare-DeliveryTests готовит шесть последовательных SDK variants (два providers × три RID), отдельные DLL/NuGet и shared bundles каждого provider. Скрипты запускаются только по отдельному разрешению точной команды; build тестов их не запускает.
- Сохранять manifest/provenance/hash/case/RID проверки, эксклюзивный lock, fresh staging и fail-fast. Generated outputs находятся только в ignored artifacts; ready публикуется после успешной подготовки.
