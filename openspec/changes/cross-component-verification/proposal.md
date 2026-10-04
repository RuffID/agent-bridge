# Сквозная проверка завершённых сценариев

## Причина

Предыдущие этапы отдельно проверили транспорт с fake HTTP и runner с actual SQLite/PostgreSQL, но gateway в DB fixtures был подставным. Нужна проверка взаимодействия этих компонентов без изменения согласованного поведения.

## Изменение

Добавить адресные регрессии public AgentRunner → actual CodexLb/HttpClientLibrary → local handler/streams → actual EFCoreLibrary CRUD/UoW. Зафиксировать результаты, версии, ограничения и передачу после этапа23. Новые production-схемы, миграции и этапы24–25 не входят в работу.

## Проверка

Конкретный test project и адресный набор на собственных SQLite/PostgreSQL. CLI OpenSpec проверяется без установки; static review не заменяет CLI validation. Этап23 принят координатором после проверки evidence/manifest; локальный commit ровно15 файлов разрешён отдельно. После23 STOP;24–25 не начаты.
