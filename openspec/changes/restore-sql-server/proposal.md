# Восстановление SQL Server

## Причина

Приложениям снова требуется Microsoft SQL Server. Реализация удалена коммитом cbc6a46 вместе с SQLite; пользователь поручил восстановить MSSQL из истории.

## Изменение

Вернуть отдельный SQL Server provider, прежние migrations и public API, provider-specific mapping, backup validation, bounded history и подготовку DLL/NuGet-поставки. PostgreSQL сохраняется; SQLite остаётся неподдерживаемым. Новые migrations и перенос данных не требуются.

## Проверка

Статическая проверка и compile-check конкретных проектов без подключения к БД. Изолированные и интеграционные тесты, упаковка и OpenSpec CLI не запускаются без отдельного разрешения.
