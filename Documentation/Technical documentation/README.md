# Техническая документация AgentBridge

Документы описывают принятые границы, фактический конфигурационный API и проектируемую реализацию следующих областей. Этапы 00–02 завершены и приняты; этапы 03–25 не начаты. Реализованы типизированные options, групповые DI-расширения, binding/validation и вычисление срока хранения. Все три production-проекта и три тестовых проекта прошли адресный compile-check; 47 изолированных тестов конфигурации пройдены. Сценарии агента, HTTP и EF-хранилище ещё не реализованы; названия их будущих типов обозначают обязанности.

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
