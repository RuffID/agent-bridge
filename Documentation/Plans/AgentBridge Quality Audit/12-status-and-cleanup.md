# 12 — Статус диалога, очистка и поздние записи

Статус: **не начат**. Предпосылки: 04, 09, 11; ясны scope, version и lifecycle semantics.

## Цель и вопросы

Проверить правдивый статус, независимую смену модели и удаление без восстановления старых данных.

## Компоненты и зависимости

[AgentSettingsService](../../../Application/AgentSettingsService.cs), ContextModelGuard, DialogStatus, [ExpiredDialogCleanup](../../../Application/ExpiredDialogCleanup.cs), ExpiredDialogReader/DeletionUnitOfWork. Тесты AgentSettingsTests, ExpiredDialogCleanupTests, Integration/DialogSettingsIntegrationTests и ExpiredDialogCleanupIntegrationTests.

## Способ проверки и границы

Safe settings/status без secrets, active window/tail без будущих instructions/providers/input/tools, null estimate и ошибка каталога; CanContinue не send permission. Opaque selected/server provenance, отсутствующее подтверждение совместимости. Смена settings при active run, stale root/selection, default change без сдвига expiry. Очистка: limit 0/1/N, now==expiry, больше кандидатов чем limit, read scope отдельно от sequential delete scopes, fresh UTC. Deleted → Conflict → Deleted даёт Partial; исключение/отмена оставляет Unknown/NotAttempted и уже принятые результаты. Cleanup Dispose после success, concurrent вызов экземпляра, delete/recreate и поздний model/tool/compact/settings write. Soft bytes не триггер удаления.

## Разрешения

A; existing fake-port tests — B; actual cascade шести таблиц и гонки — C. Расписание и системная авторизация принадлежат приложению. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Таблица статуса/предупреждений, timeline cutoff expiry, отчёты всех кандидатов и схема late-write отказов. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Статус и все исходы одного пакета проверены в разрешённых границах; связь истечения и физического удаления объяснена. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Удаление всех просроченных строк одним пакетом, точное время физического удаления, очистку старых backup или отмену внешнего действия удалением диалога.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.
