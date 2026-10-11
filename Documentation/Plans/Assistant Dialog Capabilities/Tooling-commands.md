# Точные команды migrations tooling

Дата: 11.10.2026. Первые три разрешённые команды завершились отказом загрузки Design до генерации. Исправленный набор ниже с additionalprobingpath локального cache отдельно разрешён пользователем и успешно выполнен для всех трёх providers.

Генерируют только C# migration/designer/snapshot `AddCatalogAndDurableRecovery` для трёх providers; не применяют migrations, не открывают БД, не создают SQL scripts, не выполняют restore/build/host. Проверены design-time factories с synthetic connections и пустыми args. Локальный launcher dotnet-ef 10.0.11 загрузил проектный Design/runtime 10.0.12 и предупредил о более старой версии launcher. Runtime-пакеты не менялись; сеть не использовалась. Повторная генерация этими командами не требуется.

Результаты: SqlServer `20261011024148`, Sqlite `20261011024150`, PostgreSql `20261011024151` — AddCatalogAndDurableRecovery, Designer и updated snapshot. Strict snapshot/designer/current-model differ, Up/Down metadata и legacy defaults проходят в полном изолированном persistence-наборе (422 passed). Это не применение migrations и не SQL/provider acceptance; полное [evidence](Evidence.md).

Рабочая директория: `D:/Media/User/source/repos/agent-bridge`.

## SqlServer

```powershell
dotnet exec --additionalprobingpath "C:/Users/Spike/.nuget/packages" --depsfile "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.SqlServer/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.SqlServer.deps.json" --runtimeconfig "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.SqlServer/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.SqlServer.runtimeconfig.json" "C:/Users/Spike/.dotnet/tools/.store/dotnet-ef/10.0.11/dotnet-ef/10.0.11/tools/net8.0/any/tools/net8.0/any/ef.dll" migrations add AddCatalogAndDurableRecovery --assembly "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.SqlServer/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.SqlServer.dll" --startup-assembly "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.SqlServer/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.SqlServer.dll" --project-dir "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.SqlServer" --root-namespace AgentBridge.Persistence.Migrations.SqlServer --language C# --nullable --context AgentBridgeDbContext --output-dir Migrations
```

## Sqlite

```powershell
dotnet exec --additionalprobingpath "C:/Users/Spike/.nuget/packages" --depsfile "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.Sqlite/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.Sqlite.deps.json" --runtimeconfig "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.Sqlite/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.Sqlite.runtimeconfig.json" "C:/Users/Spike/.dotnet/tools/.store/dotnet-ef/10.0.11/dotnet-ef/10.0.11/tools/net8.0/any/tools/net8.0/any/ef.dll" migrations add AddCatalogAndDurableRecovery --assembly "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.Sqlite/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.Sqlite.dll" --startup-assembly "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.Sqlite/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.Sqlite.dll" --project-dir "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.Sqlite" --root-namespace AgentBridge.Persistence.Migrations.Sqlite --language C# --nullable --context AgentBridgeDbContext --output-dir Migrations
```

## PostgreSql

```powershell
dotnet exec --additionalprobingpath "C:/Users/Spike/.nuget/packages" --depsfile "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.PostgreSql/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.PostgreSql.deps.json" --runtimeconfig "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.PostgreSql/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.PostgreSql.runtimeconfig.json" "C:/Users/Spike/.dotnet/tools/.store/dotnet-ef/10.0.11/dotnet-ef/10.0.11/tools/net8.0/any/tools/net8.0/any/ef.dll" migrations add AddCatalogAndDurableRecovery --assembly "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.PostgreSql/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.PostgreSql.dll" --startup-assembly "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.PostgreSql/artifacts/assistant01a-compile/Debug/net10.0/AgentBridge.Persistence.Migrations.PostgreSql.dll" --project-dir "D:/Media/User/source/repos/agent-bridge/adapters/AgentBridge.Persistence.Migrations.PostgreSql" --root-namespace AgentBridge.Persistence.Migrations.PostgreSql --language C# --nullable --context AgentBridgeDbContext --output-dir Migrations
```


