# Полный изолированный путь Sub2Api

- Public facade, AgentRunner, Sub2Api/HttpClientLibrary и настоящие persistence reader/UoW работают вместе; doubles только app ports, HTTP handler и base repositories существующего CatalogWriteFixture.
- Не открывать соединения, не запускать host, сеть или Docker. Подключение SQL Server используется только для проверки disconnected DI-графа; runner получает isolated storage ports до facade.
- Restart означает JSON-копию всех committed записей, новый storage и новый DI root. Shared lists или fake gateway не подтверждают этот сценарий. Это не проверка перезапуска настоящей БД.
- Проверять started checkpoint до business handler, полный canonical input после tools/restart и отсутствие повторов при отказе. Все ожидания имеют конечный срок и cleanup.
