# Обезличенные образцы wire-контракта

[Матрица и расшифровка S/A источников](wire-matrix.md). Ни один образец не является захватом live-трафика. `example-model`, example.invalid, IDs, текст и OPAQUE_* — синтетические значения; ключ заменён литералом SUB2API_KEY_PLACEHOLDER. Opaque placeholders не являются пригодными encrypted tokens. Эти материалы нельзя отправлять как acceptance-запросы без замены на подтверждённые значения.

## W01. Обычный каталог — синтетический по S1/S3

Происхождение: форма writeOpenAIModelsList / writeModelsListResponse; это модель с нераспознанным локальным ID, без обещания metadata.

```http
GET /v1/models HTTP/1.1
Host: gateway.example.invalid
Authorization: Bearer SUB2API_KEY_PLACEHOLDER
Accept: application/json
```

```json
{"object":"list","data":[{"id":"example-model","object":"model","created":1704067200,"owned_by":"openai","type":"model","display_name":"example-model"}]}
```

Пустой каталог: `{"object":"list","data":[]}`. Проверить отдельно malformed data, duplicate ID и неизвестный выбранный ID; не заменять их статическим списком.

## W02. Manifest — синтетический сокращённый фрагмент по S4/S5/S6

Происхождение: configuredCodexModelDescriptor. Для иллюстрации взяты fallback context=272000, effective percent=95 и effort=none неизвестной custom модели. Это поля локального шаблона, не подтверждённые возможности реальной модели. Сокращены остальные обязательные поля полного Codex ModelInfo: фрагмент предназначен для разработки проекции AgentBridge, не для проверки Codex CLI parser.

```http
GET /v1/models?client_version=0.0.0 HTTP/1.1
Host: gateway.example.invalid
Authorization: Bearer SUB2API_KEY_PLACEHOLDER
Accept: application/json
```

Версия 0.0.0 — синтетический непустой переключатель маршрута, не согласованная версия будущего клиента.

```json
{"models":[{"slug":"example-model","supported_in_api":true,"default_reasoning_level":"none","supported_reasoning_levels":[{"effort":"none","description":"Use the model's default behavior without configurable reasoning"}],"context_window":272000,"max_context_window":272000,"effective_context_window_percent":95,"input_modalities":["text"],"supports_parallel_tool_calls":false,"supports_reasoning_summary_parameter":true,"support_verbosity":false}]}
```

Входной бюджет и max output отсутствуют. Ожидаемая проекция сохраняет InputContextWindow=null и MaxOutputTokens=null; выбор модели останавливается Unsupported. Для пустого manifest использовать `{"models":[]}`. ETag/304 проверяются отдельным HTTP сценарием, body из 304 не парсится как новый каталог.

## W03. JSON generation и tool call — синтетический по A1/S7/S9

Происхождение: действующий ResponseRequestWriter и Responses function item shapes; example-model/effort здесь иллюстрируют wire, не подтверждают допустимый выбор settings.

```http
POST /v1/responses HTTP/1.1
Host: gateway.example.invalid
Authorization: Bearer SUB2API_KEY_PLACEHOLDER
Content-Type: application/json
Accept: application/json
```

```json
{"model":"example-model","instructions":"Use the supplied tool when needed.","stream":false,"store":false,"reasoning":{"effort":"low"},"include":["reasoning.encrypted_content"],"input":[{"type":"message","role":"user","content":[{"type":"input_text","text":"Read the example total."}]}],"tools":[{"type":"function","name":"read_example_total","description":"Read a synthetic total.","parameters":{"type":"object","properties":{},"required":[],"additionalProperties":false},"strict":true}]}
```

```json
{"id":"resp_example_1","object":"response","status":"completed","output":[{"type":"reasoning","id":"rs_example_1","summary":[],"encrypted_content":"OPAQUE_REASONING_PLACEHOLDER"},{"type":"function_call","id":"fc_example_1","call_id":"call_example_1","name":"read_example_total","arguments":"{}","status":"completed"}],"usage":{"input_tokens":20,"output_tokens":10,"total_tokens":30}}
```

Completed response с function_call — завершённый модельный шаг, а не завершение всего agent run.

## W04. Продолжение с tool output и следующий turn — синтетический по A3/S9

Происхождение: full working context стратегия плана и canonical items из W03. Здесь сознательно сохраняются исходные IDs; нормализация fc/call и reasoning id происходит на upstream границе sub2api, а не в сохранённой истории.

После durable выполнения инструмента следующий POST с теми же headers содержит:

```json
{"model":"example-model","instructions":"Use the supplied tool when needed.","stream":false,"store":false,"reasoning":{"effort":"low"},"include":["reasoning.encrypted_content"],"tools":[{"type":"function","name":"read_example_total","description":"Read a synthetic total.","parameters":{"type":"object","properties":{},"required":[],"additionalProperties":false},"strict":true}],"input":[{"type":"message","role":"user","content":[{"type":"input_text","text":"Read the example total."}]},{"type":"reasoning","id":"rs_example_1","summary":[],"encrypted_content":"OPAQUE_REASONING_PLACEHOLDER"},{"type":"function_call","id":"fc_example_1","call_id":"call_example_1","name":"read_example_total","arguments":"{}","status":"completed"},{"type":"function_call_output","call_id":"call_example_1","output":"{\"total\":42}"}]}
```

```json
{"id":"resp_example_2","object":"response","status":"completed","output":[{"type":"message","id":"msg_example_2","role":"assistant","status":"completed","content":[{"type":"output_text","text":"The example total is 42.","annotations":[]}]}]}
```

На следующем пользовательском turn в input остаются четыре items из W04, затем assistant item из resp_example_2 и новое user message. Ни previous_response_id, ни x-codex-turn-state не добавляются. Это проектный пример; реальная OAuth tool/reasoning цепочка остаётся T02.

## W05. SSE — адаптирован из fake regression fixture S14

Происхождение: `openAINonStreamRegressionSSE` в openai_oauth_passthrough_non_stream_test.go. Заменён resp_7978 на resp_example_stream; сохранены terminal shape, текст OK и usage=1/1/2. Это локальная тестовая константа sub2api, не наблюдение сервера. Request W03 с stream=true и Accept=text/event-stream; ниже показана полная минимальная последовательность fake upstream fixture.

```text
event: response.created
data: {"type":"response.created","response":{"id":"resp_example_stream","status":"in_progress","output":[]}}

event: response.completed
data: {"type":"response.completed","response":{"id":"resp_example_stream","object":"response","status":"completed","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"OK"}]}],"usage":{"input_tokens":1,"output_tokens":1,"total_tokens":2}}}

```

Оборвать после created — синтетический incomplete transport case. Добавление комментария keepalive или `data: [DONE]` само по себе не делает поток Completed (A1). Function arguments delta / indexed output и reasoning delta нужно дополнительно покрыть на03.02; эта минимальная fixture их не подтверждает.

## W06. Compact — синтетический по A1/S10/S12

Происхождение: CompactRequestWriter + whitelist normalizeOpenAICompactRequestBody. Для краткости input содержит только одно сообщение; в рабочем сценарии передаётся выбранный compact prefix полного рабочего контекста.

```http
POST /v1/responses/compact HTTP/1.1
Host: gateway.example.invalid
Authorization: Bearer SUB2API_KEY_PLACEHOLDER
Content-Type: application/json
Accept: application/json
```

```json
{"model":"example-model","instructions":"Preserve the facts needed for the next turn.","store":false,"reasoning":{"effort":"low"},"input":[{"type":"message","role":"user","content":[{"type":"input_text","text":"Example fact: total is 42."}]}]}
```

```json
{"id":"cmp_example_1","object":"response.compaction","output":[{"type":"compaction_summary","id":"cmp_example_item","encrypted_content":"OPAQUE_COMPACT_PLACEHOLDER"}]}
```

Response — проектный вариант для проверки CompactJsonReader, не установленный live discriminator. Нужно проверить также фактические compaction item types целевого upstream. Следующий generation input использует принятый canonical output compact + непокрытый хвост + новое сообщение; compact id не становится previous_response_id.

## W07. Ошибки — синтетические по S2/S7/S11

Происхождение: реальные writers формируют следующие разные envelopes. Сообщения заменены безопасным текстом; статус указан вне JSON.

HTTP401 middleware:

```json
{"code":"INVALID_API_KEY","message":"Invalid API key"}
```

HTTP503 handler, compact account отсутствует:

```json
{"error":{"type":"compact_not_supported","message":"No available accounts support /responses/compact"}}
```

HTTP400, HTTP continuation при OAuth-only pool:

```json
{"error":{"type":"invalid_request_error","message":"previous_response_id requires an OpenAI API-key account for HTTP requests"}}
```

HTTP200 после начала SSE — синтетический failure shape по writeResponsesFailedSSE, динамические поля заменены:

```text
event: response.failed
data: {"type":"response.failed","sequence_number":0,"response":{"id":"resp_example_failed","object":"response","created_at":0,"model":"example-model","status":"failed","output":[],"error":{"code":"example_unknown_code","message":"Synthetic failure"}}}

```

Unknown code не разрешает раскрывать server message или объявить Unsupported. Truncated/malformed error JSON остаётся безопасным отказом без semantic полей. Отмена caller — событие локального transport, отдельного wire response для неё не выдумывается.
