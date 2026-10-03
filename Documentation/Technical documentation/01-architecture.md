# Архитектура и обязанности типов

## Статус

Целевая платформа — `net10.0`, SDK-style .NET. Существующее ядро `agent-bridge.csproj` сохранено в корне рядом с `agent-bridge.slnx`. Добавлены отдельные проекты транспорта, хранения и тестов; все шесть проектов прошли адресный compile-check на этапе 02. Реализованы типизированные options и групповые расширения регистрации; их API описан в [конфигурации](05-configuration-and-lifecycle.md). Этап 03 добавляет основу диагностики операций через ILogger и её DI-регистрацию; [фактический API](07-tokenizer-and-settings.md#serilog) проверен отдельно от будущих сценариев. Сценарии агента и названия типов в таблице проектируемых обязанностей ниже ещё не реализованы.

## Фактические проекты

| Путь от корня | Сборка / назначение | ProjectReference |
| --- | --- | --- |
| `agent-bridge.csproj` | `AgentBridge.dll`, ядро/Application | Нет |
| `adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj` | `AgentBridge.CodexLb.dll` | Ядро |
| `adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj` | `AgentBridge.Persistence.EfCore.dll` | Ядро |
| `tests/AgentBridge.Tests/AgentBridge.Tests.csproj` | Проверки ядра | Ядро |
| `tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj` | Проверки транспорта | Адаптер codex-lb |
| `tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj` | Проверки хранения | EF-хранилище |

Во всех проектах включены nullable и генерация XML-документации. Корневой glob исключает `adapters`, `tests`, `test` и вложенные `bin/obj/artifacts`; результаты сборки соседних проектов не компилируются в ядро. Production-проекты используют обычный `Microsoft.NET.Sdk`, без явных framework references. Ядро содержит `Microsoft.Extensions.Options.ConfigurationExtensions 10.0.3` и `Microsoft.Extensions.Logging 10.0.3`; адаптеры используют общие зависимости через ProjectReference. ASP.NET Core, WPF, Telegram, Serilog, EFCoreLibrary, HttpClientLibrary и провайдеры БД в production не подключены. Serilog pipeline настраивается приложением.

Тестовые проекты используют `Microsoft.NET.Test.Sdk 18.0.1`, `xunit 2.9.3` и `xunit.runner.visualstudio 3.1.5`, а также Microsoft.Extensions.Configuration/DependencyInjection `10.0.3` для in-memory настроек и контейнера. Их стандартные SDK-артефакты предназначены только для test runner; приложение и собственный host не создаются. На этапе 02 прошли 47 изолированных проверок конфигурации через публичную DI/options-границу: 18 ядра, 20 codex-lb, 9 БД. Конкретные команды и результаты находятся в [этапе 02](<../Plans/AgentBridge Initial Implementation/02-configuration-and-defaults.md>); исходная проверка каркаса — в [этапе 01](<../Plans/AgentBridge Initial Implementation/01-solution-foundation.md>).

HttpClientLibrary и EFCoreLibrary остаются обязательными основами будущих адаптеров, но на этапе основы не подключены. Эта граница не разрешает обход библиотек или изменение их контрактов. Нормативные требования [agent-runtime](../../openspec/specs/agent-runtime/spec.md) сохранены: новое поведение не вводится.

На этапе 03 в `Diagnostics/` реализованы `AgentBridgeDiagnostics`, наблюдение `AgentBridgeDiagnosticOperation`, закрытый `AgentBridgeOperation` и `AddAgentBridgeDiagnostics`. Наблюдение измеряет длительность и выводит один явный итог; оно не исполняет делегаты и не заменяет прикладную оркестрацию. Фабрика ILogger, фильтры, провайдеры и global logger остаются у приложения. Ядро и его тестовый проект собраны без ошибок/предупреждений; 36 тестов ядра прошли, включая 18 новых. В тестовом проекте отдельно подключены Serilog `4.3.0` и Serilog.Extensions.Logging `10.0.0`: provider проверен с sink в памяти, без host/файлов. Адаптеры на этапе 03 не менялись и не перепроверялись. Команды и ограничения: [этап 03](<../Plans/AgentBridge Initial Implementation/03-serilog-integration.md>).

## Границы

| Область | Обязанности | Зависимости |
| --- | --- | --- |
| Ядро/Application | Сценарий агента, контекст, инструменты, чистые порты хранения и модели | Общие контракты, без конкретного HTTP-клиента и EF Core |
| Адаптер codex-lb | Wire DTO, Responses/SSE, compact, нормализация ошибок | HttpClientLibrary и контракт транспорта ядра |
| EF-инфраструктура | DbContext, модели хранения, репозитории и сценарные UoW | EFCoreLibrary и контракты хранения ядра |
| Провайдеры БД | Конфигурация SQLite или PostgreSQL | Соответствующий EF Core provider и общая EF-инфраструктура |
| Подключающее приложение | DI, конфигурация, права, бизнес-данные, инструменты и UI | Нужные DLL AgentBridge |

Общее EF-хранилище используется с выбранным провайдером. Не требуется дублировать все репозитории отдельно для SQLite и PostgreSQL.

## Проектируемые обязанности

| Рабочее имя | Роль |
| --- | --- |
| `AgentRunner` | Координация одного обращения и цикла инструментов |
| `ContextBuilder` | Подготовка рабочего контекста без подмены ролей сообщений |
| `IContextProvider` | Предоставление разрешённых бизнес-данных приложением |
| `IToolHandler` | Выполнение зарегистрированного инструмента приложения |
| `IModelGateway` | Независимый от codex-lb порт генерации и сжатия |
| `CodexLbGateway` | Адаптер порта к Responses API через HttpClientLibrary |
| `IContextTokenCounter` | Tokenizer для известной кодировки модели и учёт полного входного бюджета |
| `ContextCompactionService` | Создание следующего состояния контекста |
| `DialogRetentionService` | Координация применения политики хранения |
| `DatabaseMaintenanceService` | Startup-проверка/обновление схемы через развиваемый контракт EFCoreLibrary |
| `AgentSettingsService` | Безопасное чтение настроек и выбор модели/effort |
| `IApiKeyProvider` | Индивидуальный ключ пользователя или общий при его отсутствии |

Изменяющие сценарии используют собственные минимальные Unit of Work, соответствующие транзакционной границе. Один глобальный UoW со всеми репозиториями не используется. Сценарий только чтения получает узкий порт чтения.

## DLL и внешние библиотеки

AgentBridge подключается обычными DLL с необходимыми зависимостями времени выполнения. Раздельные области можно поставлять отдельными DLL, чтобы приложение подключало выбранный адаптер и провайдер.

EFCoreLibrary и HttpClientLibrary — обязательные основы соответствующих инфраструктурных областей. При реализации контракт собранной DLL сверяется с проверенным исходным кодом библиотеки; существующая копия DLL в другом проекте не доказывает совпадение версий.

Бизнес-сценарий: [назначение и границы](<../Business logic/01-purpose-and-scope.md>).
