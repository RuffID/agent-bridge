# 13 — Реализовать каталог моделей и выбор API-ключа

Статус: **Реализован и принят; запрещённые проверки пропущены**. Зависимости: **02, 04, 07** приняты. Цепочка приостановлена по указанию пользователя после этапа 13; этапы 14–25 не начаты.

## Цель

Выбирать нужный ключ доступа и проверять параметры модели по возможностям codex-lb.

## Задачи

- [x] Реализовать IIndividualModelKeySource приложения и общий SharedApiKey из CodexLbOptions, предоставленных приложением.
- [x] Использовать общий ключ только при null индивидуального; заданный ошибочный ключ/source exception/HTTP-отказ не разрешают fallback.
- [x] Получать канонический `/v1/models` через actual HttpClientLibrary и сохранять необходимые метаданные возможностей моделей независимыми снимками.
- [x] Проверять точный model/effort и threshold+reserve против объявленного input_context_window без статического каталога.
- [x] Исключить секреты/raw metadata/подключения из возвращаемых снимков и логов; проверить actual pipeline, fixed errors и отсутствие повторов.

## Проверка и завершение

Проверены приоритет/null/отсутствие/непригодный индивидуальный ключ, source failure, 401/403/429/500 без смены ключа, изменённые budgets/efforts, override без изменения defaults, пустой/повреждённый каталог, metadata gaps/types, точная граница input budget и переполнение, независимость списков, caller cancellation, stream disposal, 65536+1-byte error capture и отсутствие секретов в structured state/отформатированном ILogger. Регрессия ревью проверяет тот же ServiceError после typed catalog failure с поздней отменой; успешный каталог с отменой отклоняется. Все HTTP-ответы/потоки подставные; сеть отсутствует.

## Фактическая реализация и ограничения

Application не зависит от HTTP/EF: добавлены 4 порта, capabilities/catalog/settings snapshots и ModelSelectionValidator. CodexLbModelAccessResolver/ModelSettingsReader используют IOptionsSnapshot и копируют выбор/числовые лимиты до await. AddCodexLbModelCatalog отдельно регистрирует scoped actual HttpApiClient и порты; приложение владеет HttpClient/timeout/handlers и источником индивидуального ключа. Регистрация/разрешение не отправляют запросы. [Точные API и пример](<../../Technical documentation/13-model-catalog-and-keys.md>).

Текущий local codex-lb сверён статически по route/schema/auth/model_registry и сборке model sources. client_version не добавляется; canonical base-prefix + `/v1/models`. Только input_context_window используется как доступный входной budget; max_output_tokens повторно не вычитается. Unknown input budget даёт Unsupported без fallback на context_window. Не выдавать flags каталога за подтверждение Responses/compact или серверной аутентификации. Политика auth и возможная enforced model codex-lb не меняются.

Реализовано только чтение/проверка model/effort/threshold/reserve, без сохранения пользовательского выбора, полного settings/status, tokenizer или фиксации выполняющегося turn. Responses/SSE/compact/composition/agent loop и этапы 14–25 не реализованы. Production code, тесты, документация, текущие контракты codex-lb и финальный отчёт просмотрены координатором; этап 13 принят.

## Фактические команды и результаты

Все команды выполнены с рабочим каталогом `D:\Media\User\source\repos\agent-bridge`, ветка master. До запуска проверены затронутые csproj, ancestor Directory.Build.*/NuGet.config/locks и generated imports; custom Exec/hooks не добавлены. Соседние исходники read-only. Restore восстановил только нужную цепочку 4 проектов, без pack/publish. Во всех командах явно GeneratePackageOnBuild=false, хотя этот путь не ссылается на EFCoreLibrary.

```powershell
dotnet restore tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -p:GeneratePackageOnBuild=false
dotnet build tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage13\
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage13\ --logger "trx;LogFileName=stage13-codexlb.trx" --results-directory D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage13\results
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage13\
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage13\ --logger "trx;LogFileName=stage13-core.trx" --results-directory D:\Media\User\source\repos\agent-bridge\artifacts\compile-check\stage13\results
```

| Проверка | Фактический результат |
| --- | --- |
| Адресный restore | Exit 0; 2 проекта восстановлены, 2 уже актуальны |
| Первая сборка CodexLb.Tests | CS9007 в новой raw JSON-фикстуре; production-проекты собраны, ошибка исправлена в тесте |
| CodexLb.Tests build после исправления | Exit 0, 0 warnings/errors; также собраны ядро, адаптер и HttpClientLibrary net10 |
| Первая серия CodexLb.Tests | 55 passed / 0 failed / 0 skipped; 35 новых |
| Core build/test | Exit 0, 0 warnings/errors; 121 passed / 0 failed / 0 skipped; 14 новых |
| Финальный CodexLb.Tests build/test после 6 meaningful metadata regressions и усиления secret/log assertions | Exit 0, 0 warnings/errors; 61 passed / 0 failed / 0 skipped; 41 новых |
| Финальная статическая сверка | Manifest совпадает с git status: 39 файлов; strict UTF-8 без BOM, сохранён LF, нет mojibake, replacement chars или четырёх вопросительных знаков; git diff --check exit 0; master; HttpClientLibrary и EFCoreLibrary clean |

Финальная совокупность: **182 passed, 0 failed, 0 runner-skipped; 55 новых**. Изолированные тесты не доказывают живой сервер/upstream. После чисто документальных правок проверки не повторяются. TRX/build outputs находятся в игнорируемом artifacts/compile-check/stage13 и не входят в изменения исходников.

Реальные HTTP/codex-lb/OpenAI, hosting/запуск приложения/библиотеки, TestServer/WebApplicationFactory, БД (включая SQLite in-memory/EF InMemory), SQL, migrations apply/rollback, backup/restore/native/dump/external processes, Docker и project/user scripts — **Пропущено по указанию пользователя**. Это не runner skips. OpenSpec CLI отсутствует в PATH (повторно проверено Get-Command): CLI validation не выполнена; change не архивирован.

## Точный состав файлов

39 файлов в agent-bridge; соседние библиотеки, codex-lb, Domain, generated migrations, root csproj/slnx не изменены.

```text
AGENTS.md
Application/AGENTS.md
Application/ModelSelectionValidator.cs
Application/Models/ModelAccess.cs
Application/Models/ModelCapabilities.cs
Application/Models/ModelCatalogSnapshot.cs
Application/Models/ModelSettingsSnapshot.cs
Application/Ports/IIndividualModelKeySource.cs
Application/Ports/IModelAccessResolver.cs
Application/Ports/IModelCatalog.cs
Application/Ports/IModelSettingsReader.cs
Documentation/Business logic/06-models-and-status.md
Documentation/Plans/AgentBridge Initial Implementation/13-model-catalog-and-keys.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/README.md
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/03-http-and-codex-lb.md
Documentation/Technical documentation/05-configuration-and-lifecycle.md
Documentation/Technical documentation/07-tokenizer-and-settings.md
Documentation/Technical documentation/09-application-ports.md
Documentation/Technical documentation/13-model-catalog-and-keys.md
Documentation/Technical documentation/README.md
README.md
adapters/AgentBridge.CodexLb/AGENTS.md
adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj
adapters/AgentBridge.CodexLb/Configuration/CodexLbModelCatalogExtensions.cs
adapters/AgentBridge.CodexLb/Models/CodexLbModelAccessResolver.cs
adapters/AgentBridge.CodexLb/Models/CodexLbModelCatalog.cs
adapters/AgentBridge.CodexLb/Models/CodexLbModelSettingsReader.cs
adapters/AgentBridge.CodexLb/Models/ModelCatalogJsonReader.cs
openspec/changes/model-catalog-and-keys/context.md
openspec/changes/model-catalog-and-keys/proposal.md
openspec/changes/model-catalog-and-keys/specs/agent-runtime/spec.md
openspec/changes/model-catalog-and-keys/tasks.md
openspec/specs/agent-runtime/context.md
openspec/specs/agent-runtime/spec.md
tests/AGENTS.md
tests/AgentBridge.CodexLb.Tests/ModelCatalogTests.cs
tests/AgentBridge.Tests/ModelSelectionTests.cs
```

Координатор принял этап 13 и разрешил один локальный коммит ровно перечисленных 39 файлов после проверки diff/staged diff, состава и кодировки. Факт коммита, subject и полный hash подтверждаются отдельным отчётом; собственный будущий hash в документ не записывается. Remote/push/PR/смены ветки/worktree/других чатов или агентов нет. Цепочка приостановлена по указанию пользователя после этапа 13; этапы 14–25 не начаты и не запускаются.

Источник: [модели и статус](<../../Business logic/06-models-and-status.md>).
