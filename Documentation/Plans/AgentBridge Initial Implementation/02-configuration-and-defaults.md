# 02 — Конфигурация и начальные значения

Статус: **Завершён и принят координатором** (2026-10-03). Зависимости: **01**.

## Цель

Представить все согласованные настройки типизированными options, управляемыми приложением и явно проверяемыми.

## Задачи

- [x] Определить отдельные типы настроек агента, codex-lb, БД, хранения и сжатия.
- [x] Задать переопределяемые пробные значения: 7 дней хранения, мягкий порог 10 МиБ и порог сжатия 32 000 токенов.
- [x] Включить документированные запас бюджета, максимальное число проходов сжатия, шагов инструментов, время ожидания и effort по умолчанию.
- [x] Убедиться, что срок истечения использует настроенный период, а не фиксированное вычисление на семь дней.
- [x] Привязать и проверить настройки через расширения composition root, не внедряя `IConfiguration` в сценарии.
- [x] Разделить локальные ошибки отсутствия обязательных значений/диапазонов и будущую проверку конкретной модели по каталогу этапа 13; не вводить фиктивный серверный список.

## Фактический результат

- Ядро: `AgentOptions`, `DialogRetentionOptions`, `ContextCompactionOptions`, `AddAgentBridgeConfiguration` с binding групп и программной перегрузкой.
- Адаптер codex-lb: `CodexLbOptions` и `AddCodexLbConfiguration`; адрес и модель обязательны, общего ключа может не быть при индивидуальных ключах. Значения `medium` и модели сохраняются без скрытой подмены.
- Адаптер хранения: `DatabaseOptions`, nullable `DatabaseProvider` (`SQLite`/`PostgreSql`) и `AddDatabaseConfiguration`; провайдер и подключение обязательны, SQLite не выбирается автоматически.
- Все регистрации используют options/DI, не создают host/HTTP/EF-контекст. Локальные проверки работают при получении options и через стандартный `IStartupValidator` без hosting. Ошибки валидации не включают ключ или строку подключения.
- `CalculateExpiresAtUtc` проверен для 14 дней и 36 часов, UTC и переполнения даты. Это чистое вычисление из options; создание и сохранение доменного диалога остаются этапу 06.
- Обновлены архитектурные AGENTS, README, технические документы и контекст OpenSpec. Согласованный `spec.md` не изменён: новые бизнес-требования не вводились. Фактический API: [конфигурация](<../../Technical documentation/05-configuration-and-lifecycle.md>).

## Фактические проверки

Все команды выполнялись последовательно из `D:\Media\User\source\repos\agent-bridge`, SDK `10.0.401`. Перед запуском проверены все конкретные `.csproj`, отсутствие действующих `Directory.Build.props/targets`, `Directory.Packages.props`, `NuGet.config`, lock-файлов и custom Exec/hooks/imports. Использованы только локальные пакеты; NuGet audit отключён для этого восстановления.

Выполненные restore и compile-check:

```powershell
dotnet restore .\agent-bridge.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet restore .\adapters\AgentBridge.CodexLb\AgentBridge.CodexLb.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet restore .\adapters\AgentBridge.Persistence.EfCore\AgentBridge.Persistence.EfCore.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet restore .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet restore .\tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet restore .\tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false
dotnet build .\agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\adapters\AgentBridge.CodexLb\AgentBridge.CodexLb.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\adapters\AgentBridge.Persistence.EfCore\AgentBridge.Persistence.EfCore.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build .\tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
```

| Проект | Restore / compile-check |
| --- | --- |
| `agent-bridge.csproj` | Успех / 0 ошибок, 0 предупреждений |
| `adapters\AgentBridge.CodexLb\AgentBridge.CodexLb.csproj` | Успех / 0 ошибок, 0 предупреждений |
| `adapters\AgentBridge.Persistence.EfCore\AgentBridge.Persistence.EfCore.csproj` | Успех / 0 ошибок, 0 предупреждений |
| `tests\AgentBridge.Tests\AgentBridge.Tests.csproj` | Успех / 0 ошибок, 0 предупреждений |
| `tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj` | Успех / 0 ошибок, 0 предупреждений |
| `tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj` | Успех / 0 ошибок, 0 предупреждений |

Выполненные команды runner:

```powershell
dotnet test .\tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test .\tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test .\tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\
```

| Тестовый проект | Пройдено | Упало | Пропущено |
| --- | --- | --- | --- |
| `tests\AgentBridge.Tests\AgentBridge.Tests.csproj` | 18 | 0 | 0 |
| `tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj` | 20 | 0 | 0 |
| `tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj` | 9 | 0 | 0 |

Публичная граница тестов — DI/options, in-memory источник и явный startup validator. Проверены defaults, overrides, нестандартный период, reload/новый scope, отсутствие обязательного, локальные диапазоны, неизвестный провайдер, UTC/overflow и отсутствие синтетических секретов в ошибках.

Статически проверены изменённые Markdown-ссылки и UTF-8/LF; нет U+FFFD, четырёх вопросительных знаков и обнаруженных признаков mojibake. `git diff --check` прошёл; предупреждения Git об autocrlf не являются дефектами diff. Ветка `master` сохраняется. Координатор принял код, API, документацию и результаты 47 тестов; разрешил локальный коммит ровно перечисленных 30 файлов. После заключительных правок только документации сборки и тесты не повторялись.

## Изменённые файлы

30 путей относительно корня AgentBridge; производные build-артефакты в список не входят.

```text
AGENTS.md
README.md
agent-bridge.csproj
Configuration/AGENTS.md
Configuration/AgentOptions.cs
Configuration/DialogRetentionOptions.cs
Configuration/ContextCompactionOptions.cs
Configuration/AgentBridgeConfigurationExtensions.cs
adapters/AgentBridge.CodexLb/AGENTS.md
adapters/AgentBridge.CodexLb/Configuration/CodexLbOptions.cs
adapters/AgentBridge.CodexLb/Configuration/CodexLbConfigurationExtensions.cs
adapters/AgentBridge.Persistence.EfCore/AGENTS.md
adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseProvider.cs
adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseOptions.cs
adapters/AgentBridge.Persistence.EfCore/Configuration/DatabaseConfigurationExtensions.cs
tests/AGENTS.md
tests/AgentBridge.Tests/AgentBridge.Tests.csproj
tests/AgentBridge.Tests/ConfigurationTests.cs
tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj
tests/AgentBridge.CodexLb.Tests/CodexLbConfigurationTests.cs
tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj
tests/AgentBridge.Persistence.EfCore.Tests/DatabaseConfigurationTests.cs
Documentation/Technical documentation/01-architecture.md
Documentation/Technical documentation/05-configuration-and-lifecycle.md
Documentation/Plans/AgentBridge Initial Implementation/02-configuration-and-defaults.md
Documentation/Plans/AgentBridge Initial Implementation/README.md
openspec/specs/agent-runtime/context.md
Documentation/README.md
Documentation/Plans/README.md
Documentation/Technical documentation/README.md
```

## Ограничения и пропуски

- Проверка поддержки модели, effort, модельного бюджета и политики ключа — этап 13; локальная валидация не выдаётся за серверный каталог. Выбор ключа и API его источника не реализованы заранее.
- Доменные сущности, транспорт, persistence и безопасный settings service не реализованы; секретные входные options нельзя выдавать в UI или журнал.
- Приложения, hosting, HTTP/codex-lb/OpenAI, БД/SQL, migrations и реальный backup/restore: **Пропущено по указанию пользователя**. Произвольные проектные скрипты и OpenSpec CLI не запускались. Нормативные требования сверены статически, автоматическая OpenSpec-валидация не заявляется.
- Соседние библиотеки и codex-lb не изменены. Этапы 03–25 не начаты. Hash разрешённого локального коммита приводится в итоговом отчёте.

## Проверка и завершение

Проверить нестандартный срок хранения, недопустимые диапазоны и переопределение конфигурации. Этап завершён, когда смена настроек не требует правки бизнес-кода, а некорректные значения вызывают явную ошибку.

Источник: [конфигурация](<../../Business logic/05-application-configuration.md>).
