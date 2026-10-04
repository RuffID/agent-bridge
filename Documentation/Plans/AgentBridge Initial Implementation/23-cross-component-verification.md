# 23 — Проверить взаимодействие компонентов

Статус: **Реализован, проверен и принят координатором** (2026-10-04). Зависимости: **12, 20, 21, 22**. Разрешён локальный commit ровно15 файлов manifest; до приёмки add/commit не выполнялись. После23 **STOP;24–25 НЕ НАЧАТЫ**.

## Цель

Собрать содержательные подтверждения корректности границ библиотеки и соблюдения инвариантов при сбоях.

## Задачи

- [x] Выполнить разрешённые адресные проверки компиляции и изолированные регрессионные тесты завершённых сценариев.
- [x] Проверить канонические данные JSON/SSE/compact, непрерывность вызовов функций и обработку неполного завершения.
- [x] Проверить приоритет ключей, изменение модели и усилия, нестандартный срок хранения и пороги токенов.
- [x] Проверить отмену, гонки при очистке, восстановление после перезапуска и отсутствие повторных побочных действий инструментов.
- [x] Выполнить адресные реальные SQLite/PostgreSQL и backup/restore проверки по исходному постоянному разрешению пользователя; отдельного повторного разрешения не требуется. Работающий codex-lb — **Пропущено по указанию пользователя**.
- [x] Зафиксировать проверенные версии зависимостей, пройденные проверки и непроверенное поведение провайдеров.

## Проверка и завершение

Этап завершён, когда необходимые подтверждения зафиксированы, а выявленные сбои устранены. Не объявлять интеграцию провайдеров проверенной только на основании подставных реализаций или локальной компиляции.

Источник: [границы проверки](<../../Technical documentation/05-configuration-and-lifecycle.md>).

## Объём и адресная матрица

Baseline: чистые tree/index, master HEAD22=`89c28e839bf12584027b81494c31557c91279935`. Change [cross-component-verification](../../../openspec/changes/cross-component-verification/proposal.md) создан до кода; main/delta normative критерии синхронизированы, rationale отдельно. Production-код, схема, generated migrations, root csproj/slnx и соседние библиотеки не менялись. Добавлены два test source файла и test-only ProjectReference persistence tests → CodexLb. Корневой compile glob по-прежнему исключает tests, Documentation None Remove и solution items сохранены.

| Риск / отличие от прежних этапов | Новое evidence |
| --- | --- |
| DB runner20–22 использовал Application gateway double, transport tests не проходили durable runner | Actual AgentRunner → CodexLb catalog/JSON/SSE/compact → HttpClientLibrary → scripted handler/local responses → actual EFCoreLibrary SQLite/PostgreSQL |
| Повторяющиеся call_id между шагами и restart | JSON и SSE: две отдельные пары, три distinct StepId, committed Started виден отдельному scope до handler, succeeded journal и outputs переживают новый root; TurnId не повторяется |
| Незавершённые function arguments | EOF SSE сохраняет partial call, terminal Incomplete, без handler/output; старый turn Interrupted до HTTP, новый turn получает Conflict после catalog, без generation и без Begin |
| Compact и full budget | Только terminal history в compact; provider/new input не входят в сохраняемое окно. Text compact даёт следующий SSE request с одним provider/window/current input и прежними instructions/tools. Opaque compact version1/prefix1/provenance сохраняется, generation запрещена actual BPE unknown estimate |
| Settings/access | Saved model, per-request effort и defaults; independent settings CAS во время handler не меняет active snapshot/key. Новый run видит новый выбор. Source вызывается раз на run; null → shared, empty/source/401 → без fallback; exact case model/effort и input_context_window, не context_window |
| Полный input budget | Actual offline BPE считает instructions/provider/input/tool schema; estimate+reserve==input window разрешает generation, превышение на1 отклоняет без generation; threshold сохраняется в turn settings |
| Cancel + concurrent cleanup | Gated handler после real Started, configured37h equality, actual ExpiredDialogCleanup удаляет все6 таблиц; late outcome не возрождает данные. Без delete поздняя cancellation сохраняет confirmed output/Succeeded и Canceled turn; restart не повторяет effect |
| Неизвестное подтверждение | Новый actual outcome writer decorator выбрасывает после успешного commit: новый root видит InProgress + Succeeded/output, но run не повторяется. Повторены4 existing actual start acknowledgement/SQL rollback cases для complementary atomicity |
| Backup новых полей на EFCoreLibrary0.0.5 | Два actual SQLite/PostgreSQL backup→restore с journal/settings/turn snapshot/opaque compact/provenance; SHA256/length, equality всей схемы/всех строк, public read и no-replay на восстановленной отдельной БД |

Обычный app handler/validator и HTTP responses управляемые; они не заменяют runner, adapter, counter или DB writer. Новый пустой observer DbContext не используется как доказательство отсутствия чужой transaction. Committed Started наблюдается реальным parent-aware запросом до действия. No transaction в scoped handler относится только к его собственному контексту. HTTP сервера/hosting нет.

## Версии и команды

Рабочий каталог всех команд: `D:\Media\User\source\repos\agent-bridge`. SDK **10.0.401**, net10.0, EFCoreLibrary **0.0.5**, HttpClientLibrary FileVersion **0.0.0.5**, EF/SQLite **10.0.11**, Npgsql **10.0.3**, Microsoft.ML.Tokenizers/Data **2.0.0**. Docker Engine **29.8.1**, official postgres:18, pg_dump/pg_restore **18.6**. Проверены concrete project/build/import files; новых executable hooks нет. Устанавливать инструменты не потребовалось.

```powershell
Get-Command openspec,dotnet,docker -ErrorAction SilentlyContinue
dotnet restore tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -p:GeneratePackageOnBuild=false -p:NuGetAudit=false --source https://api.nuget.org/v3/index.json --verbosity minimal
dotnet build tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
```

Restore успешен; финальный Build всех11 проектов цепочки: **0 warnings /0 errors**. Первый build имел CS9113 unused fixture parameter после удаления несодержательного observer assertion; параметр удалён, последующие builds чистые. Compile errors не было; stale binaries после failed build не запускались.

Только перед DB run shell получал временную конфигурацию из собственного `run.json` (после cleanup удалён):

```powershell
$taskRecord=Get-Content -LiteralPath 'artifacts\integration-stage23-0565130b1ee5422fad62c6526b4ee35e\run.json' -Raw -Encoding UTF8 | ConvertFrom-Json
$env:AGENTBRIDGE_INTEGRATION='1'
$env:AGENTBRIDGE_INTEGRATION_ROOT=$taskRecord.root
$env:AGENTBRIDGE_POSTGRES_CONNECTION="Host=127.0.0.1;Port=61856;Database=postgres;Username=$($taskRecord.user);Password=$($taskRecord.password);SSL Mode=Disable;Pooling=false"
$env:AGENTBRIDGE_PG_DUMP='D:\Programs\PostgreSQL\18\bin\pg_dump.exe'
$env:AGENTBRIDGE_PG_RESTORE='D:\Programs\PostgreSQL\18\bin\pg_restore.exe'
$env:AGENTBRIDGE_PG_MAJOR='18'
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'FullyQualifiedName~CrossComponentIntegrationTests|FullyQualifiedName~OutcomeSaveFailureRollsBackOutputsAndJournalTogether|FullyQualifiedName~UnknownStartCommitPersistsBarrierWithoutExecutingAction' --logger 'trx;LogFileName=cross-final.trx' --results-directory artifacts\test-results\stage23 --verbosity minimal
```

Изолированный filter запускается в отдельном shell без DB env:

```powershell
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'Dependency!=Database&(FullyQualifiedName~PersistenceModelTests|FullyQualifiedName~PersistenceRegistrationTests|FullyQualifiedName~ProviderDesignTimeTests|FullyQualifiedName~DialogSettingsMappingTests)' --logger 'trx;LogFileName=cross-isolated.trx' --results-directory artifacts\test-results\stage23 --verbosity minimal
```

| TRX в artifacts/test-results/stage23 | Passed | Failed | Skipped | Граница |
| --- | ---: | ---: | ---: | --- |
| cross-first.trx |24|0|0|Первый запуск новых cases до добавления text compact/full budget; промежуточный, не суммируется |
| cross-final.trx |32|0|0|28 новых (SQLite19/PostgreSQL9) +4 complementary существующих (по2 на provider); в новых есть2 actual backup/restore |
| cross-isolated.trx |25|0|0|Текущие metadata, DI, mapping, runtime/design-time consistency после test-only reference; без соединения |

Финальные **57 distinct cases**. Весь old maintenance39, runner30, settings16, cleanup16 и старые core/transport suites повторно не запускались. Их отчёты остаются историческим evidence с собственными границами. Новые tests не выявили production-дефектов; runtime/schema контракт не менялся.

## Первоначальные ошибки и ограничения проверки

- Read/discovery: обращения к несуществующим guessed файлам `ResponsesTestHost.cs`/`AgentBridgeToolExtensions.cs`/каталогу src и Windows wildcard rg дали ошибки; реальные имена найдены, проверки не подменялись успехом.
- Draft `DialogId.New()` исправлен на actual `DialogId.From(Guid.NewGuid())` **до первого build**. Координатор независимо указал тот же draft риск.
- Runtime env-file первоначально собран неоднозначным PowerShell concat/array expression; исправлен и проверен на3 строки **до docker run**. Failed containers не создавались, credentials не печатались.
- Один multi-file apply_patch отклонён из-за неверного ожидаемого заголовка context; изменений не применил. Заголовок перечитан, повтор успешен.
- Единственное build warning CS9113 исправлено выше; тестовых failures не было.
- Первая PowerShell-команда статического encoding audit получила ParserError из-за inline не-ASCII regex markers; повторена с ASCII unicode escapes,15 файлов/170 local links успешно проверены. Файлы первая команда не меняла.
- Opaque compatibility port приложения, серверный estimator и реальные server token counts не реализуются в23. Шифрованные данные принимаются без выдуманного бюджета. Existing test settings/legacy Down/Up reports21 подтверждают nullable historical provenance; новые migrations/rollback в23 не генерировались/не выполнялись.
- Synthetic lost acknowledgement после настоящего writer commit и existing transaction wrapper — управляемые отказы, не kill/restart процесса и не реальная потеря network connection. Restart здесь — новый DI root над actual DB. External action не имеет общей transaction с хранилищем.
- **OpenSpec CLI не найден**; CLI validation не выполнена, static main/delta review не называется validation. Change не архивирован.

## Собственные ресурсы

Создан только контейнер `abverify-stage23-0565130b1ee5422fad62c6526b4ee35e`, ID `3359ef0839e85f49c365ecbbd1cfb6505b40d14d0d66f0b0a3a91dfb6c7ccd98`, label `agentbridge-verification=stage23-0565130b1ee5422fad62c6526b4ee35e`. Port **127.0.0.1:61856→5432**, tmpfs `/var/lib/postgresql:rw,size=1073741824`, Mounts[]/без volumes. Postgres:18 отсутствовал до задачи; digest **sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722**.

```powershell
docker version --format '{{.Server.Os}} {{.Server.Version}}'
docker image ls postgres:18 --format '{{.Repository}}:{{.Tag}} {{.ID}}'
docker pull postgres:18
docker run --detach --name abverify-stage23-0565130b1ee5422fad62c6526b4ee35e --label 'agentbridge-verification=stage23-0565130b1ee5422fad62c6526b4ee35e' --env-file 'artifacts\integration-stage23-0565130b1ee5422fad62c6526b4ee35e\container.env' --publish '127.0.0.1::5432' --tmpfs '/var/lib/postgresql:rw,size=1073741824' postgres:18
docker exec abverify-stage23-0565130b1ee5422fad62c6526b4ee35e pg_isready
docker inspect abverify-stage23-0565130b1ee5422fad62c6526b4ee35e --format '{{.Id}} {{.Name}} {{index .Config.Labels "agentbridge-verification"}} {{json .Mounts}} {{json .NetworkSettings.Ports}}'
docker exec abverify-stage23-0565130b1ee5422fad62c6526b4ee35e psql -U $taskRecord.user -d postgres -tAc "SELECT count(*) FROM pg_database WHERE datname LIKE 'abverify_%'"
docker ps -a --filter ancestor=postgres:18 --format '{{.ID}} {{.Names}}'
docker stop abverify-stage23-0565130b1ee5422fad62c6526b4ee35e
docker rm abverify-stage23-0565130b1ee5422fad62c6526b4ee35e
docker image rm postgres:18
```

Readiness подтвердил accepting connections. Перед cleanup число fixture DB было0; SQLite/backup fixture directories отсутствовали. Проверены exact label/name/mounts/port и отсутствие чужих containers образа. Собственные контейнер и newly pulled image удалены. После Resolve-Path проверенного absolute task directory удалены только2 temporary credential files и пустой каталог, без recursive shell delete:

```powershell
Remove-Item -LiteralPath 'D:\Media\User\source\repos\agent-bridge\artifacts\integration-stage23-0565130b1ee5422fad62c6526b4ee35e\container.env','D:\Media\User\source\repos\agent-bridge\artifacts\integration-stage23-0565130b1ee5422fad62c6526b4ee35e\run.json' -ErrorAction Stop
Remove-Item -LiteralPath 'D:\Media\User\source\repos\agent-bridge\artifacts\integration-stage23-0565130b1ee5422fad62c6526b4ee35e' -ErrorAction Stop
```

Test-Path=False, Docker filters по task label/image пусты. Игнорируемые compile/TRX остаются evidence и не входят в manifest.

## Передача следующему координатору

- Рабочий каталог **D:\Media\User\source\repos\agent-bridge**, существующие root csproj/slnx, master, без worktree. **19–23 реализованы, проверены и приняты. STOP после23;24–25 НЕ НАЧИНАТЬ.** Координатор отдельно разрешил локальный commit23 по explicit manifest15; full hash передаётся итоговым ответом, remote операции запрещены.
- Clean local commits:19=`de58342f5c80e46279e1d7b59fe0665c893c5d4b`,20=`95c28fae3773efa9e8e4d0281282340f5ac9e6c0`,21=`6482d595cb1f89eeb30ec33696026aa6c1bde1cc`,22=`89c28e839bf12584027b81494c31557c91279935`. Baseline23 tree/index были clean; hash локального commit23 — в git log и итоговом ответе, parent22 фиксирован выше. Previous evidence21:265core/171isolated/43transport/46DB,22:79core/61isolated/16DB — не суммировать с23 и не называть повторённым.
- Обязательные библиотеки read-only: **D:\Media\User\source\repos\work\EFCoreLibrary** (0.0.5) и **D:\Media\User\source\repos\work\HttpClientLibrary** (FileVersion0.0.0.5). Reference sources доступны: **D:\Media\User\source\repos\codex-lb**, **D:\Media\User\source\repos\TelegramCodexRelayBot**, **D:\Media\User\source\repos\work\AquaByte-Ledger\AquaByteLedger.Infrastructure\Services\DataBase**. Их исходники не менялись/не исполнялись.
- Финальная проверка библиотек: clean tree/index, EFCoreLibrary HEAD=`3a8a53187af3c5df049770dfd6727b5065159d1f`, HttpClientLibrary HEAD=`6d0528d940d1d8494c722c22464051dd961d6bf7`. При сдаче AgentBridge HEAD22 был неизменён, index пуст; только15 файлов manifest. UTF-8 без BOM/LF сохранены, U+FFFD/четыре вопросительных знака/проверенные mojibake markers отсутствуют,170 локальных ссылок ведут на существующие пути. Main/delta совпадают текстуально; это не CLI validation. Git diff --check чистый; autocrlf warnings не являются изменением файлов или ошибкой проверки.
- Actual contracts: AgentRunner.RunAsync; ContextCompactor/ContextCompactionResult; AddAgentBridgeCompaction; IDialogContextWriter.SaveAsync/SaveWithModelAsync; IModelSettingsReader.ReadWithAccessAsync; IDialogToolAttemptWriter.StartAsync/SaveOutcomesAsync; AgentSettingsService.ReadAsync/SelectAsync/GetStatusAsync; ExpiredDialogCleanup.CleanupAsync(limit,ct). HTTP только actual HttpClientLibrary; DB base CRUD + short scenario UoW, без external I/O/retry в transaction.
- Решения21: settings конкретного диалога в БД, независимая CAS version; active run держит атомарный TurnModelSettings. Opaque compatibility — порт приложения; без proof Unsupported. SelectedModel отличается от actual server model. Metadata settings/provenance не ContentBytes. Generated AddDialogSettings21 оставляет historical SettingsJson/SelectedModel nullable; null нельзя заменять defaults. Старые migrations не регенерировать.
- Individual key приоритетен, shared только при null; pinned ModelAccess не persist. Budget — только input_context_window, actual offline BPE exact mapping, unsupported IDs не prefixes/fallback. Opaque unknown budget сохраняется отдельно от known tokens; generation guard обязателен после compact. ThroughTurnSequence — непрерывный terminal prefix, включая0; activity/compact не продлевают fixed expiry. Existing TurnId не replay; repeated completed call_id допустимы. Unknown не означает rollback.
- Evidence23: **32 actual DB +25 isolated**, fake HTTP не live compatibility, restart DI root не OS crash. SQL Server/MySQL не подтверждены. CLI OpenSpec отсутствует; changes не архивированы.

**Пропущено по указанию пользователя:** working/live codex-lb/OpenAI/upstream/accounts/tokens, HTTP server/hosting/TestServer/WebApplicationFactory, Telegram/application/demo/dev server, рабочие DB/secrets/data/volumes, SQL Server/MySQL, произвольные project/user scripts, pack/publish, remote Git/PR/uploads/deployment, этапы24–25. До приёмки add/commit не выполнялись.

## Полный manifest — 15 файлов

Координатор независимо проверил manifest15, ordinary/staged diff, TRX32/0/0 и25/0/0, encoding и отсутствие собственных ресурсов23. После приёмки разрешены только финализация статусов и локальный English Conventional Commit с parent22; production/tests не меняются, успешные тесты без новой причины не повторяются. Состав коммита и чистота index/tree проверяются после записи; hash возвращается итоговым ответом без рекурсивной вставки в этот документ.

Включает modified/untracked; build outputs/TRX/credentials исключены. Перед stage/commit проверить ordinary и staged diff, exact paths и отсутствие чужих правок.

```text
README.md
Documentation/README.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/Plans/AgentBridge Initial Implementation/23-cross-component-verification.md
openspec/changes/cross-component-verification/proposal.md
openspec/changes/cross-component-verification/tasks.md
openspec/changes/cross-component-verification/context.md
openspec/changes/cross-component-verification/specs/agent-runtime/spec.md
openspec/specs/agent-runtime/spec.md
openspec/specs/agent-runtime/context.md
tests/AGENTS.md
tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj
tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md
tests/AgentBridge.Persistence.EfCore.Tests/Integration/CrossComponentFixture.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/CrossComponentIntegrationTests.cs
```
