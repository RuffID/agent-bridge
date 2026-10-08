# Usage, файлы и изображения

- Optional IModelAuxiliaryGateway подключается AddCodexLbAuxiliary после стандартного HTTP pipeline; сроки обязательны и раздельны.
- Только actual HttpClientLibrary, per-call ModelAccess, без host, retry, key/model fallback и общего Authorization.
- Usage и image JSON являются чувствительным пользовательским результатом; не логировать raw данные.
- Upload состоит из регистрации /backend-api/files, PUT signed HTTP(S) URL без Bearer и финализации uploaded. Не выдавать file_id до success; чужой ID финализации отклоняется.
- Signed URL, raw error message/body/headers и Exception.Message не выдаются. Safe allowlist ResponseErrorReader используется только для API отказов; upload transport error содержит лишь semantic status.
- До await копировать image/file bytes. ContentFactory создаёт fresh content; request/response cleanup принадлежит HttpClientLibrary. Cleanup failures не нормализуются.
- Изолированные тесты — fake HttpMessageHandler, без настоящих API, файлов и процессов.

