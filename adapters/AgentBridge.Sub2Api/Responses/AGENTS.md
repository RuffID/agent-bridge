# Responses sub2api

- Canonical гарантия принадлежит границе AgentBridge: полный request и полученный output/envelope сохраняются; existing server reasoning normalization/recovery допустимы по плану Б. Успех не доказывает upstream identity или сохранность opaque внутри сервера.
- Один dispatch через HttpClientLibrary читает JSON либо SSE по Content-Type, также для non-stream приложения. Никаких retries, previous_response_id, legacy turn-state, returned continuation или business tool execution здесь нет.
- Shared writer/accumulator/framing подключены source link. Накопитель получает null continuation; response.id/model/headers остаются только raw envelope, без local provenance04.
- Callbacks последовательны и awaited. Terminal confirmation, partial output, caller/deadline, identity callback/read primary и все cleanup failures сохраняются. Raw body/key/headers/error text не логировать.
- Compact ограждён отдельно до подтверждения совместимости actual модели с generation. Не подменять его генерацией или guessed flag.
- Проверки через existing composition, actual HttpClientLibrary, fake handler/local streams; без AppData/kit/live/host/БД.
