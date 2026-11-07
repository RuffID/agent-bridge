# 03. Коннектор sub2api

Статус: **verification** в объёме первой версии без compact (budget/settings и generation реализованы; T01/T02 открыты). Отложенный compact — blocked T05. Зависимости: Этап02.

## Цель

Реализовать обязательные операции первой версии sub2api через отдельный адаптер и общие application ports: доступ/settings, JSON/SSE generation и безопасный полный контекст без нового compact.

## Границы

Новый net10.0 adapter и isolated tests. Только ключ sub2api, HTTP/SSE и выбранное OpenAI-направление; live acceptance относится к05.03.

Общие требования и ограничения — в [главном плане](../main.md); нормативный проект — в [OpenSpec](../../../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md).

## Подэтапы

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [03.01. Настройки, ключ и каталог моделей](03.01-catalog-and-access.md) | verification | 02.02 | Доступ/ordinary и manifest settings с явным unknown window проверены; T01 не закрыт |
| [03.02. JSON/SSE генерация и инструменты](03.02-generation-and-tools.md) | verification | 03.01 | Actual JSON/SSE через HttpClientLibrary, canonical на границе AgentBridge; live T02 открыт |
| [03.03. Полный контекст и отложенный compact](03.03-compaction.md) | completed в изолированных границах первой версии | Реализованный путь03.02 | Automatic compact отключён, full request выше threshold через guard проверен; ручной/force Unsupported; дальнейший compact вынесен за объём первой версии |

### Детализация03.03

Существующие03.01/03.02 и их результаты сохранены. В обязательный объём03.03 первой версии входит03.03.01. Пригодность окна и реализация compact03.03.02–03.03.06 сохранены отдельной отложенной работой; T05 не блокирует первую версию.

| Подэтап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [03.03.01. Безопасный полный контекст](03.03.01-full-context-mode.md) | completed в изолированных границах | Реализованный путь03.02 | Отключённый automatic compact не мешает generation через guard |
| [03.03.02. Пригодность compact-окна](03.03.02-window-compatibility.md) | blocked T05; отложено за объём первой версии | 03.03.01, wire-контракт01.01 | Анализ завершён без основания допуска; мера уже выполнена в03.03.01 |
| [03.03.03. Запрос compact](03.03.03-compact-request.md) | not-started; за объёмом первой версии | Положительный результат03.03.02 | Existing endpoint/body и HttpClientLibrary transport |
| [03.03.04. Чтение compact](03.03.04-compact-response.md) | not-started; за объёмом первой версии | 03.03.03 | Canonical output/envelope и завершение операции |
| [03.03.05. Принятие окна](03.03.05-window-activation.md) | not-started; за объёмом первой версии | 03.03.04, действующее основание03.03.02 | Completed-only, сохранение до активации, prefix/хвост/история |
| [03.03.06. Проверки compact](03.03.06-compact-verification.md) | not-started; за объёмом первой версии | 03.03.05 | Регрессия и сведение evidence/статусов |

Статический вывод03.03.02 не изменился; совместимость compact не подтверждена.03.03.03–03.03.06 не начинаются;03.03.01 остаётся рабочим режимом первой версии. T01 относится к03.01 и не блокирует разрешённый unknown-window режим; live T02 остаётся05.03. Этап03 — verification, а не полная приёмка. Provenance/restart04, новая поставка05 и live-приёмка не начинались; изменение объёма не разрешает их реализацию.

## Общий результат и приёмка

Sub2Api options, access resolver, ModelCatalog и ModelSettingsReader. Sub2Api IModelGateway.GenerateAsync для полного рабочего контекста без previous_response_id. Automatic compact отключён; ручной/force CompactAsync возвращает Unsupported до factory/HTTP. Полный контекст проходит отдельный guard, превышение бюджета приводит к явной ошибке без обрезания и retry. Этап завершён только при выполнении обязательных критериев первой версии и записи фактических проверок; отложенные03.03.02–03.03.06 и T05 не входят в её gate. Пропуски остальных обязательных проверок сохраняют незавершённый статус.

## Фактический результат

Актуально: [клиентская адаптация](client-adaptation.md) реализует ServerValidationWithUnknownWindow, declared manifest settings, v1/v2 budget snapshots и JSON/SSE generation. Принята canonical гарантия на входе sub2api и в истории AgentBridge с допуском existing server transformations; неизменная upstream передача не обещается. T01 не блокирует явный unknown-window режим, T05 сохраняет Unsupported compact;04 не начат.

Доступ/ordinary каталог/manifest и generation используют existing composition/HttpClientLibrary. Один POST передаёт полный рабочий контекст без previous_response_id и legacy turn-state; actual JSON/SSE response сохраняет output/envelope, awaited callbacks, partial/terminal и caller/deadline/cleanup. Incoming continuation отклоняется до factory; returned response id не создаёт anchor. Compact отдельно остаётся Unsupported.

[Результаты, команды и проверки](results.md) · [Ограничения](contract-blockers.md). Текущее продолжение: семь конкретных compile-check без warnings/errors,557 Core +161 Sub2Api +414 CodexLb =1132 isolated tests passed, failed/skipped0. Прежние565/1151 не складываются с ними как уникальные tests. AutomaticCompactionEnabled=false даёт Disabled без compact/save; full request продолжает отдельный budget guard. Ручной/force compact Unsupported; ошибки executed compact прежних путей не маскируются. Этап не completed; T01–T05 открыты. Completed-only/save-before-activation, codex-lb continuation и история сохранены. Этап04 не начинался.

Подготовленное ранее [серверное предложение](server-contract-proposal.md) отклонено: sub2api не дорабатывается. [Опись](contract-source-evidence.json) сохраняет historical48 и отдельный current compactReview0.2.20 с10 отличиями; новые headers/profile/revision не являются зависимостью. Mapping не доказывает несовместимость; T05 требует подтвердить пригодность compact-окна для generation, включая удаление при recovery. Принятая клиентская граница canonical не разрешает client retry, смену ключа/модели/коннектора или повтор business tool. Открытых клиентских решений нет.
