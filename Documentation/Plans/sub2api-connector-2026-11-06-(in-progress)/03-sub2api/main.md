# 03. Коннектор sub2api

Статус: **blocked** (budget/settings и generation реализованы; compact T05). Зависимости: Этап02.

## Цель

Реализовать обязательные операции sub2api через отдельный адаптер и общие application ports.

## Границы

Новый net10.0 adapter и isolated tests. Только ключ sub2api, HTTP/SSE и выбранное OpenAI-направление; live acceptance относится к05.03.

Общие требования и ограничения — в [главном плане](../main.md); нормативный проект — в [OpenSpec](../../../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md).

## Подэтапы

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [03.01. Настройки, ключ и каталог моделей](03.01-catalog-and-access.md) | verification | 02.02 | Доступ/ordinary и manifest settings с явным unknown window проверены; T01 не закрыт |
| [03.02. JSON/SSE генерация и инструменты](03.02-generation-and-tools.md) | verification | 03.01 | Actual JSON/SSE через HttpClientLibrary, canonical на границе AgentBridge; live T02 открыт |
| [03.03. Сжатие рабочего контекста](03.03-compaction.md) | blocked | 03.02 | Уточнён initial model override; existing compact требует совместимости, Unsupported до T05 |

## Общий результат и приёмка

Sub2Api options, access resolver, ModelCatalog и ModelSettingsReader. Sub2Api IModelGateway.GenerateAsync для полного рабочего контекста без previous_response_id. Sub2Api CompactAsync совместим с existing ContextCompactor. Этап завершён только при выполнении критериев всех подэтапов и записи фактических проверок. Пропуски обязательной проверки сохраняют незавершённый статус.

## Фактический результат

Актуально: [клиентская адаптация](client-adaptation.md) реализует ServerValidationWithUnknownWindow, declared manifest settings, v1/v2 budget snapshots и JSON/SSE generation. Принята canonical гарантия на входе sub2api и в истории AgentBridge с допуском existing server transformations; неизменная upstream передача не обещается. T01 не блокирует явный unknown-window режим, T05 сохраняет Unsupported compact;04 не начат.

Доступ/ordinary каталог/manifest и generation используют existing composition/HttpClientLibrary. Один POST передаёт полный рабочий контекст без previous_response_id и legacy turn-state; actual JSON/SSE response сохраняет output/envelope, awaited callbacks, partial/terminal и caller/deadline/cleanup. Incoming continuation отклоняется до factory; returned response id не создаёт anchor. Compact отдельно остаётся Unsupported.

[Результаты, команды и проверки](results.md) · [Ограничения](contract-blockers.md). В продолжении по плану Б пять адресных compile-check без warnings/errors,151 Sub2Api +414 CodexLb =565 isolated tests passed. Прежние1151 относятся к предыдущему бюджетному продолжению. Этап не completed; T01–T05 открыты. ContextCompactor/IDialogContextWriter, codex-lb continuation и история сохраняют прежнюю семантику. Этап04 не начинался.

Подготовленное ранее [серверное предложение](server-contract-proposal.md) отклонено: sub2api не дорабатывается. [Опись48 источников](contract-source-evidence.json) сохраняет evidence; новые headers/profile/revision не являются зависимостью. Принятая клиентская граница canonical не разрешает client retry, смену ключа/модели/коннектора или повтор business tool. Ответ пользователя больше не требуется для generation03.02; compact остаётся технической проверкой.
