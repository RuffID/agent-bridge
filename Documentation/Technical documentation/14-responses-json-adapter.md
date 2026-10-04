# JSON Responses, этап 14

Статус: **Реализован, адресно проверен и принят координатором; локальный коммит разрешён**. OpenSpec CLI limitation принята как невыполненная проверка; change не архивирован. Нормы: [agent-runtime](../../openspec/specs/agent-runtime/spec.md), [change](../../openspec/changes/responses-json-adapter/proposal.md). [Команды, результаты и ограничения](<../Plans/AgentBridge Initial Implementation/14-responses-json-adapter.md>).

## Подключение и вызов

`AddCodexLbResponses` из `AgentBridge.CodexLb.Configuration` регистрирует scoped `IModelGateway` → `CodexLbModelGateway`, а также существующие catalog/settings/resolver и actual `HttpApiClient`. Использовать вместо отдельного `AddCodexLbModelCatalog`; регистрация/разрешение не выполняют HTTP. Приложение предоставляет logging/options, `IIndividualModelKeySource` и принадлежащий ему HttpClient. Не добавлять retry/account-changing handlers; HttpClient.Timeout должен позволять выбранный GenerationTimeout. Более короткий app-owned timeout даёт неподтверждённую адаптером cancellation, которая распространяется как исключение.

```csharp
services.AddCodexLbConfiguration(options =>
{
    options.BaseAddress = "https://gateway.example/prefix";
    options.Model = "application-model";
    options.GenerationTimeout = TimeSpan.FromSeconds(180);
});
// Приложение ранее зарегистрировало logging и IIndividualModelKeySource.
services.AddCodexLbResponses(_ => applicationHttpClient);
```

Приложение получает `IModelAccessResolver`, выбирает per-call `ModelAccess`, затем вызывает `IModelGateway.GenerateAsync(call, preparedRequest, access, cancellationToken: callerToken)` вне write UoW. Resolver сохраняет stage13 null-only shared-key rule. Gateway не перечитывает ключ и не вызывает каталог; проверка model/effort через ModelSelectionValidator выполняется приложением до вызова. Он передаёт exact значения, без скрытой подмены.

## Запрос и canonical snapshots

POST выполняется по canonical base-prefix + `/v1/responses` с per-call Bearer, `stream=false`, `store=false`, instructions/model/input/tools. Каждый CanonicalModelItem переносится полностью и по порядку: messages/content, function_call/function_call_output/call_id, reasoning/compaction/encrypted_content и unknown fields. Function definitions имеют плоскую Responses-форму type/name/description/parameters/strict; схема сохраняется полностью. Схему strict tools проверяет codex-lb; локальный stage14 не реализует executor tools или валидацию бизнес-параметров.

ModelRequest теперь принимает optional `ModelRequestParameters` после continuation. Это независимый JsonElement object snapshot без HTTP dependency. Согласован пользователем 2026-10-04.

| Контроль | Локальная форма |
| --- | --- |
| tool_choice | string/object, вложенные неизвестные поля сохраняются |
| parallel_tool_calls | boolean |
| include | array of strings; если отсутствует, отправляется reasoning.encrypted_content; explicit [] сохраняется |
| service_tier / prompt_cache_key | string без скрытой нормализации |
| truncation | auto/disabled согласно текущему codex-lb |
| text | object с полным format/schema/unknown content |
| reasoning | object с summary/unknown fields; effort только из ModelRequest.ReasoningEffort |

Неизвестный top-level параметр/override model/input/instructions/tools/stream/store/previous_response_id — Unsupported до HTTP; дубликат контроля, override reasoning.effort или неверная форма — Validation. Поддержка конкретных значений/вложенных контролей upstream не обещается по allowlist top-level; explicit серверный отказ возвращается без fallback. Envelope/continuation не входят в input. Будущий tokenizer должен учитывать весь prepared request, включая Parameters.

## Результат и отмена

Reader сохраняет output как отдельные независимые ordered canonical snapshots и полный envelope с id/usage/model/error/incomplete_details/unknown fields. Completed требует status=completed, output array и отсутствие explicit error. Failed либо non-null error возвращают ServiceResult.Ok(ModelResponse.Failed(...)); это получение отчёта, а не успех модели. Incomplete/unknown/missing lifecycle либо completed без output дают Incomplete с полным envelope. Server cancelled без фактической caller cancellation не считается подтверждённым Canceled. Malformed JSON/output shape даёт безопасный Rejected без фиктивного Completed.

GenerationTimeout ограничивает отправку и чтение; gateway owns deadline/linked CTS, HttpClientLibrary owns request/response/stream. До получения полного отчёта caller cancellation распространяется OCE с исходным caller token. После полного отчёта поздняя отмена возвращает Canceled с тем же output/envelope/continuation. Полученный explicit typed HTTP/model/JSON failure сохраняет приоритет. Caller приоритетнее deadline; только локальный deadline даёт Timeout. Неожиданные I/O и cancellation без подтверждённого источника распространяются, без retry.

## Continuation

Снимок содержит `adapter="codex-lb-json-v1"`, SHA-256 binding однозначно сериализованных dialog/owner/agent/endpoint/key и отдельные previous_response_id/headers. Ключ не сохраняется; turnId исключён, чтобы следующий turn того же диалога мог продолжиться. Binding защищает от случайного смешивания trusted application data, не доказывает upstream account ownership и не является авторизацией. codex-lb сохраняет серверную ответственность за account routing.

При несовпадении binding возвращается Conflict до HTTP; неизвестный формат — Unsupported; некорректный anchor/header — Validation. Исходящие metadata ограничены previous_response_id и x-codex-turn-state, без нормализации opaque printable ASCII token. Неизвестные continuation metadata сохраняются, но не становятся headers/input. Новый отсутствующий/непригодный id удаляет старый previous_response_id; исходный id остаётся в envelope, продолжение может содержать только допустимый turn-state header. Это явно отсутствие нового anchor, не fallback на прежний ответ. Malformed новый turn-state удаляет старое значение. Нет смены ключа/model/account или повторной отправки после неопределённого disconnect.

Например, completed output содержит только function_call и encrypted reasoning. Report сохраняет оба items и usage; следующий подготовленный request может использовать report.Continuation с новым turnId того же call context. Для другого owner/key/endpoint он отклоняется до HTTP. Локальную history/composition адаптер не собирает.

## Безопасная ошибка

HTTP failure возвращает `CodexLbServiceError : ServiceError`. Application сохраняет semantic Type/фиксированное Message; HttpStatus/ApiType/Code/Param принадлежат только адаптеру. 400/422 → Validation, 401 → Unauthorized, 403 → Forbidden, 404 → NotFound, 409 → Conflict, 408/504 → Timeout, остальные → Rejected. Никакой retry-семантики из этих статусов не выводится.

Reader извлекает только закрытые известные значения type/code/param из Complete valid `{error:{...}}`; неизвестные/нестроковые поля дают null. Список находится в ResponseErrorReader.cs. Динамические param paths и неизвестные серверные коды не публикуются даже если выглядят token-подобно. Truncated/Empty/UnsupportedContent/InvalidEncoding/malformed envelope дают только status. Message/reason/headers/body/exception message не публикуются и не логируются. Для explicit model failure исходный error полностью сохраняется только в чувствительном envelope; обычный ServiceError persistence сохраняет semantic поля, а canonical envelope — полный протокол. Server correlation/Retry-After и SSE остаются последующим работам, автоматических retries нет.

## Нереализованные методы и доказательства

На checkpoint14 onUpdate callback и CompactAsync возвращали Unsupported. Этап15 реализовал [SSE callback](15-responses-sse-adapter.md); onUpdate=null сохраняет описанный JSON путь. Composition16 и tokenizer17 приняты/закоммичены. Этап18 реализует [отдельный compact transport и сценарий](18-context-compaction.md) и принят координатором. Executor tools19 и AgentRunner20 не начаты.

Тесты ResponsesJsonTests проходят public DI/resolver/gateway через actual HttpClientLibrary 0.0.0.5 с fake handler/local streams. Они подтверждают snapshot/order/lifecycle/errors/disposal/cancellation/binding/safe logger/no retries в этом pipeline. Live codex-lb/OpenAI/account ownership/upstream compatibility и app handlers не проверены. Domain/persistence, соседние библиотеки и generated migrations не менялись; прежние DB integration results не повторены и не выдаются за проверку этого транспорта.
