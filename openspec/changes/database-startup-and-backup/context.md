# Контекст этапа 12

Цель — связать принятый maintenance API с собственным scoped контекстом AgentBridge. Общий DatabaseMaintenance coordinator уже владеет inspection/discovery/backup/migration/verification; повторение этого порядка в AgentBridge создало бы конкурирующие правила.

Приложение останавливает writes, DDL и другие экземпляры, выделяет scope и явно выбирает один метод. Например, существующая PostgreSQL БД обновляется через UpdateExistingAsync; отказ аутентификации не переключает вызов на InitializeNewAsync. Без pending backup отсутствует; перед pending migrations проверяется receipt.

Срок хранения копий не согласован как конкретное число дней. API требует явного положительного выбора приложения перед рабочим использованием, не устанавливает default и не выполняет удаление. Это отдельная политика от DialogRetentionOptions. Receipt подтверждает завершение механизма backup, а не восстановимость.

SQLite native runtime и PostgreSQL pg_dump/совпадающий major поставляет приложение. Неизвестный исход блокирует общий библиотечный gate; AgentBridge не добавляет reset/retry/recovery. Реальные provider операции не исполняются при разработке этапа; изолированные fake boundaries не доказывают работу БД, native или процесса.
