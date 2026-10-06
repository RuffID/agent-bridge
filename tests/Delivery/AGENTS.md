# Проверка поставки DLL

- Consumer/StrictConfigurationRegistration.cs — compile-only пример explicit limits/source modes Audit Remediation13, linked isolated persistence tests. Методы не исполняются; source compilation не доказывает external binary kits15/16 или runtime18.

- Consumer/SqlServerRegistration.cs — текущий compile-only пример persistence/maintenance API SQL Server: source компилируется как linked item isolated persistence tests, методы не исполняются. Это не новый DLL kit и не бинарная проверка MSSQL; комплекты трёх RID и внешний consumer относятся к Audit Remediation15/16.

- `Build/` — служебный SDK library project для разрешения общей runtime closure CodexLb + выбранной migrations assembly. Собственная DLL не поставляется. `DeliveryProvider` принимает `Sqlite` или `PostgreSql`; перед каждой сменой варианта обязателен restore, сборки последовательны с отдельными output. Только обычный Build, без targets/Exec/hooks, pack/publish и запуска.
- `Consumer/` — compile-only библиотека .NET10/win-x64 и проверяемые примеры руководства25. Только бинарные ссылки через переданный `AgentBridgeDeliveryRoot`; ни ProjectReference, ни PackageReference, ни путей к исходникам. Для проверки копировать за пределы репозиториев и собирать обоими provider kits. Методы не исполнять; app factories/авторизация/business source остаются обязанностью потребителя, не заменяются fake success. Это не готовое приложение.
- `AgentBridge.Delivery.props` поставляется рядом с `lib/` и `native/`. Он подключает managed DLL/XML и копирует native win-x64 файл стандартными SDK items. Не смешивать версии комплектов и RID.
- `Metadata/` — отдельные isolated tests: чтение PE/XML и Roslyn symbols из локального комплекта без загрузки/исполнения AgentBridge, native engine, DI, HTTP или БД. Каталог задаётся обязательной переменной `AGENTBRIDGE_DELIVERY_ROOT`.
- Во всей build chain передавать `-p:GeneratePackageOnBuild=false`. Generated DLL/XML/deps/assets/manifest не редактировать вручную и не добавлять в Git. Не заявлять runtime/native validation по результату compile/metadata checks.
