# Проверки общего EF-адаптера

- Обычные disconnected metadata/JSON/application тесты не открывают соединение. Реальные provider tests находятся в Integration и регулируются его AGENTS; в задаче03 они запрещены.
- UnknownWindowSnapshotTests проверяет только TurnSettingsMapping v1/v2 и безопасные primitive настройки, без БД, AppData, kit или frozen DLL.
- Filter перед запуском обязан исключать Dependency=Database; имя проекта не подтверждает изоляцию. Consumer исходники Compile/Link только компилируются.
