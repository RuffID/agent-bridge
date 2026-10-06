# 13 — Явная конфигурация и fail-fast

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **принят в A/B**. Зависимость: 00; 12 для MSSQL-specific settings. Область: Configuration, adapter options и контракты приложения.

## Цель и исходное состояние

Все настройки приходят из выбранного IConfiguration приложения; обязательные параметры не заменяются скрытыми defaults. Сейчас binding и ValidateOnStart уже есть, но MaxToolSteps, retention, compaction, effort и timeouts имеют начальные значения. Непереданный ключ может пройти validation с таким значением — это отличие от согласованного строгого режима.

## Работы

1. Составить точную таблицу: параметр, владелец, тип/диапазон, обязательность выбранного режима, источник и безопасный текст ошибки. Включить Agent/Retention/Compaction/CodexLb/Database и отдельно включаемое Maintenance.
2. Проверять существование выбранного раздела и обязательных ключей, пустые значения, malformed binding и диапазоны до первой операции. Нулевой допустимый reserve отличать от отсутствующего reserve; значение, равное прежнему default, не доказывает передачу настройки.
3. Не добавлять собственный config loader или appsettings lookup. Поддержать merged root/section, отдельный config и environment/secret providers с обычным приоритетом IConfiguration. В runtime services сохранять typed options.
4. Убрать скрытые defaults обязательных параметров либо применять явную проверку их передачи в configuration API; programmatic overload подчинить той же обязательности. Это изменение публичного поведения: назвать несовместимость и migration path, не выдавать её за refactoring.
5. Уточнить режим key source и источника Instructions: необязательное значение допустимо только по explicit выбранному контракту. Не заставлять приложения с индивидуальными keys хранить общий секрет и не вводить fallback при его ошибке.
6. Сохранить модельную проверку exact ID/effort/full budget в catalog/guard. Required options validation не запускает HTTP для проверки модели и не создаёт DB connection.
7. Расписание cleanup и batch limit описать как settings приложения: приложение читает их из своего IConfiguration, создаёт short scope и явно вызывает CleanupAsync. Retention и compact не становятся расписанием или background job.
8. Документировать logger ownership: file path/rotation/sinks задаются и валидируются приложением; AgentBridge получает ILogger. Нет второго logger/file writer в библиотеке. Для обязательного файлового режима отсутствие пути останавливает регистрацию логирования приложения; console/custom logger не требует фиктивный путь.

## Проверки

B: missing/empty section, каждый отсутствующий обязательный ключ, invalid types/ranges, разрешённый explicit zero, conditional keys/maintenance, несколько IConfiguration providers и overrides. При errors проверять безопасные paths/codes без secret values.

Проверить стандартный IStartupValidator и явную проверку для потребителя без host. BuildServiceProvider внутри registration, скрытый host, DB/HTTP/file logging и background jobs запрещены. Compile-check конкретных production/test проектов; logging fixture в памяти.

## Критерии завершения

Для каждого обязательного settings field отсутствие воспроизводимо даёт исключение до первого library сценария, а явно заданные значения работают. Нет незаметного default/fallback, секретов в errors, собственного файла config или logger. Несовместимость с прежним поведением отражена в docs/tests.

## Результаты

### Дата, полномочия и граница

2026-10-06, Asia/Novosibirsk. Единственный writer13 работал в existing checkout AgentBridge. Входной и финальный HEAD `7668d7a320c16ec791935e0d9a1e0c3cc77e965c`; 00 и12 приняты до начала. На входе изменён только чужой Coordination.md; он исключён из manifest и не редактировался. Stage13 исходное задание выше сохранено, изменены только статус и Results. Add/commit/push, ветки/worktrees, приложения/hosting, HTTP/БД/SQL/native/process, migrations tooling, restore/network не выполнялись. Приёмка и поручение на commit ещё ожидаются.

Соседи read-only, собственные manifests пусты: EFCoreLibrary HEAD `5962deceb6ea01306cbbda400040db88ea8e5df9`, HttpClientLibrary HEAD `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`, оба чисты на выходе. Codex-lb HEAD `f2b8e042c4ce012ae703bc939413bf9961b00032` сохраняет чужую untracked `.vs/`; она не читалась/не изменялась. Использованы csharp-project-rules (style/build-validation/agents-maintenance), aspnetcore-project-rules, backend-uow-repositories с reference; storage/UoW архитектура не менялась. Root/Configuration/Application/adapters/tests/Delivery/Integration/Documentation/Plans AGENTS, README/Decisions, Results00/12, Findings/итог15 и main spec прочитаны в относящихся к13 границах; исторические отчёты не переписаны.

### Before/after и обязательность

Before ниже — **статический actual source baseline**, а не новый прогон старой DLL. Старые тесты EmptyCoreSectionUsesTrialDefaults/MinimalConfigurationPreservesModelAndTrialDefaultsWithoutSharedKey прямо ожидали defaults; их прежний текст доступен в HEAD. Fresh B evidence получено после изменений; intermediate own failures отдельно перечислены ниже.

| Граница | Before из source | After / различающий B evidence |
| --- | --- | --- |
| Agent/Retention/Compaction | MaxToolSteps8, retention7d/10MiB, compact32000/4096/3 заменяли omission | Рабочие defaults убраны; каждое missing/blank config поле и каждое programmatic omission отвергаются; explicit прежние значения и reserve0 приняты |
| Reserve presence | Без ключа получался4096; initial own sentinel implementation не учитывала null→zero Binder | Configuration presence проверяется до Binder независимо от options; programmatic initial -1 недопустим, explicit0 допустим |
| Prior Configure | Заполненные options могли скрыть missing selected configuration | Required keys/sections проверяются непосредственно в переданном IConfiguration; regressions для prior Configure + всех core/CodexLb/Database/conditional Backup полей; valid PostConfigure сохранён |
| Instructions | request ?? options ?? string.Empty без явного режима | Nullable InstructionsSource обязателен; Configuration требует options instructions, request override сохранён; PerRequest требует request instructions до первого storage/provider/model I/O, без options fallback |
| Key source | Общий секрет был необязателен без explicit source mode | Nullable KeySource обязателен; Shared требует общий key, сохраняет individual priority; Individual + null + provided shared даёт Unauthorized/calls0; invalid individual/source/HTTP не разрешают fallback |
| CodexLb | effort medium и deadlines180s заменяли omission | Каждый scalar обязателен; arbitrary nonempty model/effort принимается локально без обещания catalog capabilities; deadline/range/address/credential guards сохранены |
| Safe binding | InvalidOperationException Binder мог включать raw malformed value/inner | Standard Binder ограждён Configure и ConfigurationChangeTokenSource; safe OptionsValidationException содержит group/property/path и code без value/unsafe inner, включая overflow/undefined enum |
| Database/maintenance | Provider/connection обязательны, Binder error unsafe; backup условный | Explicit Provider/ConnectionString, локальный generic connection syntax без provider/DB I/O; backup12 conditional presence/limits сохранены, prior Configure не скрывает missing; disabled maintenance не требует Backup |
| Options pipeline | Standard options и reload существовали | IOptions/IOptionsSnapshot/IOptionsMonitor, reload, Configure/PostConfigure, root/section/merged precedence сохранены; synthetic memory doubles environment/secret providers, без чтения actual секретов/files |

Точная [таблица settings](<../../Technical documentation/05-configuration-and-lifecycle.md>) содержит paths/types/ranges/required/conditional/owner/safe reasons, отдельно app cleanup/logging. Изменение названо breaking behavior и имеет migration path: явно передать желаемые прежние лимиты и source modes. Current README/guide/catalog examples синхронизированы; compile-only [StrictConfigurationRegistration](../../../tests/Delivery/Consumer/StrictConfigurationRegistration.cs) linked в persistence tests, методы не исполнялись. Его compilation не является external binary kit evidence.

Safe helper не открывает config и не создаёт options store: стандартный ConfigurationBinder используется внутри Configure, стандартный ConfigurationChangeTokenSource — для reload; typed runtime services не получают IConfiguration. IStartupValidator доступен явно без host; несколько local options failures дают standard AggregateException. Registration не вызывает BuildServiceProvider. Programmatic callbacks проходят те же local validators; полностью пустой callback не получает рабочую политику.

Расписание/bounded batch, provider/sinks/path/rotation/retention ILogger/Serilog принадлежат приложению. Disabled cleanup не требует schedule; console/custom sink не требует file path. App file-mode validation описана как app responsibility, её фактическая filesystem/runtime проверка13 не заявлена. Существующие logger tests используют память. Retention/fixed expiry, backup retention и compact остаются отдельными политиками; новый logger/scheduler/background service не добавлен.

### Exact manifest

Только AgentBridge, **39 файлов**; Coordination, ignored outputs и соседние repo исключены:

```text
Application/AGENTS.md
Application/AgentRunner.cs
Application/Models/AgentRunRequest.cs
Configuration/AGENTS.md
Configuration/AgentBridgeConfigurationExtensions.cs
Configuration/AgentInstructionsSource.cs
Configuration/AgentOptions.cs
Configuration/ContextCompactionOptions.cs
Configuration/DialogRetentionOptions.cs
Configuration/SafeOptionsBindingExtensions.cs
Documentation/Plans/AgentBridge Audit Remediation/13-strict-configuration.md
Documentation/Technical documentation/05-configuration-and-lifecycle.md
Documentation/Technical documentation/13-model-catalog-and-keys.md
Documentation/Technical documentation/25-usage-guide.md
README.md
adapters/AgentBridge.CodexLb/AGENTS.md
adapters/AgentBridge.CodexLb/Configuration/CodexLbConfigurationExtensions.cs
adapters/AgentBridge.CodexLb/Configuration/CodexLbOptions.cs
adapters/AgentBridge.CodexLb/Configuration/ModelKeySourceMode.cs
adapters/AgentBridge.CodexLb/Models/CodexLbModelAccessResolver.cs
adapters/AgentBridge.Persistence.EfCore/AGENTS.md
adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseConfigurationExtensions.cs
adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseMaintenanceRegistrationExtensions.cs
openspec/specs/agent-runtime/spec.md
tests/AgentBridge.CodexLb.Tests/CodexLbConfigurationTests.cs
tests/AgentBridge.CodexLb.Tests/ModelCatalogTests.cs
tests/AgentBridge.CodexLb.Tests/ResponsesJsonTests.cs
tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj
tests/AgentBridge.Persistence.EfCore.Tests/DatabaseConfigurationTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/DatabaseMaintenanceRegistrationTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/CrossComponentFixture.cs
tests/AgentBridge.Persistence.EfCore.Tests/Integration/DialogSettingsIntegrationTests.cs
tests/AgentBridge.Tests/AgentRunnerTests.cs
tests/AgentBridge.Tests/AgentSettingsTests.cs
tests/AgentBridge.Tests/ConfigurationTests.cs
tests/AgentBridge.Tests/DialogTests.cs
tests/Delivery/AGENTS.md
tests/Delivery/Consumer/StrictConfigurationRegistration.cs
```

Existing isolated fixtures задают явные значения вместо defaults. Два integration fixtures только migrated source/compile; Dependency=Database не запускался. Generated migrations/snapshots не изменялись.

### Preflight, команды и свежие результаты

Прочитаны concrete production/test csproj и transitive HTTP/EF maintenance/migrations projects, ancestor build files (до D:/), HttpClientLibrary Directory.Build.props, imports/targets/Exec/package declarations и существующие assets. Custom Exec/targets в затронутой цепочке отсутствуют; packaging EFCoreLibrary подавлен GeneratePackageOnBuild=false. Assets доступны, packages/dependencies не менялись; только compile linked source в test csproj, restore не нужен и не выполнялся. Все builds — конкретные проекты/Debug/Build/no-restore, отдельный BaseOutputPath. Перед каждым --no-build test был успешный build того же project/output, stale DLL после failed build не тестировались. Persistence filter проверен по actual class traits, все Integration классы с Dependency=Database исключены.

Финальные команды, cwd `D:/Media/User/source/repos/agent-bridge`, все **Exit0**:

```powershell
dotnet build agent-bridge.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ -flp:logfile=artifacts/stage13-core-production-final-build.log
dotnet build adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ -flp:logfile=artifacts/stage13-codex-production-final-build.log
dotnet build adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ -flp:logfile=artifacts/stage13-persistence-production-final-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ -flp:logfile=artifacts/stage13-core-tests-final-build.log
dotnet build tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ -flp:logfile=artifacts/stage13-codex-tests-final-build.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ -flp:logfile=artifacts/stage13-persistence-tests-final-build.log
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ --logger 'trx;LogFileName=stage13-core-final.trx' --results-directory artifacts/stage13
dotnet test tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ --logger 'trx;LogFileName=stage13-codex-final.trx' --results-directory artifacts/stage13
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage13/ --filter 'Dependency!=Database' --logger 'trx;LogFileName=stage13-persistence-final.trx' --results-directory artifacts/stage13
& 'D:/Media/User/AppData/npm/openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive
git diff --check
```

| Final artifact (от корня AB) | Passed / Failed / Skipped | Evidence |
| --- | --- | --- |
| artifacts/stage13/stage13-core-final.trx | 502 / 0 / 0 | Actual core/DI/Options/runner/BPE/ILogger, controlled storage/model/provider doubles |
| artifacts/stage13/stage13-codex-final.trx | 403 / 0 / 0 | Actual adapter/HTTP library с fake handler/local streams; no actual network |
| artifacts/stage13/stage13-persistence-final.trx | 232 / 0 / 0 | Actual configuration/EF disconnected metadata/maintenance coordination; repository/provider/process doubles, no DB/native/process |
| artifacts/stage13-*-final-build.log | Все6: 0 warnings / 0 errors | Concrete production/test compile; source example и integration fixtures только compile |
| OpenSpec main strict tool output | valid=true, issues=[], passed1/failed0 | Exact разрешённая CLI09 команда; schema version1.0 не выдана за CLI version |

TRX counters совпадают с result rows502/403/232; IntegrationTests rows0. Existing predecessor regressions01–12 включены в relevant suites, overlapping runs не суммируются. Wrapper openspec.cmd, bin/openspec.js и package.json прочитаны заново: wrapper указывает на @fission-ai/openspec, package version1.14.1, bin импортирует dist/cli/index.js. Version/help/install CLI не запускался; output version1.0 — schema. Historical18 changes не изменялись/не перевалидировались на13.

Промежуточные команды отличались от final только такими параметрами (их exact argv полностью задаются соответствующим final project/template и указанными заменами):

- Core build без GeneratePackageOnBuild property, log `stage13-core-initial-build.log`: Exit1, отсутствующий using для Options Bind extension; corrected `stage13-core-corrected-build.log`: Exit0. Core test без GeneratePackageOnBuild property, TRX `stage13-core-initial.trx`: Exit1, Passed475/Failed4/Skipped0. Failures обнаружили own null→zero reserve issue, неполный range fixture и standard AggregateException expectation. После correction core build `stage13-core-second-build.log`, test `stage13-core-second.trx`: Exit0,491/0/0.
- Codex build без GeneratePackageOnBuild property, log `stage13-codex-initial-build.log`: Exit1, using был добавлен в EOF, исправлен точечно; corrected `stage13-codex-corrected-build.log`: Exit0. Codex test без этой property, TRX `stage13-codex-initial.trx`: Exit0,376/0/0. Следующие build `stage13-codex-second-build.log`, test `stage13-codex-second.trx`: Exit0,395/0/0.
- Persistence build с final properties, log `stage13-persistence-initial-build.log`, test с final filter/properties, TRX `stage13-persistence-initial.trx`: Exit0,211/0/0.
- Main strict первый запуск той же exact командой: Exit1, valid=false, три length warnings новых requirements; новые тексты разделены без удаления clauses/scenarios, второй запуск final Exit0/valid=true/issues[]. Code/tests после final green не менялись, только spec/docs/results.

### Остаток и передача

Строгая UTF-8 проверка всех39 manifest files не обнаружила U+FFFD, четыре question marks или выбранные mojibake markers; рабочие manual files LF, отдельно прочитанные HEAD blobs AgentRunner/AgentSettingsTests/ModelCatalogTests/AgentOptions также LF. Проверены65 local documentation links без missing, test project XML корректен, исходное задание13 до Results совпадает с HEAD после нормализации только статуса. Diff check Exit0, staged diff пуст. Безопасные ошибки проверены на synthetic keys/connection/password/address credentials/overflow values; actual secrets/config files не открывались. Static before, actual algorithm/options/DI и doubles evidence отделены.

Блокеров локальной A/B реализации13 нет. Actual catalog/model capabilities требуют HTTP19; server/provider authentication/TLS/backup/restore17, external DLL kits15/16 и runtime18 не доказаны. App scheduler и file sink validation принадлежат приложению; этот этап не создаёт их реализацию и не запускает hosted сценарии. Результат готов к review координатором; до явной приёмки и поручения add/commit исполнитель останавливает shared writes.

**Приёмка13, 2026-10-06:** координатор от имени пользователя принял A/B после независимой сверки production/helper/source modes/options binding, missing/prior Configure/conditional и positive controls, source consumers/docs/spec, фактических6 successful builds, final TRX502/403/232 rows/counters без Integration, actual strict Exit0 и UTF8/LF39/diff check. Поручен один локальный английский breaking Conventional Commit только exact39 manifest выше, без Coordination, соседей и outputs. После final green изменены только статус и эта запись о приёмке; source/tests не менялись. Provider/native/live17–19 и kits15–16 остаются открытыми; предыдущий checkpoint передачи сохранён как история.
