# Общие механизмы Responses

- Здесь находятся внутренние JSON writers, проверка вложенных controls, чтение canonical output, строгий SSE framing и ResponseSseAccumulator. Единые исходники подключены Compile/Link в CodexLb и Sub2Api; отдельной сборки и публичных wire types нет.
- Namespace AgentBridge.Responses не является новым public API. Зависимости ограничены System.* и контрактами ядра. HTTP/options/каталог/identity и continuation policy остаются у коннектора. SSE accumulator получает continuation factory и safe error reader от владельца; codex-lb сохраняет bound continuation, sub2api всегда возвращает null.
- previousResponseId — явный аргумент writer, которым владеет вызывающий коннектор. CodexLb сохраняет прежнюю стратегию и legacy format; sub2api передаёт полный рабочий input и null, без HTTP previous_response_id. Shared код не выбирает коннектор и не читает ключи.
- Compact остаётся отдельным writer, unsupported controls не удаляются молча. Canonical JSON/unknown поля не преобразуются; SSE EOF/[DONE] не подтверждают Completed, stream принадлежит HTTP pipeline и остаётся открытым при disposal reader.
- Проверки идут через actual gateways/HttpClientLibrary и fake handlers/local streams в tests/AgentBridge.CodexLb.Tests и AgentBridge.Sub2Api.Tests. Проверять оба набора после изменения общих механизмов; isolated green не является live-приёмкой.
