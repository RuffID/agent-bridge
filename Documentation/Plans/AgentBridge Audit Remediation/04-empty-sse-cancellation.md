# 04 — Отмена после пустого SSE EOF

[Навигатор](README.md). Статус: **не начат**. Зависимость: 00. Находка: **ABQA-008, S3, подтверждена статически**.

## Цель и область

При caller cancellation до canonical данных распространять OCE с исходным caller token, включая успешный EOF/disposal. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), [SSE contract](<../../Technical documentation/15-responses-sse-adapter.md>) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md).

Область: [CodexLbModelGateway](../../../adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs), при необходимости ResponseSseState, [ResponsesSseTests](../../../tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs). HTTP2xx/empty body создаётся fake handler/local stream, без HTTP server.

## Работы

1. Добавить контрпример: empty body → EOF без Apply → успешный disposal отменяет caller → ожидать OCE с исходным token вместо Ok(Canceled).
2. Привести successful EOF путь к той же границе наличия canonical данных, что используется в catch OCE. Comments/keepalive/[DONE] сами по себе не должны выдавать фиктивные данные.
3. Сохранить partial output/envelope/continuation при допустимом Canceled после данных, приоритет explicit Failed над поздней отменой и deadline semantics.
4. Сохранить sequential awaited callbacks, callback exception identity и владение response. Throwing disposal из ABQA-002 проверяется отдельно в06.

## Проверки

- B: empty EOF + cancel-on-successful-disposal; empty EOF без отмены остаётся Incomplete.
- Empty stream read-OCE, comments-only stream, caller/deadline и отсутствие callbacks.
- Partial/terminal canonical data + поздняя отмена; explicit failure + поздняя отмена.
- Callback failure распространяется; response освобождён; fixture не запускает сервер или браузер.
- Compile-check конкретных CodexLb production/test проектов; адресный SSE-набор.

## Критерии завершения

До данных наблюдается исходный caller token и требуемое исключение. При наличии данных сохранены результаты, Failed priority и callback semantics. Проверка не заявляет live сетевую отмену; соответствующее evidence относится к19.

## Результаты

Реализация и проверки ещё не выполнялись.
