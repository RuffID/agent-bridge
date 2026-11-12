# Маппинг persistence

- Чистое преобразование записей/JSON без подключения, транзакций и сетевых операций. Однородная группа mapping; новые сценарии хранения размещать у их владельца.
- Recovery сверяет original item и output отчёта по полному JSON через JsonElement.DeepEquals: сериализация может менять пробелы/экранирование, но не значения/неизвестные поля/порядок массивов. Проверки исходных позиций и protocol call_id обязательны; originals и подтверждённые outputs не переписывать, handlers не запускать.
- TurnSettingsMapping сохраняет прежний version 1 для строгого известного окна. Явные server policies используют version 2 с nullable InputContextWindow и обязательным BudgetPolicy. Старые snapshots читаются; неизвестный/повреждённый формат не исправляется defaults.
- Изменение snapshot бюджета не добавляет connector provenance, миграции, restart/recovery либо новые таблицы этапа04.
- ProviderModelMapping допускает PostgreSQL (text/C) и SQL Server (nvarchar(max), binary UTF-16 owner, BIN2 catalog keys, DATALENGTH constraints). Bigint UTC ticks и прежние generated модели сохраняются.
