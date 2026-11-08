# 02. DI и общая обработка Responses

Статус: **completed** (композиция и общие механизмы; без транспорта этапа03). Зависимости: Этап01.

## Цель

Добавить выбор коннектора, сохраняя старую композицию и владение ресурсами.

## Границы

Integration и транспортные модули. Не менять application ports, business tools, persistence provider selection и поведение default codex-lb.

Общие требования и ограничения — в [главном плане](../main.md); нормативный проект — в [OpenSpec](../../../../openspec/changes/archive/2026-11-08-add-sub2api-connector/specs/agent-runtime/spec.md).

## Подэтапы

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [02.01. Выбор коннектора в DI](02.01-connector-selection.md) | completed | 01.02 | Один выбор bundle, прежний default/overrides, собственные options sub2api и явная staged граница. |
| [02.02. Повторное использование общего протокола](02.02-shared-responses.md) | completed | 02.01 | Единые внутренние JSON/SSE исходники в обоих адаптерах; codex-lb regression и PE/API comparison. |

## Общий результат и приёмка

Новая явная регистрация sub2api при неизменном старом AddAgentBridge. Общие JSON/SSE механизмы используются обоими коннекторами без копии всего CodexLb gateway. Этап завершён только при выполнении критериев всех подэтапов и записи фактических проверок. Пропуски обязательной проверки сохраняют незавершённый статус.

## Фактический результат

Реализованы оба подэтапа. [Результаты и точные команды](results.md): baseline127/112 и frozen DLL сохранны, compile-check трёх тестовых проектов без warnings/errors, **497 passed / 0 failed / 0 skipped** в разрешённых isolated наборах (27 + 414 + 56).

Sub2Api пока возвращает Unsupported для всех четырёх transport-портов. Каталог/access/generation/compact не реализованы; этап03 не начат. API/PE comparison не заменяет external source consumer, old→new ABI resolution, delivery/runtime и live OAuth. T01–T05 остаются открыты; main spec не синхронизирована, CLI OpenSpec не запускался.
