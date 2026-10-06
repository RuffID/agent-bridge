## ADDED Requirements

### Requirement: Каноническая JSON генерация Responses

JSON gateway MUST отправлять POST canonical base-prefix /v1/responses через HttpClientLibrary с stream=false/store=false и per-call Bearer ModelAccess. Model/instructions/exact effort, все canonical input items и function definitions MUST сохраняться; поддержанные параметры MUST иметь независимый снимок и MUST NOT переопределять mandatory fields/effort.

#### Scenario: Нет видимого текста

- **WHEN** completed JSON содержит только function_call, reasoning или compaction items
- **THEN** gateway возвращает Completed с полным ordered output и независимым envelope.

#### Scenario: Неполный результат

- **WHEN** JSON содержит incomplete, failed либо неподтверждённый status
- **THEN** известные output/envelope сохраняются в соответствующем lifecycle отчёте.

#### Scenario: Проверка правила — Каноническая JSON генерация Responses

- **WHEN** JSON GenerateAsync получает canonical items и exact effort
- **THEN** POST через HttpClientLibrary сохраняет per-call доступ и ordered input.

### Requirement: Проверка и снимок JSON controls

Unknown top-level controls MUST давать Unsupported до HTTP, malformed/duplicate controls — Validation. Default include reasoning.encrypted_content MUST добавляться только при отсутствии explicit include. JSON output MUST сохранять порядок/unknown/opaque поля отдельно от полного envelope/continuation.

#### Scenario: Проверка правила — Проверка и снимок JSON controls

- **WHEN** controls переопределяют mandatory field или имеют duplicate
- **THEN** запрос отклоняется до HTTP; default include не заменяет explicit include.

### Requirement: Canonical output и JSON completion

Completed MUST требовать явный status=completed, output array и отсутствие explicit error; failed/incomplete/unknown lifecycle MUST NOT превращаться в Completed по HTTP 2xx или видимому тексту.

#### Scenario: Проверка правила — Canonical output и JSON completion

- **WHEN** HTTP2xx содержит visible text, но нет completed output
- **THEN** Completed не заявлен; известные output и envelope сохраняются раздельно.

### Requirement: Безопасные ошибки и ограниченный вызов JSON

Gateway MUST нормализовать HTTP error по status и только известным безопасным type/code/param из Complete valid error envelope. Raw body/headers/reason/message/exception MUST NOT попадать в публичную ошибку/logger. Truncated/invalid/unsupported body MUST NOT трактоваться как complete envelope.

#### Scenario: Секрет в поле ошибки

- **WHEN** error message либо неизвестные type/code/param содержат чувствительные значения
- **THEN** публичная ошибка содержит фиксированное описание и только известные безопасные поля
- **AND** автоматический повтор отсутствует.

#### Scenario: Проверка правила — Безопасные ошибки и ограниченный вызов JSON

- **WHEN** error envelope усечён или содержит неизвестный секретный type
- **THEN** публичная ошибка не публикует raw данные и не считает prefix полным envelope.

### Requirement: Конечный JSON вызов и приоритет отмены

Вызов MUST иметь конечный GenerationTimeout на отправку/чтение. До полного отчёта caller cancellation MUST распространяться OCE с исходным token; после полного отчёта MUST возвращаться Canceled с сохранёнными output/envelope/continuation. Explicit typed HTTP/model/JSON failure MUST сохранять приоритет над поздней отменой. Caller MUST иметь приоритет над deadline.

#### Scenario: Проверка правила — Конечный JSON вызов и приоритет отмены

- **WHEN** caller отменён после полного отчёта без explicit failure
- **THEN** Canceled сохраняет output, envelope и continuation; deadline не подменяет caller.

### Requirement: Cleanup и граница compact

Неожиданный I/O MUST распространяться без retry. Request/response MUST освобождаться на успехе/отказе/JSON error/отмене. Streaming callback и compact MUST возвращать Unsupported до HTTP в JSON gateway.

#### Scenario: Проверка правила — Cleanup и граница compact

- **WHEN** исторический gateway получает неподдержанный compact запрос
- **THEN** request/response освобождаются; streaming callback и compact отклоняются до HTTP в историческом JSON gateway.

### Requirement: Продолжение только своего вызова

Continuation JSON adapter MUST связывать previous_response_id/x-codex-turn-state с dialog/owner/agent, endpoint и отпечатком выбранного ключа без сохранения ключа. Несовпадение либо unknown format MUST отклоняться до HTTP. Unknown metadata MUST сохраняться, но MUST NOT становиться input, произвольными headers или разрешением retry/смены account.

#### Scenario: Другой ключ или диалог

- **WHEN** continuation передано для другого ключа, endpoint или call context
- **THEN** gateway возвращает отказ до отправки без смены доступа.

#### Scenario: Новый ответ без anchor

- **WHEN** новый JSON не содержит пригодного id
- **THEN** новый continuation не отправляет previous_response_id старого ответа.

#### Scenario: Проверка правила — Продолжение только своего вызова

- **WHEN** continuation передано с другим owner, agent, endpoint или ключом
- **THEN** отказ происходит до HTTP; unknown metadata не превращается в headers или retry.

### Requirement: Новый anchor и upstream ownership

Новый envelope без пригодного id MUST удалять старый previous_response_id; исходный id MUST сохраняться в envelope. Upstream ownership MUST оставаться ответственностью codex-lb.

#### Scenario: Проверка правила — Новый anchor и upstream ownership

- **WHEN** новый JSON ответ не содержит пригодного id
- **THEN** старый previous_response_id удалён, raw новый id остаётся в envelope; ownership проверяет codex-lb.
