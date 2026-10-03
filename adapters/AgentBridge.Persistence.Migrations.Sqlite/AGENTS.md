# Схема SQLite

- Самостоятельная library assembly миграций для общего AgentBridgeDbContext; target и startup CLI совпадают с этим проектом. Factory создаёт options/model без connection или hosting, не принимает forwarded arguments и не читает секреты приложения.
- Identity AgentBridge.Persistence.Migrations.Sqlite соответствует AgentBridgeMigrationsAssemblies.SQLITE. Не добавлять отдельный runtime DbContext, свою DTO-модель или зависимость из общего адаптера обратно на этот проект.
- Factory и runtime используют общую AgentBridgeMigrationsHistory.TABLE_NAME (__AgentBridgeMigrationsHistory); не возвращаться к ledger __EFMigrationsHistory приложения. История EF не входит в mapped entity model или generated initial migration.
- Design 10.0.11 PrivateAssets=all нужен только tooling; Tools/PMC не подключать для CLI. GenerateRuntimeConfigurationFiles позволяет штатному dotnet-ef использовать library startup без executable entry point.
- Generated Migrations/designer/snapshot создавать только штатным tooling после согласования точной команды; вручную не редактировать. Только пять собственных таблиц AgentBridge, без owner-list index и чужих сущностей.
- Restore/build/test требуют GeneratePackageOnBuild=false и outputs artifacts/compile-check. Изолированные factory/metadata проверки находятся в persistence-тестах. SQL, БД, Up/Down/apply, приложение и процессы не запускать.
