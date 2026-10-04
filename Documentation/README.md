# Документация agent-bridge

AgentBridge — библиотека ИИ-агентов для .NET10, подключаемая обычными DLL. Этапы00–16 приняты и закоммичены. Этап17 реализован, адресно проверен и принят координатором; локальный коммит31 файлов разрешён. Доступны настройки/DI/диагностика, Domain/Application, EF-хранилище/UoW/migrations/maintenance, каталог, canonical JSON/SSE и композиция ModelRequest, offline tokenizer и явный budget guard. Этапы18–25 не начаты; checkpoint18 не достигнут. Real HTTP/upstream не проверены; исторический DB integration запуск не подтверждает текущий transport/EFCoreLibrary0.0.5. [Maintenance API](<Technical documentation/06-database-maintenance.md#подключение-agentbridge-этапа-12>), [каталог](<Technical documentation/13-model-catalog-and-keys.md>), [JSON](<Technical documentation/14-responses-json-adapter.md>), [SSE](<Technical documentation/15-responses-sse-adapter.md>), [композиция](<Technical documentation/16-context-composition.md>), [tokenizer/guard](<Technical documentation/07-tokenizer-and-settings.md#public-guard-и-подключение>).

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
10. [Сценарные Unit of Work](<Technical documentation/10-scenario-unit-of-work.md>)
11. [Provider-specific миграции](<Technical documentation/11-provider-migrations.md>)
12. [Каталог моделей и выбор ключа](<Technical documentation/13-model-catalog-and-keys.md>)
13. [JSON Responses](<Technical documentation/14-responses-json-adapter.md>)
14. [SSE Responses](<Technical documentation/15-responses-sse-adapter.md>)
15. [Композиция контекста](<Technical documentation/16-context-composition.md>)

## Требования

[OpenSpec: agent-runtime](../openspec/specs/agent-runtime/spec.md) содержит нормативные проверяемые требования. Бизнес-документы раскрывают пользовательские сценарии, технические документы описывают их реализацию.

## Планы

[Plans](Plans/README.md) — планы реализации на русском языке с английскими именами файлов и папок.

[Первоначальная реализация AgentBridge](<Plans/AgentBridge Initial Implementation/README.md>) — 26 небольших этапов от подготовки контрактов до DLL и руководства подключения.
