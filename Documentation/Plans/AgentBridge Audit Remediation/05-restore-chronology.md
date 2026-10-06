# 05 — Хронология валидирующего Restore

[Навигатор](README.md). Статус: **не начат**. Зависимость: 00. Находка: **ABQA-005, S3, подтверждена статически**.

## Цель и область

Отклонять внешний snapshot, LastChangedAtUtc которого не может соответствовать принятой mutation при заданной revision/истории. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), [Dialog](../../../Domain/Dialogs/Dialog.cs) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), валидирующее восстановление и локальные инварианты.

Работы в Domain и [DialogRestorationTests](../../../tests/AgentBridge.Tests/DialogRestorationTests.cs). Не вводить EF-зависимость в Domain, не лечить строки БД, не менять schema/migrations или обязательность timestamp полей.

## Работы

1. Добавить точный контрпример аудита: revision1, turns пусты, context(version1,prefix0,created=t0+1min), LastChanged=t0+2min при корректных UTC/owner/expiry. Restore должен отклонять состояние до выдачи агрегата.
2. Сформулировать проверку по реально известным mutation/revision границам. Не требовать LastChanged равным последнему child timestamp во всех состояниях: append имеет отдельную revision без отдельного persisted timestamp.
3. Точечно усилить фабрику; сохранить допустимые round-trip Create/Begin/Append/Complete/compact и вариант с дополнительной append revision, где LastChanged позже child date.
4. Проверить отсутствие изменения исходных snapshot collections при отказе. Сохранить lifetime/owner/fixed expiry и независимую settings version.
5. Сверить передачу времён через DialogStateLoader и context UoW. Их просмотренные штатные writes не порождали исходный контрпример; не объявлять их дефектом без нового доказательства.

## Проверки

- B: невалидный context-only snapshot и допустимый snapshot с extra append revision; existing corruption/round-trip cases.
- Границы UTC, created/expiry, revision overflow/несогласованность, prefix/status в затронутой фабрике.
- Через fake persistence loader неверное восстановление не разрешает дальнейшую запись; это не доказательство corrupt реальной БД.
- Compile-check ядра и затронутого test проекта; реальный persistence round-trip отдельно17.

## Критерии завершения

Контрпример отклонён, допустимые дополнительные mutations не запрещены. Новый инвариант защищён Domain без обходных setters/reflection/friend assembly. Физическое повреждение БД и штатное происхождение контрпримера не заявляются.

## Результаты

Реализация и проверки ещё не выполнялись.
