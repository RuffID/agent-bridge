# 03. Коннектор sub2api

Статус: **not-started**. Зависимости: Этап02.

## Цель

Реализовать обязательные операции sub2api через отдельный адаптер и общие application ports.

## Границы

Новый net10.0 adapter и isolated tests. Только ключ sub2api, HTTP/SSE и выбранное OpenAI-направление; live acceptance относится к05.03.

Общие требования и ограничения — в [главном плане](../main.md); нормативный проект — в [OpenSpec](../../../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md).

## Подэтапы

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [03.01. Настройки, ключ и каталог моделей](03.01-catalog-and-access.md) | not-started | 02.02 | Sub2Api options, access resolver, ModelCatalog и ModelSettingsReader. |
| [03.02. JSON/SSE генерация и инструменты](03.02-generation-and-tools.md) | not-started | 03.01 | Sub2Api IModelGateway.GenerateAsync для полного рабочего контекста без previous_response_id. |
| [03.03. Сжатие рабочего контекста](03.03-compaction.md) | not-started | 03.02 | Sub2Api CompactAsync совместим с existing ContextCompactor. |

## Общий результат и приёмка

Sub2Api options, access resolver, ModelCatalog и ModelSettingsReader. Sub2Api IModelGateway.GenerateAsync для полного рабочего контекста без previous_response_id. Sub2Api CompactAsync совместим с existing ContextCompactor. Этап завершён только при выполнении критериев всех подэтапов и записи фактических проверок. Пропуски обязательной проверки сохраняют незавершённый статус.

## Фактический результат

Реализация не начата. Подготовлены только план и критерии; команды этого этапа не выполнялись.
