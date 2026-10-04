# Responses SSE, этап 15

## Цель

Реализовать потоковый GenerateAsync через actual HttpClientLibrary без изменения публичного API. Callback выбирает stream=true; отсутствие callback сохраняет JSON этапа14. Частичный output не подтверждает completion.

## Объём

SSE framing, lifecycle, ordered canonical output/tools/opaque, последовательные callbacks, отмена/deadline/disposal и изолированные публичные transport tests. Composition/tokenizer/compact/orchestration и соседние библиотеки не изменяются.

## Проверка

Fake HttpMessageHandler и локальные fragmented streams через AddCodexLbResponses/IModelGateway/actual HttpApiClient. OpenSpec CLI отсутствовал; установка без разрешения запрещена, непроверенный change не архивируется.
