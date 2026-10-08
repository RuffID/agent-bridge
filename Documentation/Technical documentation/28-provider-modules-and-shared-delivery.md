# Модули провайдеров и общая поставка DLL

## Явный модуль БД

`AgentBridge.Persistence.EfCore` содержит общий DbContext, модели, repositories, read ports, сценарные UoW и настройки. Он зависит от EF Core/Relational, EFCoreLibrary CRUD и общего maintenance, но не от SQLite/Npgsql/SqlClient и конкретных maintenance-модулей. `AgentBridge.Integration` также не ссылается на конкретный провайдер.

Провайдер подключает приложение до facade или `AddAgentBridgePersistence`:

| Выбор | DLL / namespace | Регистрация |
| --- | --- | --- |
| SQL Server | `AgentBridge.Persistence.SqlServer` | `services.AddAgentBridgeSqlServer()` |
| SQLite | `AgentBridge.Persistence.Sqlite` | `services.AddAgentBridgeSqlite()` |
| PostgreSQL | `AgentBridge.Persistence.PostgreSql` | `services.AddAgentBridgePostgreSql()` |

Пример для SQL Server:

```csharp
using AgentBridge.Integration;
using AgentBridge.Persistence.SqlServer;

services.AddAgentBridgeSqlServer();
services.AddAgentBridge(configuration, appOwnedHttpClientFactory);
```

ILoggerFactory и app-owned HTTP factory остаются обязательными. `Database.Provider` должен соответствовать одному из явно зарегистрированных модулей. Missing/wrong/ambiguous module отклоняется проверкой options до операций; повтор того же модуля идемпотентен. Можно явно подключить несколько модулей в универсальном приложении, но его DLL-поставка тогда должна содержать их зависимости.

Модули реализуют `IAgentBridgeDatabaseProvider`: настройку общего DbContext, создание existing EFCoreLibrary maintenance provider и точное распознавание provider-specific PK collision. CAS остаётся в общем UoW; SQLite1555 и PostgreSQL23505 с exact table/constraint сохраняют прежнюю классификацию. SQL Server driver failures по-прежнему не маскируются Conflict. При самостоятельном создании UnitOfWorkScope provider-specific classification требует нового конструктора с модулями; прежний двухаргументный конструктор сохранён для бинарной совместимости. DI передаёт модули автоматически.

Отдельные migrations projects зависят от своего модуля, identity/history и generated схема шести таблиц не менялись. Выбранная migrations DLL поставляется вместе с модулем. Регистрация не выполняет соединение, SQL, backup или migrations.

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

Полный SQL Server bundle содержит 140 файлов, около81,68 МиБ вместо418 файлов/175,92 МиБ прежних трёх комплектов. XML и локализации сохранены; .NET runtime устанавливается приложением отдельно. Это предыдущая структура поставки TelegramCodexRelayBot; сейчас бот использует [собственные DLL с обязательными NuGet-зависимостями](29-dll-and-nuget-delivery.md), 23 файла/1,03 МиБ. Полная поставка сохранена как альтернативный формат.

## Проверка и ограничения

Изолированные persistence tests проверяют metadata/runtime-vs-factory, общий scope, maintenance registration, явный модуль/ошибки выбора/idempotency/PK classification. SharedDeliveryTests проверяет source closure, hashes, отсутствие соседних провайдеров, dedup, PE/ELF metadata и переносимые references/output/publish paths. Bot tests используют настоящие DLL с local HTTP/storage doubles.

Проверка 2026-10-08: 357 persistence tests без БД, 28 изолированных Hosting.Tests, 30 delivery metadata tests и 113 bot tests пройдены, без failed/skipped. Соответствующие сборки завершились без предупреждений и ошибок. Пять внешних DLL-only consumers и пять runtime probe variants собраны для SQL Server win-x64/linux-x64/linux-arm64 и SQLite/PostgreSQL win-x64; методы consumers и probes не исполнялись. Hosting.Tests запускали только разрешённый тестовый Generic Host с заглушками.

В выходных папках пяти consumers сверены SHA256 всех DLL/XML/resources/native с manifest: 121/119/118 файлов SQL Server, 73 SQLite и 67 PostgreSQL. Установленный bundle совпадает с manifest, исходными Release DLL и SHA256 обоих упаковщиков; повторных физических managed DLL с одинаковым SHA256 нет.

Сборка и metadata не доказывают native loading, Linux runtime, SQL constraints/atomicity, реальное обслуживание или live API. Бот, БД, SQL, Docker и миграции при изменении структуры поставки не запускаются. Исторические отчёты прежних комплектов сохраняются как evidence на свою дату.
