# 04. История и продолжение

Статус: **completed**. Зависимости: обязательный объём первой версии этапа03 без compact.

## Цель

Подтвердить безопасность сохранённого контекста и работы после перезапуска.

## Границы

Existing guard/context/run/recovery boundaries и адаптер. Автоматическая миграция старых диалогов между шлюзами не входит.

T05 и отложенные03.03.02–03.03.06 не являются зависимостью первой версии. Проверяются чужое/неизвестное opaque-состояние и codex-lb compact в единственном новом формате. Поддержка ранее сохранённых диалогов обоих коннекторов отменена; reader старого JSON и миграция данных не создаются. Новый Sub2Api compact не реализуется.

Общие требования и ограничения — в [главном плане](../main.md); нормативный проект — в [OpenSpec](../../../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md).

## Подэтапы

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [04.01. Provenance и граница смены коннектора](04.01-connector-provenance.md) | completed | 03.03.01 в обязательном объёме03 | Guards и единственный новый JSON-формат проверены изолированно. |
| [04.02. Полный ход, restart и отсутствие повторов](04.02-restart-and-recovery.md) | completed | 04.01 | 16 isolated сценариев и 5 PostgreSQL cases: новый DI, payload/provenance, fence/recovery и no-replay. |

## Общий результат и приёмка

До отправки должны различаться родной, неизвестный и чужой opaque-контекст. На04.02 требуется проверить полный AgentRunner sub2api path с durable ordering и pinned access/settings. Этап завершён только при выполнении критериев всех подэтапов и записи фактических проверок. Пропуски обязательной проверки сохраняют незавершённый статус.

## Фактический результат

04.01 завершён изолированно. На04.02 проверены полный ход, serialized restart, отказы и no-replay; на04.02.04 добавлены и выполнены5 PostgreSQL cases с настоящими persistence/EFCoreLibrary и local HTTP. Подтверждены JSON/SSE после нового facade root, сохранность original SQL rows, Started до handler, lost start/outcome acknowledgement, live/expired lease, stale epoch и durable explicit recovery без повторного инструмента.

Актуальные isolated результаты: Core569/Sub2Api178/CodexLb422/Persistence510 и Console63 —1742 passed; последние проверки persistence после новых tests также прошли. Отдельно PostgreSQL5 passed; failed/errors/skipped0. Это provider reconnect приложения/DI, без рестарта PostgreSQL/ОС и без физического сетевого сбоя. [Фактические результаты и ограничения](results.md). Этап04 completed;05 не начат, compact/T05 не изменены.
