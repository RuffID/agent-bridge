# Техническая документация AgentBridge

Документы описывают принятые границы и проектируемую реализацию. Этапы 00–01 завершены и приняты; этапы 02–25 не начаты. Основа решения содержит три production-проекта и три отдельных тестовых проекта; их адресные сборки проверены на этапе 01. Названия будущих типов обозначают обязанности; production-логика ещё не реализована.

| Раздел | Содержание |
| --- | --- |
| [Архитектура](01-architecture.md) | Фактические проекты, направления ссылок, DLL и обязанности будущих типов |
| [EFCoreLibrary](02-efcorelibrary.md) | Реальные базовые классы, приоритет CRUD, транзакционные границы |
| [HTTP и codex-lb](03-http-and-codex-lb.md) | HttpClientLibrary, Responses, compact, ошибки и сохранение протокольного состояния |
| [Переиспользование бота](04-telegram-relay-reuse.md) | Что пригодно из TelegramCodexRelayBot и что требует переработки |
| [Конфигурация и жизненный цикл](05-configuration-and-lifecycle.md) | Options, scoped-сессии, согласование удаления и проверка |
| [Обслуживание БД](06-database-maintenance.md) | AquaByte-Ledger, развитие backup-контракта EFCoreLibrary, migrations и очистка |
| [Tokenizer и настройки](07-tokenizer-and-settings.md) | Подсчёт токенов, выбор ключа, безопасные настройки и Serilog |

Связанные документы: [бизнес-логика](<../Business logic/README.md>), [OpenSpec agent-runtime](../../openspec/specs/agent-runtime/spec.md).
