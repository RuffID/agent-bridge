# 11 — Полный ход агента и восстановление

Статус: **не начат**. Предпосылки: 07–10; разобраны transport, context, persistence и tools.

## Цель и вопросы

Проверить целый путь обращения, согласованность сохранения и отсутствие автоматического повторного исполнения известных попыток.

## Компоненты и зависимости

[AgentRunner](../../../Application/AgentRunner.cs), [AgentRunSession](../../../Application/AgentRunSession.cs), AgentRunScope, AgentRunResult, IDialogToolAttemptWriter и DialogToolAttemptUnitOfWork. Тесты AgentRunnerTests, Integration/AgentRunnerIntegrationTests, CrossComponentIntegrationTests/CrossComponentFixture.

## Способ проверки и границы

Проследить чтение → pinned settings/access → providers один раз → atomic begin → compact/full guard → model step → durable Started → handler → atomic outputs/journal → terminal save. Негативные точки: отказ каждой записи, старый TurnId/legacy без journal, restart после Started, потеря acknowledgement после commit, partial call, parallel tools failure, expiration/delete/recreate во время ожидания. Проверить matching StepId для LastResult, successful compact token при следующем exception, запрет refresh/retry после blocked storage. Completed требует terminal save; late Canceled может сосуществовать с уже сохранённым Completed. Новый TurnId не считать способом recovery неизвестного действия.

## Разрешения

A; fake store orchestration — B; настоящий persistence/restart и controlled commit faults — C; живой model endpoint и crash процесса — отдельные D/C-разрешения. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Пошаговый trace с revisions и состояниями journal, actual/fake границы, число бизнес-вызовов, terminal saved отдельно от model report. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Happy path и каждая существенная точка прерывания разобраны, восстановления и запреты повтора описаны без обещания exactly-once. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Реальную аварию ОС по новому DI root, потерю сети по synthetic ack, однократность одинакового бизнес-действия с новым identity.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.
