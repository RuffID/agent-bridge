# Проверка поставки DLL

- `Build/` — служебный SDK library project для разрешения общей runtime closure CodexLb + выбранной migrations assembly. Собственная DLL не поставляется. `DeliveryProvider` принимает `Sqlite` или `PostgreSql`; перед каждой сменой варианта обязателен restore, сборки последовательны с отдельными output. Только обычный Build, без targets/Exec/hooks, pack/publish и запуска.
- `Consumer/` — compile-only библиотека .NET10/win-x64. Только бинарные ссылки через переданный `AgentBridgeDeliveryRoot`; ни ProjectReference, ни PackageReference, ни путей к исходникам. Для проверки копировать за пределы репозиториев. Это не готовое приложение/руководство этапа25.
- `AgentBridge.Delivery.props` поставляется рядом с `lib/` и `native/`. Он подключает managed DLL/XML и копирует native win-x64 файл стандартными SDK items. Не смешивать версии комплектов и RID.
- `Metadata/` — отдельные isolated tests: чтение PE/XML и Roslyn symbols из локального комплекта без загрузки/исполнения AgentBridge, native engine, DI, HTTP или БД. Каталог задаётся обязательной переменной `AGENTBRIDGE_DELIVERY_ROOT`.
- Во всей build chain передавать `-p:GeneratePackageOnBuild=false`. Generated DLL/XML/deps/assets/manifest не редактировать вручную и не добавлять в Git. Не заявлять runtime/native validation по результату compile/metadata checks.
