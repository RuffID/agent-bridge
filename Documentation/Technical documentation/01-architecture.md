# Архитектура и обязанности типов

## Статус

Целевая платформа — `net10.0`, SDK-style .NET. Существующее ядро `agent-bridge.csproj` сохранено в корне рядом с `agent-bridge.slnx`. Добавлены отдельные проекты транспорта, хранения и тестов; все шесть проектов прошли адресный compile-check. На этапе 02 реализованы типизированные options и групповые расширения регистрации; их фактический API описан в [конфигурации](05-configuration-and-lifecycle.md). Сценарии агента и названия типов в таблице проектируемых обязанностей ниже ещё не реализованы.

## Фактические проекты

| Путь от корня | Сборка / назначение | ProjectReference |
| --- | --- | --- |
| `agent-bridge.csproj` | `AgentBridge.dll`, ядро/Application | Нет |
| `adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj` | `AgentBridge.CodexLb.dll` | Ядро |
| `adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj` | `AgentBridge.Persistence.EfCore.dll` | Ядро |
| `tests/AgentBridge.Tests/AgentBridge.Tests.csproj` | Проверки ядра | Ядро |
| `tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj` | Проверки транспорта | Адаптер codex-lb |
| `tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj` | Проверки хранения | EF-хранилище |

Во всех проектах включены nullable и генерация XML-документации. Корневой glob исключает `adapters`, `tests`, `test` и вложенные `bin/obj/artifacts`; результаты сборки соседних проектов не компилируются в ядро. Production-проекты используют обычный `Microsoft.NET.Sdk`, без явных framework references. Ядро содержит `Microsoft.Extensions.Options.ConfigurationExtensions 10.0.3`, включая транзитивные options/configuration/DI abstractions; адаптеры используют эту общую зависимость через ProjectReference. ASP.NET Core, WPF, Telegram, EFCoreLibrary, HttpClientLibrary и провайдеры БД не подключены.

Тестовые проекты используют `Microsoft.NET.Test.Sdk 18.0.1`, `xunit 2.9.3` и `xunit.runner.visualstudio 3.1.5`, а также Microsoft.Extensions.Configuration/DependencyInjection `10.0.3` для in-memory настроек и контейнера. Их стандартные SDK-артефакты предназначены только для test runner; приложение и собственный host не создаются. На этапе 02 прошли 47 изолированных проверок конфигурации через публичную DI/options-границу: 18 ядра, 20 codex-lb, 9 БД. Конкретные команды и результаты находятся в [этапе 02](<../Plans/AgentBridge Initial Implementation/02-configuration-and-defaults.md>); исходная проверка каркаса — в [этапе 01](<../Plans/AgentBridge Initial Implementation/01-solution-foundation.md>).

HttpClientLibrary и EFCoreLibrary остаются обязательными основами будущих адаптеров, но на этапе основы не подключены. Эта граница не разрешает обход библиотек или изменение их контрактов. Нормативные требования [agent-runtime](../../openspec/specs/agent-runtime/spec.md) сохранены: новое поведение не вводится.

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
