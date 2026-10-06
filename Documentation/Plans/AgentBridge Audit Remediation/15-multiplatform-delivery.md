# 15 — Поставка win-x64, linux-x64 и linux-arm64

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **принят в локальной A/B-границе**. Зависимости: 12–14. Область: delivery composition/metadata/consumer, без автоматического запуска.

## Цель и исходное состояние

Подключение AgentBridge на .NET10 к трём платформам приложения, основной provider MSSQL. Сейчас delivery Build проект и native/PE checks рассчитаны на win-x64, а PostgreSQL-kit также содержит статически связанные SQLite assets. Такие native DLL нельзя переносить на Linux.

## Работы

1. Сделать RID явным параметром delivery assembly resolution и manifests: поддержать win-x64/linux-x64/linux-arm64 без fallback на Windows assets. Сохранить framework-dependent DLL model; .NET runtime устанавливает приложение.
2. Выделить SQL Server kit в каждой из трёх RID комбинаций, с actual integration facade14/migrations/dependency closure. Existing SQLite/PostgreSQL paths сохранять и проверять адресно; не выдавать их новую cross-platform матрицу за автоматически проверенную.
3. Сверить actual package/runtime assets всего графа, включая транзитивные SqlClient/SQLite зависимости. Не удалять statically referenced DLL/native assets вручную ради меньшего комплекта; разделение provider modules — отдельный согласованный шаг, если оно действительно требуется.
4. Генерировать managed/XML/native props и manifests для каждого RID. Пути и filename case должны работать на Linux; не встраивать absolute Windows paths. Корректно классифицировать PE managed metadata и PE/ELF native assets, сохраняя architecture verification.
5. Подготовить compile-only binary consumer для каждого kits/RID с общей registration14 и IConfiguration13. Не исполнять примеры при этой проверке; ARM64 artifact resolution на Windows не считать ARM64 runtime acceptance.
6. Обновить техническое руководство DLL, usage guide и README после фактической реализации. Указывать separate app RID/dependency graph и незапущенный runtime18. AOT, trimming и single-file не входят в автоматически согласованный объём.

## Проверки

B в16: актуальные assets, hashes, dependency closure, XML и binary consumer для трёх MSSQL kits. Restore может использовать сеть и требует правил/разрешения; production packaging scripts не запускать автоматически.

Runtime в18: actual Windows x64, Ubuntu x64 и Linux ARM64 среды; подключение к разрешённому remote SQL Server отдельно17/19. Отсутствующая ARM64 машина остаётся runtime gap, а не successful cross-platform выводом.

## Критерии завершения

Три корректных комплекта с совпадающими managed APIs и платформенными dependencies; compile-only consumers подтверждены. Нет смешения PE/ELF и архитектур, stale manifests или ложного обещания SQL Server Engine ARM64.

## Результаты

### Область и baseline

2026-10-06, Asia/Novosibirsk. Реализован только этап15 в локальной B-границе; результат ожидает приёмки координатором, commit не создавался. Исходное задание выше сохранено. До паузы выполнялись только чтение/preflight; первая apply_patch попытка отклонена до применения (duplicate target в patch). После прямого «Продолжай» и допуска координатора работа возобновлена в том же чате. Стартовый и текущий AgentBridge HEAD `3286b12886c34ac4b2ba9d4523fee22c3949ab0a`; baseline содержал только чужой Coordination.md. Index не менялся.

Прочитаны root/tests/Delivery/Documentation/Plans AGENTS, stage15/README/Decisions, принятые predecessor12–14 Results, относящиеся Findings/итог15 и main OpenSpec, current guide/consumer/Delivery source. Применён csharp-project-rules: style/build-validation/agents-maintenance. Storage/UoW/production API не менялись; backend/persistence skill не требовался для изменения delivery composition. Проверен concrete SDK graph14 проектов (Integration/core/CodexLb/EF adapter, HTTP, EF CRUD/четыре maintenance, три выбранные migrations, Delivery); test/consumer увеличивают snapshot до16 проектов.

Соседи read-only, собственные manifests пусты: EFCoreLibrary `5962deceb6ea01306cbbda400040db88ea8e5df9`, HttpClientLibrary `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`; оба чистые на выходе. Codex-lb `f2b8e042c4ce012ae703bc939413bf9961b00032` сохраняет foreign `.vs/`. Coordination принадлежит координатору и исключён из manifest. Ветки/worktrees/Git mutations/соседние исходники не изменялись.

### Exact manifest

Единственный writable repo — AgentBridge,18 исходных файлов; generated kit/assets/DLL/XML/JSON/TRX/logs только в ignored artifacts, external copies вне repo:

```text
AGENTS.md
README.md
agent-bridge.slnx
openspec/specs/agent-runtime/spec.md
Documentation/Plans/AgentBridge Audit Remediation/README.md
Documentation/Plans/AgentBridge Audit Remediation/15-multiplatform-delivery.md
Documentation/Technical documentation/24-dll-delivery.md
Documentation/Technical documentation/25-usage-guide.md
tests/AGENTS.md
tests/Delivery/AGENTS.md
tests/Delivery/AgentBridge.Delivery.props
tests/Delivery/Build/AgentBridge.Delivery.csproj
tests/Delivery/Build/Assemble-Delivery.ps1
tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj
tests/Delivery/Metadata/DeliveryMetadataTests.cs
tests/Delivery/Metadata/DeliveryManifestTests.cs
tests/Delivery/Metadata/NativeAssetMetadata.cs
tests/Delivery/Metadata/NativeAssetMetadataTests.cs
```

В slnx Metadata — обычный isolated test project; parameterized Build/Consumer представлены File items, чтобы solution не требовала kit/provider/RID ради обычной компиляции. Новые tests не попадают в core compile glob. Current DLL guide обновлён под фактический граф; historical Initial Implementation/audit reports/Decisions не переписаны.

### Before/after и устройство

Before из source: win-x64 зафиксирован в Build/Consumer, Delivery не ссылался на Integration, два provider variants, props копировал только одну SQLite native DLL. Managed closure/API/XML проверки были для SQLite/PostgreSQL, PE-only native checks.

After: явные три RID и SqlServer/Sqlite/PostgreSql; Error-only Build target проверяет variant, generated variant props фиксирует выбранный RID/provider и consumer target отклоняет несовпадение. SDK Build разрешает Integration + selected migrations graph; helper копирует весь выбранный runtime/native/resources graph, available XML и фиксирует hashes/provenance. Никаких publish/pack/Exec/executable hooks, manual dependency pruning либо module split. SDK aliases одной DLL допускаются только по kind/SHA и сохраняют обе provenance; конфликт отвергается.

SqlClient6.1.6 выбирает `runtimes/win/lib/net9.0` на Windows и `runtimes/unix/lib/net9.0` на Linux. SNI6.0.2 даёт только Windows native; SQLite2.1.12 native есть в каждом kit; MSAL NativeInterop0.20.6 — win-x64/linux-x64, ARM64 native asset SDK не выбрал. Общий EF adapter сохраняет статические ссылки всех трёх providers/maintenance; kits не обещают минимальный независимый provider module.

Координатор обнаружил пропуск в первой реализации: SDK deps содержал13 SqlClient culture satellites, которые helper/props не доставляли. Различающий new test на сохранённых stage15 kits: `stage15-resources-before.trx`, Exit1,Passed0/Failed5/Skipped0 — каждый variant терял `lib/net9.0/cs/Microsoft.Data.SqlClient.resources.dll`. Исправление сохраняет `resources/<culture>/` и копирует в `<culture>/` consumer output без root Reference/flattening. cs/de/es/fr/it/ja/ko/pl/pt-BR/ru/tr/zh-Hans/zh-Hant проверены по actual SDK/package hash, PE metadata/culture, exact case и missing/hash negative controls. After — новые stage15-v2 kits; старые artifacts не удалены и не принимаются как final closure.

### Final kits, hashes и источник

| Kit в artifacts/delivery/stage15-v2 | lib DLL | XML | Satellites | Native | Manifest entries |
| --- | --- | --- | --- | --- | --- |
| SqlServer/win-x64 | 64 | 57 | 13 | e_sqlite3.dll; Microsoft.Data.SqlClient.SNI.dll; msalruntime.dll | 140 |
| SqlServer/linux-x64 | 63 | 57 | 13 | libe_sqlite3.so; libmsalruntime.so | 138 |
| SqlServer/linux-arm64 | 63 | 57 | 13 | libe_sqlite3.so | 137 |
| Sqlite/win-x64 | 64 | 57 | 13 | те же3 Windows native | 140 |
| PostgreSql/win-x64 | 64 | 57 | 13 | те же3 Windows native | 140 |

Counts не включают manifest self hash; props2 и SDK deps1 входят в entries. Основные packages: EF10.0.11/Npgsql10.0.3/SqlClient6.1.6/SQLitePCLRaw2.1.12/tokenizers/data2.0.0; common extensions10.0.11, Configuration/Binder/Options.ConfigurationExtensions10.0.3; EF0.0.5/HTTP FileVersion0.0.0.5. Полный package graph, versions и hashes — actual deps/manifest, включая Azure/MSAL/IdentityModel/BCL и13 satellites. Package DLL/native/resources hashes сверены с cache originals; project DLL hashes — с fresh SDK output.

| delivery.manifest.json | SHA256 |
| --- | --- |
| SqlServer/win-x64 | e6938947a9e4cff07361665809dd8a3d9a7fe75dffe7b312b5bf63f8579f8707 |
| SqlServer/linux-x64 | a79e56b81bbe22e29c20ec5c0f75b32b04616ff22718c3ccb46519f03220c0dd |
| SqlServer/linux-arm64 | 8724ef435c425469548f6b359f8cfc06d3854777c50c7e4f6cf037f93125561c |
| Sqlite/win-x64 | c536969105b1e4a7408cda12bdd215015c3d2e6223d177fbb08b4a7b8b0aeddc |
| PostgreSql/win-x64 | 2d3d7dad10a62f2f562c6c9727bc9aae3970ca3db2b3790771ef05cbca3d144f |

SDK `dotnet --version` actual10.0.401. Manifest содержит base Git revisions (stage15 ещё не закоммичен), assets/generator SHA, file sizes/versions/origin; source snapshot дополнительно фиксирует actual рабочие inputs: `artifacts/stage15/source-snapshot.json`,16 projects/341 files,SHA256 `8f88dc5439e8dc5887cdca4a448a0b296e0ca849152550f4ca9d2fa3f7e83321`. Generator SHA256 `be36909cd70e77311e202f8cd004d6bdc5beb230dedd55416d68632e95046270`. Pinned final assets/dgspec каждого variant сохранены в `artifacts/stage15/inputs-final/<Provider>-<RID>/`; их assets hashes совпадают с manifest. Absolute sourcepaths допустимы только в таком отдельном локальном evidence, не kit props/manifest/deps.

### Preflight, permissions и точные команды

Concrete csproj, ancestor build/props/targets/NuGet/lock names и source imports проверены; исходные hooks/Exec не добавлялись. HTTP Directory.Build.props меняет только свой test output; EF GeneratePackageOnBuild=true подавлен false во всей цепочке. New targets содержат только Error validation и покрыты прямым пользовательским permission. NuGet package versions не изменялись; restore только local source `C:/Users/Spike/.nuget/packages`, NuGetAudit=false, сетевых sources не добавлено.

Первый offline restore win-x64 без TargetFramework/SelfContained properties дал NU1102 по net8 runtime packs HTTP multitarget graph; сборка после failure не запускалась. Ограничение compile-only graph на net10.0 и SelfContained=false исправило restore. Первый Linux offline restore дал NU1101 по отсутствующим host/runtime packs. Проверенные стандартные SDK switches EnableRuntimePackDownload=false/EnableAppHostPackDownload=false исключили скачивание **не поставляемого app .NET runtime/host** для framework-dependent library. Package runtime/native/resources resolution не отключён и не урезан; Linux assets взяты из current package graph, без Windows fallback и без сети.

Actual userMessage `01a11023-44fe-7543-a09f-f7e741f1c475` одобрил первоначальные template commands пяти helper launches и six compile-only/negative commands с literal placeholders и однозначными5парами. Развёрнутый повторный запрос был инициативой координатора из-за flattened snapshot и затем отозван; дополнительное подтверждение не используется как основание полномочий. Actual JSON исходного вопроса сохранял placeholders/5пары, shell expansion/пустых значений не было. Позднее прямое разрешение пользователя всех test commands/scripts передано координатором и покрывает проверочные helpers/targets/fresh повтор. Новых permission вопросов не требуется. Это не hosting/DB/live/deployment разрешение.

Ниже literal команды final v2, cwd `D:/Media/User/source/repos/agent-bridge`. `artifacts/stage15/final-commands.json` также сохраняет argv/Exit, включая intermediate strict failure и final successful strict. Каждый no-build test использовал fresh successful Metadata build того же output. No-build после failed build не выполнялся.

```powershell
# Exit0
dotnet restore tests/Delivery/Build/AgentBridge.Delivery.csproj -r win-x64 -p:DeliveryProvider=SqlServer -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false

# Exit0
dotnet build tests/Delivery/Build/AgentBridge.Delivery.csproj -c Debug --no-restore -r win-x64 -p:DeliveryProvider=SqlServer -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false -p:GeneratePackageOnBuild=false -o D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/SqlServer-win-x64 -flp:logfile=artifacts/stage15/SqlServer-win-x64-resources-build.log

# Exit0
& './tests/Delivery/Build/Assemble-Delivery.ps1' -BuildOutput 'D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/SqlServer-win-x64' -AssetsFile 'D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/obj/project.assets.json' -Destination 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15-v2/SqlServer/win-x64' -Provider 'SqlServer' -Rid 'win-x64' -SourceRevision '3286b12886c34ac4b2ba9d4523fee22c3949ab0a' -EfRevision '5962deceb6ea01306cbbda400040db88ea8e5df9' -HttpRevision 'ba961c6dbbaeb4e265ab9a03b810fb980de3fec4' -SdkVersion '10.0.401'

# Exit0
dotnet restore tests/Delivery/Build/AgentBridge.Delivery.csproj -r linux-x64 -p:DeliveryProvider=SqlServer -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false

# Exit0
dotnet build tests/Delivery/Build/AgentBridge.Delivery.csproj -c Debug --no-restore -r linux-x64 -p:DeliveryProvider=SqlServer -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false -p:GeneratePackageOnBuild=false -o D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/SqlServer-linux-x64 -flp:logfile=artifacts/stage15/SqlServer-linux-x64-resources-build.log

# Exit0
& './tests/Delivery/Build/Assemble-Delivery.ps1' -BuildOutput 'D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/SqlServer-linux-x64' -AssetsFile 'D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/obj/project.assets.json' -Destination 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15-v2/SqlServer/linux-x64' -Provider 'SqlServer' -Rid 'linux-x64' -SourceRevision '3286b12886c34ac4b2ba9d4523fee22c3949ab0a' -EfRevision '5962deceb6ea01306cbbda400040db88ea8e5df9' -HttpRevision 'ba961c6dbbaeb4e265ab9a03b810fb980de3fec4' -SdkVersion '10.0.401'

# Exit0
dotnet restore tests/Delivery/Build/AgentBridge.Delivery.csproj -r linux-arm64 -p:DeliveryProvider=SqlServer -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false

# Exit0
dotnet build tests/Delivery/Build/AgentBridge.Delivery.csproj -c Debug --no-restore -r linux-arm64 -p:DeliveryProvider=SqlServer -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false -p:GeneratePackageOnBuild=false -o D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/SqlServer-linux-arm64 -flp:logfile=artifacts/stage15/SqlServer-linux-arm64-resources-build.log

# Exit0
& './tests/Delivery/Build/Assemble-Delivery.ps1' -BuildOutput 'D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/SqlServer-linux-arm64' -AssetsFile 'D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/obj/project.assets.json' -Destination 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15-v2/SqlServer/linux-arm64' -Provider 'SqlServer' -Rid 'linux-arm64' -SourceRevision '3286b12886c34ac4b2ba9d4523fee22c3949ab0a' -EfRevision '5962deceb6ea01306cbbda400040db88ea8e5df9' -HttpRevision 'ba961c6dbbaeb4e265ab9a03b810fb980de3fec4' -SdkVersion '10.0.401'

# Exit0
dotnet restore tests/Delivery/Build/AgentBridge.Delivery.csproj -r win-x64 -p:DeliveryProvider=Sqlite -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false

# Exit0
dotnet build tests/Delivery/Build/AgentBridge.Delivery.csproj -c Debug --no-restore -r win-x64 -p:DeliveryProvider=Sqlite -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false -p:GeneratePackageOnBuild=false -o D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/Sqlite-win-x64 -flp:logfile=artifacts/stage15/Sqlite-win-x64-resources-build.log

# Exit0
& './tests/Delivery/Build/Assemble-Delivery.ps1' -BuildOutput 'D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/Sqlite-win-x64' -AssetsFile 'D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/obj/project.assets.json' -Destination 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15-v2/Sqlite/win-x64' -Provider 'Sqlite' -Rid 'win-x64' -SourceRevision '3286b12886c34ac4b2ba9d4523fee22c3949ab0a' -EfRevision '5962deceb6ea01306cbbda400040db88ea8e5df9' -HttpRevision 'ba961c6dbbaeb4e265ab9a03b810fb980de3fec4' -SdkVersion '10.0.401'

# Exit0
dotnet restore tests/Delivery/Build/AgentBridge.Delivery.csproj -r win-x64 -p:DeliveryProvider=PostgreSql -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false

# Exit0
dotnet build tests/Delivery/Build/AgentBridge.Delivery.csproj -c Debug --no-restore -r win-x64 -p:DeliveryProvider=PostgreSql -p:TargetFramework=net10.0 -p:SelfContained=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false -p:GeneratePackageOnBuild=false -o D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/PostgreSql-win-x64 -flp:logfile=artifacts/stage15/PostgreSql-win-x64-resources-build.log

# Exit0
& './tests/Delivery/Build/Assemble-Delivery.ps1' -BuildOutput 'D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/PostgreSql-win-x64' -AssetsFile 'D:/Media/User/source/repos/agent-bridge/tests/Delivery/Build/obj/project.assets.json' -Destination 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15-v2/PostgreSql/win-x64' -Provider 'PostgreSql' -Rid 'win-x64' -SourceRevision '3286b12886c34ac4b2ba9d4523fee22c3949ab0a' -EfRevision '5962deceb6ea01306cbbda400040db88ea8e5df9' -HttpRevision 'ba961c6dbbaeb4e265ab9a03b810fb980de3fec4' -SdkVersion '10.0.401'

# Exit0
dotnet restore D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-win-x64/Consumer/AgentBridge.BinaryConsumer.csproj -r win-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-win-x64/kit -p:GeneratePackageOnBuild=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false

# Exit0
dotnet build D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-win-x64/Consumer/AgentBridge.BinaryConsumer.csproj -c Debug --no-restore -r win-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-win-x64/kit -p:GeneratePackageOnBuild=false -flp:logfile=D:/Media/User/source/repos/agent-bridge/artifacts/stage15/SqlServer-win-x64-consumer-final-build.log

# Exit0
dotnet restore D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-x64/Consumer/AgentBridge.BinaryConsumer.csproj -r linux-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-x64/kit -p:GeneratePackageOnBuild=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false

# Exit0
dotnet build D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-x64/Consumer/AgentBridge.BinaryConsumer.csproj -c Debug --no-restore -r linux-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-x64/kit -p:GeneratePackageOnBuild=false -flp:logfile=D:/Media/User/source/repos/agent-bridge/artifacts/stage15/SqlServer-linux-x64-consumer-final-build.log

# Exit0
dotnet restore D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-arm64/Consumer/AgentBridge.BinaryConsumer.csproj -r linux-arm64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-arm64/kit -p:GeneratePackageOnBuild=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false

# Exit0
dotnet build D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-arm64/Consumer/AgentBridge.BinaryConsumer.csproj -c Debug --no-restore -r linux-arm64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-arm64/kit -p:GeneratePackageOnBuild=false -flp:logfile=D:/Media/User/source/repos/agent-bridge/artifacts/stage15/SqlServer-linux-arm64-consumer-final-build.log

# Exit0
dotnet restore D:/Media/User/Temp/AgentBridge-stage15-v2/Sqlite-win-x64/Consumer/AgentBridge.BinaryConsumer.csproj -r win-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/Sqlite-win-x64/kit -p:GeneratePackageOnBuild=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false

# Exit0
dotnet build D:/Media/User/Temp/AgentBridge-stage15-v2/Sqlite-win-x64/Consumer/AgentBridge.BinaryConsumer.csproj -c Debug --no-restore -r win-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/Sqlite-win-x64/kit -p:GeneratePackageOnBuild=false -flp:logfile=D:/Media/User/source/repos/agent-bridge/artifacts/stage15/Sqlite-win-x64-consumer-final-build.log

# Exit0
dotnet restore D:/Media/User/Temp/AgentBridge-stage15-v2/PostgreSql-win-x64/Consumer/AgentBridge.BinaryConsumer.csproj -r win-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/PostgreSql-win-x64/kit -p:GeneratePackageOnBuild=false -p:EnableRuntimePackDownload=false -p:EnableAppHostPackDownload=false --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false

# Exit0
dotnet build D:/Media/User/Temp/AgentBridge-stage15-v2/PostgreSql-win-x64/Consumer/AgentBridge.BinaryConsumer.csproj -c Debug --no-restore -r win-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/PostgreSql-win-x64/kit -p:GeneratePackageOnBuild=false -flp:logfile=D:/Media/User/source/repos/agent-bridge/artifacts/stage15/PostgreSql-win-x64-consumer-final-build.log

# Exit1
dotnet build D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-arm64/Consumer/AgentBridge.BinaryConsumer.csproj -c Debug --no-restore -r win-x64 -p:AgentBridgeDeliveryRoot=D:/Media/User/Temp/AgentBridge-stage15-v2/SqlServer-linux-arm64/kit -p:GeneratePackageOnBuild=false

# Exit1
& './tests/Delivery/Build/Assemble-Delivery.ps1' -BuildOutput 'D:/Media/User/source/repos/agent-bridge/artifacts/stage15/sdk-final/SqlServer-win-x64' -AssetsFile 'D:/Media/User/source/repos/agent-bridge/artifacts/stage15/inputs-final/SqlServer-win-x64/project.assets.json' -Destination 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15-negative-rid' -Provider 'SqlServer' -Rid 'linux-x64' -SourceRevision '3286b12886c34ac4b2ba9d4523fee22c3949ab0a' -EfRevision '5962deceb6ea01306cbbda400040db88ea8e5df9' -HttpRevision 'ba961c6dbbaeb4e265ab9a03b810fb980de3fec4' -SdkVersion '10.0.401'

# Exit1
& 'D:/Media/User/AppData/npm/openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive

# Exit0
dotnet restore tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false

# Exit0
dotnet build tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage15/ -flp:logfile=artifacts/stage15/metadata-resources-build.log

# Exit1
$env:AGENTBRIDGE_DELIVERY_ROOT='D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15'; dotnet test tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage15/ --filter FullyQualifiedName~ManifestMatchesCompleteSdkClosure --logger 'trx;LogFileName=stage15-resources-before.trx' --results-directory artifacts/stage15

# Exit0
$env:AGENTBRIDGE_DELIVERY_ROOT='D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage15-v2'; dotnet test tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage15/ --logger 'trx;LogFileName=stage15-metadata-resources-final.trx' --results-directory artifacts/stage15

# Exit0
& 'D:/Media/User/AppData/npm/openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive
```

### Outcomes и отличающиеся промежуточные проверки

| Evidence | Outcome и граница |
| --- | --- |
| artifacts/stage15/<Provider>-<RID>-resources-build.log,5 concrete SDK builds | Все Exit0,0 warnings/errors; fresh managed/native/resources resolution |
| artifacts/stage15/<Provider>-<RID>-consumer-final-build.log,5 external builds | Все Exit0,0 warnings/errors; copied .cs/.csproj и kits в D:/Media/User/Temp/AgentBridge-stage15-v2/<Provider>-<RID>; methods не исполнялись |
| artifacts/stage15/consumer-final-receipts.json | Binary/deps/manifest hashes и copied outputs;137/135/134/137/137 DLL/XML/native/resources файлов, satellites13 в каждом; consumer assets.libraries0, project/package references0 |
| artifacts/stage15/stage15-metadata-resources-final.trx | Exit0,Passed27/Failed0/Skipped0; counters=rows27; no DB/HTTP/DI/native execution |
| artifacts/stage15/stage15-resources-before.trx | Exit1,0/5/0; expected distinguishing loss of satellites на initial kits |
| Final main OpenSpec strict | Exit0,valid=true,issues[],passed1/failed0; actual CLI1.14.1, JSON schema version1.0 |
| Consumer linux-arm64 kit + app win-x64 | Expected Exit1, explicit RID mismatch до компиляции |
| Helper win-x64 deps + Rid linux-x64 | Expected Exit1, SDK deps RID mismatch, destination не создан |
| Delivery Build без RuntimeIdentifier | Expected Exit1, explicit RID required |

27 cases проверяют manifest files/case/hashes/runtime/native/resources SDK entries и PE metadata/architecture/culture, managed AssemblyRef closure/versions и embedded tokenizer resources;5 XML/inheritdoc cases проверяют три actual contract/implementation пары на variant,public AgentBridge signatures/XML одинаковы во всех MSSQL RID. Negative controls: ELF truncation/magic/class/endian/version/machine, wrong Windows PE machine, managed PE вместо native, PE/ELF OS mixing, unknown RID, nonportable/missing/case paths и resource wrong SHA. Classifier читает bytes/PEReader/Roslyn, библиотеку не загружает.

Initial metadata suites23/23 и24/24 (initial kits) — пересекающиеся промежуточные suites, они **не** подтверждали resource closure и не суммируются с final27. Старые consumers также не final evidence. Initial helper Exit1 на duplicate AgentBridge.dll исправлен проверяемыми SDK alias hashes; partial ignored output сохранён под `win-x64.failed-1`, не удалён. Второй rejected patch (duplicate operation) также не применил изменений. Два ошибочных read-only rg Windows-glob аргумента исправлены последующими адресными чтениями; они не были build/test failures.

First main strict Exit1 выявил >500-character warning нового RID requirement; текст разделён на три отдельных требования с сохранением clauses/scenario,final strict Exit0. CLI wrapper/package/bin прочитаны заново; version/help/install/sync/archive и historical changes validation не выполнялись. Metadata-source/build/props/helper после final27 green не менялись; последующие writes только docs/spec/Results и generated evidence.

### Ограничения и передача

B подтверждает подготовку пяти комплектов, metadata и внешнюю компиляцию текущих configuration13/facade14/usage source примеров. Core/adapter/persistence production source/API не изменялся. SDK/provider platform artifacts — actual; synthetic damaged headers/path/hash negative controls не являются runtime/resource fault injection. Даже positive framework/reference/PE/ELF evidence не доказывает native loading, runtime DI, tokenizer execution, provider initialization/SQL/server TLS/authentication/MSAL или конфликты dependency graph существующего приложения.

Полная fresh union-регрессия —16; SQL/provider/maintenance/backup/restore —17; actual Windows x64/Ubuntu x64/Linux ARM64 app/runtime/native —18; live transport/compact/recovery —19. Ни Linux machine, ни ARM64 runtime не использованы: cross-build на Windows не closes runtime gap и не обещает SQL Server Engine ARM64. SQLite/PostgreSQL Linux matrix, Release/AOT/trimming/single-file остаются непроверенными. App logger/sinks/secrets/cleanup schedule/auth/business source и установка runtime принадлежат приложению.

Результат готов к review; коммит не выполнен. Shared writes остановить после финальной проверки Results/UTF8/links/source preservation и сообщения координатору. Commit — только после явной приёмки и поручения exact manifest; соседние manifests пусты, Coordination/foreign .vs и ignored outputs исключены.

Финальный контроль:18 manifest files strict UTF-8/no BOM/LF, text issues0;98 local links/missing0; slnx130 paths/duplicates0/missing0, project/props XML valid. Source snapshot341 hashes не изменился; исходное задание15 до Results совпадает с HEAD при нормализации только статуса. Command ledger33 records,SHA256 `d6433ad17acec42379a3eea3d38886f6bd167359e1afcae663fe3e00afbb4936`; consumer receipts SHA256 `9c2eb9670ff47fd69b0855b174838a3e509ef6f2c5757a98a8aad7865af1b0a0`. Первую read-only text/link проверку остановил PowerShell ParserError из-за literal mojibake marker; исправленный запуск завершился Exit0, files не менялись этой проверкой. `git diff --check` Exit0; staged diff пуст, HEAD прежний, status только manifest18 и foreign Coordination. Соседи EF/HTTP чисты; LB foreign `.vs/` сохранена. После этой записи source/build/props/helper неизменны; shared writes остановлены для review.

**Приёмка15, 2026-10-06:** координатор от имени пользователя принял локальную A/B delivery/metadata/compile-only границу после независимого source/script/props/tests/spec/doc review,341 source hashes, kit/copied output hashes всех5variants,13 resources each, final TRX27/27 и distinguishing0/5, manifest18/status/diffcheck/UTF8/slnx130. Runtime18, union16 и C/D17–19 не закрыты. Поручен один фактический локальный английский Conventional Commit exact manifest18 выше; Coordination, ignored outputs и соседи исключены. Перед commit изменены только статус и эта запись/уточнение permission; code/build/props/helper после final green не менялись. Исторические checkpoints передачи выше сохранены.
