## Реализация

- [x] Добавить безопасные settings/status API и per-dialog выбор.
- [x] Подключить выбранные значения к фиксированному snapshot AgentRunner.
- [x] Реализовать version-aware запись через EFCoreLibrary и сценарный UoW.
- [x] Создать разрешённые tooling migrations SQLite/PostgreSQL.
- [x] Проверить ошибки выбора, секреты, конкурентные изменения и opaque-контекст.
- [x] Проверить адресные риски на настоящих SQLite/PostgreSQL.
- [x] Синхронизировать main spec, документацию и отчёт21.

## Проверка

- [x] Compile-check конкретных проектов и адресные тесты.
- [ ] OpenSpec CLI validation (CLI пока не найден).

Static review не заменяет CLI validation. Change не архивирован. Этап принят координатором; исторический запрет add/commit до приёмки снят, локальный коммит ровно78 файлов manifest разрешён.
