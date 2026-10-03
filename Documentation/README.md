# Документация agent-bridge

AgentBridge — библиотека ИИ-агентов для .NET 10, подключаемая обычными DLL. Этапы 00–03 завершены и приняты. Доступны типизированные настройки, групповая DI-регистрация, binding/validation, вычисление срока хранения и основа безопасной диагностики через ILogger приложения. На этапе 02 проверены все шесть проектов и 47 тестов конфигурации; на этапе 03 — затронутые ядро и его тестовый проект, 36 тестов ядра, включая 18 новых. Сценарии агента, транспорт, хранилище и этапы 04–25 ещё не начаты.

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

## Требования

[OpenSpec: agent-runtime](../openspec/specs/agent-runtime/spec.md) содержит нормативные проверяемые требования. Бизнес-документы раскрывают пользовательские сценарии, технические документы описывают их реализацию.

## Планы

[Plans](Plans/README.md) — планы реализации на русском языке с английскими именами файлов и папок.

[Первоначальная реализация AgentBridge](<Plans/AgentBridge Initial Implementation/README.md>) — 26 небольших этапов от подготовки контрактов до DLL и руководства подключения.
