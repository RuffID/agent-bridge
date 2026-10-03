# agent-bridge

AgentBridge — C#-библиотека ИИ-агентов для .NET 10: сайты, ASP.NET Core, WPF и Telegram-боты. Подключается обычными DLL.

Приложение → AgentBridge → [codex-lb](https://github.com/Soju06/codex-lb) → модель. AgentBridge собирает бизнес-контекст, вызывает разрешённые инструменты и сохраняет диалог в выбранной БД.

Создана основа решения .NET 10: ядро в корне, два отдельных проекта адаптеров и три проекта изолированных тестов. Этапы 00–03 завершены и приняты; этапы 04–25 не начаты. Реализованы типизированные настройки, их групповая DI-регистрация, binding/validation, вычисление срока из настроенного периода и безопасная диагностика операций через ILogger приложения. Сценарий агента, транспорт и хранилище ещё не реализованы. Следующий порядок описывает согласованную будущую интеграцию.

| Проект | Сборка / назначение | Текущие production-ссылки |
| --- | --- | --- |
| [agent-bridge.csproj](agent-bridge.csproj) | `AgentBridge.dll`, ядро/Application | Нет |
| [AgentBridge.CodexLb](adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj) | `AgentBridge.CodexLb.dll`, транспортный адаптер | Ядро |
| [AgentBridge.Persistence.EfCore](adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj) | `AgentBridge.Persistence.EfCore.dll`, общее хранилище | Ядро |
| [AgentBridge.Tests](tests/AgentBridge.Tests/AgentBridge.Tests.csproj) | Изолированные проверки ядра | Ядро |
| [AgentBridge.CodexLb.Tests](tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj) | Изолированные проверки транспорта | Адаптер codex-lb |
| [AgentBridge.Persistence.EfCore.Tests](tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj) | Изолированные проверки хранения | EF-хранилище |

Тестовая инфраструктура — xUnit. На этапе 02 прошли 47 изолированных проверок настроек; на этапе 03 — 36 тестов ядра, включая 18 новых проверок диагностики. Ядро использует Microsoft.Extensions.Options.ConfigurationExtensions и Microsoft.Extensions.Logging `10.0.3`; адаптеры получают общие зависимости транзитивно. Serilog `4.3.0` и Serilog.Extensions.Logging `10.0.0` подключены только в тестах ядра для проверки pipeline приложения. HttpClientLibrary, EFCoreLibrary и провайдеры будут подключены на соответствующих этапах, после согласования необходимых контрактов. Команды и результаты: [этап 02](<Documentation/Plans/AgentBridge Initial Implementation/02-configuration-and-defaults.md>), [этап 03](<Documentation/Plans/AgentBridge Initial Implementation/03-serilog-integration.md>).

## Подключение

Для работы с исходниками открыть [agent-bridge.slnx](agent-bridge.slnx) в IDE с поддержкой .NET 10. Production-проекты собираются в три DLL из таблицы; внешний HTTP и работа с БД в каркасе отсутствуют.

1. Добавить в .NET 10-приложение ссылки на DLL ядра, адаптера codex-lb и выбранной инфраструктуры хранения вместе с зависимостями времени выполнения. AgentBridge не распространяется NuGet-пакетом.
2. Подключить совместимые DLL EFCoreLibrary и HttpClientLibrary. Выбрать SQLite или PostgreSQL и соответствующий EF Core provider.
3. В конфигурации приложения задать адрес codex-lb, модель, общий ключ, провайдер БД и подключение. При необходимости предоставить индивидуальные ключи пользователей.
4. Настроить лимиты, срок хранения и reasoning effort. Срок хранения задаётся конфигурацией, а не константой в логике удаления.
5. Подключить Serilog и зарегистрировать зависимости AgentBridge, источники контекста и инструменты в composition root приложения.
6. Явно вызвать проверку/инициализацию БД. Для обновления существующей схемы используется порядок check → backup → migrate через EFCoreLibrary. Расписание очистки истёкших диалогов задаёт приложение.

| Пробная настройка | Значение, переопределяемое конфигурацией |
| --- | --- |
| Срок хранения от создания диалога | 7 дней |
| Мягкий порог содержимого на диалог | 10 МиБ |
| Порог сжатия контекста | 32 000 токенов |
| Reasoning effort | `medium`, если поддержан моделью и доступен ключу |
| Время ожидания генерации/compact | 180 секунд |

Все настройки и правила выбора ключей: [конфигурация приложения](<Documentation/Business logic/05-application-configuration.md>).

## Работа с библиотекой

1. Определить пользователя и создать или открыть доступный ему диалог.
2. Передать сообщение, идентификатор диалога и выбранного агента. Приложение определяет права пользователя и доступные бизнес-данные.
3. При необходимости получить безопасные текущие настройки, выбрать другую модель или effort. Effort можно переопределить для конкретного запроса; выполняющееся обращение сохраняет свой снимок настроек.
4. Получить ответ либо поток событий. Вызовы инструментов выполняются зарегистрированными обработчиками приложения, а результат возвращается модели.
5. Для следующего сообщения использовать тот же диалог. Контекст сохраняется в БД; при достижении токенного порога он сжимается. Мягкий порог байтов не удаляет историю.
6. Получить состояние диалога для интерфейса: `ExpiresAtUtc`, текущий объём, сведения о контексте и выбранную модель. По истечении настроенного срока диалог недоступен для продолжения и удаляется обслуживанием БД.
7. По желанию пользователя создать новый диалог или явно удалить старый. После обычного создания нового старый сохраняется до своего срока истечения.

Если индивидуальный ключ пользователя отсутствует, используется общий. Ошибка заданного ключа не переключает запрос скрыто на другой ключ. Чтение настроек не раскрывает ключи и строки подключения.

Уже доступны `AddAgentBridgeConfiguration`, `AddCodexLbConfiguration` и `AddDatabaseConfiguration` с binding из раздела приложения либо программными callbacks. Фактические свойства, пространства имён, правила валидации и примеры: [конфигурация и жизненный цикл](<Documentation/Technical documentation/05-configuration-and-lifecycle.md>). Расширения не запускают host, HTTP или БД. Безопасный settings service и C# API обращения к агенту будут добавлены на соответствующих этапах.

Для диагностики доступен `AddAgentBridgeDiagnostics` из `AgentBridge.Diagnostics`. Приложение передаёт свой Serilog logger стандартному `AddLogging(logging => logging.AddSerilog(applicationLogger, dispose: false))`, затем регистрирует диагностику. Приложение владеет logger, sinks и их освобождением; AgentBridge не заменяет `Log.Logger`. `BeginOperation` измеряет длительность и принимает только enum операции, GUID корреляции и раздельные caller/deadline-токены. Владелец явно завершает наблюдение через `Complete` или `Fail`; автоматической интеграции ещё не реализованных сценариев нет. Пример реальной проверки options, поля событий и контракт отмены: [Serilog и диагностика](<Documentation/Technical documentation/07-tokenizer-and-settings.md#serilog>).

## Документация

Вход в документацию: [Documentation/README.md](Documentation/README.md).

- [Бизнес-логика](<Documentation/Business logic/README.md>) — назначение, сценарии, контекст, хранение и настройки.
- [Техническая документация](<Documentation/Technical documentation/README.md>) — архитектура, зависимости и решения реализации.
- [OpenSpec](openspec/specs/agent-runtime/spec.md) — проверяемые требования.
- [План реализации](<Documentation/Plans/AgentBridge Initial Implementation/README.md>) — небольшие этапы на русском языке с английскими именами файлов и папок.
