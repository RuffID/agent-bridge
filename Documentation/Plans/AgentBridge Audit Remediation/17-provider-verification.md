# 17 — Проверка provider maintenance и сохранения

[Навигатор](README.md) · [Решения](Decisions.md). Статус: **не начат**. Зависимости: 01,02,05,07,12–16; конкретные ресурсы **Q-002**, отдельное разрешение C. Область: собственные временные SQL Server ресурсы и адресная регрессия existing providers.

## Цель и основание

Подтвердить actual MSSQL migrations/CRUD/CAS/journal и исправленные maintenance/Restore пути. Источники — [решение об основной БД](Decisions.md), [аудит05](<../AgentBridge Quality Audit/05-migrations-and-maintenance.md>), [Integration/AGENTS](../../../tests/AgentBridge.Persistence.EfCore.Tests/Integration/AGENTS.md). Старые Integration правила разрешают лишь SQLite/PostgreSQL: при реализации MSSQL их нужно явно расширить, а не считать старое разрешение подходящим.

Исторический общий DB00–13 был на EFCoreLibrary0.0.4, а compatibility0.0.5 не был его полным повтором. Этот этап даёт новое адресное evidence; не требуется заново исполнять весь исторический аудит.

## Подготовка и работы

1. Согласовать SQL Server version/edition/runtime/auth/TLS, собственные БД, server backup destination, команды запуска и очистки. SQL не выполнять в рамках этого плана; отдельное будущее разрешение должно явно охватывать соответствующие операции. Определить opt-in и фильтр `Dependency=Database` по актуальным tests; старый PostgreSQL opt-in не включать автоматически.
2. Проверить post-CREATE failure/OCE до migration и запрет следующего entrant на том же root gate. Реальное состояние созданной БД зафиксировать; очистка не считается автоматическим recovery приложения.
3. Проверить backup primary + connection cleanup secondary с actual SQL Server provider, safe codes и poison. Existing PostgreSQL process/SQLite native проявления ABQA-007 проверить адресно либо оставить их явным незакрытым остатком; переход на MSSQL не опровергает исходную находку.
4. Провести разрешённый SQL Server migration/backup/restore round-trip с отдельными test target и destination; проверить schema и данные после restore. Проверить серверный receipt/verification по EFCoreLibrary; путь и права принадлежат серверу БД. Down допускается только для собственных уничтожаемых ресурсов и отдельно согласованной команды.
5. Проверить корректный Dialog Restore round-trip с append/context. Некорректное состояние не подготавливать прямой SQL-правкой без разрешения; если соответствующий scenario остаётся только в B, явно сохранить границу.
6. Адресно проверить root/settings races, journal+outputs rollback и неизвестный commit в затронутом persistence contract через EFCoreLibrary и существующие сценарные UoW. Не заменять библиотеку прямым EF/SQL.
7. После проверки выполнить только разрешённую очистку собственных ресурсов с проверкой абсолютных путей/идентичности; сохранить sanitized evidence и manifests без секретов.

## Проверки

C: основной SQL Server сценарий, версии/DDL/backup/restore receipts/exit codes, подтверждённые scope outcomes и absence of replay. Адресные existing SQLite/PostgreSQL проверки выполняются только по отдельному разрешению ресурсов. Реальная потеря ack не доказывается подставным исключением после commit; crash/network scenarios относятся к19.

Не включать MySQL, чужие БД, production data, произвольный destructive Down или очистку shared directories. Наличие integration tests/env flag не заменяет разрешение. Linux ARM64 app client не требует размещения SQL Server Engine на той же машине.

## Критерии завершения

У основного SQL Server сценария есть текущее evidence и подтверждённая очистка собственных ресурсов; existing providers проверены в согласованной затронутой области. Для ABQA-006/007/005 различены B и C conclusions. Невоспроизведённые native/process отказы перечислены, а не закрыты общим успешным MSSQL backup.

## Результаты

Окружение, реальные проверки и очистка ещё не выполнялись.
