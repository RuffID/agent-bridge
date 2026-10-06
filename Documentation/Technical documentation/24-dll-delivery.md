# Поставка DLL для .NET10

Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). Текущий compile/metadata checkpoint — [Audit Remediation15](<../Plans/AgentBridge Audit Remediation/15-multiplatform-delivery.md#результаты>), 2026-10-06. Первоначальные [поставка24](<../Plans/AgentBridge Initial Implementation/24-dll-delivery.md>) и [consumer25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>) сохраняются как историческое evidence.

## Комплекты и граница проверки

Комплекты лежат в игнорируемом `artifacts/delivery/stage15-v2/<Provider>/<RID>`. Основной provider — `SqlServer`; `Sqlite` и `PostgreSql` сохранены. Промежуточные stage15 kits не включали satellites и сохранены только как before evidence.

| Provider | RID | Managed DLL в lib | Culture satellites | XML | Native |
| --- | --- | --- | --- | --- | --- |
| SqlServer | win-x64 | 64 | 13 | 57 | 3 PE AMD64 |
| SqlServer | linux-x64 | 63 | 13 | 57 | 2 ELF64 little-endian x86-64 |
| SqlServer | linux-arm64 | 63 | 13 | 57 | 1 ELF64 little-endian AArch64 |
| Sqlite | win-x64 | 64 | 13 | 57 | 3 PE AMD64 |
| PostgreSql | win-x64 | 64 | 13 | 57 | 3 PE AMD64 |

Это SDK-style `net10.0`, Debug, framework-dependent DLL. Приложение устанавливает .NET10 runtime и выбирает свой SDK/shared framework; runtime/host, Serilog/sinks, secrets, сервер БД и приложение в комплект не входят. RID описывает приложение-клиент; Linux ARM64 не обещает SQL Server Engine на ARM64. SQLite/PostgreSQL для Linux этим checkpoint не подтверждены. Release, NativeAOT, trimming и single-file не проверялись.

```text
<kit>/
  AgentBridge.Delivery.props
  AgentBridge.Delivery.variant.props
  delivery.manifest.json
  lib/                                  managed DLL и доступные XML
  native/<RID>/                         SDK-selected native assets
  resources/<culture>/                  managed culture satellites
  evidence/AgentBridge.Delivery.deps.json
```

Manifest содержит RID/provider, framework/configuration, проверенную SDK version10.0.401, исходные Git revisions, SHA256 assets/generator и каждого файла, размеры, assembly/file versions и package/project provenance. Alias одной DLL, которую SDK также перечисляет как Reference, сохраняется отдельно и допускается лишь при совпадении kind/hash. Manifest не включает собственный hash; его hash записан в Results15. В manifest/props/deps нет абсолютных путей к Windows source tree; filename case и разделители переносимы. Raw project.assets/source snapshots остаются отдельным локальным evidence вне комплекта.

## Полная closure

Пять AgentBridge DLL: ядро, CodexLb, Persistence.EfCore, Integration и ровно одна выбранная `AgentBridge.Persistence.Migrations.<Provider>`. Собственная `AgentBridge.Delivery.dll`, Design/Roslyn/xUnit/testhost/PDB в комплект не входят.

Closure разрешает стандартный SDK Build [Delivery project](../../tests/Delivery/Build/AgentBridge.Delivery.csproj), который ссылается на Integration facade и выбранную migrations library. [Assemble-Delivery.ps1](../../tests/Delivery/Build/Assemble-Delivery.ps1) читает fresh RID-specific deps/assets/output, сверяет package/output hashes и генерирует комплект в новом каталоге; DLL/native не загружаются. Скрипт не запускается автоматически из Build. Перед каждым вариантом выполняется отдельный restore; runtime/native package assets не удаляются вручную.

Общий EF adapter статически ссылается на SQLite/PostgreSQL/SQL Server и их maintenance-модули. Поэтому **все** kits содержат эти managed зависимости; выбор provider не делает комплект минимальным и не разрешает выкидывать соседние зависимости. Основные packages: EF10.0.11, Npgsql10.0.3, SqlClient6.1.6, SQLitePCLRaw2.1.12, tokenizers/data2.0.0, Microsoft.Extensions10.0.11/Configuration Binder и Options.ConfigurationExtensions10.0.3, HttpClientLibrary FileVersion0.0.0.5, EFCoreLibrary0.0.5. Дополнительные Azure/MSAL/IdentityModel/BCL dependencies перечислены в actual deps/manifest, а не подбираются вручную.

| Native source | win-x64 | linux-x64 | linux-arm64 |
| --- | --- | --- | --- |
| SQLitePCLRaw.lib.e_sqlite3/2.1.12 | e_sqlite3.dll | libe_sqlite3.so | libe_sqlite3.so |
| Microsoft.Data.SqlClient.SNI.runtime/6.0.2 | Microsoft.Data.SqlClient.SNI.dll | Нет selected native asset | Нет selected native asset |
| Microsoft.Identity.Client.NativeInterop/0.20.6 | msalruntime.dll | libmsalruntime.so | Нет selected native asset |

SqlClient на Linux выбран из `runtimes/unix`, Windows — из `runtimes/win`; отсутствие Linux SNI DLL следует из actual SDK/package graph. Windows fallback не используется. Native MSAL/SQLite сохраняются ровно там, где их выбрал SDK. Package selection/PE/ELF checks не являются native loading, server/provider или authentication evidence.

## Бинарное подключение

Скопируйте **полный** выбранный комплект, например в `vendor/AgentBridge/SqlServer/linux-x64`. RID приложения должен совпадать с generated variant props:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
<Import Project="vendor/AgentBridge/SqlServer/linux-x64/AgentBridge.Delivery.props" />
```

Props использует свой `MSBuildThisFileDirectory`, импортирует generated RID/provider, подключает все managed DLL как `Reference Private=true`, доставляет XML и **все** native assets выбранного RID в корень output стандартными SDK items. При несовпадении RID Build завершается с понятной ошибкой. У binary Reference нет NuGet-восстановления транзитивных пакетов; нельзя копировать лишь пять AgentBridge DLL или смешивать kits/builds/RID. Потребитель формирует свой deps при обычном Build; supplied Delivery deps — evidence разрешения SDK graph, не deps приложения.

Culture satellites `Microsoft.Data.SqlClient.resources.dll` копируются из `resources/<culture>/` в `<culture>/` consumer output стандартными SDK items, сохраняя cs/de/es/fr/it/ja/ko/pl/pt-BR/ru/tr/zh-Hans/zh-Hant и filename case. Они не становятся прямыми корневыми assembly references. SDK/package hashes, assembly culture/version/PE metadata и полная resources closure входят в manifest/checks; flattening запрещён.

[Внешний compile-only Consumer](../../tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj) принимает `AgentBridgeDeliveryRoot` и явный `RuntimeIdentifier`; скопированные .cs/.csproj собираются вне всех repo с пятью kits. В нём нет ProjectReference/PackageReference/sourcepaths или entry point. [Регистрация](../../tests/Delivery/Consumer/SimpleRegistration.cs) использует actual `services.AddAgentBridge(configuration, httpClientFactory)`; app ILoggerFactory и client lifetime задаёт приложение, Individual source регистрируется до facade. [Полное руководство](25-usage-guide.md) и [facade](26-integration-registration.md) описывают эти обязанности.

## Конфигурация и внешние требования

`Database:Provider` и `Database:ConnectionString` обязательны; MSSQL выбирает `SqlServer` и migrations SqlServer. SQLite/PostgreSQL — явные альтернативы со своей migrations assembly, без fallback. Выбранные migrations и `__AgentBridgeMigrationsHistory` задаются runtime adapter. Наличие DLL и `AddAgentBridge` не запускают соединения, SQL, migrations, maintenance или cleanup.

Maintenance отдельно подключает `AddAgentBridgeDatabaseMaintenance(..., MaintenanceExecutionMode.SingleInitializer)`. App останавливает writes/DDL/другие экземпляры, предоставляет права/secrets/TLS, вызывает операцию и управляет retention. MSSQL backup path доступен **серверу БД**, не обязательно app filesystem. PostgreSQL требует отдельный toolchain/pg_dump с согласованным major, absolute paths и конечным cleanup timeout; для отдельного restore — совместимый pg_restore. SQLite требует обычный файловый main и соответствующие ограничения native backup. Подробности: [SQL Server](12-sql-server-provider.md), [maintenance](06-database-maintenance.md), [migrations](11-provider-migrations.md), [configuration](05-configuration-and-lifecycle.md).

## XML и проверенное поведение

Все пять AgentBridge assembly имеют generated XML. Доступные XML зависимостей копируются из того же Build/package graph без преобразования; отсутствующая XML у зависимости не выдумывается. Generated `<inheritdoc/>` не разворачивается компилятором в текст: metadata-тесты разрешают actual interface/implementation по Roslyn и проверяют русский summary контракта. Подтверждены ContextTokenCounter/IContextTokenCounter, CodexLbModelCatalog/IModelCatalog и DialogReader/IDialogReader во всех пяти kits.

Results15 фиксирует27 isolated metadata tests,0 failed/0 skipped: managed AssemblyRef closure/versions/tokenizer resources и culture satellites, manifest/hashes/SDK entries/case, native PE/ELF и wrong/mixed/missing controls, одинаковый public AgentBridge API/XML между MSSQL RID. Пять внешних consumers собраны с0 warnings/errors; DLL/XML/native/satellites output проверен побайтно. Исходники примеров и их методы не исполнялись.

Свежая общая union-регрессия относится к16. Actual Windows x64/Ubuntu x64/Linux ARM64 runtime/native/DI и app dependency conflicts — отдельный этап18; SQL/provider/backup/restore —17, live transport —19. Успешный Windows cross-build не закрывает ARM64 runtime gap.
