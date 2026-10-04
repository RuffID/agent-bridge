# 10 — Инструменты, права и внешние действия

Статус: **не начат**. Предпосылки: 01, 04, 08; известны app authorization и checkpoint contract.

## Цель и вопросы

Проверить допуск действия, идентичность попытки, ограниченную параллельность и честность Unknown.

## Компоненты и зависимости

[ToolRegistry](../../../Application/ToolRegistry.cs), [ToolExecutor](../../../Application/ToolExecutor.cs), ToolExecutionSession/Identity/Limits, IToolInvocationValidator/IToolExecutionCheckpoint, AgentBridgeToolsExtensions. [ToolExecutorTests](../../../tests/AgentBridge.Tests/ToolExecutorTests.cs), compile-only AccountSummaryValidator/Tool.

## Способ проверки и границы

Неизвестное/невыбранное имя, duplicate registration, metadata mismatch, неверная object schema, отказ актуальных прав, incomplete model step. Проверить repeated call_id против StepId/output position, закрытые пары и повтор attempted step. Validator до checkpoint, confirmed checkpoint до handler; отказ/exception/cancel checkpoint запрещает действие. Каждый invocation получает отдельный scope, все workers ожидаются при частичном сбое. Проверить MaxSteps/MaxCalls/MaxConcurrency, expiry и cooperative timeout; late success соседа, handler Fail против exception после начала, primary+Dispose errors. Отдельно описать гонку изменения бизнес-прав между validation и action как ответственность обработчика приложения.

## Разрешения

A; существующие isolated executor tests — B; внешние изменяющие действия не выполнять. Реальные бизнес-системы потребуют отдельного D-разрешения вне обычного прогона. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Порядок validator/checkpoint/handler/outcome, таблица identity и статусов каждого соседа, владельцы scopes/tasks. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Все ветви допуска, bounds и частичных исходов имеют вывод; граница session-memory против durable journal ясна. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Ровно одно действие во внешней системе, атомарность бизнес-БД или полную JSON Schema validation без обязательного validator приложения.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.
