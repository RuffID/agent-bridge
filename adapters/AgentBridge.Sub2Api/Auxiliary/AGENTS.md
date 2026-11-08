# Auxiliary sub2api

- GET /v1/usage через HttpClientLibrary возвращает самостоятельный JSON-снимок sub2api, не quota ChatGPT и не budget контекста. Не дополнять optional-поля и не использовать wire-формат CodexLb.
- Per-call ModelAccess, per-request Bearer и существующий GenerationTimeout; caller cancellation имеет приоритет. Нет retry, смены ключа и upstream OAuth.
- HTTP factory отложена до локальных проверок. Upload/images всегда Unsupported до factory. Borrowed HttpClient принадлежит приложению; cleanup failures не скрываются.
- Подключение только явным Integration.AddAgentBridgeSub2ApiAuxiliary после выбора Sub2Api. Safe status errors не содержат body/URL/секретов; данные результата не логировать.
