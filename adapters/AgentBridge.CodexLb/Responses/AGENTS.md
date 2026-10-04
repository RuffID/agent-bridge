# JSON Responses

## Граница этапа 14

- CodexLbModelGateway реализует только GenerateAsync JSON через actual HttpApiClient. SSE callback и CompactAsync возвращают Unsupported до HTTP; их не объявлять реализованными.
- Writer передаёт canonical input и function definitions полностью, exact model/effort, stream=false/store=false. Поддержанные контроли: tool_choice, parallel_tool_calls, include, service_tier, truncation, prompt_cache_key, text, дополнительные reasoning поля без override effort. Незнакомый top-level control — Unsupported, неверная форма/дубликат — Validation. Default include reasoning.encrypted_content добавляется только при отсутствии explicit include.
- Output остаётся ordered canonical snapshots; envelope/continuation отдельны от input. Completed требует явный status, output array и отсутствие error. Unknown lifecycle/missing output — Incomplete, explicit error/failed — Failed с полным envelope. Server cancelled без подтверждённой caller cancellation не объявлять Canceled.
- Continuation хранит adapter version и SHA-256 binding dialog/owner/agent/endpoint/key без ключа; turnId не входит, чтобы продолжать следующий turn. Это защита от случайного смешивания trusted application data, не криптографическая авторизация и не доказательство upstream owner. codex-lb отвечает за account routing. Неизвестные metadata сохраняются, но outgoing только previous_response_id и x-codex-turn-state. Новый envelope без безопасного id удаляет прежний anchor; id сохраняется без изменений в envelope. Не использовать fallback на старый anchor.
- HttpClientLibrary owns request/response, including body stream. Gateway owns deadline/linked CTS; приложение owns HttpClient/handlers. Никаких manual SendAsync, retry либо key/model/account fallback. Unexpected I/O/OCE без подтверждённого источника распространяется.
- До полного отчёта caller cancellation распространяется OCE с исходным токеном; после полного отчёта — Canceled с сохранёнными output/envelope/continuation. Explicit typed HTTP/model/JSON failure не заменяется поздней отменой. Caller имеет приоритет над deadline.
- ResponseErrorReader публикует status и закрытый allowlist type/code/param из Complete valid error JSON. Unknown fields — null; raw message/headers/body/reason/exception не логировать и не публиковать. Envelope/continuation сами чувствительны и не являются безопасным UI DTO.

## Проверка

Public AddCodexLbResponses → IModelGateway → actual HttpClientLibrary → fake HttpMessageHandler/local streams в ResponsesJsonTests. Cancellation после полного JSON моделировать cancellation на disposal локального stream; tasks отменять/await. Сеть, host, app, DB и произвольные scripts не запускать. Commands/results — в плане этапа 14.
