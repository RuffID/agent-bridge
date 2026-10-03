# Provider-specific схема AgentBridge

Этап 11: **Реализован и принят; запрещённые проверки пропущены**. После разрешения пользователя «Разрешаю обе команды генерации» однократно созданы InitialAgentBridgeSchema, designer и snapshot для SQLite/PostgreSQL. Startup/backup относятся к этапу 12 и не подключены.

## Реализованные проекты и API

| Проект / target = startup | Factory | Identity |
| --- | --- | --- |
| `adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj` | `SqliteAgentBridgeDbContextFactory` | `AgentBridge.Persistence.Migrations.Sqlite` |
| `adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj` | `PostgreSqlAgentBridgeDbContextFactory` | `AgentBridge.Persistence.Migrations.PostgreSql` |

Оба проекта — `net10.0` libraries с runtimeconfig для штатного dotnet-ef, без Program/host. Factories реализуют `IDesignTimeDbContextFactory<AgentBridgeDbContext>` и возвращают общий контекст. SQLite использует `Data Source=agent-bridge-design-time-never-open.db`; PostgreSQL — синтетические Host=invalid.example/credentials. Соединения не открываются; конфигурация и секреты приложения не читаются. Переданные args, включая попытку сменить подключение/provider, отклоняются без раскрытия содержимого. SensitiveDataLogging выключен.

`AgentBridge.Persistence.EfCore.Configuration.AgentBridgeMigrationsAssemblies.SQLITE/POSTGRESQL` задаёт устойчивые identities. `AddAgentBridgePersistence` выбирает соответствующий `MigrationsAssembly` вместе с runtime provider. Общий адаптер не ссылается обратно на provider projects. Для migrations service приложение должно поставить выбранную DLL; автоматическая поставка/maintenance API этапа 12 ещё не реализованы. Пример текущего подключения исходников: приложение со SQLite ссылается на общий EF-адаптер и SQLite migrations csproj; вызовы `AddDatabaseConfiguration`/`AddAgentBridgePersistence` остаются прежними и ничего не запускают.

CLI workflow: уже установленный dotnet-ef **10.0.11**, Microsoft.EntityFrameworkCore.Design **10.0.11** с PrivateAssets=all в каждом target/startup проекте. EF/Relational/SQLite runtime **10.0.11**, Npgsql.EntityFrameworkCore.PostgreSQL **10.0.3**. Tools/Package Manager Console не используются; NuGet pack/publish не выполняются. Design-time зависимости не становятся транзитивными runtime dependencies приложения.

`AgentBridgeMigrationsHistory.TABLE_NAME` задаёт `__AgentBridgeMigrationsHistory` в runtime и обеих factories через MigrationsHistoryTable. Общий `__EFMigrationsHistory` подключающего приложения не используется. Это отдельная служебная таблица EF, которой provider history repository управляет при явном обслуживании; она не входит в пять mapped таблиц, initial Up или snapshots. History schema option оставлен null для выбранного provider default; таблица не создавалась и не читалась в этих проверках.

## Границы модели

Только `Dialogs`, `DialogTurns`, `CanonicalItems`, `ModelSteps`, `DialogContexts`. Прежний mapping не изменён: parent-local composite keys, required FK/cascade, сохранение всех versions/history и полного response payload, UTC ticks INTEGER/bigint, OwnerId BINARY/C без нормализации/MaxLength, root immutable/concurrency metadata. Локальный Domain lifetime не сохраняется вместо incarnation.

На root остаётся ровно expiry/Id index. Формулировка прежнего плана об индексе владения уточнена: текущие read ports используют dialog ID, owner-list порта нет. FK/order indexes остаются из существующего mapping. ThroughTurnSequence не заменяет item cutoff. Generated constraints/operations проверены как артефакты; enforcement на БД не проверен.

## Сгенерированные артефакты

| Provider | Migration ID | Snapshot path внутри provider проекта |
| --- | --- | --- |
| SQLite | `20261003155233_InitialAgentBridgeSchema` | `AgentBridge/Persistence/Migrations/Sqlite/Migrations/AgentBridgeDbContextModelSnapshot.cs` |
| PostgreSQL | `20261003155235_InitialAgentBridgeSchema` | `AgentBridge/Persistence/Migrations/PostgreSql/Migrations/AgentBridgeDbContextModelSnapshot.cs` |

Migration и designer расположены в `Migrations/`, snapshots tooling разместил по namespace path. Generated файлы не перемещались и не редактировались вручную. Up создаёт ровно пять таблиц, четыре cascade FK, три индекса и 20 исходных check constraints; Down описывает удаление детей до родителей. Composite keys/FK, payload columns, BINARY/C и INTEGER/bigint UTC ticks согласованы с общим mapping. Нет чужих таблиц, seed/backfill или SQL operations. Runtime fixed-field/concurrency metadata сохраняется в исходном контексте; snapshot/DDL не заменяет application guards.

## Проверки и ограничения

После генерации три concrete builds (оба provider проекта и persistence tests) — 0 warnings/errors; 114 passed / 0 failed / 0 skipped, включая 6 новых за этап. Четыре проверки factory/runtime model/assembly/args дополнены двумя проверками единственной migration и snapshot через IMigrationsAssembly. Snapshot, designer target model и текущая initialized relational model сравниваются через IMigrationsModelDiffer: различий нет. SQL generator, UpOperations/DownOperations, connection и hosting не используются; metadata не доказывает реальное исполнение схемы.

После замечания ревью изолирована history table; те же три builds — 0 warnings/errors; только адресные ProviderDesignTimeTests — 6 passed / 0 failed / 0 skipped. Options runtime/factory явно содержат отдельное имя и null schema; реальный IHistoryRepository выбирает SqliteHistoryRepository/NpgsqlHistoryRepository без вызова его DB/SQL методов. Snapshot/target/current model diff остаётся пустым. Широкие тесты не повторялись после этой правки; результат 114 выше относится к проверке до history isolation. Все шесть generated файлов сохранены без изменений, повторной генерации не было.

В [плане этапа 11](<../Plans/AgentBridge Initial Implementation/11-initial-provider-migrations.md>) сохранены разрешение пользователя, выполненный блок с pinned tool DLL/--no-build/временным BaseOutputPath, фактические пути всех 33 файлов и проверки. Up/Down прочитаны, но не исполнялись. БД/SQL script/apply/rollback/restart/hosting — **Пропущено по указанию пользователя**, не runner-skipped. Metadata/fakes не доказывают реальные ограничения, locking или восстановимость. OpenSpec CLI validation не выполнена; change не архивирован. Этап принят координатором; локальный коммит разрешён только по явному списку 33 файлов.

[Нормативные требования](../../openspec/specs/agent-runtime/spec.md), [контекст изменения](../../openspec/changes/initial-provider-migrations/context.md), [проверенные контракты EFCoreLibrary](02-efcorelibrary.md).
