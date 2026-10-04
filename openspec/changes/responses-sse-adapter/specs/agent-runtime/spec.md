## ADDED Requirements

### Requirement: Потоковый Responses gateway

GenerateAsync MUST выбирать stream=true при наличии onUpdate и MUST сохранять JSON stream=false при null. SSE MUST читаться через HttpStreamResponseResult actual HttpClientLibrary. Parser MUST поддерживать строгий UTF-8 fragmentation, optional начальный BOM, LF/CRLF/CR, comments и многострочные data. [DONE], HTTP2xx, delta и EOF MUST NOT подтверждать Completed; незакрытый frame на EOF MUST NOT dispatch. Completed MUST требовать response.completed с completed response без error и canonical output либо собранными item events. Непустой response.output MUST быть авторитетным и заменять collected items; absent/empty MUST допускать backfill. Failed/incomplete MUST сохранять известные output/envelope; unknown/opaque поля и output order MUST сохраняться. Function arguments и текстовые delta MUST сохраняться в неполных items при EOF. Continuation MUST применять binding/allowlist/id rules JSON adapter.

#### Scenario: EOF после tool arguments

- **WHEN** поток содержит function_call и фрагменты arguments, но не terminal event
- **THEN** отчёт Incomplete сохраняет call_id/arguments/unknown поля без фиктивного completion.

#### Scenario: Авторитетный terminal output

- **WHEN** collected items содержат больше элементов, чем непустой terminal output
- **THEN** итоговый output точно соответствует terminal array и не содержит старых extra items.

### Requirement: Владение streaming lifecycle

Callbacks MUST вызываться последовательно и ожидаться, MUST NOT вызываться после возврата. Callback exceptions MUST распространяться неизменными и MUST NOT становиться server/JSON/timeout errors. Поток/обёртка MUST освобождаться при любом выходе. Caller cancellation до данных MUST распространяться с исходным token, после полученного отчёта MUST сохранять данные в Canceled. Deadline MUST возвращать typed Timeout; при наличии данных MUST сохранять их в Failed. Explicit typed failure MUST иметь приоритет над поздней отменой; caller MUST иметь приоритет над deadline. Unexpected I/O MUST распространяться без retries/fallback.

#### Scenario: Callback бросает JsonException

- **WHEN** callback бросает JsonException
- **THEN** вызывающий получает тот же exception после освобождения потока без synthetic server error и следующих callbacks.

## MODIFIED Requirements

### Requirement: Безопасные ошибки и ограниченный JSON вызов

JSON gateway MUST нормализовать HTTP error по status и закрытым известным безопасным type/code/param только из Complete valid error envelope. Raw body/headers/reason/message/exception MUST NOT попадать в публичную ошибку/logger. Truncated/invalid/unsupported body MUST NOT трактоваться как complete envelope. Вызов MUST иметь конечный GenerationTimeout на отправку/чтение. До получения полного отчёта caller cancellation MUST распространяться OCE с исходным token; после полного отчёта MUST возвращаться Canceled с сохранёнными output/envelope/continuation. Explicit typed HTTP/model/JSON failure MUST сохранять приоритет над поздней отменой. Caller MUST иметь приоритет над deadline. Неожиданный I/O MUST распространяться без retry. Request/response MUST освобождаться на успехе/отказе/JSON error/отмене. Compact gateway MUST давать Unsupported до HTTP.

#### Scenario: Поздняя отмена

- **WHEN** caller отменяется после полного получения canonical JSON
- **THEN** отчёт Canceled сохраняет output/envelope/continuation
- **AND** explicit typed failure не подменяется отменой.

#### Scenario: Error prefix и секретные values

- **WHEN** HTTP error body неполный, либо error type/code/param содержит неизвестный текст
- **THEN** public error сохраняет status и только известные безопасные поля
- **AND** raw message не публикуется, retry отсутствует.
