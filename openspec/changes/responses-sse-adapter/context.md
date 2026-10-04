# Контекст SSE этапа15

Основание: существующие IModelGateway/ModelStreamUpdate и принятый JSON этап14. Публичный API не расширяется. Нормы: [delta spec](specs/agent-runtime/spec.md).

HttpClientLibrary0.0.0.5 owns HTTP request/response; gateway owns returned stream wrapper и deadline/linked CTS. Callback awaited без фоновых tasks; его exception не является transport failure. Библиотеку и codex-lb не изменяем.

Актуальный codex-lb public SSE collector собирает indexed output_item.added/done и backfills пустой terminal output. Адаптер сохраняет raw response envelope отдельно от накопленного output: восстановленные partial items не выдаются за raw server envelope. Неполные text/function arguments остаются в canonical items; tools не выполняются. Unknown поля полных items/envelopes не отбрасываются.

Пример: function_call.added с arguments="", затем arguments.delta="{\"id\":", затем EOF. Report Incomplete содержит partial arguments и call_id; callback не разрешает выполнение инструмента. response.completed с canonical output подтверждает Completed даже без видимого текста.

Caller после данных даёт Canceled; deadline после данных — Failed с Timeout, без потери output. До данных сохраняются stage14 OCE/ServiceResult.Fail правила. Ошибочный JSON после данных — Failed/Rejected с сохранённым состоянием; I/O остаётся exception. Live HTTP/account ownership и hosting запрещены; fake pipeline не подтверждает live compatibility. CLI validation при отсутствии CLI остаётся невыполненной, архивирование запрещено.
