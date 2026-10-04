# Сжатие контекста — фактический API этапа18

Источник требований: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). [Change/context](../../openspec/changes/context-compaction/context.md), [проверки](<../Plans/AgentBridge Initial Implementation/18-context-compaction.md>).

## Подключение

`AddCodexLbResponses` регистрирует IModelGateway с JSON/SSE GenerateAsync и JSON CompactAsync. Compact использует actual HttpClientLibrary, per-call ModelAccess, отдельный CompactTimeout и POST base-prefix + `/v1/responses/compact`. Нет retry, fallback ключа/модели, хоста или скрытого текстового резюме.

`AddAgentBridgeCompaction` регистрирует scoped ContextCompactor и TimeProvider.System через TryAdd. Приложение регистрирует ContextBuilder с явно выбранными ordered providers, IContextTokenCounter, IModelGateway, IDialogContextWriter и существующие ContextCompactionOptions. Например, после регистрации приложения:

```csharp
services.AddAgentBridgeTokenization();
services.AddScoped<ContextBuilder>(sp => new ContextBuilder(
    new IContextProvider[] { sp.GetRequiredService<AuthorizedContextProvider>() }));
services.AddAgentBridgeCompaction();
```

AuthorizedContextProvider — тип приложения, проверяющий его права. AddAgentBridgeTokenization сохраняет заранее зарегистрированный app counter. AddAgentBridgePersistence предоставляет writer; приложение не держит UoW/транзакцию вокруг CompactAsync.

Публичный сценарий:

```csharp
ServiceResult<ContextCompactionResult> result = await compactor.CompactAsync(
    call, dialogSnapshot, newRequest, checkedSettings, modelAccess, cancellationToken);
```

newRequest содержит только ещё не сохранённый input. Snapshot читается обычным IDialogReader до вызова. Threshold/reserve берутся из checkedSettings, MaxPasses фиксируется из IOptionsSnapshot на начало вызова. Exact model/effort и settings проверяются повторно. Continuation здесь явно Unsupported: сценарий требует полный input, а не скрытую server history.

## Граница данных

ContextBuilder вызывается один раз. Его provider items фиксируются на весь вызов. Полный запрос состоит из providers → active window → uncovered history → unsaved input. Trigger использует полную оценку этого запроса со всеми Instructions, Tools и controls. Компактируется только active window плюс terminal turns после ThroughTurnSequence до первого InProgress. Все turns после этой границы, provider items и unsaved input остаются отдельно и добавляются ровно один раз после замены окна.

Compact request сохраняет модель/effort/instructions и поддержанные controls reasoning/service_tier/prompt_cache_key. Tools и generation controls остаются в PreparedRequest, но отсутствуют в отдельном compact payload. Для прямого gateway-вызова Tools/continuation и неизвестные controls отклоняются явно; их нельзя передавать с ожиданием применения. Сервер по текущим исходникам удаляет tools/text/tool_choice из compact.

Известные function pairs проверяются и в полном запросе, и в compact history, и в candidate full request. Пара, разрезанная границей сохраняемой истории и transient tail, не допускает отправку незакрытого compact input. Данные не удаляются и фиктивные function results не создаются. Пустой Completed output при непустой истории отклоняется прикладным сценарием; общий ModelResponse.Completed([]) остаётся допустимым контрактом.

Например: окно покрывает turn1; turn2 terminal, turn3 InProgress. Compact получает окно+turn2, through=2. После принятия generation request содержит provider+compact output+turn3+новый input. Through0 поддерживается: активное окно может иметь through0, весь turn tail остаётся после него. Пустая сохраняемая история даёт NoPersistableHistory без HTTP.

## Принятие, бюджет и остановка

Сценарий запускает compact при полной estimate >= threshold. Отдельный compact payload проверяется ContextBudgetGuard перед HTTP; превышение полного generation budget само по себе не мешает сжать допустимую по размеру сохраняемую часть. Это не подтверждение бюджета последующей генерации.

После Completed candidate пересчитывается полный generation request с фиксированными transient вкладами. При known estimate >= previous estimate кандидат не сохраняется, возвращается NoReduction. Уменьшившийся кандидат сохраняется через existing IDialogContextWriter.SaveAsync; свежий UTC берётся после сетевого ожидания и подсчёта непосредственно перед save. now==expiry запрещает запись. Token исходный, затем только из успешных save; refresh/retry нет. Новый ActiveContext/PreparedRequest публикуется после успешного save. Owner, history, fixed expiry не меняются.

При unknown estimate валидное окно сохраняется, возвращается UnknownBudget и проходы прекращаются. При known estimate < threshold — TargetReached, иначе ограниченное повторение до MaxPasses/PassLimitReached. Статусы NotRequired, NoPersistableHistory, NoReduction и PassLimitReached также не являются разрешением генерации. **После любого отчёта приложение отдельно вызывает ContextBudgetGuard для PreparedRequest.** Встроенный offline counter для opaque даёт null estimate; guard отказывает. Прошлый response.usage, KnownTokens и reserve не подменяют полную оценку.

ServiceResult.Success подтверждает получение отчёта. ContextCompactionResult.Status=Failed содержит исходный Error; ActiveContext/Token/PreparedRequest отражают последнее успешно принятое состояние. LastResponse сохраняет даже непринятый incomplete/failed/canceled/candidate output. Если первый проход сохранён, а второй неудачен, остаётся окно первого прохода. При неожиданном exception/OCE он распространяется; уже сохранённые проходы не откатываются, актуальное состояние читается через IDialogReader. Поздняя отмена после успешного save не означает отмену commit.

## Compact JSON

Discriminator после trim должен начинаться с `response.compact` (actual CompactResponsePayload), output — массив canonical объектов. Отсутствующий/null status допустим; completed подтверждается также явным status=completed. Explicit error/failed даёт Failed; иной lifecycle либо отсутствие output — Incomplete. Нарушенная форма — Rejected. Полный исходный envelope сохраняется без нормализации, output сохраняет unknown/opaque поля и порядок. HTTP 2xx без этого контракта не подтверждает успех.

Compact id и response headers не превращаются в generation continuation. Возвращается null continuation; subsequent generation отправляет canonical output в input. Envelope и usage не становятся input и не определяют новый бюджет. Safe errors/cancellation/disposal используют правила JSON transport14; caller после полного ответа даёт Canceled с данными, explicit failure приоритетнее. Неожиданный I/O распространяется.

## Проверенные границы

Public DI/gateway → actual HttpClientLibrary → fake handler/local streams; public ContextCompactor → actual ContextBuilder, actual offline counter в opaque-сценарии и fake gateway/writer для управляемых version/expiry/failure границ. Это не live codex-lb compatibility и не новая проверка транзакционности реальной БД. EFCoreLibrary0.0.5 integration на18 не повторяется, persistence code/migrations не меняются. Архивная история не удаляется. [Tools19](19-application-tools.md), [runner20](20-agent-turn-orchestration.md) и [settings21](21-settings-and-dialog-status.md) реализованы позднее; их evidence отдельно от isolated compact18.
