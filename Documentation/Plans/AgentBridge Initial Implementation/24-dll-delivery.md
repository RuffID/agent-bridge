# 24 — Подготовить поставку DLL

Статус: **Реализован, проверен в пределах compile/metadata и принят координатором** (2026-10-04). Зависимости: **23**. До приёмки add/commit не выполнялись; после неё разрешён локальный commit ровно24 файлов manifest. Этап25 этому исполнителю не поручен.

## Цель

Подготовить полный комплект библиотек для подключения к приложениям .NET 10 через ссылки на бинарные файлы.

## Задачи

- [x] Определить состав DLL ядра, адаптера codex-lb, выбранного хранилища и зависимостей времени выполнения.
- [x] Включить совместимые EFCoreLibrary, HttpClientLibrary и зависимости токенизатора по необходимости.
- [x] Включить generated XML; проверить summary интерфейсов и metadata-связи inheritdoc. Автоматическое отображение каждой IDE не подтверждено.
- [x] Документировать выбор провайдера SQLite/PostgreSQL и необходимую конфигурацию без публикации в NuGet.
- [x] Проверить, что потребитель может подключить комплект без путей к зависимостям относительно дерева исходного кода.

## Проверка и завершение

Проверить состав бинарных файлов и выполнить разрешённые проверки компиляции потребителя. Этап завершён, когда требования к зависимостям времени выполнения описаны явно, а сгенерированные DLL и выходные файлы не изменялись вручную.

Источник: [архитектура DLL](<../../Technical documentation/01-architecture.md>).

## Checkpoint и реализация

Все команды выполнялись с явным workdir `D:\Media\User\source\repos\agent-bridge`. Baseline: чистые master/tree/index, HEAD23=`7e9d533d80393bd85b08e1b17567038e12ec8a16`, parent22=`89c28e839bf12584027b81494c31557c91279935`. История21/20/19 соответствует передаче пользователя. Обязательные read-only библиотеки доступны и чистые: EFCoreLibrary0.0.5 HEAD=`3a8a53187af3c5df049770dfd6727b5065159d1f`, HttpClientLibrary FileVersion0.0.0.5 HEAD=`6d0528d940d1d8494c722c22464051dd961d6bf7`.

Прочитаны applicable AGENTS,23–25, actual csproj/imports, specification, документация и используемые контракты. [OpenSpec change](../../../openspec/changes/dll-delivery/proposal.md) создан до реализации. Root csproj неизменён: Documentation исключено из None; root slnx сохранён на месте, добавлен только24 technical document в существующие solution items. Production API/schema/migrations и исходники соседних библиотек не менялись.

Добавлены стандартный SDK aggregation project, локальный props и compile-only consumer, а также isolated metadata test project. Новых targets/Exec/scripts нет. Aggregation включает CodexLb и выбранную migrations assembly, получает общую closure NuGet/MSBuild, не распространяет private EF Design. Сам aggregation output не является частью поставки.

## Комплекты

[Полная таблица39 DLL, конфигурация, внешние требования, XML и подключение](<../../Technical documentation/24-dll-delivery.md>).

| Каталог от корня | Состав |
| --- | --- |
| `artifacts/delivery/stage24-win-x64/Sqlite` |39 managed DLL,35 XML,1 native DLL,props,3 evidence JSON,manifest |
| `artifacts/delivery/stage24-win-x64/PostgreSql` |Тот же состав, SQLite migrations заменена PostgreSQL migrations |

По80 файлов на комплект. `delivery.manifest.json` содержит79 записей (не включает самого себя), размеры/SHA256, assembly/file versions и package/project origins. Для DLL package origin подтверждён сравнением с NuGet asset; все DLL также сверены с build output. EFCoreLibrary DLL имеет assembly/file0.0.5.0; HttpClientLibrary — assembly1.0.0.0 и file0.0.0.5, поэтому AssemblyVersion HTTP нельзя выдавать за FileVersion.

Общий persistence adapter статически ссылается на оба провайдера и оба maintenance-модуля: обе closure сохраняются в каждом варианте, а AgentBridge migrations assembly ровно одна. Tokenizer содержит две embedded resource DLL2.0.0 и Google.Protobuf3.30.2/Bcl dependencies. Native `e_sqlite3.dll`2.1.12 — win-x64, PE AMD64. Другие RID не объявляются подготовленными.

## Точные build/test команды

Проверка инструментов: `Get-Command openspec,dotnet -ErrorAction SilentlyContinue`; OpenSpec не найден, dotnet доступен. `dotnet --version` → **10.0.401**. Concrete projects и ancestor/generated package imports проверены до build. Для restore использован только официальный NuGet source, NuGet audit отключён явно; это не security audit.

Для `$provider='Sqlite'`, затем `$provider='PostgreSql'` **последовательно** выполнены:

```powershell
dotnet restore tests\Delivery\Build\AgentBridge.Delivery.csproj -p:DeliveryProvider=$provider -p:GeneratePackageOnBuild=false -p:NuGetAudit=false --source https://api.nuget.org/v3/index.json --verbosity minimal
dotnet build tests\Delivery\Build\AgentBridge.Delivery.csproj -c Debug --no-restore -p:DeliveryProvider=$provider -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\stage24-$provider\ -m:1 --verbosity minimal
```

Каждый build:10 projects, **0 warnings/0 errors**. Это Debug Build, не Rebuild, pack или publish. Нужен restore при смене provider, общий obj используется последовательно. `GeneratePackageOnBuild=false` передан во всей цепочке, включая EFCoreLibrary. Output соседних библиотек — их игнорируемые `artifacts/stage24-<provider>`, исходники read-only.

Комплекты подготовлены штатными `New-Item`/`Copy-Item` в shell, без запуска сохранённого скрипта. Суть фактически выполненной операции для обоих provider (пустой новый destination):

```powershell
$root=Join-Path (Get-Location) 'artifacts\delivery\stage24-win-x64'
$source=Join-Path (Get-Location) "tests\Delivery\Build\artifacts\stage24-$provider\Debug\net10.0\win-x64"
$destination=Join-Path $root $provider
New-Item -ItemType Directory -Path (Join-Path $destination 'lib'),(Join-Path $destination 'native\win-x64'),(Join-Path $destination 'evidence') -Force
Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in '.dll','.xml' -and $_.BaseName -notin 'AgentBridge.Delivery','e_sqlite3' } | Copy-Item -Destination (Join-Path $destination 'lib')
Copy-Item -LiteralPath (Join-Path $source 'e_sqlite3.dll') -Destination (Join-Path $destination 'native\win-x64')
Copy-Item -LiteralPath 'tests\Delivery\AgentBridge.Delivery.props' -Destination $destination
Copy-Item -LiteralPath (Join-Path $source 'AgentBridge.Delivery.deps.json') -Destination (Join-Path $destination 'evidence')
$deps=Get-Content -LiteralPath (Join-Path $source 'AgentBridge.Delivery.deps.json') -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$target=$deps.targets[$deps.runtimeTarget.name]
foreach($entry in $target.GetEnumerator()) {
    $library=$deps.libraries[$entry.Key]
    if($library.type -ne 'package'){continue}
    foreach($asset in $entry.Value.runtime.Keys) {
        $xmlPath=Join-Path (Join-Path 'C:\Users\Spike\.nuget\packages' $library.path) ([IO.Path]::ChangeExtension($asset,'.xml'))
        if(Test-Path -LiteralPath $xmlPath) { Copy-Item -LiteralPath $xmlPath -Destination (Join-Path $destination 'lib') }
    }
}
```

Необязательные XML копируются только при наличии в исходном package. Это не fallback для DLL: отсутствие runtime dependency завершает проверку ошибкой. Generated DLL/XML/deps не редактировались. Manifest сгенерирован из `Get-ChildItem -Recurse -File`, `Get-FileHash -Algorithm SHA256`, `AssemblyName.GetAssemblyName` (чтение metadata), `FileVersionInfo` и исходного deps через `ConvertTo-Json`; перечисление payload выполнено до записи самого manifest. Ручного изменения generated outputs нет.

Consumer + комплект скопированы в новый внешний каталог:

`C:\Users\Spike\AppData\Local\Temp\AgentBridge-stage24-b3e12e3dc9ae4b52b8c9c10b24111959`

В каждом `$provider` находятся `Consumer/` и `kit/`. Ancestor build/config files этого каталога проверены; дополнительных imports нет. Для обоих вариантов выполнены:

```powershell
$external=Get-Content artifacts\delivery\stage24-win-x64\consumer-location.txt -Raw -Encoding UTF8
$project=Join-Path $external "$provider\Consumer\AgentBridge.BinaryConsumer.csproj"
$kit=Join-Path $external "$provider\kit"
dotnet restore $project "-p:AgentBridgeDeliveryRoot=$kit" -p:GeneratePackageOnBuild=false -p:NuGetAudit=false --source https://api.nuget.org/v3/index.json --verbosity minimal
dotnet build $project -c Debug --no-restore "-p:AgentBridgeDeliveryRoot=$kit" -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet msbuild $project -t:ResolveReferences "-p:AgentBridgeDeliveryRoot=$kit" -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -getItem:ReferencePath,ProjectReference,PackageReference -getProperty:TargetFramework,RuntimeIdentifier,OutputType -verbosity:quiet
```

Оба restore/build успешны, **0 warnings/0 errors**. В каждом receipt206 references:39 из внешнего `kit/lib`,167 из `Microsoft.NETCore.App.Ref`; ProjectReference/PackageReference пусты. В restored assets нет package/project libraries. Ссылок на исходные репозитории/соседние библиотеки/packages cache в resolved compile references нет.39 managed DLL +35 XML +native file в consumer output сверены с kit через SHA256; все75 совпадают. Разрешён только compile, методы consumer не вызывались.

Изолированная PE/XML проверка:

```powershell
dotnet restore tests\Delivery\Metadata\AgentBridge.Delivery.Metadata.Tests.csproj -p:GeneratePackageOnBuild=false -p:NuGetAudit=false --source https://api.nuget.org/v3/index.json --verbosity minimal
dotnet build tests\Delivery\Metadata\AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
$env:AGENTBRIDGE_DELIVERY_ROOT=Join-Path (Get-Location) 'artifacts\delivery\stage24-win-x64'
dotnet test tests\Delivery\Metadata\AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ --logger 'trx;LogFileName=delivery-metadata.trx' --results-directory artifacts\test-results\stage24 --verbosity minimal
```

Итоговый metadata build:0 warnings/errors. TRX `artifacts/test-results/stage24/delivery-metadata.trx`: **8 passed /0 failed /0 skipped**.2 PE closure/native-resource checks и6 Roslyn XML/interface-link checks. Это новый адресный набор; старые23 и предшествующие suites не повторялись, результаты не суммируются.

## Первоначальные ошибки и ограничения

- Read/discovery: Windows wildcard `rg ...\*.cs`/`*.props` и guessed имя `SqliteDesignTimeDbContextFactory.cs` дали ошибки чтения. Фактические файлы найдены через listing; это не build/test failures.
- Первый metadata build: **CS0103** (`XmlDocumentationProvider` находится в Workspaces.Common), **CS8714** nullable method-group key и **xUnit2031** Where перед Assert.Single. Добавлена прямая test-only ссылка `Microsoft.CodeAnalysis.Workspaces.Common5.0.0`, method group заменён typed lambda, выбран predicate overload. Restore/build повторены, итог0/0. Production и готовые комплекты этим исправлением не менялись. Tests до исправления build не запускались; test failures не было.
- Печатный diagnostic итог bytes при генерации manifest оказался пустым из-за Measure-Object над hashtable. Сами записи размера взяты из FileInfo.Length; проверены отдельно вместе с hash/count. Это не отсутствие размеров в manifest.
- **Runtime/native загрузка не проверена**: PE AMD64/наличие ресурсов/compile/копирование не запускают SQLite, BPE, EF, DI, HTTP или pg_dump. Ни приложение, hosting/TestServer/WebApplicationFactory, Docker, SQL, рабочие DB/secrets/data/volumes, ни интеграции23 не запускались.
- XML inheritdoc не развёрнут компилятором; metadata-связь и русский текст контракта доступны, но отображение конкретной IDE не проверено. EFCoreLibrary CRUD XML не генерируется самим read-only проектом; три SQLitePCLRaw managed assets также без XML. Описания не выдумывались.
- Комплект **только win-x64/Debug/net10.0**; другие RID, Release, trimming/AOT/single-file и runtime conflict с пакетами существующего приложения не проверены. Полный integration usage guide — этап25.
- **OpenSpec CLI отсутствует**: CLI validation не выполнена, только static main/delta review. Change не архивирован, инструменты не устанавливались.

## Полный manifest исходных изменений — 24 файла

Приёмка координатора: независимо проверены actual code/build props, metadata tests, документы, diff/manifest24, оба manifests (79+1 файлов с совпадающими SHA256/размерами), TRX8/0/0 и receipts206=39kit+167framework без ProjectReference/PackageReference. После приёмки разрешено обновить только статусы и сделать локальный English Conventional Commit explicit manifest24, без повторного запуска успешных проверок. Parent должен быть `7e9d533d80393bd85b08e1b17567038e12ec8a16`, branch master; full hash, parent, состав и post-commit status/index проверяются и передаются итоговым ответом.25 выполняет отдельный исполнитель после проверки коммита24 координатором.

Финальный static audit: все24 файла UTF-8 без BOM/LF, U+FFFD/четыре вопросительных знака/проверенные mojibake markers отсутствуют;231 локальная Markdown-ссылка ведёт на существующий путь. Main/delta requirements текстуально совпадают (не CLI validation). `git diff --check` успешен; Git предупреждает о возможном будущем LF→CRLF из-за autocrlf, фактические файлы сохранены в LF. Ordinary diff просмотрен, staged diff пуст. Root csproj и файл25 без diff; solution items проверены. Обе соседние библиотеки clean на прежних HEAD, AgentBridge HEAD23/master сохранён.

Оба manifest проверены против фактического полного набора файлов, каждого размера и SHA256;35 XML assembly identities совпадают с именами DLL. Payload без самого manifest: SQLite32 500 234 байта, PostgreSQL32 501 650 байт. Внешние consumer assets имеют пустой package/project graph. В Git status только24 исходных файла ниже, generated outputs отсутствуют.

```text
AGENTS.md
README.md
agent-bridge.slnx
Documentation/README.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/Plans/AgentBridge Initial Implementation/23-cross-component-verification.md
Documentation/Plans/AgentBridge Initial Implementation/24-dll-delivery.md
Documentation/Technical documentation/README.md
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/24-dll-delivery.md
openspec/changes/dll-delivery/proposal.md
openspec/changes/dll-delivery/tasks.md
openspec/changes/dll-delivery/context.md
openspec/changes/dll-delivery/specs/agent-runtime/spec.md
openspec/specs/agent-runtime/spec.md
openspec/specs/agent-runtime/context.md
tests/AGENTS.md
tests/Delivery/AGENTS.md
tests/Delivery/AgentBridge.Delivery.props
tests/Delivery/Build/AgentBridge.Delivery.csproj
tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj
tests/Delivery/Consumer/BinaryContractProbe.cs
tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj
tests/Delivery/Metadata/DeliveryMetadataTests.cs
```

Ignored artifacts не входят в Git manifest. Сохранены оба готовых комплекта с manifests/evidence, TRX, стандартные build outputs и внешний compile-only каталог; там нет secrets/DB/процессов. Общий `consumer-location.txt` сохраняет путь к внешнему evidence. Никакие ресурсы23 не создавались заново. Координатор независимо принял24 и разрешил локальный English Conventional Commit ровно24 файлов manifest; перед stage index пуст. Full hash и parent передаются итоговым ответом, собственный будущий hash в документ не вставляется. **После commit24 остановиться;25 продолжит отдельный исполнитель координатора.**
