# Задачи

- [x] Подключить общий coordinator и SQLite/PostgreSQL модули EFCoreLibrary к зарегистрированному контексту без startup side effects.
- [x] Валидировать backup directory, явный retention и PostgreSQL dump settings до операций; не задавать default retention или purge.
- [x] Проверить реальные registration/options/metadata и coordinator через fake provider/migration boundaries: порядок, receipt, частичные ошибки, отмену, collision и общий gate. 164 passed / 0 failed / 0 skipped, 50 новых; финальные builds 0 warnings/errors.
- [x] Обновить фактический API, архитектурные границы, навигацию и отчёт этапа 12.

Статус: **Реализован и принят; запрещённые проверки пропущены**. Конкретный production срок backup выбирает приложение до использования; API требует явное значение без default.

Реальные БД/SQL/backup/restore/native/process/hosting проверки пропускаются по указанию пользователя. OpenSpec CLI отсутствует в PATH; CLI validation не выполнена, change не архивировать. Этап принят; разрешён локальный коммит ровно 29 согласованных файлов, подтверждаемый отдельным отчётом.
