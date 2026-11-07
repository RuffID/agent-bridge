# Отклонённое предложение серверного контракта

Дата: 2026-11-07. Статус: **отклонено пользователем; не выполнять**. [Текущий путь адаптации](contract-blockers.md) · [Результаты03](results.md) · [Опись исходников](contract-source-evidence.json).

Пользователь исключил любые доработки sub2api: AgentBridge должен адаптироваться к существующему серверу. Этот документ сохранён только как запись отвергнутого варианта. Его поля, single-attempt profile, server regression и запрос согласования не являются текущими требованиями или зависимостями плана. Запрет клиентских retries не означает запрет всех внутренних server retries. Приведённые ниже предложения не следует реализовывать или повторно выносить на согласование как необходимый следующий шаг. Фактические ограничения текущего сервера исследуются отдельно от желаемого контракта.

Изменения предлагаются владельцу sub2api. Этот документ не разрешает менять sub2api, не задаёт действующие поля его API и не включает транспорт AgentBridge. Существующие access/catalog/settings не реализуются повторно. T01/T05 и этап03 остаются открытыми; этап04 не начат.

## Почему существующих настроек недостаточно

Повторная проверка локального0.2.19 подтвердила28 прежних sources без изменений и выявила дополнительные владеющие границы:

| Конкретный пробел | Основание в sub2api | Следствие для предложения |
| --- | --- | --- |
| Каталог уже пересекает часть account metadata, но это не привязанный контракт generation pool | `loadCodexGroupCatalogAccounts` использует persistently enabled accounts, но при ошибке availability query возвращает schedulable subset; `groupCodexModelMetadata`/`intersectUpstreamModelMetadata` пересекают effort/modalities/context, затем descriptor может сохранить локальные defaults | Переиспользовать существующие источники, добавив доказуемую полноту, revision и отдельную fail-closed проекцию. Наличие пересечения не считать достаточным |
| Модель зависит от ветки | `resolveOpenAIAccountUpstreamModelForRequest`: ordinary применяет account mapping/normalization; passthrough generation сохраняет channel-mapped ID, игнорируя обычный account mapping; protocol bridge выбирается ещё до passthrough | Считать capabilities по фактическому пути, включая channel mapping и upstream protocol, а не по одному `GetMappedModel` |
| Compact меняет модель до первого HTTP, а не только после ошибки | `Forward` и `forwardOpenAIPassthrough` вызывают `resolveOpenAICompactFallbackModel` до send. Account compact mapping приоритетен; иначе global compact model | Enforcement обязан проверять первоначальное разрешение модели и последующий fallback |
| Encrypted reasoning может исчезнуть до нового send | `deriveOpenAIForwardAttemptBody` удаляет целые encrypted items при passthrough→ordinary failover; ordinary `Forward` удаляет известные invalid-lineage items ещё до отправки | Запретить оба пути в обязательном профиле, а не только retry после400 |
| Настройки retries не образуют strict profile | Handler принимает `MaxAccountSwitches` только при `>0`, иначе default3. `sameAccountRetryAllowed` разрешает deadline-based retry независимо от обычного pool count; OAuth429 задаёт такой deadline | Нужен общий request-scoped лимит upstream attempts, проверяемый каждым send, независимо от recovery/failover настроек |
| Наблюдение upstream model уже существует, но не является публичным Responses-контрактом | `upstream_response_model.go`, `OpenAIForwardResult.UpstreamModel/UpstreamResponseModel/UpstreamResponseModelConflict`; usage mapper публикует upstream детали только admin. Observer не запрещает forwarding и нормализует/truncates строки | Развить existing observation в достоверную публичную metadata без admin API; exact validation и отказ отделить от billing/audit heuristics |

Полные пути, функции, ограничения публичных источников и hashes находятся в [contract-blockers.md](contract-blockers.md) и описи. Это уточнения ранее найденных препятствий, а не новые согласованные требования к обычному sub2api.

## Предлагаемые точки изменения в sub2api

Все пути относительно read-only `sub2api/`. Новые имена типов/функций ниже — предложение; существующие функции указаны точно.

| Владеющий файл / существующая функция | Адресная доработка для отдельной задачи |
| --- | --- |
| `backend/internal/server/routes/gateway.go`: `RegisterGatewayRoutes`, `modelsHandler` | Распознать обязательный version/profile на всех Responses/compact aliases и versioned model list. Невалидный opt-in отклонить; не отправлять в legacy handler. Не смешивать с `client_version` |
| `backend/internal/handler/gateway_handler.go`: `Models`, `writeOpenAIModelsList`; `openai_codex_models_service.go`: `loadCodexGroupCatalogAccounts`, `BuildGroupConfiguredCodexModelsManifest` | Добавить отдельную versioned проекцию к existing authenticated models route, сохранив ordinary ID list и Codex manifest без opt-in. Ошибка полного pool snapshot не разрешает сужение до временно доступного subset |
| `backend/internal/service/openai_codex_model_metadata.go`: `groupCodexModelMetadata`, `intersectUpstreamModelMetadata`, `applyUpstreamModelMetadataToCodexDescriptor`; `upstream_models.go`: `UpstreamModelMetadata`, `mergeUpstreamModelMetadata` | Сохранить provenance каждого поля при слиянии, неизвестность и expiry. Не брать guessed descriptor/windows или aggregate Source как доказательство каждого capability |
| `backend/internal/service/openai_account_scheduler.go`: `SelectAccountWithSchedulerForCapability`, `selectAccountWithSchedulerOnce`; `openai_gateway_scheduling.go`: `resolveOpenAIAccountUpstreamModelForRequest`, `resolveOpenAIForwardMappedModels` | Использовать тот же immutable pool/profile snapshot при discovery, selection, sticky lookup и финальном recheck. Проверять actual generation/compact mapping и native Responses protocol; исключить bridge/fallback за пределы профиля |
| `backend/internal/handler/openai_gateway_handler.go`: `Responses`, `NewOpenAIGatewayHandler`; `failover_loop.go`: `sameAccountRetryAllowed`, `effectiveSameAccountRetryLimit`; `openai_gateway_reasoning_failover.go`: `deriveOpenAIForwardAttemptBody` | До mapping, moderation с преобразованием body, compact keepalive и upstream операций зафиксировать профиль/revision. Не входить в same-account/account-switch/cross-mode sanitizer при strict profile. Использовать общий attempt guard, а не интерпретацию нулей старых config |
| `backend/internal/service/openai_gateway_forward.go`: `Forward`, `buildUpstreamRequest`; `openai_gateway_passthrough.go`: `forwardOpenAIPassthrough`, `buildUpstreamRequestOpenAIPassthrough` | Пронести принятый профиль до последней границы send; проверить фактический body/model после всех преобразований. Не разрешать early return через bridge или иной forwarding без enforcement |
| `backend/internal/service/openai_codex_transform.go`: `applyCodexOAuthTransformWithOptions`, `filterCodexInputWithOptions`; `openai_gateway_request_body.go`: `normalizeOpenAIResponsesReasoningContentReplay`, `normalizeOpenAIAPIKeyStoreFalseReasoningReplay`, `normalizeOpenAIPassthroughOAuthBody`, `normalizeOpenAICompactRequestBody`, `SanitizeOpenAICrossModeFailoverReasoning` | В strict path сохранить canonical input/tools/effort/instructions/order/opaque. Если ветке для upstream требуется изменение этих данных — явный отказ, без удаления/исправления. Не ломать legacy behavior без opt-in |
| `backend/internal/service/openai_responses_rejected_field_retry.go`: `normalizeOpenAIResponsesRejectedFieldRetryBody`; `openai_gateway_upstream_errors.go`: `newOpenAIAccountFailoverErrorWithClassificationHeaders` | Не запускать rejected-field repair, invalid-encrypted recovery, agent-task recovery либо OAuth429 replay в strict path. Вернуть безопасную исходную категорию отказа |
| `backend/internal/service/openai_compact_fallback.go`: `resolveOpenAICompactFallbackModel`, `prepareOpenAICompactFallbackRetry`, `applyOpenAIPassthroughCompactFallbackFromSignal` | Strict compact использует подтверждённый exact target; global/account fallback и initial override исключены, если отличаются. Unsupported до upstream при отсутствии поддержки |
| `backend/internal/service/upstream_response_model.go`: `beginUpstreamResponseModelObservation`, `ObserveOpenAI`; `openai_gateway_service.go`: `OpenAIForwardResult` | Сохранить отдельно requested, фактически отправленный и raw reported IDs; валидировать exact strings без truncation/case-fold guessing. Не подставлять requested/sent вместо неизвестного reported |
| `backend/internal/service/openai_gateway_response_handling.go`: `handleStreamingResponseWithReasoning`, `handleNonStreamingResponse`, `handleSSEToJSON`; `openai_gateway_passthrough.go`: `handleStreamingResponsePassthrough`, `handleNonStreamingResponsePassthrough`, `handlePassthroughSSEToJSON` | На обеих ветках собрать доверенную metadata до client model rewrite, проверить её перед выдачей terminal success, сохранить partial и явно сообщить failure |
| `backend/internal/service/openai_compact_stream_bridge.go`: `writeOpenAICompactSSEBridge`; `openai_compact_sse_keepalive.go`: `StartOpenAICompactSSEKeepalive` | Выдавать согласованный compact media/envelope/discriminator; при запрете/несовпадении не успевать подтвердить HTTP200 или terminal success через keepalive |

Общий профиль целесообразно разместить отдельным internal service-компонентом рядом с existing OpenAI routing. Это не новый публичный transport framework и не дублирование каталога. Конкретное хранение server profile/revision согласовывается владельцем сервера; схема БД заранее не предписана.

## Предлагаемый versioned wire contract

Рабочие имена для согласования:

```http
Sub2api-Contract: agent-bridge.responses.v1
Sub2api-Processing-Profile: canonical-single-attempt.v1
Sub2api-Pool-Revision: <opaque revision из принятого каталога>
```

`Contract` и `Processing-Profile` обязательны вместе при opt-in на GET `/v1/models`, POST `/v1/responses` и POST `/v1/responses/compact`, а также на существующих эквивалентных aliases. `Pool-Revision` обязателен на POST; первый GET получает текущую ревизию без него. Это gateway-only заголовки: сервер удаляет их перед upstream и не доверяет одноимённым upstream response headers/fields. Duplicate/conflicting headers, неизвестные значения и смешение с `client_version` дают явный отказ. Поддержка не определяется тем, что старый сервер проигнорировал headers и ответил200.

Request body остаётся canonical Responses: `model`, полное рабочее `input`, `instructions`, `tools`, `reasoning`, controls. Vendor request body fields не нужны: compact whitelist иначе мог бы молча их вырезать. Профиль не принимает `previous_response_id`, transport anchors и legacy turn-state. App API и существующая композиция AgentBridge сохраняются; новый opt-in в AgentBridge появится только после согласования и доказательства серверного контракта.

Versioned GET сохраняет `object=list/data`, добавляя root `sub2api` и per-model `sub2api.capabilities`. Пример формы ниже **синтетический**; числа, IDs, revision и источник не являются обнаруженными возможностями0.2.19:

```json
{
  "object": "list",
  "sub2api": {
    "contract": "agent-bridge.responses.v1",
    "processing_profile": "canonical-single-attempt.v1",
    "pool_revision": "example-revision",
    "valid_until": "2026-11-07T12:00:00Z"
  },
  "data": [{
    "id": "example-model", "object": "model",
    "sub2api": {
      "capabilities": {
        "generation": {
          "supported": true,
          "input_context_window": 120000,
          "max_output_tokens": null,
          "output_limit_control": false,
          "reasoning_efforts": ["medium"],
          "default_reasoning_effort": "medium",
          "input_modalities": ["text"],
          "function_tools": true,
          "encrypted_reasoning_replay": true,
          "supports_parallel_tool_calls": false,
          "supports_reasoning_summaries": false,
          "supports_verbosity": false,
          "wire_modes": ["json", "sse"],
          "opaque_context_domain": "example-domain"
        },
        "compact": {"supported": false},
        "provenance": {
          "coverage": "all_eligible_accounts",
          "fields": {
            "generation.input_context_window": {
              "source": "verified_server_contract",
              "evidence_id": "example-budget-contract-v1",
              "verified_at": "2026-11-07T10:00:00Z",
              "valid_until": "2026-11-07T12:00:00Z"
            }
          }
        }
      }
    }
  }]
}
```

Для всех подтверждённых полей нужны такие field-level provenance records; пример сокращает `fields` до одного. `verified_server_contract` означает реальную версионированную гарантию с доступным evidence, а не произвольное значение администратора. Сервер не публикует ключи, account IDs, account names или внутренние URLs. Точный supported/default effort не выводится из имени модели. Нет положительного доказательства — `null`, пустой список или false; нет бюджета/полного coverage — generation не объявляется пригодной для AgentBridge. Проекция в существующие ModelCapabilities не должна делать `HasMetadata=true` только из наличия vendor object.

### Семантика бюджета и pool

1. `input_context_window` — гарантированный доступный объём **полного клиентского рабочего input в upstream tokens**, применимый для любого аккаунта, который эта ревизия вправе выбрать для данной операции. Учитываются instructions, tools/schema, сообщения, calls/outputs, reasoning/opaque/compact items и envelope overhead. Скрытые server additions уже вычтены из доступного бюджета. Это не JSON bytes, context window или percent от него.
2. Источник обязан доказать резерв вывода и внутренние ограничения upstream. Если бюджет зависит от effort/controls, единое значение — безопасная нижняя граница по всем опубликованным combinations либо схема v1 не объявляет их поддержанными. OAuth без `max_output_tokens` control может публиковать бюджет только при отдельной доказанной гарантии с учётом максимального серверного вывода. Неизвестный output reserve или стоимость opaque не заменяются локальной арифметикой. `max_output_tokens=null` допустим только с независимым доказательством input budget; `output_limit_control=false` не обещает enforcement клиентского параметра.
3. Для pool берётся минимум доказанных budget/output bounds и пересечение поддержанных effort/modalities/capabilities. Наличие одного неизвестного кандидата не исключает его молча из расчёта: поле становится неподтверждённым либо сам кандидат исключается **и из runtime selection** обязательным профилем. Compact имеет независимые capabilities/budget/provenance, а не наследует generation автоматически.
4. Полный eligible set включает sticky, routed, advanced и legacy scheduler paths и аккаунты, временно находящиеся в cooldown, но способные вернуться. Нельзя рекламировать расширенные возможности по временно здоровому subset. Не удалось прочитать полный set — controlled unavailable, без fallback к schedulable snapshot. Нельзя незаметно включать другие platform/protocol bridge/группы.
5. `pool_revision` — immutable fingerprint серверного snapshot: scope ключа/группы и прав, exact model resolution, eligible members и их capability evidence, processing profile, forwarding/protocol, compact routing и opaque compatibility domain. Это не raw hash ключа и не секрет. Изменения этих входов инвалидируют revision; TTL не заменяет recheck. `valid_until` не позднее срока weakest evidence. Expired/stale/чужая revision отклоняется перед upstream, без автоматического refresh/replay POST.
6. Между discovery и send сервер либо удерживает принятый snapshot до завершения операции, либо атомарно повторно проверяет его и отказывает. Изменение конфигурации во время запроса не переключает политику. Временно недоступный pool даёт отказ, а не уход на аккаунт с другой capability set.
7. `opaque_context_domain` обозначает доказанную взаимную совместимость аккаунтов/модели для encrypted replay/compact. Совпадение model ID её не доказывает. Без межаккаунтного доказательства допустим только серверно ограниченный single-account domain; при его недоступности — отказ. Добавление/замена аккаунта требует новой revision/domain. Сохранение этой provenance в AgentBridge и поведение после restart относятся к04; этот документ их не реализует и не снимает guard.

### Enforcement processing profile

Предлагаемый минимальный профиль `canonical-single-attempt.v1` выбирает один аккаунт и допускает **не более одного upstream model request** на входящий POST. Ограничение проверяется общей границей send, включая внутренние recovery loops. Selection без отправки может пропускать непригодных кандидатов только внутри принятого pool; после первой upstream отправки same-account retry, OAuth429 deadline retry, account failover, transport-error retry, rejected-field recovery, agent-task recovery, invalid-encrypted recovery и compact fallback запрещены. Refresh credentials не разрешает повторить model request. Криптографический отказ возвращается явно.

В первой версии exact requested model совпадает с sent model; все channel/account/global/compact mappings, меняющие ID, исключаются до send. Это намеренно более узкий предлагаемый профиль: explicit aliases с доказанным revision-bound mapping могут стать отдельным согласованным профилем, но не скрытым исключением v1. Выбирается native OpenAI Responses; bridge через Chat Completions/Anthropic и WS не разрешён.

Canonical items, unknown canonical fields, encrypted_content, порядок, точные пары call_id и данные tools, effort, instructions не меняются. Запрещены lineage strip, удаление reasoning.content, ID/name/schema repair, effort normalization, hosted tool injection/execution и незаявленное compact supplementation. Если upstream не принимает исходный canonical формат, профиль отвечает Unsupported, а не делает «полезную» нормализацию. Обычные legacy ветки вне opt-in продолжают свою политику.

Разрешён только закрытый перечень транспортных изменений: `store=false`, generation upstream `stream=true` для OAuth, удаление gateway-only headers, компактный upstream envelope без `store/stream` при явно подтверждённом compact body, lossless SSE→JSON для клиентского non-stream. Каждое реально применённое изменение отражается в response metadata; canonical input/output и их порядок остаются прежними. Неподдержанный control, например обязательный output cap, отклоняется до upstream, а не silently drops. Неизвестное преобразование — отказ профиля.

Проверка профиля должна стоять до общих normalizers `Forward` и повторно перед send: один guard лишь внутри `forwardOpenAIPassthrough` не защищает shared prelude и handler failover. Final body сравнивается по canonical структуре с принятой до преобразований версией, без raw payload logging. Безопасные диагностические события содержат operation ID, contract/profile/revision и код результата, без содержимого, ключей и account identity.

### Достоверная metadata ответа

На root JSON Responses/compact предлагается server-owned поле `sub2api.processing`. В SSE оно находится на root каждого `response.*` event; обязательные окончательные сведения — в terminal event. Snapshot/profile/revision/request ID фиксированы на весь поток. В keepalive/comments metadata не считается terminal подтверждением.

```json
{
  "sub2api": {
    "processing": {
      "contract": "agent-bridge.responses.v1",
      "processing_profile": "canonical-single-attempt.v1",
      "pool_revision": "example-revision",
      "operation": "generation",
      "request_id": "example-operation",
      "requested_model": "example-model",
      "sent_model": "example-model",
      "reported_model": "example-model",
      "reported_model_source": "upstream_terminal",
      "model_conflict": false,
      "upstream_attempts": 1,
      "opaque_context_domain": "example-domain",
      "transformations": ["oauth_upstream_stream", "sse_to_json"],
      "profile_satisfied": true
    }
  }
}
```

Это пример vendor fragment, не полный Responses envelope. Requested берётся до channel mapping; sent — из финального upstream request; reported — из raw upstream terminal/JSON **до** восстановления клиентского `model`. `reported_model_source` имеет закрытые значения `upstream_terminal`, `upstream_json`, `unavailable`; до terminal допустим null/unavailable. Нельзя заполнять reported из requested/sent, имени аккаунта, billing model, reconstructed response либо server error shell. Существующий observer — источник наработок, но его trimming/truncation/case-insensitive conflict не доказывают exact identity. Некорректный/отсутствующий/конфликтующий reported ID не даёт `profile_satisfied=true` и успешный terminal strict v1. Если provider возвращает отличный snapshot ID, требуется отдельное доказанное правило identity, а не сравнение по похожему имени.

Не следует называть это доказательством физически исполнявшейся модели внутри провайдера: сервер способен подтвердить отправленное имя и raw декларацию upstream. `profile_satisfied` выводится из enforcement и observations, а не копируется из client/upstream JSON. Server-owned vendor поля очищаются от upstream spoofing; duplicate/vendor collisions отклоняются или удаляются до формирования доверенного envelope по единому правилу. Attempts считаются на реальном send, transformations — в точках применения; пустой список не вычисляется из успешного status.

Обе ветки одинаково выдают JSON для клиентского `stream=false` даже при upstream SSE/missing Content-Type. При terminal с пустым output допустима лишь доказанно lossless сборка всех canonical items по полным events; отсутствие encrypted item нельзя восполнить текстом. EOF/`[DONE]`/HTTP200 без completed не становится успехом. При partial/failed/incomplete/cancelled сохраняются известные items и соответствующий исход. Для stream metadata mismatch даёт `response.failed` с partial, а не completed; non-stream возвращает описанный ниже error envelope с partial output, если он известен. Эта форма требует отдельного parser regression до реализации клиента.

Надёжность metadata не может быть реализована только HTTP headers: reported model/transformations определяются после начала SSE. Финальные поля обязательны в terminal body/event; version/profile headers служат только ранним подтверждением выбранной схемы.

### Compact v1

`compact.supported=true` требует отдельно опубликованных `path=/v1/responses/compact`, request fields, `response_object=response.compaction`, terminal/status semantics, media types, canonical output types и opaque domain. До подтверждения этих полей — false/Unsupported. Route/body whitelist0.2.19 не доказывает response discriminator.

Предлагаемая unary форма — JSON `object=response.compaction`, `status=completed`, полный canonical `output` и `sub2api.processing.operation=compact`. Если upstream не даёт такого status, сервер может подтвердить completed только по документированному terminal правилу, записанному в контракте и проверенному fixtures. До этого — отказ, не искусственное completed. Upstream SSE допустим лишь как доказанный внутренний transport; наружный compact v1 остаётся JSON. SSE keepalive/bridge не должен успевать commit200 до capability/revision проверок и не заменяет terminal.

Initial compact model override и fallback по400/404/context-window/failed shell/SSE failure запрещены strict v1. Generation не используется вместо compact. Profile metadata и sent/reported validation применяются так же, как к generation. Existing AgentBridge Completed-only/save-before-activation/history path сохраняется; после серверного расширения ему ещё нужны isolated adapter checks, а persisted provenance04 остаётся отдельной зависимостью.

### Безопасные отказы

Предлагается существующий OpenAI error envelope `error.type/code/message` с фиксированными безопасными кодами:

| Условие | HTTP до начала body | Code / результат |
| --- | --- | --- |
| Неизвестная schema/profile, неполный или конфликтный opt-in, неподдержанный control/anchor | 400 | `sub2api_contract_unsupported` / `sub2api_profile_unsupported` / `sub2api_control_unsupported` |
| Revision изменилась, просрочена либо не принадлежит текущему access scope | 409 | `sub2api_pool_revision_mismatch` |
| Нет полного capability evidence или нет доступного кандидата принятого pool | 503 | `sub2api_capabilities_unavailable` / `sub2api_profile_pool_unavailable` |
| Нет подтверждённого compact | 400 | `sub2api_compact_unsupported` |
| Upstream не подтвердил модель, нарушил canonical/profile или не прислал terminal | 502 | `sub2api_upstream_contract_violation`; known partial сохраняется |

401/403 доступа сохраняются. После начала SSE — terminal `response.failed` с тем же code и processing metadata/known partial, без fake success. Сервер не выдаёт Retry-After как разрешение AgentBridge автоматически повторить POST; клиент не меняет ключ/модель/коннектор и не повторяет business tool. Caller/deadline прекращают новые sends; разрешённый billing drain уже отправленного запроса не запускает новую попытку и не подтверждает результат клиенту.

Предлагаемая JSON error форма сохраняет стандартный `error` и добавляет root `partial_output` — массив известных canonical items в исходном порядке — и ту же `sub2api.processing` metadata. `partial_output=[]` означает отсутствие известных items, а не подтверждение отсутствия исполнения upstream. `upstream_attempts=0` допустим только при отказе до send; после отправки —1, даже если ответ потерян. Неизвестные reported model/domain остаются null. Пример отказа до отправки:

```json
{
  "error": {
    "type": "invalid_request_error",
    "code": "sub2api_pool_revision_mismatch",
    "message": "The selected processing contract is unavailable."
  },
  "partial_output": [],
  "sub2api": {
    "processing": {
      "contract": "agent-bridge.responses.v1",
      "processing_profile": "canonical-single-attempt.v1",
      "pool_revision": "example-revision",
      "operation": "generation",
      "request_id": "example-operation",
      "requested_model": "example-model",
      "sent_model": null,
      "reported_model": null,
      "reported_model_source": "unavailable",
      "model_conflict": false,
      "upstream_attempts": 0,
      "opaque_context_domain": null,
      "transformations": [],
      "profile_satisfied": false
    }
  }
}
```

В terminal SSE error тот же `error` помещается в `response.error`, partial items — в `response.output`, статус — failed; `sub2api.processing` остаётся на root event. Для malformed opt-in сервер может не иметь принятой версии/profile/revision: соответствующие поля null, нельзя echo недоверенные headers как принятый профиль. Это фиксированный vendor v1 контракт для согласования; нынешний shared error reader не объявляется уже поддерживающим новые partial поля.

## Необходимые regression tests отдельной серверной задачи

Tests должны вызывать реальные route/handler/forwarding paths с fake upstream/scheduler/catalog storage и счётчиком send. Проверки только helper-функций недостаточны. Go suites сейчас **не запускались**. Existing legacy tests сохраняются; добавляются opt-in варианты.

| Область / существующие тестовые файлы | Обязательные новые проверки |
| --- | --- |
| Routes/models: `openai_codex_models_handler_test.go`, `openai_codex_models_service_test.go`, `openai_codex_model_metadata_test.go` | Legacy ordinary/client_version без изменений; strict schema negotiation на canonical/aliases/trailing slash; unknown/duplicate headers; exact ID/effort/default, missing per-field provenance, expired source; неизвестный кандидат и ошибка availability query не превращаются в успешный subset |
| Scheduler: `openai_account_scheduler_test.go`, `openai_account_scheduler_compact_test.go` | Все selection branches/sticky/recheck ограничены одной revision; cooldown return, channel/account mapping, смена pool/profile между GET и POST и между selection/send дают отказ с send=0; соседний access scope не принимает чужую revision |
| Обе `Forward` ветки: `openai_gateway_service_test.go`, `openai_responses_rejected_field_retry_test.go`, `openai_gateway_reasoning_failover_test.go` | Таблица ordinary/passthrough × OAuth/API-key × JSON/SSE: invalid encrypted, known invalid lineage, rejected field, task recovery, transport error,429/5xx/pre-output failure. Upstream model send=1, без второго аккаунта/изменённого body; cross-mode stripping не вызывается; явно недопустимая canonical нормализация даёт send=0 |
| Canonical: `openai_codex_transform_test.go`, `openai_codex_function_call_id_test.go`, `openai_gateway_request_body_reasoning_test.go` | Принятое compact-окно + tail + current input, unknown fields, encrypted reasoning.content, несколько tool pairs/repeated IDs, tools/schema/instructions/effort/order. Сравнить upstream payload и возвратные canonical items; запретить repair/injection/drop, проверить однократность external tool invocation на последующей клиентской приёмке |
| Response metadata: `openai_response_model_rewrite_test.go`, `upstream_response_model_test.go`, `dto/mappers_usage_test.go` | Requested A/sent B/reported C никогда не схлопываются в A; missing/overlong/case-different/conflicting reported model, early vs terminal conflict, upstream vendor spoof и reconstruction не дают fake satisfied. Internal observation публикуется без admin credentials и без account/secret leakage. До terminal неизвестные поля остаются неизвестными |
| Non-stream: `openai_oauth_passthrough_non_stream_test.go` | Обе ветки × upstream SSE/JSON/missing Content-Type; full canonical terminal/empty-output lossless reconstruction, encrypted/tool items, incomplete/cancelled/failed, EOF/`[DONE]` без terminal. Наружный media type/metadata/partial совпадают с контрактом; non-stream не получает raw SSE как успешный JSON |
| Compact: `openai_compact_model_mapping_test.go`, `openai_compact_fallback_test.go`, `openai_compact_stream_bridge_test.go`, `openai_compact_sse_keepalive_test.go` | Обе ветки: initial global/account override блокируется **до** первого send;400/404/window error/failed shell/SSE failure не повторяются; Unsupported send=0; verified discriminator/Completed/output/metadata. Keepalive не commit200 до guards; missing terminal не заменяется status=completed |
| Lifecycle/cancellation | Caller/deadline до selection, перед send и после partial; ни одной дополнительной попытки после отмены. Finalizer/usage settlement/cleanup выполняются, primary+cleanup failures не скрываются, billing drain не меняет публичный исход. Fault-injection на late metadata failure и bounded buffering |

Все имена существующих файлов проверены в checkout; предложенные route/profile assertions могут потребовать отдельного нового тестового файла у соответствующего владельца. Server tests для этого контракта должны быть изолированы от реальных HTTP/OAuth/БД/Docker. Их green не заменяет отдельно разрешённую целевую OAuth-приёмку05.03.

## Порядок снятия блокировки и возврат в03

1. Отдельно согласовать это расширение либо предоставить **эквивалентный существующий versioned контракт**: точная версия/immutable source, schema, budget evidence по всем eligible accounts, enforceable profile, reported/sent model и compact semantics. README/release version/client_version, скрин настроек и один успешный ответ недостаточны.
2. В отдельной задаче владельца sub2api реализовать/проверить выбранный контракт. Передать versioned schema и fixtures JSON/SSE/non-stream/compact/errors, source/build provenance, доказанный pool snapshot и результаты regression обоих paths с actual upstream send counters. Потребитель не использует admin API и не получает upstream credentials.
3. Только затем вернуться в03.01: проекция подтверждённых capabilities в existing ModelCatalog/ModelSettingsReader через HttpClientLibrary с прежним validator. Если бюджет не доказан — остаётся null/Unsupported. Уточнить, как contract/profile/revision snapshot сохраняется на run в existing contracts; недостающий public/provenance контракт вынести отдельной зависимостью, не обходить custom settings.
4. В03.02/03.03 реализовать разрешённые JSON/SSE/compact части existing bundle/shared writers с полным рабочим input, awaited callbacks/partial/terminal/caller/deadline/cleanup и без client retry. Изолированная приемка адаптера обязательна, одной server fixture недостаточно.
5. Persisted provenance, legacy handling и restart потребуют отдельного этапа04. Если без этой защиты запуск03 небезопасен, transport guard остаётся; здесь допустим только внутренний проверенный transport path, не открытие production gateway через фиктивный settings path. К04 текущая работа не переходит. Live05 и новая DLL-поставка также не считаются выполненными.

На текущем checkout нельзя честно завершить03.01 целиком, включить03.02 или03.03. Подготовка этого адресного предложения завершает только исследовательскую работу03, не T01/T05 и не этап.
