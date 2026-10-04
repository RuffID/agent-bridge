# 25 — Руководство по использованию и закрытие первоначального плана

Статус: **Реализован, адресно проверен и принят координатором; локальный commit manifest36 разрешён**. Дата: **2026-10-04, Asia/Novosibirsk**. Зависимость24 принята и закоммичена: HEAD `495e2b857bc36171ce97b82eb0fef77eeac593ea`, parent23 `7e9d533d80393bd85b08e1b17567038e12ec8a16`; baseline tree/index чистые. Реализация первоначального плана00–25 завершена в документированных границах. OpenSpec CLI validation невыполнена, changes не архивированы. Новых этапов за пределами25 нет.

## Результат и задачи

- [x] Краткое назначение/поток и последовательность подключения в [корневом README](../../../README.md); история проверок остаётся в плане.
- [x] [Руководство](<../../Technical documentation/25-usage-guide.md>) и проверяемые C# исходники [Consumer](../../../tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj): DLL Import, DI/options, создание/run, каталог/выбор/status и bounded cleanup.
- [x] Явные app factories для ключей/ordered providers/HttpClient/business source, read-only tool с полной простой schema и текущей авторизацией; тип порта примера отделён от AgentBridge API.
- [x] Retention/soft bytes/token thresholds/reserve/passes переопределяемы; fixed expiry, Unknown full budget, typed failure/cancel/no-replay и partial cleanup объяснены.
- [x] Навигация, local/root AGENTS, solution items, main/delta spec/context и устаревшие текущие утверждения technical docs актуализированы точечно; старые plan reports сохранены.
- [x] Два внешних бинарных compile-check и PE/XML tests; фактическая карта evidence каждого этапа00–25.
- [ ] OpenSpec CLI validation: CLI не найден, установка не выполнялась; статический аудит не заменяет validation. Архивирование не выполняется.

Production API/схема/generated migrations/root csproj не менялись. Documentation по-прежнему исключён из None корневого проекта; новый guide добавлен только в solution items. Соседние исходники read-only, обязательные пути EFCoreLibrary/HttpClientLibrary/codex-lb/TelegramCodexRelayBot/AquaByteLedger.Infrastructure/Services/DataBase доступны. Текущие версии библиотек сверены по исходникам: EFCoreLibrary0.0.5, HttpClientLibrary FileVersion0.0.0.5. Исследование соседних приложений/запуск не требовались.

## Карта evidence00–25

Каждая ссылка ведёт на отчёт с командами, результатами, первоначальными ошибками и ограничениями своей даты. Таблица фиксирует реализованную область и границу доказательств, а не новую агрегированную статистику. **Пересекающиеся suites не суммируются.** Для00–13 дополнительное actual DB evidence находится в [датированном integration report](README.md#дополнительный-интеграционный-запуск-2026-10-04); он использовал предыдущую EFCoreLibrary0.0.4. [Отдельная проверка0.0.5](README.md#проверка-совместимости-с-efcorelibrary-005) — compile/isolated; current-schema DB проверки20–23 используют0.0.5.

| Этап / отчёт | Реализованный результат | Фактическая граница evidence |
| --- | --- | --- |
| [00](00-contract-baseline.md) | Проверенные исходные контракты обязательных библиотек/источников | Статическое чтение, не live сервер |
| [01](01-solution-foundation.md) | Корневое ядро .NET10, отдельные adapters/tests, правила документации | Compile/структура, не приложение |
| [02](02-configuration-and-defaults.md) | Typed options и настраиваемые defaults | Isolated validation/DI; не budget конкретного upstream |
| [03](03-serilog-integration.md) | ILogger diagnostics с pipeline приложения | Isolated logger/Serilog sink, безопасные поля/cancellation |
| [04](04-httpclientlibrary-logging.md) | Safe HTTP metadata, None/JsonStructure, bounded errors | Isolated net8/net10 actual library; downstream fake HTTP, не live |
| [05](05-efcorelibrary-maintenance.md) | Relational maintenance и optional provider modules | Первоначально isolated; later integration только SQLite/PostgreSQL, не SQLServer/MySQL |
| [06](06-dialog-domain-state.md) | Owner/fixed expiry/status/version/terminal prefix | Domain isolated; DB guards подтверждены отдельно |
| [07](07-application-ports.md) | Независимые ports, lifecycle/canonical snapshots | Isolated contracts/fakes, не atomic persistence |
| [08](08-persistence-models.md) | EF metadata/DTO/serialization/DI | Первоначально без БД; later actual provider tests отдельно |
| [09](09-base-repository-adapters.md) | Base CRUD read/staging, full protected read | Первоначально repository fakes; later actual translation отдельно |
| [10](10-scenario-unit-of-work.md) | Short scenario UoW, CAS/rollback/Domain Restore | Первоначально fake transactions; actual DB/ack faults отдельно |
| [11](11-initial-provider-migrations.md) | Generated provider factories/migrations/history isolation | Metadata/model comparison; later real Up/Down в своих test DB |
| [12](12-database-startup-and-backup.md) | Explicit SingleInitializer inspect/initialize/update/backup | Первоначально fake providers; later real native/pg_dump/restore отдельно |
| [13](13-model-catalog-and-keys.md) | Dynamic catalog, exact selection/input window, key priority | Actual HttpClientLibrary + fake handler; не live доступность |
| [14](14-responses-json-adapter.md) | Canonical JSON Responses, safe errors, bound continuation | Fake HTTP/local content; metadata binding не proof account ownership |
| [15](15-responses-sse-adapter.md) | SSE framing, awaited callback, partial/terminal lifecycle | Fragmented local streams/fake handler; не сервер |
| [16](16-context-composition.md) | Ordered providers/window/tail/new input, known pairs | Public ContextBuilder isolated, no history loss/global call_id dedup |
| [17](17-model-tokenizer.md) | Offline BPE exact mapping и full input guard | Actual embedded dictionaries; не upstream tokenizer equivalence/opaque estimator |
| [18](18-context-compaction.md) | Compact transport/terminal prefix/versioned save/bounded passes | Public compactor/gateway/actual BPE + fake writer; не real writer atomicity |
| [19](19-application-tools.md) | Registry/validator/scoped handler/bounds/partial results | Isolated tasks/scopes/checkpoint fakes; standalone memory не restart protection |
| [20](20-agent-turn-orchestration.md) | Full run/durable Started+outcomes/no-replay/terminal save | Isolated + actual SQLite/PostgreSQL; root-DI restart не OS crash |
| [21](21-settings-and-dialog-status.md) | Independent per-dialog settings CAS, pinned run/provenance/status | Actual DB/current migrations Up/Down + isolated; live HTTP не выполнялся |
| [22](22-expired-dialog-cleanup.md) | One bounded batch/separate scopes/cascade/honest outcomes | Isolated + actual SQLite/PostgreSQL; stale mutation test использует pre-expiry time |
| [23](23-cross-component-verification.md) | Cross-component current-schema lifecycle/guards/backup+restore | 32 actual DB и25 isolated; fake HTTP; synthetic ack fault не network loss |
| [24](24-dll-delivery.md) | Два автономных win-x64 комплекта, manifest/XML/native/props | Два binary compile-check и8 PE/XML cases; не runtime/native/IDE presentation |
| [25](25-usage-guide-and-closure.md) | Guide/короткий README/проверяемые C# examples/актуальный checkpoint | Два новых binary compile-check, повтор8 PE/XML cases, static navigation/UTF-8; методы не исполнялись |

00–24 приняты и закоммичены;25 принят координатором после независимой проверки; локальный commit manifest36 разрешён. Датированные STOP/«не начат» в старых отчётах относятся к своим checkpoints и сохраняют историческое evidence; актуальное состояние — в начале этого файла и plan README.

## Команды и результаты25

Все команды выполнялись с явным workdir `D:\Media\User\source\repos\agent-bridge`. Прочитаны применимые ancestor/root/local AGENTS, C# style/build-validation/AGENTS maintenance, ASP.NET DI/options и OpenSpec context skills, production публичные типы, существующий Consumer csproj, Delivery props, Metadata csproj/tests и действующие build/import/config files. Custom Exec/targets/hooks и новые imports не добавлялись.

Baseline/read-only проверки:

```powershell
git status --short
git rev-parse HEAD
Get-Command openspec -ErrorAction SilentlyContinue
```

Status пустой, HEAD24 совпал. `openspec` в PATH не найден. `openspec validate --specs` / change strict validation **не выполнялись**; установка и архивирование не проводились.

Новый внешний каталог:

`C:\Users\Spike\AppData\Local\Temp\AgentBridge-stage25-705a040aa77a4419ad87e9304deea1d7`

Создан штатными inline PowerShell `New-Item`/`Copy-Item`, без сохранённого project/user script. Ancestor AGENTS/Directory.Build.props/targets/NuGet.config/global.json внешнего каталога проверены; дополнительных файлов не обнаружено. Исходный Consumer (8 файлов) и полные комплекты24 скопированы в отдельные `$provider/Consumer` и `$provider/kit`. Путь сохранён в `artifacts/verification/stage25/consumer-location.txt`. Готовые kits24 не изменялись.

Точная операция создания и копирования (случайный Guid первого вызова дал указанный выше путь):

```powershell
$outside = Join-Path $env:TEMP ('AgentBridge-stage25-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outside | Out-Null
foreach ($provider in @('Sqlite','PostgreSql')) {
    $destination=Join-Path $outside $provider
    New-Item -ItemType Directory -Path $destination | Out-Null
    Copy-Item -LiteralPath tests\Delivery\Consumer -Destination $destination -Recurse
    Copy-Item -LiteralPath "artifacts\delivery\stage24-win-x64\$provider" -Destination (Join-Path $destination 'kit') -Recurse
}
New-Item -ItemType Directory -Path artifacts\verification\stage25 -Force | Out-Null
Set-Content -LiteralPath artifacts\verification\stage25\consumer-location.txt -Value $outside -Encoding utf8NoBOM -NoNewline
```

Для **Sqlite**, затем **PostgreSql** выполнены (переменные раскрываются по указанному external path):

```powershell
$outside=Get-Content -LiteralPath artifacts\verification\stage25\consumer-location.txt -Raw -Encoding UTF8
$project=Join-Path $outside "$provider\Consumer\AgentBridge.BinaryConsumer.csproj"
$kit=Join-Path $outside "$provider\kit"
dotnet restore $project "-p:AgentBridgeDeliveryRoot=$kit" -p:GeneratePackageOnBuild=false -p:NuGetAudit=false --source https://api.nuget.org/v3/index.json --verbosity minimal
dotnet build $project -c Debug --no-restore "-p:AgentBridgeDeliveryRoot=$kit" -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet msbuild $project -t:ResolveReferences "-p:AgentBridgeDeliveryRoot=$kit" -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -getItem:ReferencePath,ProjectReference,PackageReference -getProperty:TargetFramework,RuntimeIdentifier,OutputType -verbosity:quiet
```

Оба restore/build успешны, **0 warnings /0 errors**. После удаления unused using и уточнения XML комментария helper cleanup копии исходников обновлены и адресный Build повторён с теми же параметрами; финальные logs — `artifacts/verification/stage25/Sqlite-build.txt`, `PostgreSql-build.txt`. Не запускались consumer methods, DI/options execution, приложения, HTTP или БД.

Receipts: `artifacts/verification/stage25/Sqlite-references.json` и `PostgreSql-references.json`. В каждом206 refs=39 external kit+167 Microsoft.NETCore.App.Ref; ProjectReference/PackageReference пусты. Restored assets имеют пустой package/project graph. SHA256 всех8 consumer source/project files совпадают с repo source;39 DLL+35 XML+native output (75 файлов) совпадают с kit. Оригинальные kits24 и внешние копии проверены по всем79 manifest entries/80 files, размерам/SHA256; сам manifest также совпадает. Generated artifacts вручную не редактировались.

Compile генерирует XML consumer:27 members,17 русских summary,10 inheritdoc elements (включая3 primary constructors). Методы реализации GetKeyAsync/ValidateAsync/ExecuteAsync и свойство Definition имеют inheritdoc. Это сохранённые XML elements, не доказательство отображения IDE/разворачивания текста.

PE/XML regression существующего test binary24:

```powershell
$env:AGENTBRIDGE_DELIVERY_ROOT=Join-Path (Get-Location) 'artifacts\delivery\stage24-win-x64'
dotnet test tests\Delivery\Metadata\AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ --logger 'trx;LogFileName=delivery-metadata-stage25.trx' --results-directory artifacts\test-results\stage25 --verbosity minimal
```

**8 passed /0 failed /0 skipped**. TRX: `artifacts/test-results/stage25/delivery-metadata-stage25.trx`.2 closure/native metadata cases +6 Roslyn interface/XML linkage cases. Состав tests/kit24 не менялся; их сборка не повторялась. Это тот же набор24, повторённый для требований25; он не добавляется к историческому общему count. Старые DB/isolated suites23 и ранее не повторялись без нового риска.

Финальные read-only Git проверки:

```powershell
git diff --check
git diff --stat
git diff --name-only
git diff --cached --stat
git status --short --untracked-files=all
git rev-parse HEAD
git branch --show-current
```

До приёмки ordinary diff и untracked manifest проверены полностью, index пуст, HEAD24/master сохранены. После приёмки координатор разрешил только explicit stage36 и локальный коммит с parent HEAD24 на master. Git autocrlf warning о будущем LF→CRLF не означает изменения фактической кодировки/EOL.

Финальный static audit:36 исходных файлов UTF-8 без BOM/LF, без U+FFFD/четырёх вопросительных знаков/проверенных mojibake markers. Все локальные Markdown пути и20 anchors соответствуют существующим файлам/заголовкам; solution items существуют. Main/delta requirements текстуально совпадают (не CLI validation), manifest равен tracked+untracked перечню. `git diff --check` успешен; ordinary diff просмотрен, staged diff пуст. В production/build/metadata/корневом csproj нет diff.

## Первоначальные ошибки и исправления

- Read/discovery ошибочно предположил имена CanonicalItem/ModelCatalogEntry/AvailableModel/ServiceResultT и папку Application/Configuration; rg wildcard по Windows path также не принимался. Фактические CanonicalModelItem/ModelCapabilities/ModelCatalogSnapshot/Results/Generic найдены по listing, контракты перечитаны. Это ошибки чтения, не build/test failures.
- Первый patch попытался Delete+Add README одним apply_patch и был отвергнут как multiple operations target. Изменений от него не было; применён единый Update README и отдельные точечные updates остальных файлов.
- Первый inline XML audit считал пустой `<inheritdoc/>` через PowerShell truthiness и ожидал неверный count; второй ожидал7 без3 primary constructors. Проверки остановились явно. Исправлен XPath и проверка конкретных interface members; фактический count10/27 с17 summaries. Источники/generated XML из-за ошибок аудита не менялись.
- Compile/test failures не было. Уточнение cleanup helper честно отделяет прямой app вызов с доступом к LastResult до Dispose от helper, который освобождает caller scope до возврата/exception.

## Ограничения и пропуски

- **Пропущено по указанию пользователя**: live codex-lb/OpenAI/upstream/accounts/tokens, HTTP-server/hosting/TestServer/WebApplicationFactory/Telegram/application/demo, произвольные project/user scripts, remote/branch changes/push/PR/публикации/uploads.
- Runtime/native loading комплектов, исполнение DI/options/sample methods, IDE presentation inheritdoc, другие RID/Release/AOT/trimming/single-file и конфликт с runtime packages стороннего приложения не проверялись. Compile не является runtime validation.
- EFCoreLibrary CRUD0.0.5 не генерирует XML;3 SQLitePCLRaw DLL также без XML. Чужая библиотека read-only, описания не выдуманы.
- Actual DB evidence SQLite/PostgreSQL не подтверждает SQL Server/MySQL. Fake HTTP подтверждает только actual HttpClientLibrary/local protocol paths; DI-root restart не OS crash, synthetic ack fault не реальная потеря сети.
- Новые DB/Docker/SQL/migrations/native backup resources не создавались: новых persistence рисков в25 нет. Готовые artifacts24 сохранены; test suites23 не повторялись.
- OpenSpec CLI отсутствует: validation невыполнена, changes не архивированы. Main/delta textual/static consistency не заменяет CLI validation.
- До приёмки add/commit не выполнялись. Координатор независимо принял25 и разрешил локальный commit ровно36 исходных файлов после проверки ordinary/staged diff. Production дефектов, требующих расширения scope, не обнаружено.

## Полный manifest25 — 36 исходных файлов

### Приёмка и локальная фиксация

Координатор независимо проверил6 новых C# источников/public contracts, руководство/README/technical docs, TRX8/0/0, оба финальных Build logs0 warnings/0 errors и receipts206=39kit+167framework без project/package/посторонних references. Совпадение7 C# consumer sources в external/repo, manifest36,351 локальный путь, solution items, main/delta exact block и пустой index подтверждены. Замечаний нет. Принятый объём разрешён к локальной фиксации на master с parent495e2b857bc36171ce97b82eb0fef77eeac593ea; успешные проверки не повторяются.

Перед stage выполняются обычный diff/check/status и проверка UTF-8/LF; затем `git add --` только36 явных путей manifest ниже. До commit проверяются полный staged diff, `git diff --cached --check`, отсутствие generated outputs/секретов и точное совпадение staged paths с manifest. Команда commit: `git commit -m "docs: complete AgentBridge usage guide and implementation plan"`. После commit проверяются fullhash/parent, состав36, чистые status/index. Remotes/branch changes/новые этапы не выполняются.

### Состав manifest

Ниже включены tracked modifications и все untracked files. Generated/ignored outputs в Git не входят; сторонних исходных изменений на baseline не было.

```text
AGENTS.md
README.md
agent-bridge.slnx
Documentation/AGENTS.md
Documentation/README.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md
Documentation/Technical documentation/README.md
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/05-configuration-and-lifecycle.md
Documentation/Technical documentation/07-tokenizer-and-settings.md
Documentation/Technical documentation/09-application-ports.md
Documentation/Technical documentation/10-scenario-unit-of-work.md
Documentation/Technical documentation/11-provider-migrations.md
Documentation/Technical documentation/13-model-catalog-and-keys.md
Documentation/Technical documentation/14-responses-json-adapter.md
Documentation/Technical documentation/15-responses-sse-adapter.md
Documentation/Technical documentation/16-context-composition.md
Documentation/Technical documentation/18-context-compaction.md
Documentation/Technical documentation/19-application-tools.md
Documentation/Technical documentation/20-agent-turn-orchestration.md
Documentation/Technical documentation/24-dll-delivery.md
Documentation/Technical documentation/25-usage-guide.md
openspec/specs/agent-runtime/spec.md
openspec/specs/agent-runtime/context.md
openspec/changes/usage-guide-and-closure/proposal.md
openspec/changes/usage-guide-and-closure/tasks.md
openspec/changes/usage-guide-and-closure/context.md
openspec/changes/usage-guide-and-closure/specs/agent-runtime/spec.md
tests/Delivery/AGENTS.md
tests/Delivery/Consumer/UsageRegistration.cs
tests/Delivery/Consumer/IndividualKeySource.cs
tests/Delivery/Consumer/IAccountSummarySource.cs
tests/Delivery/Consumer/AccountSummaryValidator.cs
tests/Delivery/Consumer/AccountSummaryTool.cs
tests/Delivery/Consumer/UsageFlow.cs
```

Сохраняются ignored evidence stage25 и внешний compile-only каталог; они содержат source copies/DLL/XML/metadata/logs/TRX без рабочих БД/секретов. Ресурсы25 не требуют удаления DB/container. Координатор независимо принял25, проверил manifest36 и разрешил English Conventional Commit только explicit paths. Full hash итоговой локальной фиксации и parent сообщаются отдельно; собственный hash не вставляется рекурсивно в документы. На этом первоначальный план00–25 завершён; новые этапы/remote/worktree/делегирование не разрешены.
