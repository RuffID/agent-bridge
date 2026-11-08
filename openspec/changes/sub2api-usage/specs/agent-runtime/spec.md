## ADDED Requirements

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

- **WHEN** вызываются UploadFileAsync, GenerateImageAsync или EditImageAsync
- **THEN** gateway MUST вернуть Unsupported до HTTP factory и HTTP.
