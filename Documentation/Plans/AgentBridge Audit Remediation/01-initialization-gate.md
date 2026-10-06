# 01 — Gate после начавшейся первой установки

[Навигатор](README.md). Статус: **не начат**. Зависимость: 00. Находка: **ABQA-006, S2, подтверждена статически**.

## Цель и область

После отказа начавшейся установки блокировать последующее обслуживание через тот же SingleInitializerGate, независимо от текущего диагностического Stage. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), требование запрета обслуживания после неуспешной первой установки.

Причина принадлежит EFCoreLibrary: [DatabaseMaintenance](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/DatabaseMaintenance.cs), [SingleInitializerGate](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance/Coordination/SingleInitializerGate.cs). Изменять эту библиотеку только отдельным поручением. AgentBridge не вводит собственный gate или SQL recovery.

## Работы

1. Добавить изолированный контрпример: Missing → начало CREATE → успешные create/binding → ошибка или OCE в PendingAsync при успешном cleanup → следующий scope на том же gate.
2. Точечно сохранять факт начала необратимой initialization отдельно от меняющегося Stage. Защита должна охватывать отказ CREATE и последующие discovery/recheck/verification, а не только успешный CREATE.
3. Poison выполнять до release lease; ожидающий и последующий entrant не должны добраться до provider/migration boundaries после такого отказа.
4. Сохранить нормальную установку, update existing, ранний отказ до начала установки, caller/deadline classification и safe logging. Не добавлять автоматический reset/retry gate.
5. В AgentBridge добавить адресный сценарий через существующую maintenance-регистрацию и actual coordinator с подставными provider/migration ports. Синхронизировать ближайшие инструкции, если меняется устойчивое правило.

## Проверки

- B, после проверки проекта и правил запуска: тесты `EFCoreLibrary.Maintenance.Tests`; адресные `DatabaseMaintenanceTests` AgentBridge с `Dependency!=Database`.
- Отдельные контролируемые отказы до CREATE, внутри CREATE, в pending после CREATE, в final recheck, на migration/verification. Проверять последовательность poison/release и отсутствие повторных вызовов provider.
- Успешная initialization и допустимое обслуживание existing DB не должны получить ложный poison. Gated задачи освобождать и await в finally.
- Compile-check затронутых конкретных проектов с `GeneratePackageOnBuild=false`. Реальные provider effects относятся к17.

## Критерии завершения

Контрпример воспроизведён до правки и проходит после неё; все контроли сохранены. Защита не зависит от текущего Stage. Есть библиотечное и адаптерное evidence; scope/lease завершаются. Статус C пока не заявляется.

## Результаты

Реализация и проверки ещё не выполнялись.
