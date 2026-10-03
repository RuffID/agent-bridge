# Проверка БД, бэкап, миграции и очистка

## Принятое решение

AgentBridge использует тот же функциональный порядок обслуживания, что AquaByte-Ledger: проверить подключение, определить pending migrations, создать резервную копию существующей БД, применить миграции. Ошибка подключения, backup или migration не маскируется и не превращается в успешный startup.

Пользователь выбрал развитие EFCoreLibrary для необходимых backup-возможностей SQLite/PostgreSQL. AgentBridge вызывает её контракт обслуживания; SQL Server backup-код и прямое соединение в обход библиотеки не копируются.

Подключающее приложение явно вызывает инициализацию/обновление БД перед использованием агента. Очистка истёкших диалогов предоставляется библиотекой и вызывается приложением по расписанию. Подключение DLL само по себе не запускает миграции или фоновую задачу.

## Проверенные классы AquaByte-Ledger

Статическая проверка исходников выполнена 2026-10-03.

| Класс | Текущее поведение | Применение в AgentBridge |
| --- | --- | --- |
| `DataBaseCheckUpService<TContext, TContextKey>` | Использует `IAppDbContext<TContextKey>`, проверяет подключение и pending migrations; backup выполняется до `Database.Migrate()` | Сохранить последовательность и fail-fast через контракт EFCoreLibrary |
| `BackupService<TContext>` | Открывает `SqlConnection`, выполняет SQL Server `BACKUP DATABASE` | Функциональный образец backup, но не готовая реализация SQLite/PostgreSQL |
| `IBackupService<TContext>` | Содержит SQL Server-специфичный `CreateSqlServerBackup()` | Будущий общий контракт обслуживания должен учитывать выбранный provider |
| `BackupFilePathBuilder` | Формирует `.bak` из entry assembly и timestamp с точностью до секунды | Отдельная обязанность формирования пути, без SQL Server формата по умолчанию |
| `IBackupFilePathBuilder` | Отделяет формирование пути от создания backup | Сохранить разделение обязанностей |

В регистрации AquaByte-Ledger пути привязаны к MSSQL: `AppContext.BaseDirectory/Backups` на Windows и `/var/opt/mssql/backups` на Linux. В универсальную библиотеку эти provider-specific пути не переносятся как общие значения.

## EFCoreLibrary

Текущий `IAppDbContext<TContextKey>` уже предоставляет `DatabaseFacade`. Это позволяет использовать EF migrations через библиотечный adapter. Базовые delete-репозитории и `SaveChangesAsync` уже достаточны для удаления строк диалогов.

Готового provider-independent backup API в проверенных исходниках нет. Требуемое расширение принадлежит EFCoreLibrary и реализуется на последующем этапе, после согласования контрактов. В документации не объявляется уже существующим новый интерфейс библиотеки.

Сценарий очистки использует базовое чтение по сроку истечения и `Delete`/`DeleteRange` через сценарный UoW. Custom query не становится предпочтительным только потому, что операция называется обслуживанием.

## Порядок обслуживания

Для существующей БД:

1. Проверить подключение и совместимость выбранного provider.
2. Определить pending migrations только для схемы AgentBridge.
3. При наличии изменений создать и подтвердить успешную резервную копию через EFCoreLibrary.
4. Применить миграции AgentBridge.
5. Вернуть приложению результат проверки, backup и обновления.

Для первой установки требуется явный путь инициализации. Старый `CanConnect() == false → ошибка` сам по себе не создаёт новую БД. Отсутствующая БД и ошибка подключения различаются; backup ещё не существующей БД не имитируется.

Внедрённая библиотека не применяет миграции произвольной схемы приложения. Перед миграцией нескольких экземпляров приложения требуется согласованное единоличное выполнение, а не параллельные `backup → migrate`.

## Резервная копия

Backup должен быть согласованной копией выбранного provider. Простое копирование активного SQLite-файла не принимается как универсальная замена provider API, поскольку база может работать с WAL. SQL Server-команда не подходит PostgreSQL.

Путь, формат и срок хранения backup принадлежат конфигурации обслуживания. Имя файла должно исключать коллизии одновременных запусков; один timestamp до секунды из исходного примера не обеспечивает этого.

Backup является отдельной копией данных. Удаление истёкшего диалога из рабочей БД не изменяет старые backup-файлы. Они не учитываются в мягком пороге диалога и не восстанавливаются автоматически во время обычного продолжения агента.

## Ошибки и диагностика

Обслуживание поддерживает cancellation и конечный deadline. В журнал Serilog через `ILogger<T>` попадают стадия, статус и безопасные метаданные. Строки подключения и содержимое backup не логируются.

Ошибка backup при обязательном backup перед migration останавливает применение migration. Ошибка удаления не выдаётся за очищенный диалог. Удаление и позднее сохранение результата согласуются с актуальностью диалога.

## Источники

- [DataBaseCheckUpService](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs)
- [BackupService](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/BackupService.cs)
- [BackupFilePathBuilder](../../../work/AquaByte-Ledger/AquaByteLedger.Infrastructure/Services/DataBase/BackupFilePathBuilder.cs)
- [EFCoreLibrary: IAppDbContext](../../../work/EFCoreLibrary/Abstractions/Database/IAppDbContext.cs)
- [EFCoreLibrary: DeleteItemRepository](../../../work/EFCoreLibrary/EfCore/Repository/Base/DeleteItemRepository.cs)

Связанные документы: [EFCoreLibrary](02-efcorelibrary.md), [хранение и удаление](<../Business logic/04-storage-and-retention.md>).
