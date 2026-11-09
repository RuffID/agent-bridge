# Миграции PostgreSQL

Действующий контракт: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). AgentBridge поддерживает только PostgreSQL. Отдельный library target/startup — `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj`; общий контекст остаётся в EF-адаптере.

## Структура

```text
AgentBridge.Persistence.Migrations.PostgreSql/
  AgentBridge.Persistence.Migrations.PostgreSql.csproj
  PostgreSqlAgentBridgeDbContextFactory.cs
  Migrations/
    <timestamp>_<name>.cs
    <timestamp>_<name>.Designer.cs
    AgentBridgeDbContextModelSnapshot.cs
```

Namespace `AgentBridge.Persistence.Migrations.PostgreSql.Migrations` не требует повторять своё дерево в физическом пути. Snapshot перенесён из лишней вложенности без изменения содержимого. История сохраняет InitialAgentBridgeSchema, AddDurableToolAttempts, AddDialogSettings и AddCatalogAndDurableRecovery с прежними ID; последняя модель содержит десять таблиц AgentBridge. Новая миграция для удаления других провайдеров не нужна.

Runtime и factory задают `AgentBridgeMigrationsAssemblies.POSTGRESQL` и `__AgentBridgeMigrationsHistory`. Factory использует синтетическую строку подключения, отклоняет аргументы и не открывает БД. Пакет Design находится только в tooling-проекте; CLI не требует Tools/PMC.

## Последующие изменения схемы

Tooling-команда указывает этот проект одновременно как target и startup и явно задаёт `--output-dir Migrations` и `--namespace AgentBridge.Persistence.Migrations.PostgreSql.Migrations`. Относительный output-dir задаётся от корня проекта, а не из полного namespace. Генерация и применение миграций требуют отдельного разрешения точной команды.

Designer, snapshot, Up и Down проверяются вместе; generated-содержимое вручную не подгоняется. Исторические миграции PostgreSQL не пересоздаются. Disconnected ProviderDesignTimeTests сравнивают runtime/factory/snapshot/последний designer через IMigrationsModelDiffer без подключения. Компиляция не подтверждает применение миграций или сохранность данных на сервере.

Обслуживание схемы приложение вызывает отдельно через EFCoreLibrary; [порядок и backup](06-database-maintenance.md). Перенос данных из удалённых SQLite/SQL Server в эту задачу не входит.
