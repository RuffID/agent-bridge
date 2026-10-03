# agent-bridge

AgentBridge — C#-библиотека ИИ-агентов для .NET 10: сайты, ASP.NET Core, WPF и Telegram-боты. Подключается обычными DLL.

Приложение → AgentBridge → [codex-lb](https://github.com/Soju06/codex-lb) → модель. AgentBridge собирает бизнес-контекст, вызывает разрешённые инструменты и сохраняет диалог в выбранной БД.

Создана основа решения .NET 10: ядро в корне, два отдельных проекта адаптеров, два provider migrations проекта и три проекта изолированных тестов. Этапы 00–11 приняты; этап 12: **Реализован и принят; запрещённые проверки пропущены**; этапы 13–25 не начаты. Доступны настройки, безопасная диагностика, защищённый Domain, прикладные порты, persistence DTO/EF-маппинг, base read/staging adapters, write ports/UoW, валидирующий Domain Restore и явно вызываемый maintenance API SQLite/PostgreSQL. Сценарий агента и транспорт ещё отсутствуют; следующий порядок описывает интеграцию с учётом этих ограничений.

| Проект | Сборка / назначение | Текущие production-ссылки |
| --- | --- | --- |
| [agent-bridge.csproj](agent-bridge.csproj) | `AgentBridge.dll`, ядро/Application | Нет |
| [AgentBridge.CodexLb](adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj) | `AgentBridge.CodexLb.dll`, транспортный адаптер | Ядро |
| [AgentBridge.Persistence.EfCore](adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj) | `AgentBridge.Persistence.EfCore.dll`, общее хранилище | Ядро, EFCoreLibrary |
| [AgentBridge.Persistence.Migrations.Sqlite](adapters/AgentBridge.Persistence.Migrations.Sqlite/AgentBridge.Persistence.Migrations.Sqlite.csproj) | SQLite design-time factory; InitialAgentBridgeSchema сгенерирована | EF-хранилище |
| [AgentBridge.Persistence.Migrations.PostgreSql](adapters/AgentBridge.Persistence.Migrations.PostgreSql/AgentBridge.Persistence.Migrations.PostgreSql.csproj) | PostgreSQL design-time factory; InitialAgentBridgeSchema сгенерирована | EF-хранилище |
| [AgentBridge.Tests](tests/AgentBridge.Tests/AgentBridge.Tests.csproj) | Изолированные проверки ядра | Ядро |
| [AgentBridge.CodexLb.Tests](tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj) | Изолированные проверки транспорта | Адаптер codex-lb |
| [AgentBridge.Persistence.EfCore.Tests](tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj) | Изолированные проверки хранения и design-time моделей | EF-хранилище, оба migrations проекта |

Тестовая инфраструктура — xUnit. На этапе 02 прошли 47 изолированных проверок настроек; на этапе 03 — 36 тестов ядра, включая 18 новых проверок диагностики. Ядро использует Microsoft.Extensions.Options.ConfigurationExtensions и Microsoft.Extensions.Logging `10.0.3`; адаптеры получают общие зависимости транзитивно. Serilog `4.3.0` и Serilog.Extensions.Logging `10.0.0` подключены только в тестах ядра для проверки pipeline приложения. На этапе 04 HttpClientLibrary 0.0.0.5 получила default-safe/opt-in JsonStructure и ограниченный error contract; по 44 теста прошли на net8/net10, проверено происхождение DLL. EF-адаптер подключает EFCoreLibrary с этапа 08; HTTP-ссылка остаётся этапу транспорта. Команды и результаты: [этап 02](<Documentation/Plans/AgentBridge Initial Implementation/02-configuration-and-defaults.md>), [этап 03](<Documentation/Plans/AgentBridge Initial Implementation/03-serilog-integration.md>), [этап 04](<Documentation/Plans/AgentBridge Initial Implementation/04-httpclientlibrary-logging.md>).

## Подключение

Для работы с исходниками открыть [agent-bridge.slnx](agent-bridge.slnx) в IDE с поддержкой .NET 10. Production-проекты собираются в пять DLL из таблицы; HTTP ещё не реализован; EF-хранилище имеет read/write ports, выбирает provider migrations assembly и предоставляет явный maintenance API. Приложение поставляет выбранную migrations DLL и зависимости EFCoreLibrary maintenance, SQLite native runtime либо PostgreSQL pg_dump. [Фабрики и схема](<Documentation/Technical documentation/11-provider-migrations.md>), [подключение обслуживания](<Documentation/Technical documentation/06-database-maintenance.md#подключение-agentbridge-этапа-12>).

1. Добавить в .NET 10-приложение ссылки на DLL ядра, адаптера codex-lb и выбранной инфраструктуры хранения вместе с зависимостями времени выполнения. AgentBridge не распространяется NuGet-пакетом.
2. Подключить совместимые DLL EFCoreLibrary и HttpClientLibrary. Выбрать SQLite или PostgreSQL и соответствующий EF Core provider.
3. В конфигурации приложения задать адрес codex-lb, модель, общий ключ, провайдер БД и подключение. При необходимости предоставить индивидуальные ключи пользователей.
4. Настроить лимиты, срок хранения и reasoning effort. Срок хранения задаётся конфигурацией, а не константой в логике удаления.
5. Подключить Serilog и зарегистрировать зависимости AgentBridge, источники контекста и инструменты в composition root приложения.
6. После `AddDatabaseConfiguration`/`AddAgentBridgePersistence` отдельно вызвать `AddAgentBridgeDatabaseMaintenance` с backup options и явным `SingleInitializer`. Задать абсолютный backup directory и собственный положительный `BackupRetentionPeriod` без default; PostgreSQL также требует pg_dump path, major и cleanup timeout. Приложение останавливает другие экземпляры/writes/DDL, выделяет scope и явно вызывает `IDatabaseMaintenance<AgentBridgeContextKey>.InspectAsync`, `UpdateExistingAsync` либо `InitializeNewAsync` с timeout/отменой. Регистрация не запускает операции. Retention backup и расписание очистки диалогов исполняет приложение.

| Пробная настройка | Значение, переопределяемое конфигурацией |
| --- | --- |
| Срок хранения от создания диалога | 7 дней |
| Мягкий порог содержимого на диалог | 10 МиБ |
| Порог сжатия контекста | 32 000 токенов |
| Reasoning effort | `medium`, если поддержан моделью и доступен ключу |
| Время ожидания генерации/compact | 180 секунд |

Все настройки и правила выбора ключей: [конфигурация приложения](<Documentation/Business logic/05-application-configuration.md>).

## Работа с библиотекой

Существующий write API и правила DI scope: [сценарные Unit of Work](<Documentation/Technical documentation/10-scenario-unit-of-work.md>). Этап 10: **Реализован и принят; запрещённые проверки пропущены**.

В `AgentBridge.Application` доступны независимые порты шлюза модели, источника контекста, обработчика инструментов, tokenizer и коротких сценариев хранения. Полные канонические items/envelope/continuation сохраняются отдельными снимками; gateway получает выбранный ключ на вызов. ServiceResult описывает ожидаемый отказ, ModelResponse отдельно различает lifecycle. Storage token выражает сохраняемые incarnation/revision; этап 09 реализует чтение и staging, этап 10 — сохранение/write ports/UoW и валидирующее восстановление Domain. Проверки: 107 core/108 persistence tests; реальная БД не проверялась. [Фактические контракты и ограничения](<Documentation/Technical documentation/09-application-ports.md>), [этап 07](<Documentation/Plans/AgentBridge Initial Implementation/07-application-ports.md>): 93 теста ядра, 27 новых; реализован и принят; запрещённые проверки пропущены.

В `AgentBridge.Persistence.EfCore.Configuration` доступен `AddAgentBridgePersistence`, вызываемый после `AddDatabaseConfiguration`. Он использует общий scoped `AgentBridgeDbContext`, актуальные context-key/base repositories EFCoreLibrary и выбранный SQLite/PostgreSQL; соединение не открывает. Модели и payload-сериализация проверены без БД: 34 теста, включая 25 новых; этап 08 реализован и принят; запрещённые проверки пропущены. [Формат, пример регистрации и ограничения](<Documentation/Technical documentation/02-efcorelibrary.md#реализация-этапа-08>).

Этап 09 добавляет к этой регистрации `IDialogReader` и `IExpiredDialogReader`: полная упорядоченная история, активный compact по максимальной версии и ограниченные кандидаты очистки. Повторная проверка root отклоняет изменившийся диалог без выдачи смешанного снимка. Base CRUD делегируется как staging без сохранения; на этапе 09 write ports не регистрировались; этап 10 добавляет их в ту же регистрацию. **71 persistence-тест, 37 новых**, без БД; реализован и принят; запрещённые проверки пропущены. [Фактические адаптеры и ограничения](<Documentation/Technical documentation/02-efcorelibrary.md#адаптеры-этапа-09>).

Чистый доменный API `AgentBridge.Domain.Dialogs` уже доступен: `Dialog.Create`, владение, фиксированные даты, упорядоченные обращения, конечные статусы, версии контекста и отклонение устаревших результатов. На границе истечения продолжение запрещено. Покрытие контекста — только префикс обращений с конечным статусом, включая 0. Снимок версии действует в памяти; этап 10 добавляет Restore и атомарные write ports; реальный restart на БД не проверялся, cutoff событий Responses ещё не реализован. [Фактический API и ограничения](<Documentation/Technical documentation/08-dialog-domain-state.md>), [проверки этапа 06](<Documentation/Plans/AgentBridge Initial Implementation/06-dialog-domain-state.md>): 66 тестов ядра, включая 30 новых доменных. Далее описан будущий прикладной сценарий.

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
