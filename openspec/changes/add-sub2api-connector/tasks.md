# Задачи реализации

Этапы01–02 завершены в объёме baseline, DI и общих механизмов. Этап03 и budget/manifest settings03.01/actual JSON/SSE generation03.02 — verification в объёме первой версии без compact;03.03 завершён в изолированных границах03.03.01. Отложенный compact blocked T05 и не блокирует приёмку первой версии. [План](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/main.md), [результаты03](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/results.md) и [клиентская адаптация](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/client-adaptation.md) описывают принятую canonical границу AgentBridge без обещания upstream сохранности. T01/T02/T05 открыты; T03 проверен на04, T04 закрыт локальными проверками05.02. Evidence isolated, статической ABI, Windows binary и PostgreSQL acceptance учитывается раздельно; live OAuth05.03 не начат.

- [x] 01.01 Зафиксировать wire-матрицу sub2api и происхождение возможностей моделей.
- [x] 01.02 Зафиксировать legacy API/ABI и контрольные consumers.
- [x] 02.01 Реализовать выбор одного connector bundle в DI с прежним default codex-lb (sub2api staged ports — Unsupported).
- [x] 02.02 Выделить подтверждённый общий Responses-код без регрессий legacy поверхности (linked source, isolated regression, PE/API).
- [ ] 03.01 Реализовать Sub2Api options, ключи, каталог и settings.
  - [x] Подключить actual scoped доступ/Bearer/ordinary каталог ID/settings к existing bundle; fake HTTP/caller/deadline/cleanup/log tests.
  - [x] Повторно сверить current/versioned public sources; подготовленное серверное предложение отклонено, sub2api не дорабатывается.
  - [x] Реализовать согласованный ServerValidationWithUnknownWindow, declared manifest settings и budget snapshot v1/v2; проверить guard/status/compactor и сохранить прежние публичные сигнатуры.
  - [x] Заменить выбор manifest на прямой /backend-api/codex/models без client_version; сохранить ordinary /v1/models и добавить регрессию root/prefix/конечного /v1 и HTTP 502 без fallback. Compile-check и178 Sub2Api tests прошли; старое общее ожидание /v1/ в pinned-run regression заменено точными manifest/generation URL.
  - [x] Выполнить подготовленные регрессионные тесты смены manifest-маршрута и проверить actual generation через текущую Sub2Api DLL:178 isolated Sub2Api cases и5 PostgreSQL restart/recovery cases с local HTTP прошли. Это не live OAuth-приёмка.
  - [ ] Подтвердить источник input budget/provenance metadata (T01 открыт; не блокирует явно разрешённый unknown-window settings).
- [ ] 03.02 Реализовать HTTP JSON/SSE generation и tools с полным рабочим контекстом.
  - [x] Разобрать обе forwarding ветки/non-stream и сохранить fail-closed gateway до HTTP при недоказанном T05.
  - [x] Уточнить pre-send/cross-mode encrypted stripping и internal raw-model observation; прежнее серверное предложение отклонено.
  - [x] Отделить transport normalization/обратимые tool-ID преобразования от конкретных потерь reasoning.content/encrypted items; установить no-op text hooks и сформулировать C01 о клиентской гарантии.
  - [x] Применить принятую canonical границу AgentBridge с допуском existing server transformations, сохранив no-client-retry и отсутствие обещания upstream identity.
  - [x] Реализовать actual JSON/SSE lifecycle/tools через existing bundle/HttpClientLibrary: полный request без continuation, non-stream SSE, awaited callbacks, partial/terminal/caller/deadline/cleanup и safe outcome logs; проверить public facade и gateway.
  - [ ] Подтвердить live OAuth/tools chain отдельно в05.03 (T02; не заменяется isolated fixtures).
- [x] 03.03 Реализовать и проверить безопасный полный контекст без compact в изолированных границах первой версии.
  - [x] Зафиксировать compact endpoint/body/mapping/fallback и безопасный Unsupported до HTTP.
  - [x] Зафиксировать initial compact model override до первого send; новые server metadata/versioned API не требуются.
  - [x] Проверить новый budget режим через existing core compactor: Completed-only/save-before-activation/хвост/history и Unsupported без generation fallback (не Sub2Api transport).
  - [x] Отделить возможность model mapping от доказанной несовместимости; зафиксировать конкретный HTTP recovery path удаления encrypted compaction и current source drift0.2.20 без переписывания evidence01–02.
  - [x] Реализовать выбранный безопасный режим: automatic compact sub2api отключён до dispatch/save, полный PreparedRequest выше threshold проходит separate guard; explicit/force compact Unsupported. Сохранить default automatic прежних gateways.
  - [x] Проверить actual AgentRunner/ContextCompactor/Sub2Api/HttpClientLibrary через in-memory storage/fake handler: полный canonical input/tool pair, known/unknown budget, server rejection без retry, force и прежнее окно/хвост; сохранить core/CodexLb regression.
  - [x] [03.03.01. Безопасный полный контекст](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.01-full-context-mode.md): реализован и проверен изолированно; результаты двух предыдущих отметок, без нового прогона.
  - [x] Завершить статический анализ03.03.02: routing/capability/mapping/sticky/forwarding не исключают same-account HTTP recovery с удалением окна и успехом по хвосту; отделить это от неподтверждённой несовместимости deployment/pool. Минимальная клиентская мера уже выполнена в03.03.01; source hashes сверены без запуска tests/CLI/live. Отрицательный результат сохранён для отложенного compact.

## Отложенный compact — за обязательным объёмом первой версии

T05 остаётся открытым. Следующие задачи не начаты и не являются gate приёмки первой версии; возобновление требует нового достаточного основания и отдельного поручения.

- [ ] [03.03.02. Пригодность compact-окна](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.02-window-compatibility.md): положительный gate использования окна следующей generation с учётом recovery (blocked T05; анализ завершён без основания допуска). Passthrough сам не имеет этого recovery, но исключительный passthrough для deployment/pool не установлен.
- [ ] [03.03.03. Compact request](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.03-compact-request.md): existing endpoint/body через HttpClientLibrary без активации (not-started; после положительного03.03.02).
- [ ] [03.03.04. Canonical response](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.04-compact-response.md): output/envelope и подтверждённый исход (not-started; после03.03.03).
- [ ] [03.03.05. Сохранение и активация](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.05-window-activation.md): existing Completed-only writer path, prefix/хвост/история (not-started; после03.03.04 и при действующем основании03.03.02).
- [ ] [03.03.06. Регрессия и результаты](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03.06-compact-verification.md): адресные проверки и синхронизация evidence/статусов (not-started; после03.03.05, без04/live/поставки).

## Оставшиеся обязательные работы первой версии

- [x] 04.01 Проверить connector provenance каждого opaque источника после обязательного объёма03 без зависимости от T05. Sidecar metadata, guard до Begin/HTTP и единственный новый JSON-формат проверены изолированно; старые диалоги не читаются.
- [x] 04.02 Проверить public AgentRunner path, restart, pinned settings/access и no-replay.
  - [x] 04.02.01 Проверить actual facade/runner/Sub2Api/HttpClientLibrary/persistence ports, JSON/SSE, несколько turns/tools, durable checkpoint и pinned snapshot изолированно.
  - [x] 04.02.02 Проверить serialized committed state и новый DI root, родное opaque продолжение и отказ чужого access без tool replay.
  - [x] 04.02.03 Проверить model error/partial/cancel, expiry/deletion/fence, unknown commit, recovery и cleanup без retry/replay изолированно.
  - [x] 04.02.04 Подтвердить реальный persistence restart/recovery:5 PostgreSQL cases с actual facade/persistence/EFCoreLibrary и local HTTP прошли. Новый DI/DbContext, JSON/SSE, original SQL rows, lost start/outcome acknowledgement, lease/fence, durable explicit recovery и no-replay; без рестарта PostgreSQL/ОС или физического сетевого сбоя.
- [x] 05.01 Выполнить разрешённые compile-check, isolated regression и strict validation:10 builds с0 warnings/errors,1707 passed; обе strict validation прошли,35 сценариев delta сопоставлены с tests/evidence.
- [x] 05.02 Подготовить и проверить DLL-поставку, прежний и новый external consumer:5 Release SDK kits/3 DLL-NuGet/shared,23 external builds,42 Metadata/23 ABI/7 Windows binary tests passed. Frozen SQL Server DLL проверены на трёх SQL Server RID; semantic API/source compilation — на пяти variants. Baseline сохранён.
- [ ] 05.03 Выполнить отдельно разрешённую live OAuth-приёмку первой версии без нового compact и зафиксировать границы результата.

Отметки01–02 и частичные03 подтверждают только фактическую реализацию/проверки в своих границах.03 и03.01/03.02 остаются verification; обязательный03.03 завершён по03.03.01, отложенный compact03.03.02 blocked T05. 04.01 completed изолированно;04.02 и этап04 completed после PostgreSQL04.02.04. Результаты04: Core569/Sub2Api178/CodexLb422/Persistence510/Console63 —1742 passed; отдельно PostgreSQL5 passed. На05.01 Core/Sub2Api/CodexLb/Persistence/Hosting —1707 passed;05.02 добавляет42 Metadata/23 ABI/7 Windows binary cases. Failed/errors/skipped0; strict CLI и новая поставка/API/ABI подтверждены, live05.03 не начат. [Результаты05](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/05-acceptance/results.md). Main spec не синхронизирована; список задач не разрешает запуск команд.

[Серверное предложение](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/server-contract-proposal.md) отклонено; его headers/revision/enforcement не требуются. [Опись](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-source-evidence.json) сохраняет historical48 и current compactReview0.2.20. Прогон03:557 Core +161 Sub2Api +414 CodexLb =1132 cases,0 failed/skipped; он предшествует изменениям04.01 и не подтверждает их. [Результаты04](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/04-dialog-state/results.md) учитываются отдельно. T01/T05 открыты.

**Актуальное решение:** доработка sub2api исключена; single-attempt profile/new metadata не требуются. [Адаптация](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-blockers.md) использует существующий сервер; no-client-retry не запрещает server retries. Поддержка старых сохранённых диалогов отменена, используется один новый формат. T01/T05 открыты,04 completed с PostgreSQL evidence;05.01–05.02 completed локально,05 verification до live05.03.
