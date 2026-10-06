# 18 — Runtime проверка DLL-комплектов

[Навигатор](README.md). Статус: **частично; Linux runtime заблокирован ресурсами**. Зависимости: 12–16, ресурсы **Q-002**, отдельное разрешение запуска consumer/native операций. Реальные MSSQL provider операции дополнительно зависят от17 и его разрешённых ресурсов.

## Цель и область

Проверить загрузку актуальных комплектов в целевом app dependency graph. Источник — [аудит13](<../AgentBridge Quality Audit/13-dll-delivery.md>) и Q-002. Старые framework-dependent net10.0/win-x64/Debug kits и PE/XML checks не доказывают runtime/native/DI совместимость.

## Работы

1. Проверить .NET10/configuration/app graph и hashes kits16 на **win-x64, linux-x64, linux-arm64**. Три RID входят в согласованный объём; каждую runtime среду подтвердить отдельно. Release и конкретные Ubuntu versions указать по actual deployment; AOT/trimming/single-file не становятся поддержанными автоматически.
2. Подготовить минимального выделенного потребителя с actual app dependency graph, без запуска production приложения. Изменение потребителя и его исполнение — разные области разрешения.
3. Проверить каждый MSSQL RID kit, общую registration14, strict configuration13, ILogger приложения и offline BPE resource loading. Не выполнять database initialization как незаметную часть DI smoke. Existing providers получают адресный smoke при изменении общего closure.
4. Для необходимых native операций актуального dependency graph получить отдельное разрешение на конкретный ресурс/команду. Проверить architecture/PE/ELF соответствие и missing/mixed dependency failure без fallback; Windows native DLL не копировать в Linux kit.
5. Если требуется IDE/XML acceptance, проверить конкретную IDE/version и доступность public API comments. Это отдельное evidence от XML file presence.
6. Зафиксировать successful и failure outcomes, versions/hash/dependency graph; не публиковать или устанавливать комплект в приложение пользователя автоматически.

## Проверки

Runtime/native — только после явного разрешения запуска. Сверить hashes перед загрузкой; отличать actual resource/DI/native проверки от compiler/PE/ELF observations16. Проверки с БД используют только окружение17; live HTTP здесь не требуется. Windows cross-build не является Linux ARM64 runtime evidence.

## Критерии завершения

Проверены три согласованных MSSQL RID kits и relevant native boundaries на соответствующих ОС/архитектурах. Недоступная Linux ARM64 среда остаётся незакрытой частью поддержки. Прочие RID/modes не объявляются проверенными. Shared app files и чужие installations не изменены.

## Результаты

### Выполненная область и baseline

2026-10-06, Asia/Novosibirsk. Исходное задание выше сохранено, изменён только статус. Переданное прямое разрешение пользователя всех test commands/scripts включает выделенный runtime consumer, offline restore/build и безопасный native Load/Free без DB/business операций. Допуск координатора уточнил manifest enumeration и negative controls. Установка runtime, remote copy/run, hosting, реальные HTTP/БД/SQL, migrations, provider initialization, deployment и cleanup не выполнялись.

Before: kits16 имели compile/metadata evidence, выделенного executable/runtime evidence не было. After: новый test-only binary consumer, пять fresh внешних сборок, три Windows runtime runs, native Load/Free и различающие отказы. Production/API/dependencies не менялись. Полный этап18 **не завершён**: Linux x64/ARM64 runtime и production app graph остаются открытыми.

Прочитаны актуальные root/tests/Delivery/Documentation/Plans AGENTS, README/Decisions, Results12–17, Findings/итог аудита15 и относящиеся main OpenSpec clauses. Использованы csharp-project-rules/style/build-validation/agents-maintenance, aspnetcore-project-rules для DI/configuration; backend-uow-repositories/reference прочитан для сверки границы, storage/UoW не менялся. Q-003–005 не переоткрывались; исторические отчёты не переписаны.

AgentBridge HEAD `d22f322580dcecc18026bea3a297ef543609f89f`, принятый17 `9ba43d7ffb449099948a846824d53cd6c9fee171`. Kits16 sourceRevision `a874c346c288c6b5b6343a59dea4a1220bd785f8`: заново сверены329 production/library `.cs/.csproj/.props/.targets` из snapshot16, changed0 (`artifacts/stage18/production-continuity.json`). Из474 прежних inputs отличаются только Integration/AGENTS этапа17 и собственный Delivery/AGENTS18. Новые prepared fixtures17 не являются production изменением.

Соседи read-only, manifests пусты: EFCoreLibrary `5962deceb6ea01306cbbda400040db88ea8e5df9`, HTTP `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4` чисты; codex-lb `f2b8e042c4ce012ae703bc939413bf9961b00032` сохраняет foreign untracked `.vs/`. Чужой Coordination.md сохранён и исключён; index/add/commit/push не менялись.

### Exact writable manifest

Только AgentBridge,9 файлов:

```text
tests/Delivery/Runtime/AgentBridge.RuntimeProbe.csproj
tests/Delivery/Runtime/Program.cs
tests/Delivery/Runtime/ProbeChecks.cs
tests/Delivery/Runtime/BlockingHandler.cs
tests/Delivery/Runtime/ProbeLoggerProvider.cs
tests/Delivery/Runtime/Run-RuntimeProbe.ps1
tests/Delivery/Runtime/AGENTS.md
tests/Delivery/AGENTS.md
Documentation/Plans/AgentBridge Audit Remediation/18-runtime-delivery.md
```

Generated logs/receipts/assets/snapshots только ignored `artifacts/stage18/`; external source/kit/output copies — `C:/Users/Spike/Documents/Codex/Stage18-20261006/run1|run2|run3/`. Оригинальные kits16 и промежуточные failures не удалены. Parameterized probe project не добавлен в solution Build; root glob уже исключает tests/nested outputs.

### Preflight и actual app graph

Перед запуском прочитаны concrete Runtime csproj, imported delivery/variant props и helper; проверены ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json до drive roots AgentBridge/external workspace. Applicable ancestor файлов нет. ProjectReference/PackageReference отсутствуют; SDK-only offline restore использует `--source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false`, runtime/apphost download отключён. Generated NuGet props/targets не импортируют packages, assets.libraries0 каждого variant; imports/targets не содержат Exec/hooks. EF packaging подавлен `GeneratePackageOnBuild=false`, обычный concrete Build, без solution/Rebuild/publish/pack. После failed build stale DLL не использовались; каждый positive run связан с собственной successful fresh сборкой. Source/scripts parse проверены; copied project inputs закреплены в `run3/project-inputs.json`.

Actual local OS из Win32_OperatingSystem: **Windows11 Pro 10.0.26200 x64**, SDK **10.0.401**, MSBuild18.9.11, Microsoft.NETCore.App **10.0.12**. `run3/os.json` и `dotnet-info.txt` сохраняют source observations. Конфигурация **Debug/net10.0/framework-dependent**, apphost отсутствует.

App graph probe — Microsoft.NETCore.App и полный выбранный binary kit; собственного ASP.NET Core/Serilog/production packages graph нет. App создает ILoggerFactory с in-memory provider, IConfiguration memory values и HttpClient с fail-fast blocking handler. Actual facade13/14 выполняет startup validation, scoped resolution runner/settings/compactor/cleanup/readers/DbContext и проверку distinct DbContext в двух scopes. Ни methods runner/storage/cleanup, ни maintenance не исполняются; Database.ProviderName — metadata, не соединение. HTTP factory не вызывается до startup validation, после scoped graph resolution вызывается1 раз, sends0. Logger instance сохраняется, app получает1 marker. Invalid missing MaxToolSteps даёт OptionsValidationException до I/O.

Program перед загрузкой сверяет actual RID, schemaVersion1, net10.0/frameworkDependent, provider/RID manifest и runtime major10; проверяет size/SHA всех copied managed/XML/resource/native entries. Затем **все64 managed** Windows manifest entries проходят Assembly.LoadFrom/GetTypes с locations внутри собственного output. Все13 satellites проверены по hashes, но actual localized ResourceManager/IDE IntelliSense не проверялись. Каждая из3 native DLL проходит NativeLibrary.Load/Free по absolute verified path: `msalruntime.dll`, `e_sqlite3.dll`, `Microsoft.Data.SqlClient.SNI.dll`. Это loader boundary, не вызов exports/authentication/SQL/backup или provider readiness.

Actual IContextTokenCounter читает оба embedded deflate словаря: `gpt-4.1/o200k_base` и `gpt-4/cl100k_base`, input instructions `Hello world`, knownTokens2, estimatedInputTokens16, opaque=false. Это локальный BPE/framing результат, не server billing count.

### Комплекты и source continuity

До копирования/build/loading helper проверил existence/size/SHA **каждого** manifest entry; copied kit и output проверены отдельно. Manifest SHA ниже совпадают с принятыми Results16. Kits не пересобирались/не редактировались.

| Kit16 | Entries | Manifest SHA256 |
| --- | --- | --- |
| SqlServer/win-x64 | 140 | 78fba70ca00fa5a85a31da7c6f11e5d051483b4295f5a6c715116a39da279f34 |
| SqlServer/linux-x64 | 138 | 16dbe5c17de729e9ea2f2207c8b4cd8f197fe1a56e7ed43401d828fa2b45d29e |
| SqlServer/linux-arm64 | 137 | 626d4d422504ced9e38e9f311405e9a49cf359c7e913ead010fd36dfcc6088b8 |
| Sqlite/win-x64 | 140 | 4b39cc40fee2f94ba33dda600866050ec43583948579db62db166c983e28c6ba |
| PostgreSql/win-x64 | 140 | 4acc5e38f61931be10df80e77af71a37f18f37f8abbff6d2d9b1a7719387f1d8 |

Package versions из immutable kit manifests/deps: EF10.0.11, Npgsql10.0.3, SqlClient6.1.6/SNI6.0.2, SQLitePCLRaw2.1.12, tokenizers/data2.0.0, MSAL NativeInterop0.20.6, EFCoreLibrary0.0.5/HTTP FileVersion0.0.0.5; Extensions10.0.11 и Configuration/Binder/Options.ConfigurationExtensions10.0.3. Consumer создаёт собственные deps; SDK delivery deps не подменяют его runtime graph. `run3/kits.json`, `*-assets.json`, `*-output.json`, `verification.json` содержат exact SHA/versions/source/copies. Sources всех5 external consumers совпали с final Runtime source, output hashes до/после runtime не изменились. Все loaded managed locations принадлежат собственному output; загрузки из оригинального kit/repo не наблюдались.

### Exact commands и outcomes

Все команды из cwd `D:/Media/User/source/repos/agent-bridge`. Actual helper launches:

```powershell
& './tests/Delivery/Runtime/Run-RuntimeProbe.ps1' -DeliveryRoot 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage16' -Workspace 'C:/Users/Spike/Documents/Codex/Stage18-20261006/run1' -Evidence 'D:/Media/User/source/repos/agent-bridge/artifacts/stage18/run1'
& './tests/Delivery/Runtime/Run-RuntimeProbe.ps1' -DeliveryRoot 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage16' -Workspace 'C:/Users/Spike/Documents/Codex/Stage18-20261006/run2' -Evidence 'D:/Media/User/source/repos/agent-bridge/artifacts/stage18/run2'
& './tests/Delivery/Runtime/Run-RuntimeProbe.ps1' -DeliveryRoot 'D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage16' -Workspace 'C:/Users/Spike/Documents/Codex/Stage18-20261006/run3' -Evidence 'D:/Media/User/source/repos/agent-bridge/artifacts/stage18/run3'
```

Run1 helper Exit1: successful restore/build0 warnings/errors, runtime Exit-532462766 BadImageFormatException на `msalruntime.dll`. Это ошибка первоначального probe enumeration `*.dll` с неполным name exclusion, не defect комплекта. Исправлено manifest kind enumeration и отдельным native Load/Free. Run2 helper Exit0: пять builds/три runtime/negative controls до дополнительного schema/provider guard review. Run3 helper Exit0 после guard correction: definitive source/binaries,15:45:47–15:46:20 UTC+07:00. Runs перекрываются, не суммируются; run1/run2 evidence сохранено, final source hashes относятся только run3.

Полные literal argv/start/end/cwd/exit/log SHA всех17 final dotnet commands — [commands.json](../../../artifacts/stage18/run3/commands.json); [commands-expanded.ps1](../../../artifacts/stage18/run3/commands-expanded.ps1) сгенерирован из receipt и не запускался. Новый test executable не использует xUnit/TRX: фактические assertions/outcomes — JSON stdout/logs, counters TRX не выдумываются.

| Final run3 | Outcome | Граница |
| --- | --- | --- |
| 5 offline restores + 5 fresh concrete builds | Exit0 each, warnings0/errors0 | SQLServer3 RID, SQLite/PostgreSQL win-x64; libraries0 в consumer assets |
| SqlServer/Sqlite/PostgreSql win-x64 runtime | Exit0 each | Managed64/native3 each, facade/options/scopes/app logger/BPE, sends0 |
| SqlServer linux-x64/linux-arm64 | Build Exit0 only | Managed63/native2 либо1 delivered; runtime **не запускался** |
| Missing O200kBase copied kit | Helper gate rejected | File отсутствует; оригинал сохранён |
| Mixed ARM64 libe_sqlite3.so под именем Windows e_sqlite3.dll copied kit | Helper gate rejected | Hash mismatch; fallback не применяется |
| Missing O200kBase fresh output copy | Expected Exit-532462766, FileNotFoundException | Runtime output gate отказывает до managed/native load; BPE fallback не происходит |
| Wrong RID win-x64 consumer / linux-arm64 argument | Expected Exit-532462766 | Actual runtime RID mismatch до loads |
| Wrong provider Sqlite argument / SqlServer kit | Expected Exit-532462766 | Schema/framework/provider/RID guard до loads/DI |
| Mixed native fresh output copy | Expected Exit-532462766, Output hash mismatch e_sqlite3.dll | Runtime hash gate отказывает до native loading; loader bad-image fallback не тестировался |

Negative source outputs созданы копированием с исключением одного файла либо адресной подменой только нового disposable copy; original manifest immutable. Не было recursive delete/destructive cleanup. Native positive Load/Free не превращает synthetic hash gate controls в проверку отказов native exports/DB/provider initialization.

| Final evidence | SHA256 |
| --- | --- |
| run3/commands.json | 4746da221f5cde1f175f9f88a46ef8f34dd6c121c9406da20873c9a5db2cf6e6 |
| run3/kits.json | caeeed1b3a57161aaca3d03f91189732b5230f6b0c5b8b9a8953c357bb73140b |
| run3/verification.json | fe55c585414d62d379b4ae28666efeab496fd4feb486b300341320e265c8e7cd |
| run3/source-snapshot.json | a30dc004a888bcd62264f8a4aab7001365fba46743289eff97848553591064f0 |
| Runtime/Run-RuntimeProbe.ps1 | 84f5da5a21bdb9ab769c82b9532391b4302bc4c90ca7e13ae5e02ec6e729ca7a |

Consumer DLL SHA: SqlServer Windows `a592ae9665d26274e63ea08892f2968983d65b72f0adfc07b77b4299617e40ac`, Linux x64 `d2cf7b0bce061573a183f08c1a25a58d50c8211f2761450f01750d9081dcf5e1`, ARM64 `1348d089bed3f293a9cc9005cfb5b1626dcaece6e9407ff37f229a66c03470c1`; SQLite Windows `fd4a9bf4a8b637e7be3e29cd47b8dec6ef5b6011dfdbadb7223823ec3a611cc0`, PostgreSQL Windows `e794f6815897adffb741718b9fad5d246ab16cae6b348ea212371699db92188f`. Detailed deps/log SHA и unchanged output snapshots — verification.json.

### Открытая runtime матрица и передача

| Область | Actual ресурс / остаток |
| --- | --- |
| Windows x64 минимальный выделенный app graph | Проверен final run3; production ASP.NET Core/дополнительные app package conflicts не проверены |
| Ubuntu x64 | Машина/доступ/версия/.NET/app graph не предоставлены; весь runtime/native остаток открыт |
| Linux ARM64 | Пользователь предоставил stdout: Raspberry Pi5, Ubuntu24.04.3 LTS, kernel6.8.0-1060-raspi/aarch64, LAN pi@192.168.1.16, SSH доступен. `dotnet --info` и `--list-runtimes`: command not found. .NET10 отсутствует; remote installation/copy/run не выполнялись. Нужны separately согласованные runtime provisioning и выделенный test directory/access |
| Provider SQL/TLS/authentication/native exports/maintenance | Ресурсы17 не определены;17 C отложен пользователем. Здесь не выполнены |
| IDE/XML acceptance | IDE/version/IntelliSense не проверены; XML presence/hash не IDE evidence |
| Release/AOT/trimming/single-file/SQLite-PG Linux matrix | Не проверены и не объявлены поддержанными этим результатом |

ARM64 app client не означает SQLServer Engine ARM64; Linux native assets не заменены Windows DLL. Факт существования Pi не закрывает runtime gap при отсутствии .NET. Установка/remote операция требует ресурсного согласования; test command permission не разрешает их незаметно. Shared app files/installations не изменены. OpenSpec/production/предшественники не менялись; повтор общей регрессии16/CLI без новой причины не выполнялся.

Доступная локальная часть готова к независимому review координатором. После final контроля UTF-8/EOL/diff/manifest writes останавливаются; add/commit только после явной приёмки и отдельного поручения exact9 manifest. Поздний ответ о Linux ресурсах после handoff не возобновляет writes без нового допуска координатора. Полный18 и общая приёмка20 не заявляются.

Final контроль9 files: strict UTF-8/no BOM/LF, U+FFFD/четыре question marks/проверенные mojibake markers отсутствуют; csproj XML/helper PowerShell parse корректны. Исходное задание до Results совпало с HEAD кроме статуса. Scoped diff --check Exit0, staged diff пуст; status только manifest9 и foreign Coordination. `artifacts/stage18/manifest-validation.json` содержит per-file SHA/text evidence. Production source329 не изменился; sources всех5 consumers и final output hashes сверены. Code/helper после final run3 не менялись, последующие writes только Results и generated verification evidence. Shared writes остановлены для review; actual commit не выполнен.

**Приёмка доступной части18, 2026-10-06:** координатор от имени пользователя независимо проверил final source/guards/helper/AGENTS/Results,17 command/log hashes,710 output hashes/7 source hashes и три runtime JSON; Windows loader/DI/BPE граница принята. Linux runtime/production app graph/provider exports/IDE остаются открытыми; весь18 не завершён. Поручен фактический локальный English Conventional Commit exact9 manifest выше, Coordination/соседи/outputs исключены. Code/tests/helper после final run3 не менялись; повтор проверки неизменного binary не требовался. Предыдущие checkpoints передачи сохранены как история.
