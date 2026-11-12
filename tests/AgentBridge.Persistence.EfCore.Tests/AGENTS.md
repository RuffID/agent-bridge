# Проверки общего EF-адаптера

- SqlServer/ содержит восстановленные disconnected provider/owner/maintenance tests, model differ и bounded SQL translation tests без подключения. SQL Server migrations и consumer подключены к compile-check; PostgreSQL fixtures сохранены.

- Обычные disconnected metadata/JSON/application тесты не открывают соединение. Реальные provider tests находятся в Integration и регулируются его AGENTS; в задаче03 они запрещены.
- UnknownWindowSnapshotTests проверяет только TurnSettingsMapping v1/v2 и безопасные primitive настройки, без БД, AppData, kit или frozen DLL.
- DialogCanonicalJsonTests проходит actual runner/write/recovery/read ports над CatalogWriteFixture: raw JSON с кириллицей/пробелами после сериализации принимается, изменённые значения/неизвестные поля/порядок массивов и outputs отклоняются. Проверяются rollback, сохранение originals и отсутствие replay; fresh instance этих doubles не подтверждает restart/recovery настоящей БД.
- Filter перед запуском обязан исключать Dependency=Database; имя проекта не подтверждает изоляцию. Consumer исходники Compile/Link только компилируются.
- Sub2Api/ проверяет public facade/AgentRunner/настоящий transport и persistence ports вместе над local HTTP/base repository doubles. Границы serialized restart и no-replay — в ближайшем AGENTS; это не Integration provider restart.
