# 11 — Association повторных call_id при compact

[Навигатор](README.md) · [Принятое решение](Decisions.md). Статус: **не начат**. Зависимость: 00. **Q-005 согласован**: единый FIFO и отказ compact при неопределимой связи. Подтверждённой audit находки о дефекте на прежнем внешнем контракте нет.

## Цель и область

Реализовать совместимость repeated-ID occurrences через compact по принятому FIFO и проверить конечный subset. Источник — [решение Q-005](Decisions.md), исторический [вопрос](<../AgentBridge Quality Audit/OpenQuestions.md#abqa-q-005>), [аудит09](<../AgentBridge Quality Audit/09-token-budget-and-compaction.md>) и [аудит10](<../AgentBridge Quality Audit/10-tools-and-side-effects.md>).

Сверить actual ContextBuilder/ToolExecutor FIFO и codex-lb compact helper на выбранной версии. Durable identity StepId/output index и внешний call_id — разные границы; изменение pairing не разрешает повтор side effects.

## Работы

1. Воспроизвести различающий пример call(x,argsA), call(x,argsB), output(x,resultA), output(x,resultB) с различимыми args/results. Отдельно проверить call/output/call/output, где исходного различия нет.
2. Сохранить trace helper для selected={3} и selected={2}, а затем проверить final protected/required/fitting и opaque branches. Helper intermediate set не объявлять фактическим отправленным payload.
3. Закрепить принятый FIFO: первый результат относится к первому ещё не закрытому вызову с тем же call_id. Если связь нельзя достоверно установить, отклонять compact без активации/потери последнего окна и без повторного вызова handler.
4. Исправить несогласованные pairing boundaries в владеющем компоненте, синхронизировать контракт и различающие tests. Для codex-lb получить отдельное поручение на этот репозиторий. Не менять исходные canonical calls/outputs и не вводить глобальную дедупликацию по call_id как обход.
5. Проверить поведение на границе compact → сохранение контекста → последующий build/executor, включая incomplete occurrence и full request budget. Сохранить исходную историю, fixed expiry, attempt identity и no-replay.

## Проверки

B: различимые повторные IDs, оба selected sets, required/protected fitting, достаточный/недостаточный budget, incomplete prefix, контроль alternating sequence. Новый Python test/harness или script запускается только по точному отдельному разрешению; чтение trace не является B-run.

D: actual deployed compact и конечный payload в19 после согласования ресурсов. Без этого не утверждать live потерю данных, ошибочный результат или повтор действия.

## Критерии завершения

Принятый FIFO действует в local и включённой server boundary, final subset сохраняет правильные пары. Неопределимая связь даёт явный отказ без потери истории; no-replay и сохранение occurrences подтверждены отдельно от local balance. Непроверенная deployment часть остаётся в19.

## Результаты

Решение принято в обсуждении 2026-10-06 и записано в Decisions. Реализация и проверки ещё не выполнялись.
