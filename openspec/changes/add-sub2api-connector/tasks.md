# Задачи реализации

Этапы01–02 завершены в объёме baseline, DI и общих механизмов. Этап03 частично реализован: budget/manifest settings03.01 и actual JSON/SSE generation03.02 verification; compact blocked T05. [План](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/main.md), [результаты03](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/results.md) и [клиентская адаптация](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/client-adaptation.md) описывают принятую canonical границу AgentBridge без обещания upstream сохранности. T01–T05 открыты; isolated green не является OAuth/old→new ABI приёмкой.

- [x] 01.01 Зафиксировать wire-матрицу sub2api и происхождение возможностей моделей.
- [x] 01.02 Зафиксировать legacy API/ABI и контрольные consumers.
- [x] 02.01 Реализовать выбор одного connector bundle в DI с прежним default codex-lb (sub2api staged ports — Unsupported).
- [x] 02.02 Выделить подтверждённый общий Responses-код без регрессий legacy поверхности (linked source, isolated regression, PE/API).
- [ ] 03.01 Реализовать Sub2Api options, ключи, каталог и settings.
  - [x] Подключить actual scoped доступ/Bearer/ordinary каталог ID/settings к existing bundle; fake HTTP/caller/deadline/cleanup/log tests.
  - [x] Повторно сверить current/versioned public sources; подготовленное серверное предложение отклонено, sub2api не дорабатывается.
  - [x] Реализовать согласованный ServerValidationWithUnknownWindow, declared manifest settings и budget snapshot v1/v2; проверить guard/status/compactor и сохранить прежние публичные сигнатуры.
  - [ ] Подтвердить источник input budget/provenance metadata (T01 открыт; не блокирует явно разрешённый unknown-window settings).
- [ ] 03.02 Реализовать HTTP JSON/SSE generation и tools с полным рабочим контекстом.
  - [x] Разобрать обе forwarding ветки/non-stream и сохранить fail-closed gateway до HTTP при недоказанном T05.
  - [x] Уточнить pre-send/cross-mode encrypted stripping и internal raw-model observation; прежнее серверное предложение отклонено.
  - [x] Отделить transport normalization/обратимые tool-ID преобразования от конкретных потерь reasoning.content/encrypted items; установить no-op text hooks и сформулировать C01 о клиентской гарантии.
  - [x] Применить принятую canonical границу AgentBridge с допуском existing server transformations, сохранив no-client-retry и отсутствие обещания upstream identity.
  - [x] Реализовать actual JSON/SSE lifecycle/tools через existing bundle/HttpClientLibrary: полный request без continuation, non-stream SSE, awaited callbacks, partial/terminal/caller/deadline/cleanup и safe outcome logs; проверить public facade и gateway.
  - [ ] Подтвердить live OAuth/tools chain отдельно в05.03 (T02; не заменяется isolated fixtures).
- [ ] 03.03 Реализовать и проверить compact через existing application path.
  - [x] Зафиксировать compact endpoint/body/mapping/fallback и безопасный Unsupported до HTTP.
  - [x] Зафиксировать initial compact model override до первого send; новые server metadata/versioned API не требуются.
  - [x] Проверить новый budget режим через existing core compactor: Completed-only/save-before-activation/хвост/history и Unsupported без generation fallback (не Sub2Api transport).
  - [x] Отделить возможность model mapping от доказанной несовместимости; зафиксировать конкретный HTTP recovery path удаления encrypted compaction и current source drift0.2.20 без переписывания evidence01–02.
  - [x] Реализовать выбранный безопасный режим: automatic compact sub2api отключён до dispatch/save, полный PreparedRequest выше threshold проходит separate guard; explicit/force compact Unsupported. Сохранить default automatic прежних gateways.
  - [x] Проверить actual AgentRunner/ContextCompactor/Sub2Api/HttpClientLibrary через in-memory storage/fake handler: полный canonical input/tool pair, known/unknown budget, server rejection без retry, force и прежнее окно/хвост; сохранить core/CodexLb regression.
  - [x] [03.03.01. Безопасный полный контекст](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.01-full-context-mode.md): реализован и проверен изолированно; результаты двух предыдущих отметок, без нового прогона.
  - [x] Завершить статический анализ03.03.02: routing/capability/mapping/sticky/forwarding не исключают same-account HTTP recovery с удалением окна и успехом по хвосту; отделить это от неподтверждённой несовместимости deployment/pool. Минимальная клиентская мера уже выполнена в03.03.01, нового решения нет; source hashes сверены без запуска tests/CLI/live.
  - [ ] [03.03.02. Пригодность compact-окна](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.02-window-compatibility.md): положительный gate использования окна следующей generation с учётом recovery (blocked T05; анализ завершён без основания допуска). Passthrough сам не имеет этого recovery, но исключительный passthrough для deployment/pool не установлен.
  - [ ] [03.03.03. Compact request](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.03-compact-request.md): existing endpoint/body через HttpClientLibrary без активации (not-started; после положительного03.03.02).
  - [ ] [03.03.04. Canonical response](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.04-compact-response.md): output/envelope и подтверждённый исход (not-started; после03.03.03).
  - [ ] [03.03.05. Сохранение и активация](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.05-window-activation.md): existing Completed-only writer path, prefix/хвост/история (not-started; после03.03.04 и при действующем основании03.03.02).
  - [ ] [03.03.06. Регрессия и результаты](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.06-compact-verification.md): адресные проверки и синхронизация evidence/статусов (not-started; после03.03.05, без04/live/поставки).
- [ ] 04.01 Проверить connector provenance и safe handling legacy/cross-connector opaque state.
- [ ] 04.02 Проверить public AgentRunner path, restart, pinned settings/access и no-replay.
- [ ] 05.01 Выполнить разрешённые compile-check, isolated regression и strict validation.
- [ ] 05.02 Подготовить и проверить DLL-поставку, прежний и новый external consumer.
- [ ] 05.03 Выполнить отдельно разрешённую live OAuth-приёмку и зафиксировать границы результата.

Отметки01–02 и частичные03 подтверждают только фактическую реализацию/проверки в своих границах.03.01/03.02 остаются verification по открытым техническим проверкам, compact03.03 blocked. Новая поставка/ABI, strict CLI и live-приёмка не завершены. Main spec не синхронизирована; список задач не разрешает запуск команд. Работа04 не начиналась.

[Серверное предложение](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/server-contract-proposal.md) отклонено; его headers/revision/enforcement не требуются. [Опись](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-source-evidence.json) сохраняет historical48 и отдельный current compactReview0.2.20:10 hashes отличаются. Текущее продолжение проверило557 Core +161 Sub2Api +414 CodexLb =1132 cases,0 failed/skipped; семь конкретных compile-check без warnings/errors. Прежние1151 budget и565 generation — пересекающиеся прогоны, не суммарные уникальные tests. Это не live или Sub2Api compact acceptance. T01/T05 открыты,04 не начат.

**Актуальное решение:** серверное предложение отклонено; доработка sub2api исключена, дальнейшее согласование его расширения не требуется. Завершённые исследовательские отметки выше не превращают single-attempt profile/new metadata в обязательные требования. [Текущий путь адаптации](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-blockers.md) использует существующие серверные возможности; no-client-retry не запрещает все server retries. T01/T05 остаются открытыми,04 не начат.
