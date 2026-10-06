# 12 — Подключение Microsoft SQL Server

[Навигатор](README.md) · [Согласованные решения](Decisions.md). Статус: **не начат**. Зависимости: 00,01,02,05,07. Область: новый provider AgentBridge; EFCoreLibrary без обхода.

## Цель и исходное состояние

Основной сценарий — MSSQL, .NET10, приложение на Windows/Linux. Сейчас AgentBridge enum/registration/migrations поддерживают только SQLite/PostgreSQL. В EFCoreLibrary уже существует [SQL Server maintenance](../../../../work/EFCoreLibrary/maintenance/EFCoreLibrary.Maintenance.SqlServer/AGENTS.md); его существование не доказывает реальную MSSQL-проверку AgentBridge.

## Работы

1. Сверить actual EFCoreLibrary CRUD/context/maintenance API и supported SQL Server editions. Использовать существующие base repositories и сценарные UoW; не добавлять собственный coordinator, raw SQL или обходной persistence layer.
2. Добавить явный provider SQL Server, соответствующее конфигурирование EF, зависимости и migrations identity. Не переключать provider автоматически при ошибке подключения; не менять значения existing enum несовместимым образом.
3. Создать отдельный library target/startup migrations проект по существующим правилам решения, с собственными AGENTS/design-time factory. Генерацию выполнить tooling только после согласования точной команды; generated migration/snapshot/designer вручную не править.
4. Проверить model mapping шести таблиц, composite keys/FK/cascades, JSON/bytes, UTC ticks, revision/settings CAS и journal atomicity на provider-specific metadata. Устранить реальные SQL Server особенности каскадов/типов, сохранив контракт; schema integration evidence отдельно17.
5. Подключить actual SQL Server maintenance module EFCoreLibrary. Settings для backup — серверный путь, receipt/verification и privileges по текущему API. Путь на Windows/Linux app host не подменяет server backup destination.
6. Согласовать и документировать поддержку конкретной редакции/версии/TLS/auth; Linux ARM64 app может подключаться к отдельному поддержанному серверу БД. Azure SQL/MI/Synapse и локальный SQL Server Engine ARM64 не объявлять поддержанными по этой работе.
7. Обновить карту проектов/ближайшие AGENTS, `.slnx`, technical/provider docs и binary consumer для существующего API. Устаревший PostgreSQL example заменить MSSQL только после реальной реализации/compile-check16.

## Проверки

B: provider/options/DI metadata, mapping, history identity, actual EFCoreLibrary registrations и безопасные errors без открытия соединения. Собирать конкретные новые/затронутые проекты с `GeneratePackageOnBuild=false`. Для изолированного persistence набора сохранять фильтр `Dependency!=Database`.

C в17: собственный SQL Server, migrations/CRUD/CAS/journal/backup/restore. SQL и DB-команды отдельно разрешаются; отсутствие сервера не позволяет считать интеграцию успешной.

## Критерии завершения

Есть actual SQL Server provider/migrations/maintenance и адресное B evidence. SQLite/PostgreSQL не сломаны и не выбираются как fallback. Область MSSQL-поддержки документирована; сведения о реальных provider outcomes относятся только к выполненному17.

## Результаты

Реализация, генерация миграций и проверки ещё не выполнялись.
