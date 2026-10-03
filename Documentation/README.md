# Документация agent-bridge

AgentBridge — библиотека ИИ-агентов для .NET 10, подключаемая обычными DLL. Этапы 00–07 приняты. Доступны настройки/DI, безопасная диагностика, защищённый Domain и прикладные порты. Этап 08 реализован и принят: persistence DTO, полный payload, общий EF-контекст и явный SQLite/PostgreSQL через EFCoreLibrary; 34 изолированных persistence-теста, 25 новых. Запрещённые проверки пропущены. Этапы 09–25 не начаты: CRUD/UoW/migrations/startup, сценарий агента и транспорт ещё не реализованы; restart/атомарность на БД не подтверждены.

## Бизнес-логика

[Навигатор Business logic](<Business logic/README.md>)

1. [Назначение и границы](<Business logic/01-purpose-and-scope.md>)
2. [Диалоги и инструменты](<Business logic/02-dialogs-and-tools.md>)
3. [Контекст и сжатие](<Business logic/03-context-and-compaction.md>)
4. [Хранение, лимиты и удаление](<Business logic/04-storage-and-retention.md>)
5. [Настройки подключающего приложения](<Business logic/05-application-configuration.md>)
6. [Модели, настройки и состояние диалога](<Business logic/06-models-and-status.md>)

## Техническая документация

[Навигатор Technical documentation](<Technical documentation/README.md>)

1. [Архитектура и обязанности типов](<Technical documentation/01-architecture.md>)
2. [Доступ к данным через EFCoreLibrary](<Technical documentation/02-efcorelibrary.md>)
3. [HttpClientLibrary и текущие контракты codex-lb](<Technical documentation/03-http-and-codex-lb.md>)
4. [Результаты анализа TelegramCodexRelayBot](<Technical documentation/04-telegram-relay-reuse.md>)
5. [Конфигурация, жизненный цикл и проверка](<Technical documentation/05-configuration-and-lifecycle.md>)
6. [Проверка БД, бэкап, миграции и очистка](<Technical documentation/06-database-maintenance.md>)
7. [Tokenizer, настройки и Serilog](<Technical documentation/07-tokenizer-and-settings.md>)
8. [Доменное состояние диалога](<Technical documentation/08-dialog-domain-state.md>)
9. [Прикладные контракты](<Technical documentation/09-application-ports.md>)

## Требования

[OpenSpec: agent-runtime](../openspec/specs/agent-runtime/spec.md) содержит нормативные проверяемые требования. Бизнес-документы раскрывают пользовательские сценарии, технические документы описывают их реализацию.

## Планы

[Plans](Plans/README.md) — планы реализации на русском языке с английскими именами файлов и папок.

[Первоначальная реализация AgentBridge](<Plans/AgentBridge Initial Implementation/README.md>) — 26 небольших этапов от подготовки контрактов до DLL и руководства подключения.
