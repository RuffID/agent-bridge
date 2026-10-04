# 05 — Миграции и обслуживание БД

Статус: **не начат**. Предпосылки: 04; зафиксированы схема и transactional boundaries.

## Цель и вопросы

Проверить соответствие схемы, historical rows и безопасную последовательность обслуживания без автоматического запуска.

## Компоненты и зависимости

[SQLite migrations](../../../adapters/AgentBridge.Persistence.Migrations.Sqlite), [PostgreSQL migrations](../../../adapters/AgentBridge.Persistence.Migrations.PostgreSql), factories, AgentBridgeMigrationsHistory, DatabaseMaintenanceRegistrationExtensions/DatabaseBackupOptions. EFCoreLibrary maintenance: IDatabaseMaintenance и coordinator/provider modules. Тесты ProviderDesignTimeTests, DatabaseMaintenanceTests, Integration/MaintenanceIntegrationTests, DialogSettingsIntegrationTests.

## Способ проверки и границы

Сверить InitialAgentBridgeSchema → AddDurableToolAttempts → AddDialogSettings с model snapshots обоих providers, отдельным __AgentBridgeMigrationsHistory и шестью mapped таблицами. Проверить nullable legacy значения без выдуманной provenance. Для разрешённых C-сценариев: Up/Down/Up на копии, сохранение payload, отсутствие pending drift, Missing против auth failure, initialization против update, no-pending без backup, backup до DDL, отказ backup/DDL, poisoned gate, отмена/cleanup. Проверить SingleInitializer как обязанность приложения, отдельный scope, явную backup retention и restore в другую БД с сопоставлением схемы/данных.

## Разрешения

A; metadata/fake maintenance — B; любые БД/SQL/Up/Down/native backup/pg_dump/restore/процессы — C. Generated migrations не редактировать и новые не создавать. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Карта migrations и данных до/после, порядок обслуживания, receipt против фактической восстановимости, отдельные ограничения каждого provider. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Схема, legacy совместимость и все ветви обслуживания получили результаты либо объяснённые пропуски. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

SQL Server/MySQL, работоспособность multi-instance SingleInitializer без приложения, автоматическое удаление backup и восстановление любой production-БД.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.
