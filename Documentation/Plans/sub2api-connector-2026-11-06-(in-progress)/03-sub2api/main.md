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
| [03.03. Сжатие рабочего контекста](03.03-compaction.md) | blocked | 03.02 | Automatic compact отключён, full request выше threshold через guard проверен; ручной/force Unsupported, T05 открыт |

### Детализация03.03

Существующие03.01/03.02 и их результаты сохранены. Оставшаяся работа compact разделена на последовательные единицы; каждый файл содержит границы, проверки и критерии готовности.

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [03.03.01. Безопасный полный контекст](03.03.01-full-context-mode.md) | completed в изолированных границах | Реализованный путь03.02 | Отключённый automatic compact не мешает generation через guard |
| [03.03.02. Пригодность compact-окна](03.03.02-window-compatibility.md) | blocked T05; анализ завершён | 03.03.01, wire-контракт01.01 | Контракт не исключает удаление окна и успех по хвосту; мера уже выполнена в03.03.01 |
| [03.03.03. Запрос compact](03.03.03-compact-request.md) | not-started | Положительный результат03.03.02 | Existing endpoint/body и HttpClientLibrary transport |
| [03.03.04. Чтение compact](03.03.04-compact-response.md) | not-started | 03.03.03 | Canonical output/envelope и завершение операции |
| [03.03.05. Принятие окна](03.03.05-window-activation.md) | not-started | 03.03.04, действующее основание03.03.02 | Completed-only, сохранение до активации, prefix/хвост/история |
| [03.03.06. Проверки compact](03.03.06-compact-verification.md) | not-started | 03.03.05 | Регрессия и сведение evidence/статусов |

Статический анализ03.03.02 завершён без основания допуска: routing/capability/mapping/sticky не исключают same-account ordinary HTTP recovery с потерей окна. Passthrough не имеет именно этого recovery, но его исключительность для deployment/pool не подтверждена.03.03.03–03.03.06 не начинаются;03.03.01 остаётся рабочим режимом и уже содержит минимальную клиентскую меру. Нового клиентского решения нет. T01 относится к03.01 и не блокирует разрешённый unknown-window режим; live T02 остаётся05.03. Provenance/restart04, новая поставка05 и live-приёмка не начинались.03 остаётся blocked.

## Общий результат и приёмка

Sub2Api options, access resolver, ModelCatalog и ModelSettingsReader. Sub2Api IModelGateway.GenerateAsync для полного рабочего контекста без previous_response_id. Sub2Api CompactAsync совместим с existing ContextCompactor. Этап завершён только при выполнении критериев всех подэтапов и записи фактических проверок. Пропуски обязательной проверки сохраняют незавершённый статус.

## Фактический результат

Актуально: [клиентская адаптация](client-adaptation.md) реализует ServerValidationWithUnknownWindow, declared manifest settings, v1/v2 budget snapshots и JSON/SSE generation. Принята canonical гарантия на входе sub2api и в истории AgentBridge с допуском existing server transformations; неизменная upstream передача не обещается. T01 не блокирует явный unknown-window режим, T05 сохраняет Unsupported compact;04 не начат.

Доступ/ordinary каталог/manifest и generation используют existing composition/HttpClientLibrary. Один POST передаёт полный рабочий контекст без previous_response_id и legacy turn-state; actual JSON/SSE response сохраняет output/envelope, awaited callbacks, partial/terminal и caller/deadline/cleanup. Incoming continuation отклоняется до factory; returned response id не создаёт anchor. Compact отдельно остаётся Unsupported.

[Результаты, команды и проверки](results.md) · [Ограничения](contract-blockers.md). Текущее продолжение: семь конкретных compile-check без warnings/errors,557 Core +161 Sub2Api +414 CodexLb =1132 isolated tests passed, failed/skipped0. Прежние565/1151 не складываются с ними как уникальные tests. AutomaticCompactionEnabled=false даёт Disabled без compact/save; full request продолжает отдельный budget guard. Ручной/force compact Unsupported; ошибки executed compact прежних путей не маскируются. Этап не completed; T01–T05 открыты. Completed-only/save-before-activation, codex-lb continuation и история сохранены. Этап04 не начинался.

Подготовленное ранее [серверное предложение](server-contract-proposal.md) отклонено: sub2api не дорабатывается. [Опись](contract-source-evidence.json) сохраняет historical48 и отдельный current compactReview0.2.20 с10 отличиями; новые headers/profile/revision не являются зависимостью. Mapping не доказывает несовместимость; T05 требует подтвердить пригодность compact-окна для generation, включая удаление при recovery. Принятая клиентская граница canonical не разрешает client retry, смену ключа/модели/коннектора или повтор business tool. Открытых клиентских решений нет.
