# Задачи

- [x] Подключить EFCoreLibrary и согласованный EF10 dependency graph.
- [x] Разделить persistence DTO и Domain; сохранить ownership, fixed expiry, incarnation/revision и порядок.
- [x] Сохранить канонические items и полный lifecycle/envelope/continuation шагов и compact.
- [x] Настроить обязательные связи, каскады, concurrency metadata и индексы.
- [x] Зарегистрировать общий контекст и базовые репозитории для явного SQLite/PostgreSQL.
- [x] Проверить metadata, сериализацию и DI без подключения к БД; обновить фактическую документацию.

Статус: реализован и принят; запрещённые проверки пропущены. 34 passed / 0 failed / 0 skipped; 25 новых тестов. OpenSpec CLI validation не выполнена: CLI отсутствует в PATH. Relational integration пропущена по указанию пользователя. Не архивировано: CLI validation и реальные интеграции не подтверждены. Локальный коммит разрешён после приёмки; точный hash — в истории Git.
