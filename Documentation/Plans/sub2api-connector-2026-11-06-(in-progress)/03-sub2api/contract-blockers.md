# Препятствия этапа03 и необходимый контракт

Дата проверки: 2026-11-07. [Результаты](results.md) · [Вопросы T01–T05](../open-questions.md). Это статическое evidence локального checkout, без live HTTP/OAuth и без Git revision.

## Проверенная версия и происхождение

Файл [VERSION](../../../../../sub2api/backend/cmd/server/VERSION) содержит `0.2.19`; SHA256 `efb53e7bfd5da3c290cea3bba09b00ec3011b2637638142a816fd27f9e029c51`. Это версия исходников, а не доказательство версии целевого deployment. Повторно проверены size/SHA256 всех28 sub2api entries [описи01](../01-contracts/source-evidence.json):28 совпадений,0 изменений. Поэтому [матрица01](../01-contracts/wire-matrix.md) и её синтетические fixtures сохраняют указанное происхождение. Исторические файлы01–02 не исправлялись.

В `backend` и `docs` sub2api не найдено `input_context_window`. Публичный route `/v1` и query `client_version` не задают versioned семантику входного бюджета. В [routes/gateway.go](../../../../../sub2api/backend/internal/server/routes/gateway.go), `modelsHandler`, непустой client_version только выбирает Codex manifest. В `FetchCodexModelsManifest` [models service](../../../../../sub2api/backend/internal/service/openai_codex_models_service.go) query передаётся upstream, а header Version нормализуется независимо. Подставить `0.0.0` из W02 как рабочую версию нельзя.

## T01: почему пригодный settings path заблокирован

Обычный каталог [Models/writeOpenAIModelsList](../../../../../sub2api/backend/internal/handler/gateway_handler.go) и [Model](../../../../../sub2api/backend/internal/pkg/openai/constants.go) подтверждают публичные ID. `configuredCodexModelDescriptor/newConfiguredCodexModelDescriptor` из models service добавляют локальные окна/effort. [Metadata projection](../../../../../sub2api/backend/internal/service/openai_codex_model_metadata.go) объединяет разные источники; происхождение каждого поля для generation account публично не доказано. Внутренний [UpstreamModelMetadata](../../../../../sub2api/backend/internal/service/upstream_models.go) содержит MaxOutputTokens, но это не публичный input budget и не гарантия OAuth output control.

AgentBridge реализует ordinary каталог без client_version. Даже присланные похожие metadata/input_context_window игнорируются как неподтверждённые. Safe snapshot: exact ID, HasMetadata=false, окна/лимиты=null, efforts/modalities пусты, флаги false. Пример: context_window272000 и percent95 не дают ни272000, ни258400 входного бюджета. Existing validator возвращает Unsupported, включая ServerValidation policy: ослабления checks нет.

Для снятия T01 нужен **новый согласованный публичный серверный контракт либо предоставленное доказательство существующего versioned контракта**. Минимальные сведения:

- версия схемы и идентификатор processing profile; exact публичный model ID;
- `input_context_window` с определением доступного входного бюджета и единицами, источник/актуальность; null при отсутствии доказательства;
- точные effort/default и capabilities с происхождением, применимые ко всем аккаунтам, которые профиль вправе выбрать, либо явное ограничение selection pool;
- output limit с отдельной семантикой применимости: для OAuth нельзя обещать enforcement max_output_tokens, который сервер удаляет;
- ошибки unsupported version/profile и правила изменения pool/mapping, исключающие использование устаревших capabilities с другим профилем.

Это описание расширения, а не существующие поля sub2api и не разрешение менять соседний репозиторий. Новый endpoint/настройка в AgentBridge не вводятся до согласования контракта. HttpClientLibrary0.0.0.6 позволяет реализованный путь; недостающий HTTP API библиотеки не обнаружен. Блокер принадлежит серверным данным, его нельзя устранить обходным HttpClient.

## T02/T05: обе forwarding ветки и non-stream

В [Forward](../../../../../sub2api/backend/internal/service/openai_gateway_forward.go) после invalid_encrypted_content `trimOpenAIEncryptedReasoningItems` меняет body и запускает повтор. Есть rejected-field recovery и server account failover. [OAuth transform](../../../../../sub2api/backend/internal/service/openai_codex_transform.go) принуждает upstream stream=true и удаляет max_output_tokens, нормализует tools/IDs/instructions/reasoning. [Passthrough body normalization](../../../../../sub2api/backend/internal/service/openai_gateway_request_body.go), `normalizeOpenAIPassthroughOAuthBody`, также принуждает stream=true для generation, store=false и применяет controls/reasoning normalization.

Non-stream приложения остаётся отдельным режимом: `handleNonStreamingResponse/handleSSEToJSON` в [обычной ветке](../../../../../sub2api/backend/internal/service/openai_gateway_response_handling.go) и `handleNonStreamingResponsePassthrough/handlePassthroughSSEToJSON` в [passthrough](../../../../../sub2api/backend/internal/service/openai_gateway_passthrough.go) собирают upstream SSE terminal в JSON, восстанавливают пустой output по deltas. Обе ветки учитывают отсутствующий upstream Content-Type у OAuth; при отсутствии подходящего terminal JSON могут вернуть SSE. Terminal failure/incomplete/cancelled не равен Completed. Обе ветки могут переписать mapped model обратно в original model. Поэтому response.model или HTTP200 не доказывают actual upstream model/неизменность opaque.

[Локальная regression fixture sub2api](../../../../../sub2api/backend/internal/service/openai_oauth_passthrough_non_stream_test.go) подтверждает наличие проверяемых форм в исходниках; Go-тест здесь не выполнялся, fixture не является живым ответом. Shared writers/framing и codex-lb tests AgentBridge не заменяют Sub2Api gateway acceptance.

Для снятия T05 нужен проверенный профиль **обеих используемых forwarding веток** на целевой поставке: version/profile ID, generation/compact mappings, выбор/failover аккаунтов, разрешённые transformations/retries и пригодность encrypted context при смене аккаунта. Нужен серверный способ потребовать этот профиль или отказать до обработки при несовпадении. Должны быть исключены неподтверждённые потери reasoning/скрытый model fallback либо явно согласован иной контракт; один client request не подтверждает серверную политику. Надёжные публичные response metadata должны отличать requested ID от фактической модели и сообщать применённый профиль/преобразования. Точных существующих таких гарантий не установлено; произвольный клиентский флаг opt-in их не создаст.

## T05: compact

В [Responses handler](../../../../../sub2api/backend/internal/handler/openai_gateway_handler.go) есть explicit `/v1/responses/compact`; [normalizeOpenAICompactRequestBody](../../../../../sub2api/backend/internal/service/openai_gateway_request_body.go) оставляет model/input/instructions/tools/parallel_tool_calls/reasoning/service_tier/text/previous_response_id и удаляет store/stream. Это описание серверного whitelist, а не разрешение AgentBridge отправлять continuation.

[resolveOpenAICompactFallbackModel/prepareOpenAICompactFallbackRetry](../../../../../sub2api/backend/internal/service/openai_compact_fallback.go) используют account compact-only mapping, затем Gateway.OpenAICompactModel; fallback может сработать при error либо failure shell. Обе forwarding ветки вызывают эту логику. [Stream bridge](../../../../../sub2api/backend/internal/service/openai_compact_stream_bridge.go) и [keepalive](../../../../../sub2api/backend/internal/service/openai_compact_sse_keepalive.go) допускают SSE; Accept JSON не доказывает unary body. Успешный ответ с восстановленным original model не доказывает сохранение фактической модели.

До подтверждения профиля/mapping и canonical discriminator/items CompactAsync остаётся Unsupported. Не отправляется обычная generation вместо compact. Existing ContextCompactor/IDialogContextWriter сохраняют прежние Completed-only/save-before-activation/history правила; новая Sub2Api интеграция с ними не выполнена.

## Граница этапа04

Без доказанного происхождения encrypted reasoning/compact одинаковый model ID недостаточен. Transport нельзя открыть через custom settings, обходя T01. Текущий gateway отклоняет все generation/compact до HTTP, сохраняя input/continuation. Persisted connector provenance, legacy handling, restart и durable no-replay остаются04; эти работы не начинались. После снятия T01/T05 ещё нужны настоящие isolated Sub2Api JSON/SSE/tool/compact проверки и отдельно live OAuth приёмка05.03.
