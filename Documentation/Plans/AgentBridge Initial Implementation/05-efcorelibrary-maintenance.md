# 05 — Развитие контракта обслуживания EFCoreLibrary

Статус: **Реализовано и принято; запрещённые проверки пропущены**. Общий relational контракт и четыре optional модуля SQLite/PostgreSQL/SQL Server/MySQL. Зависимости: **00**. EFCoreLibrary commit: `a1747388ab0eb2be6da3031535fd88e7533b8df1` — `feat(maintenance): add relational database maintenance modules`.

## Дополнительная интеграционная проверка 2026-10-04

Ранее запрещённая граница SQLite/PostgreSQL дополнительно проверена настоящими provider/coordinator/native/process механизмами EFCoreLibrary через AgentBridge registration. 39 integration cases суммарно, из них 15 maintenance/migrations cases: missing/initialize/already-exists/no-pending; backup существующей БД до migration; полный backup; native SQLite restore и pg_restore 18.6 в отдельных БД с совпадением live схемы/данных и hash/length артефакта. Реальные backup configuration/executable/DDL/auth/major failures останавливают операцию; migration failure poisons общий root gate и сохраняет восстановимый backup; corrupted PostgreSQL dump отклоняется.

Повторные library regressions: **89 passed / 0 failed / 0 skipped**, семь проектов без warnings/errors, GeneratePackageOnBuild=false. **Реальные SQL Server/MySQL/Unix/TLS/unknown process-stop условия не подтверждены**; receipts не объявляются универсальной гарантией восстановления. [Команды, ограничения и cleanup](README.md#дополнительный-интеграционный-запуск-2026-10-04). Соседняя EFCoreLibrary не изменена; аналитические и первоначальные отчёты ниже сохранены.

## Цель

Добавить в общую библиотеку возможности бэкапа и обслуживания БД с учётом провайдера.

## Задачи

- [x] Согласовать с пользователем точные контракты обслуживания и бэкапа до изменения соседней библиотеки.
- [x] Сохранить разделение технического обслуживания и базовых CRUD-репозиториев бизнес-данных.
- [x] Реализовать общий контракт и SQLite/PostgreSQL/SQL Server/MySQL, receipt, отмену и безопасные ошибки.
- [x] Сохранить порядок проверки, подтверждённого бэкапа и миграций существующей БД.
- [x] Реализовать первую установку отдельно от ошибок авторизации, прав и подключения.
- [x] Не копировать в AgentBridge SQL Server-код бэкапа с прямым подключением.
- [x] Получить приёмку координатора и отдельное разрешение на локальные коммиты явных файлов этапа 05.

## Проверка и завершение

Проверить контракты провайдеров и по возможности использовать изолированные заглушки. Реальные бэкапы и проверки БД требуют отдельного разрешения. Этап завершён, когда AgentBridge может использовать проверенный контракт EFCoreLibrary без внутреннего обхода библиотеки.

Источник: [обслуживание БД](<../../Technical documentation/06-database-maintenance.md>).

## Аналитический проход 2026-10-03

Разделы ниже до «Реализация и проверка этапа 05» сохраняют исторические результаты анализа; их прежние статусы не описывают текущую реализацию.

- Проверены применимые инструкции, требования AgentBridge, текущие контракты EFCoreLibrary и образец AquaByte-Ledger. Исходный commit EFCoreLibrary: `8a67729c73914a04807e71d028f42057b6287e5d`.
- [EFCoreLibrary.csproj](../../../../work/EFCoreLibrary/EFCoreLibrary.csproj): `net10.0`, версия `0.0.4`, EF Core и DI Abstractions `10.0.3`; Relational, SQLite и PostgreSQL не подключены. `GeneratePackageOnBuild=true`: будущий адресный compile-check требует `-p:GeneratePackageOnBuild=false`, без pack/publish.
- [IAppDbContext](../../../../work/EFCoreLibrary/Abstractions/Database/IAppDbContext.cs) предоставляет `DatabaseFacade`. [DI-регистрация](../../../../work/EFCoreLibrary/Extensions/ServiceCollectionExtensions.cs) использует `TContextKey`; готового check/backup/migrate API в production-исходниках нет. CRUD-контракты и обслуживание пока не объединены.
- [DataBaseCheckUpService](../../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs) выполняет синхронные `CanConnect`, pending migrations, backup, migrate; при `CanConnect=false` выбрасывает ошибку, отдельной первой установки нет. [BackupService](../../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/BackupService.cs) относится только к SQL Server; возвращаемого подтверждения backup и cancellation нет.
- По XML-документации локальных пакетов Microsoft.Data.Sqlite `10.0.3`/`10.0.11` метод `BackupDatabase` синхронный и не принимает cancellation token. По документации EF Core `CanConnectAsync` перехватывает ошибки подключения; отрицательный результат не доказывает отсутствия БД.
- В проверенном публичном API локального Npgsql `10.0.3` готового backup/dump API отдельной PostgreSQL БД не найдено. Полноценный механизм PostgreSQL backup и гарантии отмены требуют согласования; наличие интерфейса не считается реализацией backup.
- По локальному nuspec Npgsql.EntityFrameworkCore.PostgreSQL `10.0.3` требует EF Core/Relational `[10.0.4, 11.0.0)`. Совпадение номера `10.0.3` с текущей EFCoreLibrary не означает совместимость фиксированного набора пакетов.
- Проверенные потребители DLL: AquaByte-Ledger использует EF Core/SqlServer `10.0.11`; TelegramCodexRelayBot и AutoOrderServerBodroCoffe — `10.0.8`, все на `net10.0`. Их DLL не заменялись; бинарная совместимость новой реализации не проверена. Адаптер AgentBridge пока не ссылается на EFCoreLibrary и провайдеры.

Конкретное предложение API, состава файлов и блокирующих решений передано в отчёте этого прохода. Оно не зафиксировано как согласованное требование. Исходники, документация и инструкции соседних проектов не изменялись; обход обслуживания в AgentBridge не реализован. Коммита нет, этап не завершён.

Проверки этого прохода: статическое чтение контрактов, проектов, локальной документации пакетов и разрешённые Git status/rev-parse/diff; проверка ссылок и UTF-8 изменённых файлов плана. Restore, compile-check и тесты не запускались: код не изменён.

Приложения/хостинг, Docker, реальные HTTP/БД/SQL, применение migrations, backup/restore, внешние процессы и проектные скрипты — **Пропущено по указанию пользователя**. Эти проверки не считаются успешными.

## Уточнение области после разрешения пользователя

Пользователь разрешил правки EFCoreLibrary и выбрал общий расширяемый реляционный контракт с четырьмя встроенными провайдерами: SQLite/PostgreSQL/SQL Server/MySQL. Механизмы backup, режим SingleInitializer и политика хранения копий остаются техническим предложением на проверке координатора. AgentBridge сохраняет свой исходный набор SQLite/PostgreSQL; новые провайдеры в его options не добавлялись.

Дополнительная статическая проверка:

- Текущие CRUD/adapter-контракты EFCoreLibrary не привязаны к одному реляционному провайдеру; реального backup API по-прежнему нет. Универсальность контракта не подтверждает наличие реализации backup каждого провайдера.
- Локальный nuspec Microsoft.EntityFrameworkCore.SqlServer `10.0.11` указывает Relational `10.0.11` и SqlClient `6.1.6`. Пример AquaByte отдельно использует SqlClient `7.0.2`; перенос этой версии автоматически не требуется. XML SqlClient подтверждает async command с cancellation token и описывает отмену как запрос на остановку.
- В распакованном global-packages отсутствуют MySqlConnector, MySql.Data, MySql.EntityFrameworkCore и Pomelo.EntityFrameworkCore.MySql. В отдельном HTTP-кэше NuGet найдены архивы MySqlConnector `2.4.0`, MySql.Data `9.3.0` и Pomelo `9.0.0`; manifest последнего требует Relational `[9.0.0, 9.0.999]`, что несовместимо с текущим EF Core 10. Проверенный индекс Pomelo датирован 2025-08-21: это не доказательство отсутствия более нового совместимого пакета. Для выбора зависимости требуется актуальная официальная metadata; restore разрешён, но в этом аналитическом проходе не выполнялся. MariaDB не объявляется поддержанной.
- Прочитаны локальные исходники установленного MySQL Workbench `modules/wb_admin_export.py` и `modules/wb_admin_export_options.py`: экспорт отдельно обрабатывает single-transaction, routines, events, triggers, GTID и CREATE DATABASE. Наличие установленного mysqldump не считается проверкой его версии, совместимости или успешного backup; executable не запускался.

Скорректированное предложение общей orchestration и отдельных реализаций SQLite/PostgreSQL/SQL Server/MySQL передано в отчёте. SQL Server native backup, внешние dump-инструменты, границы согласованности и восстановления остаются предметом согласования. Новые нормативные требования и production-код не изменены; соседние проекты не изменены. Ограничения и пропуски проверок предыдущего прохода сохраняются.

## Разрешённая проверка MySQL-зависимостей

После отдельного указания координатора выполнены `dotnet package search --exact-match` для двух кандидатов через официальный NuGet и restore двух изолированных `net10.0` проектов с прямой ссылкой на Relational `10.0.11`. Временные проекты находятся в игнорируемом `artifacts/dependency-probe`; перед restore проверены их SDK csproj, родительские build-файлы и NuGet config. Источник ограничен nuget.org, package imports отключены, `NuGetAudit=false`, `GeneratePackageOnBuild=false`; сборка и executable hooks не запускались.

- Актуальный поиск стабильных Pomelo.EntityFrameworkCore.MySql версий заканчивается `9.0.0`. Restore этой версии отклонён с `NU1608` (warning-as-error): требуется Relational `[9.0.0, 9.0.999]`, выбран `10.0.11`.
- Актуальный поиск Oracle MySql.EntityFrameworkCore включает `10.0.1`, `10.0.7`, `10.0.9`. Restore точной версии `10.0.9` успешен, без warnings/errors. Её nuspec содержит группу `net10.0` с EF Core/Relational `10.0.9` как нижней границей и MySql.Data `26.7.0`; выбранный граф содержит EF Core/Relational `10.0.11` и MySql.Data `26.7.0`, с compile/runtime assets `lib/net10.0`.
- Совместимый по metadata и restore кандидат найден: Oracle MySql.EntityFrameworkCore `10.0.9`. Downgrade EF, изменение TFM и замена EF сырым ADO.NET не нужны. Это не compile/runtime-проверка provider, migrations или backup; они не запускались. MariaDB не проверялась.
- Прочитаны README, nuspec, assets и generated imports. У MySql.EntityFrameworkCore нет собственных build props/targets; generated imports probe не подключают package targets. Временные файлы и outputs не предназначены для коммита.

Дополнительно проверен вариант без обязательной зависимости maintenance-модуля от Oracle EF provider: официальный поиск MySqlConnector и restore точной версии `2.6.2` с Relational `10.0.11` в отдельном игнорируемом probe успешны. Nuspec MySqlConnector подтверждает MIT и группу `net10.0`; assets выбрали `lib/net10.0/MySqlConnector.dll`, собственных build props/targets нет. XML-документация содержит типизированные `MySqlException.Number`, `SqlState` и `MySqlErrorCode.UnknownDatabase`. Это техническая ADO.NET-зависимость для обсуждаемого maintenance-модуля, а не замена EF CRUD приложения. Oracle restore подтверждает существование EF10-совместимого MySQL provider; выбор EF provider приложением не требует включать Oracle-пакет в обязательный граф EFCoreLibrary. Сборка и runtime-проверки не выполнялись.

## Реализация и проверка этапа 05

После явного согласования SingleInitializer, внешних dump-инструментов, scope и отсутствия автоматического удаления backup реализованы production-механизмы в соседней EFCoreLibrary. Изменены только её код и согласованные документы AgentBridge. Этап 06 не начат; AgentBridge adapter пока не подключён. После review и приёмки отдельно разрешена локальная фиксация явных файлов: EFCoreLibrary зафиксирована коммитом `a1747388ab0eb2be6da3031535fd88e7533b8df1`; изменения этой документации фиксируются отдельно. Ветки, remotes и author/email не менялись; push не выполнялся.

### Состав

- `EFCoreLibrary.csproj`: исключены maintenance/tests из root compile glob; версия CRUD `0.0.4`, EF/DI `10.0.3` сохранены. `EFCoreLibrary.sln` включает пять новых production-проектов и один test project.
- `maintenance/EFCoreLibrary.Maintenance`: общий scoped coordinator, EF migration boundary, local gate, budget, safe errors/receipt, parameterized commands, private workspace, process supervisor/recovery. Relational/Logging.Abstractions `10.0.11`.
- `.Sqlite`: Microsoft.Data.Sqlite.Core `10.0.11`, SQLitePCLRaw.core `2.1.12`; приложение поставляет обычный native runtime. Native stepper с DONE/finish и ограничением BUSY/LOCKED.
- `.PostgreSql`: Npgsql `10.0.3`, pg_dump custom выбранной БД с совпадающим major; private PGPASSFILE, escaped conninfo.
- `.SqlServer`: SqlClient `6.1.6`, server COPY_ONLY/CHECKSUM, HEADERONLY/VERIFYONLY и server artifact. ConnectRetryCount=0, обычные EngineEdition 2/3/4.
- `.MySql`: MIT MySqlConnector `2.6.2`, mysqldump выбранной InnoDB БД с routines/triggers/events. Mandatory Oracle/Pomelo зависимости нет. Частичная видимость, partial_revokes, MariaDB и mixed engines fail-fast.
- `tests/EFCoreLibrary.Maintenance.Tests`: fake boundaries без БД/native/process; `AGENTS.md` разделяют общий контракт и provider-specific инварианты. Root README описывает реальный API, зависимости, lifecycle, ограничения и recovery.

### Контракт и исправленные отказные пути

`IDatabaseMaintenance<TKey>` предоставляет inspection/update/initialize. Нет pending — нет backup. Missing не выводится из общей ошибки подключения; registered EF target сверяется с maintenance identity до pending и перед migration. EF connection удерживается до конца workflow; внешняя транзакция и EF retry strategy запрещены. Приложение обеспечивает прямой стабильный endpoint и отсутствие writes/DDL/других экземпляров; local gate не distributed lock.

Receipt проверяет operation/target/provider/format/scope, local/server kind и UTC-время в пределах операции. Poison устанавливается до освобождения gate, чтобы queued waiter не продолжил после fatal failure. Typed errors не заменяются одновременно сработавшей отменой; raw exception/credentials не попадают в логи.

SQLite CreateNew collision отделён от отказа после начала установки; явный custom Vfs отвергается до I/O. SQL Server pre-dispatch failure сохраняет код, после dispatch неизвестная остановка блокирует gate. Admin probe после 4060 отдельно классифицирует auth/permission/connectivity и не выдаёт их за Missing. MySQL partial_revokes=1 отклоняется до dump; прямые global grants используются только при выключенном partial_revokes. PostgreSQL проверяет major до CREATE через admin database.

Process handle создаётся до старта, а reader/wait setup после старта принадлежит наблюдаемым задачам. При неподтверждённой остановке handle/credentials сохраняются в singleton recovery. Файловая очистка сохраняет владельца и безопасный PrimaryError при вторичном отказе. Публикация без перезаписи защищает owner-only права и сохраняет готовый backup после Dispose. Явный recovery не сбрасывает gate; SQL Server требует отдельного доказательства остановки оператором.

### Выполненные проверки

До restore/build проверены csproj, отсутствие дополнительных локальных Directory.Build/lock hooks, NuGet sources и generated package imports. Все новые проекты исключают bin/obj/artifacts из default items. Пакетные imports — стандартные EF/Extensions/test SDK/xUnit и копирование MSAL nativeinterop; произвольные проектные scripts/hooks не запускались. Restore конкретного test csproj с официальным NuGet успешен, без pack/publish.

Финальные команды выполнялись из `D:\Media\User\source\repos\agent-bridge`:

```powershell
dotnet build 'D:\Media\User\source\repos\work\EFCoreLibrary\tests\EFCoreLibrary.Maintenance.Tests\EFCoreLibrary.Maintenance.Tests.csproj' -c Debug --no-restore -p:BaseOutputPath=artifacts\compile-check\ -p:GeneratePackageOnBuild=false -m:1 --verbosity minimal
dotnet test 'D:\Media\User\source\repos\work\EFCoreLibrary\tests\EFCoreLibrary.Maintenance.Tests\EFCoreLibrary.Maintenance.Tests.csproj' -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts\compile-check\ -p:GeneratePackageOnBuild=false -m:1 --logger 'trx;LogFileName=maintenance.trx' --results-directory 'D:\Media\User\source\repos\work\EFCoreLibrary\artifacts\test-results' --verbosity minimal
```

Результат: **7 проектов собраны, 0 warnings, 0 errors; 89 тестов passed, 0 failed, 0 skipped**. Покрыты coordinator order/modes/receipt/target, cancellation/deadline/poison race, pin lifecycle, scoped DI/CRUD/no-autostart, logger sanitization, SQLite finish/BUSY/VFS/create failures, PostgreSQL target/errors/version, MySQL rights/engines/partial revokes/flags, dump publication/failures, SQL Server server receipt/pre-dispatch/unknown VERIFYONLY, process setup/stop/recovery и private file cleanup/no-overwrite.

Тестовый процесс работал на Windows: DACL-проверка выполнена. Unix-specific mode 0600 присутствует в платформенной ветке теста, **не исполнялся** и не включается в доказательства Unix-поведения. Test runner сообщает 0 skipped, поскольку условная ветка не является отдельным skipped test.

Статическая проверка 92 исходных/документальных файлов: корректный UTF-8, без U+FFFD, mojibake, последовательностей из четырёх вопросительных знаков, trailing whitespace и битых локальных Markdown-ссылок. Корневые EFCoreLibrary AGENTS/README/csproj/sln сохранили CRLF; `git diff --check` обоих репозиториев без ошибок. Git status показывает только исходники/проекты/инструкции/документацию, без bin/obj/artifacts; новые файлы отдельно учтены, так как обычный diff --stat их ещё не включает.

### Ограничения и приёмка

Реальные БД, SQL, SQLite engine (включая in-memory), native backup, pg_dump/mysqldump, server backup/restore, применение migrations, HTTP/hosting/Docker и проектные scripts — **Пропущено по указанию пользователя**. Restore backup и реальная интеграционная совместимость провайдеров не подтверждены. Receipt не является доказательством успешного восстановления; политика retention, место, ключи и restore-проверки остаются оператору. OpenSpec CLI не запускался; спецификация проверяется статически в пределах этого этапа.

Координатор принял этап 05 по результатам review контрактов/DI/документации, сборки 7 проектов (0 warnings/errors) и 89 изолированных тестов (0 failed/skipped). Известные замечания устранены. Приёмка учитывает перечисленные пропуски реальных интеграций и невыполненную Unix-ветку; она не подтверждает реальное восстановление БД. Runtime-интеграция AgentBridge adapter не реализована и остаётся последующим этапам. Разрешены только два отдельных локальных коммита явных файлов этапа 05; этап 06 не начат.
