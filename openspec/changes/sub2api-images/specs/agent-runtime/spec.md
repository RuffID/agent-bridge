## MODIFIED Requirements

### Requirement: Optional usage sub2api

Sub2Api MUST предоставлять явное optional AddAgentBridgeSub2ApiAuxiliary для существующего IModelAuxiliaryGateway. Регистрация MUST NOT подключать CodexLb, создавать HTTP-клиент, выполнять I/O или требовать новые настройки у потребителя без auxiliary.

#### Scenario: Чтение usage выбранного доступа

- **WHEN** приложение вызывает ReadUsageAsync с выбранным ModelAccess
- **THEN** gateway MUST выполнить единственный GET prefix/v1/usage через HttpClientLibrary с per-request Bearer и конечным GenerationTimeout, охватывающим send и чтение body
- **AND** root/prefix и один конечный /v1 MUST нормализоваться без потери prefix; полный endpoint и повторный /v1 MUST отклоняться до HTTP
- **AND** caller cancellation MUST сохранять токен; retry, смена ключа и shared fallback MUST NOT выполняться.

#### Scenario: Сохранение серверной семантики

- **WHEN** сервер возвращает непосредственный JSON-объект с mode quota_limited или unrestricted
- **THEN** gateway MUST вернуть независимый ModelAuxiliaryResult без изменения полей и значений
- **AND** отсутствие quota, rate_limits, subscription, balance, remaining, usage, daily_usage или model_stats MUST NOT заменяться нулями либо безлимитом
- **AND** сведения sub2api MUST NOT трактоваться как upstream ChatGPT quota или доступный context window.

#### Scenario: Отказы и неподдержанные операции

- **WHEN** получены malformed JSON, error envelope, неизвестный mode или HTTP-отказ
- **THEN** gateway MUST вернуть безопасный typed failure без raw body, URL или секретов и освободить transport resources
- **AND** cleanup failures MUST оставаться наблюдаемыми; borrowed HttpClient MUST NOT освобождаться адаптером

#### Scenario: Неподдержанные auxiliary-операции

- **WHEN** вызывается UploadFileAsync
- **THEN** gateway MUST вернуть Unsupported до HTTP factory и HTTP.

## ADDED Requirements

### Requirement: Завершённые изображения sub2api

Optional sub2api auxiliary MUST реализовать GenerateImageAsync через JSON POST prefix/v1/images/generations и EditImageAsync через multipart POST prefix/v1/images/edits в HttpClientLibrary. Gateway MUST использовать выбранный per-call ModelAccess, per-request Bearer и GenerationTimeout на send/body, сохраняя usage, selector isolation, CodexLb API, текстовые JSON/SSE и provenance guard AgentRunner.

#### Scenario: Сохранение запроса

- **WHEN** приложение передало валидные параметры изображения
- **THEN** model, prompt, count как n, size, quality, background и output_format MUST передаваться без скрытой замены или удаления, со stream=false
- **AND** edits MUST скопировать коллекцию и bytes до первого await и отправить fresh multipart с image[] без клиентского OAuth/upload
- **AND** невалидный запрос, доступ или endpoint MUST отклоняться до HTTP factory
- **AND** retry, смена ключа/модели, fallback, маски, async tasks, batches и image streaming MUST NOT добавляться.

#### Scenario: Проверка результата

- **WHEN** получен завершённый JSON, в том числе с ведущими whitespace heartbeat
- **THEN** успех MUST требовать объект без non-null error envelope, непустой data и непустой b64_json либо url в каждом элементе
- **AND** malformed JSON, повреждённый UTF-8, неоднозначные duplicate fields, пустой data или отсутствие image payload MUST возвращать Rejected
- **AND** HTTP 200 с error MUST NOT считаться успехом; HTTP 401/403 MUST возвращать Unauthorized/Forbidden, прочие отказы MUST возвращать Rejected без server text
- **AND** результат MUST сохраняться самостоятельным JSON-снимком без автоматического скачивания URL.

#### Scenario: Отмена, очистка и секреты

- **WHEN** send или body завершаются отказом, caller cancellation, timeout либо cleanup failure
- **THEN** caller cancellation MUST сохранять исходный токен и иметь приоритет над deadline; timeout MUST возвращать Timeout
- **AND** transport resources MUST освобождаться, borrowed HttpClient MUST оставаться у приложения, primary/cleanup failures MUST оставаться наблюдаемыми
- **AND** logs и безопасные ошибки MUST NOT содержать prompt, image bytes, URL, credentials, raw body или exceptions
- **AND** отказ сервера по платформе, группе, модели или capability MUST NOT запускать повтор платной операции или удаление параметров.
