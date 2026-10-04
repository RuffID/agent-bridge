# Техническая документация AgentBridge

Документы описывают фактические API ядра, хранения, транспорта, AgentRunner20, settings/status21, очистки22 и поставку24. Вход для потребителя — [руководство25](25-usage-guide.md) с бинарно проверенными C# примерами. Реализация00–25 завершена в документированных границах; этап25 принят координатором; full hash итоговой локальной фиксации сообщается отдельно. Проверки и ограничения находятся в [карте evidence](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>).

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
| [JSON Responses](14-responses-json-adapter.md) | Public DI/gateway, canonical controls/items/envelope, bound continuation, safe errors, deadline/caller/disposal |
| [SSE Responses](15-responses-sse-adapter.md) | Fragmented/multiline framing, partial/terminal canonical state, callback ownership, cancellation/disposal и изолированные проверки |
| [Композиция контекста](16-context-composition.md) | Public ContextBuilder, ordered providers/window/tail/new input, роли/known function pairs, snapshot guards, отмена и ограничения |
| [Сжатие контекста](18-context-compaction.md) | Compact JSON, terminal prefix/transient граница, atomic save, bounded passes, UnknownBudget и отдельный guard генерации |
| [Инструменты приложения](19-application-tools.md) | Registry/обязательный validator/scoped handler, step/output identity, bounds, canonical результаты, partial failures и граница durable recovery20 |
| [Полный ход агента](20-agent-turn-orchestration.md) | Fixed settings/access/providers, compact/guard/model/tools, short scopes, durable journal, partial/cancel/restart и ограничения |
| [Настройки и статус диалога](21-settings-and-dialog-status.md) | Safe read/select/status, per-dialog independent version, atomic run snapshot, expiry/bytes/context и opaque compatibility |
| [Очистка истёкших диалогов](22-expired-dialog-cleanup.md) | Явный bounded пакет, отдельные scopes/fresh UTC, partial/canceled/unknown и existing guards/cascade |
| [Поставка DLL](24-dll-delivery.md) | .NET10/win-x64, полный состав SQLite/PostgreSQL, XML/inheritdoc, бинарные ссылки и внешние требования |
| [Подключение и использование](25-usage-guide.md) | Проверяемые C# примеры DI/config/dialog/tools, ключи/модели, status/expiry/cleanup и обязанности приложения |

Связанные документы: [бизнес-логика](<../Business logic/README.md>), [OpenSpec agent-runtime](../../openspec/specs/agent-runtime/spec.md).
