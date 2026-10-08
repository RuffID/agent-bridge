# Документация agent-bridge

AgentBridge — библиотека ИИ-агентов для .NET10, подключаемая обычными DLL. Начните с [руководства потребителя](<Technical documentation/25-usage-guide.md>): бинарные ссылки, DI/options и проверенные C# сценарии. [Карта evidence00–25](<Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>) содержит результаты принятой реализации и явно отмечает непроверенные окружения. [OpenSpec workflow](../openspec/README.md) закрепляет приоритет main над историческими deltas. Фактические final strict results09: main и18 changes проходят, ERROR0/WARNING0; этап09 принят в A workflow/B CLI, archive INFO2 отдельно, changes не архивированы.

## Бизнес-логика

[Навигатор Business logic](<Business logic/README.md>)

1. [Назначение и границы](<Business logic/01-purpose-and-scope.md>)
2. [Создание диалога и обработка обращения](<Business logic/02-dialogs-and-tools.md>)
3. [Контекст и сжатие](<Business logic/03-context-and-compaction.md>)
4. [Хранение, лимиты и удаление](<Business logic/04-storage-and-retention.md>)
5. [Обязанности и настройки приложения](<Business logic/05-application-configuration.md>)
6. [Модели, effort, ключи и состояние диалога](<Business logic/06-models-and-status.md>)
7. [Пользователи, владельцы и доступ](<Business logic/07-users-and-access.md>)
8. [Инструменты и бизнес-действия](<Business logic/08-tools-and-permissions.md>)
9. [Ошибки, отмена и восстановление](<Business logic/09-errors-and-recovery.md>)
10. [Типовые сценарии и ограничения](<Business logic/10-scenarios-and-limitations.md>)

## Техническая документация

[Usage, документы, изображения и явная серверная проверка opaque бюджета](<Technical documentation/27-auxiliary-model-operations.md>) описывают optional расширение для Telegram consumer.

Actual короткое подключение — [Integration facade](<Technical documentation/26-integration-registration.md>); [fresh evidence16](<Plans/AgentBridge Audit Remediation/16-isolated-regression-and-delivery.md#результаты>) подтверждает пять binary kits/external compile-only consumers. [Windows runtime18](<Plans/AgentBridge Audit Remediation/18-runtime-delivery.md#результаты>) проверен в loader/DI/BPE границе; provider/Linux/live/crash evidence остаётся открытым.

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
16. [Сжатие контекста](<Technical documentation/18-context-compaction.md>)
17. [Инструменты приложения](<Technical documentation/19-application-tools.md>)
18. [Полный ход агента](<Technical documentation/20-agent-turn-orchestration.md>)
19. [Настройки и статус диалога](<Technical documentation/21-settings-and-dialog-status.md>)
20. [Очистка истёкших диалогов](<Technical documentation/22-expired-dialog-cleanup.md>)
21. [Автономная поставка DLL](<Technical documentation/24-dll-delivery.md>)
22. [Подключение и использование](<Technical documentation/25-usage-guide.md>)

## Требования

[OpenSpec: agent-runtime](../openspec/specs/agent-runtime/spec.md) содержит нормативные проверяемые требования. Бизнес-документы раскрывают пользовательские сценарии, технические документы описывают их реализацию.

## Планы

[Plans](Plans/README.md) — планы реализации и проверок на русском языке с английскими именами файлов и папок.

[Первоначальная реализация AgentBridge](<Plans/AgentBridge Initial Implementation/README.md>) — 26 небольших этапов от подготовки контрактов до DLL и руководства подключения.

[Аудит качества AgentBridge](<Plans/AgentBridge Quality Audit/README.md>) — статический проход 00–15 завершён с ограничениями; общий аудит A/B/C/D частичный. [Реестр](<Plans/AgentBridge Quality Audit/Findings.md>) содержит находки и пределы их доказательств.

[Исправление проблем аудита AgentBridge](<Plans/AgentBridge Audit Remediation/README.md>) —00/08 приняты в A,01–07/10–15 в локальной A/B-границе,09 в A workflow/B CLI,16 в B. [Решения Q-003–005](<Plans/AgentBridge Audit Remediation/Decisions.md>) реализованы09–11; MSSQL12, breaking explicit settings13, facade14 и kits15 проверены локально.17 имеет принятую подготовку A/B, C отложен пользователем;18 принята Windows runtime часть, Linux открыт;19 только A readiness без live/crash harness. [Итог20](<Plans/AgentBridge Audit Remediation/20-final-acceptance.md#результаты>) принят координатором в границе **частичной приёмки доступного локального объёма**; обязательные C/D17–19 остаются открытыми.
