# 03 — Cleanup scope агента и caller cancellation

[Навигатор](README.md). Статус: **не начат**. Зависимость: 00. Находка: **ABQA-009, S3, подтверждена статически**.

## Цель и область

Не превращать неожиданный OperationCanceledException из DisposeAsync scope в штатный Canceled только потому, что caller уже отменён. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), запрет маскировать unexpected exceptions.

Область: [AgentRunScope](../../../Application/AgentRunScope.cs), [AgentRunSession](../../../Application/AgentRunSession.cs), [AgentRunner](../../../Application/AgentRunner.cs), адресные AgentRunnerTests. Не менять durable schema, SQL, checkpoints или бизнес-обработчики.

## Работы

1. Воспроизвести successful Append → принятие step/token → caller canceled → cleanup-only OCE(default/другой token). Зафиксировать текущее исчезновение исключения.
2. Различать происхождение operation cancellation и cleanup failure на владеющей scope границе. Проверки только caller flag или только равенства token недостаточно: cleanup может выдать OCE с тем же caller token.
3. Сохранить подтверждённые token/step и blocking дальнейших writes. Ошибка cleanup должна оставаться наблюдаемой после honest finalization; не делать повтор Append, terminal write при blocked session или повтор handler.
4. Сохранить действующие primary-only и primary+cleanup пути, exception identity/stack в пределах текущего контракта. Не оборачивать все ожидаемые отказы в общий successful result.
5. Сверить аналогичную границу ExpiredDialogCleanup как отрицательный контроль; не переносить на неё исправление без собственного дефекта.

## Проверки

B через публичный runner/DI с fake write ports и управляемым scope disposal:

- cleanup-only OCE с default, другим и тем же caller token при canceled caller;
- caller не отменён; cleanup IOException; primary+cleanup aggregate;
- обычная caller cancellation при успешном cleanup;
- принятые step/token сохранены, дополнительных write/handler/model calls нет;
- существующий контроль ExpiredDialogCleanup.DisposeCancellationIsAnUnexpectedCleanupFailure.

Compile-check ядра и `tests/AgentBridge.Tests/AgentBridge.Tests.csproj`; restart реального процесса относится к19, а не к этим doubles.

## Критерии завершения

Cleanup-origin exception не скрыт ни в одном токенном варианте; штатная отмена остаётся штатной. Подтверждённая запись не теряется, blocked состояние и no-replay сохранены. Фактические тестовые результаты записаны отдельно от будущего C/D.

## Результаты

Реализация и проверки ещё не выполнялись.
