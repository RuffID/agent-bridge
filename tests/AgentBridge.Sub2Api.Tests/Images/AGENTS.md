# Изолированные Images проверки sub2api

- Actual optional auxiliary, HttpClientLibrary и public Integration DI; fake handler/local streams, без host, live HTTP/OAuth, БД, Docker и процессов из тестов.
- Проверять JSON generation, multipart edits и snapshot коллекции/bytes до await; явные параметры нельзя удалять ради fallback. URL результатов только возвращаются, не скачиваются.
- HTTP 200 после whitespace heartbeat не подтверждает успех: error/empty/malformed и каждый payload проверяются отдельно. Cancellation/deadline должны достичь проверяемой стадии; gated задачи освобождаются и await в finally.
- Проверять safe logging/errors, per-request key, no retry, владение borrowed client и primary/cleanup failures. Source-linked consumer compile не доказывает обновлённый binary kit или live приёмку.
