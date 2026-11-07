# Общие механизмы Responses

- Здесь находятся только внутренние JSON writers, проверка вложенных controls, чтение canonical output и строгий SSE framing. Единые исходники подключены Compile/Link в CodexLb и Sub2Api; отдельной сборки и публичных wire types нет.
- Namespace AgentBridge.Responses не является новым public API. Зависимости ограничены System.* и canonical контрактами ядра. HTTP, options, каталог, identity, lifecycle mapping и continuation остаются в конкретном адаптере.
- previousResponseId — явный аргумент writer, которым владеет вызывающий коннектор. CodexLb сохраняет прежнюю стратегию и legacy format; будущий sub2api передаёт полный рабочий input и null, без HTTP previous_response_id. Shared код не выбирает коннектор и не читает ключи.
- Compact остаётся отдельным writer, unsupported controls не удаляются молча. Canonical JSON/unknown поля не преобразуются; SSE EOF/[DONE] не подтверждают Completed, stream принадлежит HTTP pipeline и остаётся открытым при disposal reader.
- Проверки идут через public CodexLb gateway, actual HttpClientLibrary и fake handlers/local streams в tests/AgentBridge.CodexLb.Tests. Компиляция linked кода в Sub2Api не подтверждает реализацию его HTTP операций.
