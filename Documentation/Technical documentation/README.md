# Техническая документация AgentBridge

Документы описывают фактические API конфигурации, диагностики, Domain/Application, EF-хранилища, каталога и JSON gateway. Этапы 00–14 реализованы и приняты координатором; локальный коммит этапа14 разрешён. SSE/composition/tokenizer/compact и сценарий агента отсутствуют; этапы 15–25 не начаты, checkpoint18 не достигнут. Проверки и ограничения находятся в соответствующих планах.

| Раздел | Содержание |
| --- | --- |
| [Архитектура](01-architecture.md) | Фактические проекты, направления ссылок, DLL и обязанности будущих типов |
| [EFCoreLibrary](02-efcorelibrary.md) | Актуальный API, persistence DTO, scoped DI, base read/staging adapters и границы непроверенной БД |
| [HTTP и codex-lb](03-http-and-codex-lb.md) | HttpClientLibrary, Responses, compact, ошибки и сохранение протокольного состояния |
| [Переиспользование бота](04-telegram-relay-reuse.md) | Что пригодно из TelegramCodexRelayBot и что требует переработки |
| [Конфигурация и жизненный цикл](05-configuration-and-lifecycle.md) | Options, scoped-сессии, согласование удаления и проверка |
| [Обслуживание БД](06-database-maintenance.md) | Явный maintenance API AgentBridge, backup options, стадии/ошибки/отмена и ограничения EFCoreLibrary |
| [Tokenizer и настройки](07-tokenizer-and-settings.md) | Подсчёт токенов, выбор ключа, безопасные настройки и фактическая диагностика/Serilog приложения |
| [Доменное состояние диалога](08-dialog-domain-state.md) | Фактический агрегат, владение, фиксированный срок, статусы, версии и границы cutoff/concurrency |
| [Прикладные контракты](09-application-ports.md) | Независимые порты, результаты lifecycle, канонические снимки и короткие сценарии хранения |
| [Сценарные Unit of Work](10-scenario-unit-of-work.md) | Write ports, общий scope EFCoreLibrary, Domain Restore, guards и ограничения атомарности |
| [Provider migrations](11-provider-migrations.md) | Отдельные сборки/factories, runtime identity, tooling, подготовленная модель и блокер генерации |
| [Каталог моделей и ключи](13-model-catalog-and-keys.md) | Application source, per-call доступ, dynamic capabilities, проверка model/effort/input budget и безопасные снимки |
| [JSON Responses](14-responses-json-adapter.md) | Public DI/gateway, canonical controls/items/envelope, bound continuation, safe errors, deadline/caller/disposal и Unsupported SSE/compact |

Связанные документы: [бизнес-логика](<../Business logic/README.md>), [OpenSpec agent-runtime](../../openspec/specs/agent-runtime/spec.md).
