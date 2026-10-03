# Адаптеры базовых репозиториев — этап 09

Адаптировать чтение диалога, истории и версий compact через текущие базовые репозитории EFCoreLibrary. Реализовать защищённое чтение Application и ограниченную выборку истёкших кандидатов. Общие create/update/delete делегировать библиотеке как staging в общей scoped session.

Изменяющие Application ports, сохранение, транзакции и восстановление Domain остаются этапу 10. Новые Application repository ports не вводятся: адаптеры строк остаются внутри Infrastructure. Реальные providers/БД не запускаются; custom query не нужен, поскольку predicate/include поддерживают фильтры и сортировку до take.
