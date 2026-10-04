# JSON Responses, этап 14

## Причина

Application уже сохраняет canonical items, envelope и continuation, но IModelGateway ещё не реализован. Нужен JSON transport текущего POST /v1/responses через actual HttpClientLibrary.

## Изменения

Реализовать GenerateAsync с полным canonical input/output, exact model/effort, function definitions и независимым снимком поддержанных параметров. Сохранять lifecycle, полный envelope и продолжение отдельно; нормализовать безопасные HTTP ошибки. Deadline, caller cancellation и освобождение request/response обязательны; retry/fallback отсутствуют.

Согласовано пользователем 2026-10-04: расширить ModelRequest снимком параметров, а continuation адаптера привязать к dialog/owner/agent, endpoint и отпечатку выбранного ключа. Несовпадение отклонять до HTTP; неизвестные continuation сохранять без отправки произвольных headers.

## Границы

Только этап 14. SSE, composition, tokenizer, compact и выполнение tools не реализуются. CompactAsync и streaming callback возвращают Unsupported до HTTP. Domain/persistence, соседние библиотеки, каркас проектов и generated files не меняются.

## Проверка

Public DI → IModelGateway → actual HttpClientLibrary → fake handler/local streams. Проверить opaque/unknown/order, lifecycle, complete/malformed/truncated errors, отсутствие retries/fallback, cancellation/deadline, disposal и safe logs. Реальная сеть/hosting не разрешены. OpenSpec CLI не установлен; change не архивировать без CLI validation.
