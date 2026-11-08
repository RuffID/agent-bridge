# 05. Проверки и поставка

Статус: **verification**. Зависимости: Этап04.05.01–05.02 completed;05.03 not-started.

## Цель

Подтвердить старый и новый consumer, подготовить DLL и разделить локальные проверки с live-приёмкой.

## Границы

Тесты, delivery scripts/manifests и документация. Подключение рабочего приложения, секретов и серверов только по отдельному поручению.

Приёмка первой версии не требует успешного Sub2Api compact или закрытия T05. Проверяются полный контекст, budget guard и явный Unsupported compact; прежний compact codex-lb сохраняется. Отложенная работа03.03.02–03.03.06 не считается выполненной. Выполнен локальный объём05.01–05.02, live05.03 не начинался.

Общие требования и ограничения — в [главном плане](../main.md); нормативный проект — в [OpenSpec](../../../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md).

## Подэтапы

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [05.01. Изолированная регрессия и требования](05.01-regression.md) | completed | 04.02 |1707 isolated tests;10 compile-check; обе strict validation зелёные;35 сценариев delta сопоставлены с evidence. |
| [05.02. DLL-поставка и внешние потребители](05.02-binary-delivery.md) | completed | 05.01 | Fresh kits,23 external builds,42 metadata/23 ABI/7 Windows binary cases; baseline сохранён. |
| [05.03. Контролируемая проверка sub2api](05.03-live-acceptance.md) | not-started | 05.02 | Фактическое подтверждение выбранного HTTP/SSE пути на sub2api с OAuth upstream. |

## Общий результат и приёмка

Проверенные core/CodexLb/Sub2Api/facade сценарии и согласованные OpenSpec artifacts. Воспроизводимые комплекты с новым коннектором и проверенный прежний consumer. Фактическое подтверждение выбранного HTTP/SSE пути на sub2api с OAuth upstream. Этап завершён только при выполнении критериев всех подэтапов и записи фактических проверок. Пропуски обязательной проверки сохраняют незавершённый статус.

## Фактический результат

2026-11-08 выполнены05.01 и05.02. [Результаты](results.md) содержат команды, hashes и границы; [покрытие](scenario-coverage.md) — все35 сценариев delta. Production-код не менялся, добавлены отдельный ABI verifier и Sub2Api binary/compile-only consumers. Live OAuth, Linux execution, sync/archive и deployment не выполнялись. Без05.03 этап05 и весь план не завершены.
