# Техническая документация AgentBridge

Документы описывают фактические API конфигурации, диагностики, Domain, прикладных портов и EF-хранилища. Этапы 00–08 приняты; этап 09 реализован и принят (71 persistence-тест, 37 новых); этапы 10–25 не начаты. Запрещённые проверки пропущены. Base read/staging adapters и read ports реализованы; сценарии агента, HTTP-адаптер, saving/write ports/UoW/migrations/startup ещё отсутствуют. Проверки и ограничения находятся в соответствующих планах.

| Раздел | Содержание |
| --- | --- |
| [Архитектура](01-architecture.md) | Фактические проекты, направления ссылок, DLL и обязанности будущих типов |
| [EFCoreLibrary](02-efcorelibrary.md) | Актуальный API, persistence DTO, scoped DI, base read/staging adapters и границы непроверенной БД |
| [HTTP и codex-lb](03-http-and-codex-lb.md) | HttpClientLibrary, Responses, compact, ошибки и сохранение протокольного состояния |
| [Переиспользование бота](04-telegram-relay-reuse.md) | Что пригодно из TelegramCodexRelayBot и что требует переработки |
| [Конфигурация и жизненный цикл](05-configuration-and-lifecycle.md) | Options, scoped-сессии, согласование удаления и проверка |
| [Обслуживание БД](06-database-maintenance.md) | AquaByte-Ledger, развитие backup-контракта EFCoreLibrary, migrations и очистка |
| [Tokenizer и настройки](07-tokenizer-and-settings.md) | Подсчёт токенов, выбор ключа, безопасные настройки и фактическая диагностика/Serilog приложения |
| [Доменное состояние диалога](08-dialog-domain-state.md) | Фактический агрегат, владение, фиксированный срок, статусы, версии и границы cutoff/concurrency |
| [Прикладные контракты](09-application-ports.md) | Независимые порты, результаты lifecycle, канонические снимки и короткие сценарии хранения |

Связанные документы: [бизнес-логика](<../Business logic/README.md>), [OpenSpec agent-runtime](../../openspec/specs/agent-runtime/spec.md).
