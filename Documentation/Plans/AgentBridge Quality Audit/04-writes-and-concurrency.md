# 04 — Атомарная запись, UoW и конкуренция

Статус: **не начат**. Предпосылки: 03; понятны read snapshots и сохраняемые tokens.

## Цель и вопросы

Проверить короткую общую транзакцию и честный результат после conflict, отмены или неизвестного commit.

## Компоненты и зависимости

[UnitOfWorkScope](../../../adapters/AgentBridge.Persistence.EfCore/UnitOfWork/UnitOfWorkScope.cs), EfUnitOfWorkSession, PersistenceOperationGate, DialogWriteGuard, DialogCreation/Turn/Context/Settings/ToolAttempt/DeletionUnitOfWork, TurnContentStaging. EFCoreLibrary IUnitOfWorkContext и base CRUD. Тесты UnitOfWorkScopeTests, DialogWritePortsTests и Integration/PersistenceIntegrationTests.

## Способ проверки и границы

Для каждого write port проследить owner/expiry/incarnation/revision → staging → save → commit → cleanup. Проверить независимую settings CAS, root revision для изменений истории, atomic Started/outcomes+outputs, отсутствие HTTP/tools внутри транзакции. Границы: stale token, два writer, same-scope concurrency, ambient transaction/retry strategy, duplicate PK, FK failure, exception до/после SaveChanges, commit/rollback/dispose failure. Не приравнивать provider busy/serialization к ожидаемому Conflict. Проверить блокировку gate при неопределённости и запрет refresh/retry, сохранение первичной и cleanup ошибок.

## Разрешения

A; fake scope tests — B; два реальных контекста, блокировки, rollback/commit и каскады на SQLite/PostgreSQL — C. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Таблица транзакционных границ и ошибок по port, временные диаграммы гонок, разделение статического вывода, fake и реального persistence. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Пути всех write ports и неопределённого завершения разобраны; каждая заявка на атомарность имеет правильную границу доказательства. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Однократность действия во внешней бизнес-БД, распределённую блокировку или реальную потерю сети по synthetic acknowledgement.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.
