# Маппинг persistence

- Чистое преобразование записей/JSON без подключения, транзакций и сетевых операций. Однородная группа mapping; новые сценарии хранения размещать у их владельца.
- TurnSettingsMapping сохраняет прежний version 1 для строгого известного окна. Явные server policies используют version 2 с nullable InputContextWindow и обязательным BudgetPolicy. Старые snapshots читаются; неизвестный/повреждённый формат не исправляется defaults.
- Изменение snapshot бюджета не добавляет connector provenance, миграции, restart/recovery либо новые таблицы этапа04.
