# 03 — Подключение Serilog приложения

Статус: **Завершён и принят координатором** (2026-10-03). Зависимости: **01, 02**. Разрешён локальный коммит перечисленных 19 файлов.

## Цель

Направить диагностику библиотеки через Serilog приложения.

## Задачи

- [x] Использовать `ILogger<T>` в реализованной основе диагностики и описать регистрацию Serilog приложением.
- [x] Определить полезные поля операции, корреляции, статуса и длительности.
- [x] Отражать отмену вызывающего кода как отмену, а не как таймаут с уровнем ошибки; сам сценарий сохраняет исходное исключение.
- [x] Исключить API-ключи, строки подключения и обычное содержимое диалогов из диагностических событий.
- [x] Не создавать глобальный logger и не выбирать файловые sinks внутри библиотеки.

## Фактический результат

- `Diagnostics/`: `AgentBridgeDiagnostics` с `ILogger<AgentBridgeDiagnostics>`, явное наблюдение `AgentBridgeDiagnosticOperation`, закрытый `AgentBridgeOperation` и групповая регистрация `AddAgentBridgeDiagnostics`.
- DI подключает Microsoft.Extensions.Logging `10.0.3`, сохраняет фабрику, providers и фильтры приложения; повторная регистрация не дублирует службу. В production нет Serilog-зависимости, global logger и sinks.
- Единственное итоговое событие содержит Operation, OperationId, CorrelationId, Status, ErrorCode и монотонно измеренную DurationMs. Произвольные строки и объекты конфигурации API не принимает; Exception не передаётся logger, Message/ToString/Data/InnerException и имя типа не читаются.
- Для OperationCanceledException используются два отдельных исходных токена. Caller имеет приоритет (Canceled/Information); только deadline — DeadlineExceeded/Warning; неизвестная причина — Failed/Error с UnattributedCancellation. Обычная ошибка не превращается в отмену или таймаут по имени исключения либо токену. Наблюдение не исполняет сценарии, не отменяет работу и не заменяет исходное исключение.
- 18 новых изолированных тестов через публичную DI/diagnostics-границу: собирающий logger, сохранение DI/фильтров, метаданные/длительность/корреляция, отмена/deadline, отсутствие утечек, fail-fast и явное завершение. Реальный Serilog provider приложения проверен с sink в памяти, без host и файлов. Serilog `4.3.0` и Serilog.Extensions.Logging `10.0.0` подключены только в тестовом проекте.
- Обновлены README, техническая документация, OpenSpec context, ближайшие AGENTS и навигаторы. Нормативный `spec.md` сохранён: новые бизнес-сценарии не введены. Пример наблюдает реально существующий IStartupValidator, не фиктивный AgentRunner. Автоматической диагностики всех сценариев агента ещё нет.
- По ревью координатора XML comments классов test doubles с интерфейсами заменены на inheritdoc с русским remarks; замечание касается только документации.

## Фактические проверки

Все команды выполнялись последовательно из `D:\Media\User\source\repos\agent-bridge`, SDK `10.0.401`. Перед запуском проверены затронутые `.csproj`, отсутствие действующих общих Directory.Build/Directory.Packages, NuGet.config и lock-файлов, проектных Exec/hooks/imports; новые пакетные build imports сверены. В Serilog.targets только RuntimeHostConfigurationOption для trimming. Тесты используют только контейнер, синтетические исключения и события в памяти. Восстановление выполнено из локального кэша с NuGetAudit=false, без публикации.

Точные команды:

```powershell
dotnet restore .\agent-bridge.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet restore .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet build .\agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
```

| Проверка | Результат |
| --- | --- |
| Restore ядра и тестового проекта | Успех, локальные пакеты |
| Compile-check ядра | 0 ошибок, 0 предупреждений |
| Первый compile-check тестового проекта | CS0619: xUnit выбрал async-перегрузку Throws для всегда выбрасывающей lambda; исправлено явным Action |
| Compile-check тестового проекта после исправления | 0 ошибок, 0 предупреждений |
| Дополнительный compile-check тестового проекта после XML-cref правок по приёмке | 0 ошибок, 0 предупреждений; XML references подтверждены |
| Изолированные тесты ядра | Пройдено 36 (18 прежних + 18 новых), упало 0, пропущено 0 |

Первоначальный build тестового проекта выполнен дважды из-за указанной ошибки; тесты — один раз после исправления. После XML-cref правок и приёмки координатором выполнен дополнительный адресный build той же командой для проверки генерируемых ссылок документации: 0 ошибок, 0 предупреждений. Поведение не менялось, runner повторно не запускался. Проверки других двух адаптеров не повторялись: их исходники и project-файлы на этапе 03 не менялись.

Статически проверены все 19 изменённых файлов: строгая UTF-8-декодировка, отсутствие U+FFFD, четырёх вопросительных знаков, обнаруживаемых маркеров mojibake и trailing whitespace. Локальные Markdown-ссылки ведут на существующие файлы, LF сохранён. `git diff --check` прошёл; предупреждения Git об autocrlf не являются ошибками diff. Координатор принял исходники, тесты, API, документацию и результаты проверок, разрешил локальный коммит ровно перечисленных 19 файлов. Статусы этапа 03 и навигаторов синхронизированы с приёмкой.

## Изменённые файлы

19 путей относительно `D:\Media\User\source\repos\agent-bridge`; производные файлы исключены из Git.

```text
AGENTS.md
README.md
agent-bridge.csproj
Diagnostics/AGENTS.md
Diagnostics/AgentBridgeDiagnostics.cs
Diagnostics/AgentBridgeDiagnosticsExtensions.cs
Diagnostics/AgentBridgeDiagnosticOperation.cs
Diagnostics/AgentBridgeOperation.cs
tests/AGENTS.md
tests/AgentBridge.Tests/AgentBridge.Tests.csproj
tests/AgentBridge.Tests/DiagnosticsTests.cs
Documentation/README.md
Documentation/Plans/README.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
Documentation/Plans/AgentBridge Initial Implementation/03-serilog-integration.md
Documentation/Technical documentation/README.md
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/07-tokenizer-and-settings.md
openspec/specs/agent-runtime/context.md
```

## Ограничения и пропуски

- Проверена доступная основа диагностики, а не интеграция AgentRunner, HTTP или БД. Имена будущих операций в enum не означают их реализации. Владелец сценария отвечает за явное завершение наблюдения и исходные, не linked, caller/deadline-токены.
- Приложение отвечает за безопасные собственные scopes/enrichers и события других компонентов; диагностика AgentBridge не очищает сторонний pipeline.
- HttpClientLibrary и EFCoreLibrary не изменены. Известные HTTP response snippet/error metadata и backup gaps остаются этапам 04/05. Эти этапы не начаты; соседние проекты не анализировались повторно без необходимости.
- Приложение, hosting/TestServer/WebApplicationFactory, реальные HTTP/codex-lb/OpenAI, БД/SQL, migrations и backup/restore: **Пропущено по указанию пользователя**.
- Произвольные проектные скрипты и OpenSpec CLI: **Пропущено по указанию пользователя**. Соответствие требованиям проверено статически, автоматическая OpenSpec-валидация не заявляется.
- Ветка `master` сохранена. Координатор разрешил точечный git add и локальный commit перечисленных 19 файлов после проверки diff/staged diff; remote/push/PR отсутствуют. Hash коммита приводится в итоговом отчёте.

## Проверка и завершение

Использовать тестовый logger, сохраняющий события, для проверки структуры записей и отсутствия секретов. Этап завершён, когда регистрация библиотеки сохраняет конфигурацию логирования приложения.

Источник: [решение по Serilog](<../../Technical documentation/07-tokenizer-and-settings.md>).
