# Изолированные проверки codex-lb

- Actual public gateway/HttpClientLibrary и локальные handlers/streams; без сервера, host и внешнего HTTP.
- Protocol fixtures явно задают synthetic connector provenance для вручную подготовленного opaque input. Проверки отказов вызывают gateway напрямую, без автоматической маркировки fixture.
- Проверять generation/compact и continuation отдельно от локального происхождения. Последнее не попадает в payload и не доказывает upstream identity.
