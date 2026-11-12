# Модули провайдеров и общая поставка DLL

## Явный модуль БД

`AgentBridge.Persistence.EfCore` содержит общий DbContext, модели, repositories, read ports, сценарные UoW и настройки. Он зависит от EF Core/Relational, EFCoreLibrary CRUD и общего maintenance, но не от SQLite/Npgsql/SqlClient и конкретных maintenance-модулей. `AgentBridge.Integration` также не ссылается на конкретный провайдер.

Провайдер подключает приложение до facade или `AddAgentBridgePersistence`:

| Выбор | DLL / namespace | Регистрация |
| --- | --- | --- |
| PostgreSQL | `AgentBridge.Persistence.PostgreSql` | `services.AddAgentBridgePostgreSql()` |
| SQL Server | `AgentBridge.Persistence.SqlServer` | `services.AddAgentBridgeSqlServer()` |

Пример PostgreSQL:

```csharp
using AgentBridge.Integration;
using AgentBridge.Persistence.PostgreSql;

services.AddAgentBridgePostgreSql();
services.AddAgentBridge(configuration, appOwnedHttpClientFactory);
```

ILoggerFactory и app-owned HTTP factory остаются обязательными. `Database.Provider` должен соответствовать одному из явно зарегистрированных модулей. Missing/wrong/ambiguous module отклоняется проверкой options до операций; повтор того же модуля идемпотентен. AgentBridge подключает PostgreSQL или SQL Server по явному выбору; собственные модификации соседней EFCoreLibrary не входят в эту задачу.

Модули реализуют `IAgentBridgeDatabaseProvider`: настройку общего DbContext, создание existing EFCoreLibrary maintenance provider и точное распознавание provider-specific PK collision. CAS остаётся в общем UoW; PostgreSQL23505 классифицируется только по exact table/constraint. При самостоятельном создании UnitOfWorkScope provider-specific classification требует нового конструктора с модулями; прежний двухаргументный конструктор сохранён для бинарной совместимости. DI передаёт модули автоматически.

Отдельные migrations projects зависят от своего модуля, identity/history и generated схема десяти таблиц не менялись. Выбранная migrations DLL поставляется вместе с модулем. Регистрация не выполняет соединение, SQL, backup или migrations.

## Структура общей поставки

Штатный [Assemble-Delivery.ps1](../../tests/Delivery/Build/Assemble-Delivery.ps1) сначала создаёт отдельные SDK closure комплекты. [Assemble-SharedDelivery.ps1](../../tests/Delivery/Build/Assemble-SharedDelivery.ps1) проверяет все входные SHA256/size/case/provenance и объединяет три RID одного провайдера:

```text
<bundle>/
  AgentBridge.Delivery.props
  common/lib/                             одинаковые DLL/XML всех RID
  common/resources/<culture>/             одинаковые ресурсы
  common/variants/<sha256>/lib/...          одинаковые файлы части RID
  platform/<rid>/lib/...                   варианты только одного RID
  platform/<rid>/native/...                native только выбранного RID
  platform/<rid>/AgentBridge.Delivery.variant.props
  platform/<rid>/delivery.manifest.json
  platform/<rid>/evidence/AgentBridge.Delivery.deps.json
  platform/<rid>/evidence/source.manifest.json
```

Одинаковый relative asset любого двух RID хранится один раз только при совпадении kind/SHA256. Разные bytes одного имени сохраняются раздельно. Native остаются привязанными к RID независимо от hash. Generated variant props перечисляет точные ссылки common/platform без glob соседних платформ и повторных assembly names; XML/resources/native получают output/publish metadata. Ресурсы сохраняют culture-relative paths. Missing RID отклоняется Build.

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
<Import Project="vendor/AgentBridge/AgentBridge.Delivery.props" />
```

Schema2 manifest каждого RID использует пути относительно корня bundle, включает общие и платформенные файлы, размеры/SHA256 и sourcePath исходного SDK kit. SDK deps и исходный manifest сохраняются как evidence, не как deps приложения. Generated outputs вручную не редактируются; оба упаковщика запускаются только после разрешения точной команды и требуют новый destination.

Матрица подготовки содержит PostgreSQL и SQL Server win-x64/linux-x64/linux-arm64, отдельные DLL/NuGet и shared bundles каждого provider. Скрипты обновлены; новые комплекты в этой задаче не создавались. Прежние artifacts относятся к историческим поставкам и не подтверждают текущую сборку обоих providers.

## Проверка

Source compile-check выполняется отдельно от подготовки комплектов. Metadata tests проверяют manifests/hashes/SDK closure, отсутствие чужих провайдеров в каждом комплекте и переносимость путей. Runtime, реальное PostgreSQL/SQL Server/backup/restore и external consumer требуют отдельной проверки свежего целого комплекта.
