# Поставка DLL для .NET10

Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). Статус24: реализован, compile/metadata проверены, принят координатором; локальный commit manifest24 разрешён. [Команды, результаты, ошибки и manifest изменений](<../Plans/AgentBridge Initial Implementation/24-dll-delivery.md>). Этап25 этому исполнителю не поручен; продолжение ведёт координатор.

## Граница комплекта

Локальные каталоги от корня AgentBridge:

- `artifacts/delivery/stage24-win-x64/Sqlite`
- `artifacts/delivery/stage24-win-x64/PostgreSql`

Каждый комплект рассчитан на SDK-style `net10.0`, **win-x64**, framework-dependent приложение и установленный подходящий .NET10 runtime. Для WPF/ASP.NET Core приложение отдельно выбирает свой SDK/shared framework. .NET runtime, host, Serilog/sinks, pg_dump, secrets и приложение в комплект не входят. Другие RID, trimming, single-file и NativeAOT не проверялись; Windows native DLL нельзя переносить на другую ОС/архитектуру.

```text
<kit>/
  AgentBridge.Delivery.props
  delivery.manifest.json
  lib/                          39 managed DLL + 35 XML
  native/win-x64/e_sqlite3.dll   1 native DLL
  evidence/
    AgentBridge.Delivery.deps.json
    AgentBridge.BinaryConsumer.deps.json
    consumer-references.json
```

Manifest перечисляет79 файлов с SHA256/размером, assembly/file versions и package/project происхождением DLL; сам manifest —80-й файл, без рекурсивного self hash. Evidence содержит сведения локальной проверки, включая абсолютные локальные пути. Эти пути не участвуют в подключении приложения. `AgentBridge.Delivery.deps.json` — исходное evidence разрешения зависимостей, **не deps приложения**. Приложение создаёт свой deps стандартным Build; готовый consumer deps также только evidence.

## Полный состав managed DLL

В таблице указаны имена без `.dll`. XML с тем же именем расположен рядом, кроме четырёх явно отмеченных файлов. В обеих поставках одинаковая closure; различается migrations DLL.

| DLL | Версия / роль |
| --- | --- |
| AgentBridge | 1.0.0.0; ядро, Application, offline tokenizer |
| AgentBridge.CodexLb | 1.0.0.0; actual HttpClientLibrary transport |
| AgentBridge.Persistence.EfCore | 1.0.0.0; общий EF adapter |
| AgentBridge.Persistence.Migrations.Sqlite **либо** AgentBridge.Persistence.Migrations.PostgreSql | 1.0.0.0; выбранная текущая схема, включая durable journal/settings/provenance |
| EFCoreLibrary | 0.0.5, assembly/file0.0.5.0; XML исходный проект не генерирует |
| EFCoreLibrary.Maintenance | 1.0.0.0; общий coordinator |
| EFCoreLibrary.Maintenance.Sqlite | 1.0.0.0; native backup adapter |
| EFCoreLibrary.Maintenance.PostgreSql | 1.0.0.0; pg_dump adapter |
| HttpClientLibrary | FileVersion0.0.0.5, AssemblyVersion1.0.0.0 |
| Microsoft.ML.Tokenizers | package2.0.0 |
| Microsoft.ML.Tokenizers.Data.O200kBase | package2.0.0; embedded dictionary |
| Microsoft.ML.Tokenizers.Data.Cl100kBase | package2.0.0; embedded dictionary |
| Google.Protobuf | package3.30.2; tokenizer dependency |
| Microsoft.Bcl.AsyncInterfaces | package9.0.4 |
| Microsoft.Bcl.HashCode | package6.0.0 |
| Microsoft.Bcl.Memory | package10.0.4 |
| Microsoft.Data.Sqlite | Microsoft.Data.Sqlite.Core10.0.11 |
| Microsoft.EntityFrameworkCore | package10.0.11 |
| Microsoft.EntityFrameworkCore.Abstractions | package10.0.11 |
| Microsoft.EntityFrameworkCore.Relational | package10.0.11 |
| Microsoft.EntityFrameworkCore.Sqlite | Microsoft.EntityFrameworkCore.Sqlite.Core10.0.11 |
| Npgsql | package10.0.3 |
| Npgsql.EntityFrameworkCore.PostgreSQL | package10.0.3 |
| SQLitePCLRaw.batteries_v2 | bundle_e_sqlite3 2.1.12; XML в пакете отсутствует |
| SQLitePCLRaw.core | package2.1.12; XML отсутствует |
| SQLitePCLRaw.provider.e_sqlite3 | package2.1.12; XML отсутствует |
| Microsoft.Extensions.Caching.Abstractions | package10.0.11 |
| Microsoft.Extensions.Caching.Memory | package10.0.11 |
| Microsoft.Extensions.Configuration | package10.0.3 |
| Microsoft.Extensions.Configuration.Abstractions | package10.0.11 |
| Microsoft.Extensions.Configuration.Binder | package10.0.3 |
| Microsoft.Extensions.DependencyInjection | package10.0.11 |
| Microsoft.Extensions.DependencyInjection.Abstractions | package10.0.11 |
| Microsoft.Extensions.DependencyModel | package10.0.11 |
| Microsoft.Extensions.Logging | package10.0.11 |
| Microsoft.Extensions.Logging.Abstractions | package10.0.11 |
| Microsoft.Extensions.Options | package10.0.11 |
| Microsoft.Extensions.Options.ConfigurationExtensions | package10.0.3 |
| Microsoft.Extensions.Primitives | package10.0.11 |

Native asset — `SQLitePCLRaw.lib.e_sqlite3/2.1.12`, исходный package path `runtimes/win-x64/native/e_sqlite3.dll`. Вместе с `batteries_v2`, provider и core он поставляется **в обоих** вариантах. Общий EF-адаптер уже имеет статические ссылки на оба provider/maintenance-модуля; выбор PostgreSQL не разрешает вручную удалять SQLite-зависимости. Он также не требует запуска SQLite при работе PostgreSQL. Разделение этих зависимостей — отдельное изменение архитектуры, не24.

MSBuild разрешил общие Microsoft.Extensions зависимости по общему графу, поэтому нужно копировать комплект целиком, не смешивая DLL из прежних builds. SQL Server/MySQL, EF Design, Roslyn, xUnit, testhost и служебная `AgentBridge.Delivery.dll` не поставляются. PDB не нужны для подключения и не включены.

## Бинарное подключение

Скопировать выбранный комплект в каталог приложения, например `vendor/AgentBridge`. Импортировать поставляемый props **после PropertyGroup**:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
<Import Project="vendor/AgentBridge/AgentBridge.Delivery.props" />
```

Props использует только свой `MSBuildThisFileDirectory`: managed DLL становятся `Reference` с `Private=true`, XML копируются в output, native файл попадает в корень output как `e_sqlite3.dll`. Никакие пути к AgentBridge/EFCoreLibrary/HttpClientLibrary source tree и packages cache потребителю не нужны. `HintPath` на исходные проекты не используется. Не копировать только четыре AgentBridge DLL: у binary Reference нет NuGet-механизма восстановления транзитивных пакетов.

Проверяемый [compile-only проект](../../tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj) принимает абсолютный `AgentBridgeDeliveryRoot` как параметр Build. Его [BinaryContractProbe](../../tests/Delivery/Consumer/BinaryContractProbe.cs) компилирует реальные DI/options/maintenance и публичные типы. Это библиотека без entry point, не полный composition root и не пример диалога этапа25. Методы не исполнялись. В приложении остаются logging, HttpClient lifecycle, IIndividualModelKeySource, ordered context providers, validators/handlers и права пользователя.

## Конфигурация и внешние требования

| Параметр | SQLite | PostgreSQL |
| --- | --- | --- |
| `DatabaseOptions.Provider` | `DatabaseProvider.SQLite` | `DatabaseProvider.PostgreSql` |
| `ConnectionString` | Файловая БД; доступный каталог и права приложения | Сервер, database, user и параметры приложения; secret не в комплекте |
| Migrations assembly | `AgentBridge.Persistence.Migrations.Sqlite` | `AgentBridge.Persistence.Migrations.PostgreSql` |
| Native/сервер | Поставляемая x64 SQLite DLL, обычный filesystem main | Доступный PostgreSQL; SQLite assets сохраняются из-за общей closure |
| Backup | Native SQLite backup API, без sqlite CLI | Отдельно установленный полный PostgreSQL client toolchain с pg_dump |

Сначала `AddDatabaseConfiguration`, затем `AddAgentBridgePersistence`. Provider обязателен, fallback на SQLite отсутствует. Runtime сам задаёт выбранную migrations identity и собственную history table `__AgentBridgeMigrationsHistory`; отсутствующая migrations DLL не заменяется общей сборкой. Наличие DLL не запускает подключение/миграции.

Maintenance подключается отдельным `AddAgentBridgeDatabaseMaintenance(..., MaintenanceExecutionMode.SingleInitializer)`. Обязательны абсолютный `BackupDirectory` и явный положительный `BackupRetentionPeriod`; cleanup/retention выполняет приложение. PostgreSQL дополнительно требует `PostgreSqlDumpExecutablePath` (абсолютный путь, без поиска PATH), `PostgreSqlServerMajorVersion` (10+, фактический major dump/server должен совпасть) и конечный положительный `PostgreSqlCleanupTimeout`.

PostgreSQL maintenance поддерживает стабильный прямой TCP endpoint и TLS `Disable` либо `VerifyFull` с CA; connection whitelist Npgsql/libpq ограничен actual EFCoreLibrary. Права CONNECT/backup и при явном создании CREATEDB/эквивалентные права предоставляет администратор. Backup custom-format относится к выбранной БД, не к global roles. `pg_dump.exe` и зависимые libpq/прочие DLL поставляет PostgreSQL toolchain, они не входят в AgentBridge. Для отдельной операции восстановления нужен совместимый `pg_restore`; текущий AgentBridge maintenance не предоставляет автоматический restore API.

SQLite maintenance ограничен обычным файловым main: не memory/URI/custom VFS/encryption/attachments/reparse points. Приложение останавливает writes/DDL/другие экземпляры, выделяет scope и явно вызывает `IDatabaseMaintenance<AgentBridgeContextKey>`; конфигурация и регистрация этого не делают. Детали: [maintenance](06-database-maintenance.md), [provider migrations](11-provider-migrations.md), [configuration](05-configuration-and-lifecycle.md). Процессы, серверы, БД и секреты в24 не использовались.

## XML и inheritdoc

Все четыре выбранные AgentBridge assembly имеют соседние generated XML, также включены XML HttpClientLibrary и трёх maintenance-модулей. XML пакетов копируются из того же package/runtime asset каталога без преобразований; всего35 XML. EFCoreLibrary0.0.5 не включает `GenerateDocumentationFile` и многие её CRUD API не имеют source summary: описания для них не выдумываются, read-only библиотека не менялась. Три SQLitePCLRaw managed файла также не предоставляют XML.

Компилятор C# сохраняет `<inheritdoc/>` в XML, **не разворачивает** его в текст. Шесть checks (по3 на комплект) читают Roslyn symbols и XML только из DLL-комплекта: `ContextTokenCounter → IContextTokenCounter`, `CodexLbModelCatalog → IModelCatalog`, `DialogReader → IDialogReader`. Проверяются типы, все методы соответствующего интерфейса, реальные metadata-связи, inheritdoc реализации и русский summary интерфейса. Контракты доступны без исходников, в том числе между сборками.

Это не подтверждение автоматического показа унаследованного текста во всех IDE. IDE/documentation renderer должен поддерживать inheritdoc и разрешение интерфейсов по metadata; иначе читать описание через интерфейс. XML вручную не разворачивались и не редактировались.

## Что подтверждено

Оба комплекта собраны в Debug, .NET SDK10.0.401. Два отдельных внешних потребителя собраны с39 DLL комплекта и .NET reference pack; source/package references отсутствуют. SHA256 всех39 DLL совпадает с build output, package DLL/native — с исходным NuGet asset. Копирование39 managed DLL,35 XML и native DLL в output потребителя проверено побайтно.

Два PE checks проверили closure AssemblyRef и достаточные assembly versions, отсутствие design/test assemblies, selected migrations, embedded tokenizer resources и формат native PE AMD64. Вместе с6 XML checks: **8 passed /0 failed /0 skipped**. Ни один из этих checks не выполняет AgentBridge, tokenizer или native code; runtime resolution, SQLite initialization, dump, DI runtime, HTTP и БД этими результатами не доказаны. Старые наборы23 не повторялись.
