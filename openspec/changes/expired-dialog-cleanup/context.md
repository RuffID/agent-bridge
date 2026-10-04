# Контекст этапа22

Цель — ограничить работу одного app-invoked вызова поверх существующих портов этапов09/10. Один пакет вместо drain-loop позволяет приложению управлять частотой и бюджетом вызовов. Новых defaults, scheduler/background service, HTTP или схемы нет.

Каждый кандидат получает отдельный scope/Serializable deletion UoW через base CRUD EFCoreLibrary0.0.5. Scope чтения закрывается до первого deletion; контекст не разделяется между операциями. Clock читается внутри каждого scope перед портом, поэтому длительность предыдущего удаления не фиксирует время всего пакета.

Например, limit3 возвращает Deleted/Failed(Conflict)/Deleted. Это partial результат, а не полный успех. Повторную попытку приложение решает отдельным вызовом с новой выборкой; оркестрация не refresh/retry исходный token.

При cancel/exception после начала delete acknowledgement может отсутствовать: Unknown не доказывает rollback или commit. LastResult содержит безопасный отчёт; неожиданная ошибка продолжает распространяться. Если success уже возвращён портом, поздняя scope cleanup ошибка не стирает его. Cancellation и cleanup aggregate не скрывается как обычная отмена.

Удаление во время active run использует existing root guards: поздний ответ/compact/tools/settings не создаёт диалог заново. Мягкий bytes threshold и выбор model/effort не участвуют в выборке истёкших строк. Доказательство cascade/settings и late writes требует настоящих SQLite/PostgreSQL, fake ports его не заменяют.
