# Auxiliary sub2api

- GET /v1/usage через HttpClientLibrary возвращает самостоятельный JSON-снимок sub2api, не quota ChatGPT и не budget контекста. Не дополнять optional-поля и не использовать wire-формат CodexLb.
- Per-call ModelAccess, per-request Bearer и существующий GenerationTimeout; caller cancellation имеет приоритет. Нет retry, смены ключа и upstream OAuth.
- HTTP factory отложена до локальных проверок. UploadFileAsync остаётся Unsupported до factory. Borrowed HttpClient принадлежит приложению; cleanup failures не скрываются.
- Подключение только явным Integration.AddAgentBridgeSub2ApiAuxiliary после выбора Sub2Api. Safe status errors не содержат body/URL/секретов; данные результата не логировать.
- Images используют JSON generation и multipart edits с image[], stream=false и исходными model/prompt/n/size/quality/background/output_format. RequestFactory фиксирует коллекцию/bytes до await; ImageTransport выполняет один dispatch с GenerationTimeout; ResultReader требует непустой data с payload каждого элемента и отклоняет error даже при HTTP 200 после whitespace heartbeat.
- Не переносить server OAuth/upload/account selection, не скачивать result URL, не угадывать capabilities и не удалять параметры ради fallback. AgentRunner input/provenance не относится к image edits. Async tasks/batches/masks/streaming здесь не поддерживаются.
- Image outcome ILogger event 5201 содержит только фиксированные operation/outcome и ServiceErrorType. Completed пишется после проверки JSON и cleanup; exception не передаётся logger. Ошибка logger не меняет результат и не запускает повтор.
