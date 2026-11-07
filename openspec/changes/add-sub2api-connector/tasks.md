# Задачи реализации

Этапы01–02 завершены в объёме baseline, DI и общих механизмов; транспорт sub2api03 не реализован. Детали, зависимости, проверки и ограничения находятся в [плане](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/main.md) и [результатах02](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/02-composition/results.md). T01–T05 открыты; isolated DI/transport и PE/API comparison не являются OAuth или old→new ABI приёмкой.

- [x] 01.01 Зафиксировать wire-матрицу sub2api и происхождение возможностей моделей.
- [x] 01.02 Зафиксировать legacy API/ABI и контрольные consumers.
- [x] 02.01 Реализовать выбор одного connector bundle в DI с прежним default codex-lb (sub2api staged ports — Unsupported).
- [x] 02.02 Выделить подтверждённый общий Responses-код без регрессий legacy поверхности (linked source, isolated regression, PE/API).
- [ ] 03.01 Реализовать Sub2Api options, ключи, каталог и settings.
- [ ] 03.02 Реализовать HTTP JSON/SSE generation и tools с полным рабочим контекстом.
- [ ] 03.03 Реализовать и проверить compact через existing application path.
- [ ] 04.01 Проверить connector provenance и safe handling legacy/cross-connector opaque state.
- [ ] 04.02 Проверить public AgentRunner path, restart, pinned settings/access и no-replay.
- [ ] 05.01 Выполнить разрешённые compile-check, isolated regression и strict validation.
- [ ] 05.02 Подготовить и проверить DLL-поставку, прежний и новый external consumer.
- [ ] 05.03 Выполнить отдельно разрешённую live OAuth-приёмку и зафиксировать границы результата.

Отметки01–02 подтверждают фиксацию, композицию и isolated проверки в объёме этапов. Операции Sub2Api, новая поставка/ABI, strict CLI и live-приёмка не отмечены выполненными. Main spec не синхронизирована; список задач не разрешает запуск команд.
