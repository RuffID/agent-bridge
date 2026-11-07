# 03. Коннектор sub2api

Статус: **blocked** (частичная реализация03.01; T01/T05). Зависимости: Этап02.

## Цель

Реализовать обязательные операции sub2api через отдельный адаптер и общие application ports.

## Границы

Новый net10.0 adapter и isolated tests. Только ключ sub2api, HTTP/SSE и выбранное OpenAI-направление; live acceptance относится к05.03.

Общие требования и ограничения — в [главном плане](../main.md); нормативный проект — в [OpenSpec](../../../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md).

## Подэтапы

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [03.01. Настройки, ключ и каталог моделей](03.01-catalog-and-access.md) | blocked | 02.02 | Доступ/каталог/settings реализованы; пригодный для запуска settings path заблокирован T01 |
| [03.02. JSON/SSE генерация и инструменты](03.02-generation-and-tools.md) | blocked | 03.01 | Контракты forwarding разобраны; gateway явно Unsupported до T05 |
| [03.03. Сжатие рабочего контекста](03.03-compaction.md) | blocked | 03.02 | Endpoint/fallback разобраны; compact явно Unsupported до T05 |

## Общий результат и приёмка

Sub2Api options, access resolver, ModelCatalog и ModelSettingsReader. Sub2Api IModelGateway.GenerateAsync для полного рабочего контекста без previous_response_id. Sub2Api CompactAsync совместим с existing ContextCompactor. Этап завершён только при выполнении критериев всех подэтапов и записи фактических проверок. Пропуски обязательной проверки сохраняют незавершённый статус.

## Фактический результат

Доступ и ordinary каталог через actual HttpClientLibrary подключены к existing bundle. Settings не угадывает capabilities/input budget и сохраняет прежний validator. Sub2ApiPendingConnector заменён самостоятельными портами; generation/compact ограждены до HTTP и не считаются реализованным транспортом.

[Результаты, команды и проверки](results.md) · [Доказательства препятствий и необходимое расширение](contract-blockers.md). Этап не completed; T01–T05 открыты. История/ContextCompactor/IDialogContextWriter и codex-lb не менялись. Этап04 не начинался.
