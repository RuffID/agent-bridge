# Задачи реализации

Этапы01–02 завершены в объёме baseline, DI и общих механизмов. Этап03 частично реализован и blocked T01/T05. Детали, зависимости, проверки и ограничения — в [плане](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/main.md), [результатах03](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/results.md) и [описании необходимых контрактов](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-blockers.md). T01–T05 открыты; isolated DI/transport и PE/API comparison не являются OAuth или old→new ABI приёмкой.

- [x] 01.01 Зафиксировать wire-матрицу sub2api и происхождение возможностей моделей.
- [x] 01.02 Зафиксировать legacy API/ABI и контрольные consumers.
- [x] 02.01 Реализовать выбор одного connector bundle в DI с прежним default codex-lb (sub2api staged ports — Unsupported).
- [x] 02.02 Выделить подтверждённый общий Responses-код без регрессий legacy поверхности (linked source, isolated regression, PE/API).
- [ ] 03.01 Реализовать Sub2Api options, ключи, каталог и settings.
  - [x] Подключить actual scoped доступ/Bearer/ordinary каталог ID/settings к existing bundle; fake HTTP/caller/deadline/cleanup/log tests.
  - [x] Повторно сверить current/versioned public sources; подготовленное серверное предложение отклонено, sub2api не дорабатывается.
  - [ ] Подтвердить пригодный settings path из существующих возможностей без guessed budget; если требуется изменение клиентского бюджетного контракта, явно выделить зависимость (T01, blocked).
- [ ] 03.02 Реализовать HTTP JSON/SSE generation и tools с полным рабочим контекстом.
  - [x] Разобрать обе forwarding ветки/non-stream и сохранить fail-closed gateway до HTTP при недоказанном T05.
  - [x] Уточнить pre-send/cross-mode encrypted stripping, retry-limit trapdoors и internal raw-model observation; предложить enforcement/metadata/regression обеих веток.
  - [ ] Установить совместимый existing forwarding path, отделить server retries от client retries; реализовать actual JSON/SSE lifecycle/tools без изменения sub2api и проверить их в Sub2Api gateway.
- [ ] 03.03 Реализовать и проверить compact через existing application path.
  - [x] Зафиксировать compact endpoint/body/mapping/fallback и безопасный Unsupported до HTTP.
  - [x] Зафиксировать initial compact model override до первого send и предложить отдельные versioned compact/terminal/metadata/tests.
  - [ ] Подтвердить профиль/actual модель/discriminator; реализовать transport и Completed-only writer integration (T05, blocked).
- [ ] 04.01 Проверить connector provenance и safe handling legacy/cross-connector opaque state.
- [ ] 04.02 Проверить public AgentRunner path, restart, pinned settings/access и no-replay.
- [ ] 05.01 Выполнить разрешённые compile-check, isolated regression и strict validation.
- [ ] 05.02 Подготовить и проверить DLL-поставку, прежний и новый external consumer.
- [ ] 05.03 Выполнить отдельно разрешённую live OAuth-приёмку и зафиксировать границы результата.

Отметки01–02 и частичные03 подтверждают только фактическую реализацию/проверки в своих границах. Ни03.01 целиком, ни generation/compact03.02–03.03, новая поставка/ABI, strict CLI и live-приёмка не завершены. Main spec не синхронизирована; список задач не разрешает запуск команд. Работа04 не начиналась.

[Предложение серверного расширения](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/server-contract-proposal.md) ранее подготовлено, затем отклонено; headers/response fields не являются existing API или текущей зависимостью. [Опись46 sources](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-source-evidence.json) и [уточнения блокеров](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-blockers.md) не закрывают T01/T05. На этом продолжении C#/.NET/Go tests и compile-check не повторялись: выполнены только документация и статическая проверка; транспорт остаётся Unsupported.

**Актуальное решение:** серверное предложение отклонено; доработка sub2api исключена, дальнейшее согласование его расширения не требуется. Завершённые исследовательские отметки выше не превращают single-attempt profile/new metadata в обязательные требования. [Текущий путь адаптации](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-blockers.md) использует существующие серверные возможности; no-client-retry не запрещает все server retries. T01/T05 остаются открытыми,04 не начат.
