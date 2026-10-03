# Контекст этапа 13

Источник wire-контракта — текущие `app/modules/proxy/api.py`, `schemas.py`, `app/core/auth/dependencies.py` и `app/core/openai/model_registry.py` соседнего codex-lb, прочитанные статически 2026-10-03. В `/v1/models` не передаётся client_version: он выбирал бы другую Codex-native форму. Metadata содержит input_context_window, context_window, уровни effort и флаги. Каталог фильтруется по ключу; created не является версией.

`_resolved_context_window` явно описывает input budget, `_to_model_metadata` передаёт его в input_context_window. Запас InputTokenReserve относится к входу; max_output_tokens не вычитается повторно. Неизвестный входной бюджет не заменяется context_window или статической константой. Это проверка настроек, а не локальный подсчёт подготовленного запроса/opaque-состояния.

Например, threshold=32000 и reserve=4096 допустимы при input_context_window=36096; при 36095 возвращается Validation. Незнакомый effort не заменяется default_reasoning_level. Отсутствие metadata не мешает увидеть ID в каталоге, но не позволяет проверить выбранную модель.

HTTP-статус достаточен для безопасного отказа чтения каталога. Raw headers/body/preview/exception message не возвращаются и не логируются адаптером. HttpClientLibrary ограничивает тело ошибки 64 КиБ и сохраняет полноту; адаптер не трактует preview как envelope. Сеть выполняется вне БД. Изолированные fake handlers не подтверждают работу сервера или upstream.
