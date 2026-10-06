# 10 — Глубина проверки вложенных controls

[Навигатор](README.md) · [Принятое решение](Decisions.md). Статус: **не начат**. Зависимость: 00. **Q-004 согласован**: malformed/duplicate known nested fields отклоняются до HTTP. Подтверждённой audit находки о дефекте на прежнем неоднозначном контракте нет.

## Цель и область

Реализовать согласованную границу known nested validation до HTTP. Источник — [решение Q-004](Decisions.md), исторический [вопрос](<../AgentBridge Quality Audit/OpenQuestions.md#abqa-q-004>) и controls в [OpenSpec](../../../openspec/specs/agent-runtime/spec.md).

Точки сверки: [ResponseRequestWriter](../../../adapters/AgentBridge.CodexLb/Responses/ResponseRequestWriter.cs), JSON/SSE/compact paths, [requests.py codex-lb](../../../../codex-lb/app/core/openai/requests.py). Чтение исходников codex-lb не разрешает их изменение; локальный source не доказывает deployed contract.

## Работы

1. Подготовить различающие inputs: reasoning.summary=42, duplicate known nested summary, корректный str/null, unknown nested field. Сверить known shape целевой версии и прежнюю top-level validation.
2. Перечислить фактически известные nested controls и проверить types/duplicates по их подтверждённому контракту. Применять pre-HTTP Validation для неверной known формы; локально не угадывать model-specific ограничения.
3. Синхронизировать нормативный текст и техническое описание с принятым решением. Unknown nested fields сохранять без удаления/нормализации; policy unknown top-level остаётся прежней.
4. Точечно реализовать недостающую проверку в request writer и адресных tests с сохранением canonical input/opaque controls. Для части правил, уже выполняемых кодом, добавить различающий контроль без ненужной переписи.
5. Проверить одинаковую agreed validation для JSON, SSE и compact. Если нужен server contract change, вынести отдельную согласованную работу в codex-lb, не имитировать её в адаптере.

## Проверки

B с actual writer/gateway и fake HTTP handler: Validation для malformed/duplicate known nested fields и **HTTP calls=0**, valid known inputs, top-level duplicates, сохранение unknown nested fields и input ordering. Live поведение отдельно19; ошибка fake handler не доказывает server rejection.

## Критерии завершения

Known nested форма отклоняется локально до HTTP, unknown fields сохранены; JSON/SSE/compact используют одинаковую проверку. Есть адресное evidence, нормативное описание и safe Validation. Принятое решение не повышает задним числом достоверность аудита.

## Результаты

Решение принято в обсуждении 2026-10-06 и записано в Decisions. Реализация и проверки ещё не выполнялись.
