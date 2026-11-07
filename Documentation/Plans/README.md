# Планы AgentBridge

Содержимое планов пишется на русском, имена файлов и папок — на английском. Планы разделены на небольшие Markdown-этапы. Поведение продукта определяется [бизнес-логикой](<../Business logic/README.md>) и [требованиями OpenSpec](../../openspec/specs/agent-runtime/spec.md).

| План | Статус | Содержание |
| --- | --- | --- |
| [Коннектор sub2api](<sub2api-connector-2026-11-06-(in-progress)/main.md>) | in-progress | Этапы01–02 готовы;03.01/03.02 verification: [settings/generation и режим без automatic compact](<sub2api-connector-2026-11-06-(in-progress)/03-sub2api/client-adaptation.md>) проверены;03 blocked compact compatibility T05, T01/T02 открыты; sub2api не дорабатывается,04–05 не начаты |

Создание плана само по себе не разрешает реализацию. Завершённые планы удалены; их материалы доступны в истории Git.

Оставшаяся работа03 разделена в [навигаторе compact03.03](<sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03-compaction.md>) на03.03.01–03.03.06: безопасный режим выполнен, пригодность окна blocked T05, transport/response/активация/регрессия не начаты.
