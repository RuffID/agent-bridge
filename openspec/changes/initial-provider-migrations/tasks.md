# Задачи

- [x] Подготовить provider-specific проекты и design-time factories общего контекста.
- [x] Связать runtime provider с устойчивой migrations assembly identity.
- [x] Изолировать служебную историю EF через __AgentBridgeMigrationsHistory в runtime/factories; после правки 6 адресных tests passed, 0 failed/skipped и три builds 0 warnings/errors; generated artifacts не изменены, model diff отсутствует.
- [x] Проверить metadata/DI/factories без подключения и собрать затронутые проекты: после генерации 114 passed / 0 failed / 0 skipped, 6 новых за этап; builds 0 warnings/errors.
- [x] Зафиксировать фактические API, результаты и точные команды генерации.
- [x] Получить согласование команд генерации: пользователь «Разрешаю обе команды генерации».
- [x] Сгенерировать и проверить InitialAgentBridgeSchema, designer и snapshot обоих providers: SQLite 20261003155233, PostgreSQL 20261003155235; static review и relational model differences без SQL/Up/Down исполнения.

БД/SQL/apply/rollback/hosting пропускаются по указанию пользователя. OpenSpec CLI validation не выполнена; change не архивировать до проверки.
