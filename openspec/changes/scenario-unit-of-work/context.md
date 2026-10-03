# Контекст этапа 10

Основа проверена по текущим `IUnitOfWorkContext`, `UnitOfWorkContext`, base repositories и DI EFCoreLibrary. DatabaseFacade позволяет короткую явную transaction; соседняя библиотека не требует изменения. Сценарные write ports уже определены этапом 07; новые бизнес-сценарии не добавляются.

Реализованы сериализуемая transaction плюс concurrency metadata root, сохранение через общий scope, очистка tracker после собственной начатой transaction. Проверяется переданное `DialogAccess.NowUtc`, а не скрытые часы. Внешний результат приходит уже готовым; исходный token не обновляется ради принятия старого результата. Begin failure блокирует scope без очистки чужой transaction/tracker.

Например, после ответа модели `AppendAsync` принимает полный отчёт и канонические items. Если revision изменился или диалог был пересоздан, сохраняется отказ без данных. Output/envelope/continuation остаются отдельными; terminal prefix ничего не удаляет.

Ограничения: fakes проверяют порядок и отказы, но не relational atomicity SQLite/PostgreSQL. Commit/cleanup failures имеют неизвестный исход и не допускают повторного использования scope. Реальные БД, hosting, сеть и процессы запрещены пользователем; OpenSpec CLI отсутствует, изменение не архивируется.
