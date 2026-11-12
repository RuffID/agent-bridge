# Задачи

- [x] Восстановить SQL Server provider, migrations, настройки, mapping и bounded history из истории.
- [x] Восстановить provider-specific isolated tests и compile-only consumer; сохранить PostgreSQL.
- [x] Включить SQL Server в подготовку поставки, обновить требования и актуальную документацию.
- [x] Выполнить статическую проверку и compile-check затронутых проектов.
- [ ] Отдельно выполнить разрешённые тесты, подготовку поставки и строгую OpenSpec validation перед архивированием.

## Проверено

Локальный restore из C:/Users/Spike/.nuget/packages и Debug compile-check с GeneratePackageOnBuild=false и BaseOutputPath=./artifacts/compile-check/ выполнены для tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj и tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj. Оба проекта собраны без ошибок/предупреждений; первый включает provider/migrations обоих провайдеров и linked MSSQL consumer. Тестовые методы не выполнялись.

Git blob hashes всех пяти generated SQL Server файлов совпадают с родителем cbc6a46. PowerShell Parser проверил синтаксис четырёх изменённых скриптов без их выполнения. Diff whitespace и UTF-8 проверки прошли. Приложение, БД, SQL, Docker, упаковка и OpenSpec CLI не запускались; change остаётся открытым.
