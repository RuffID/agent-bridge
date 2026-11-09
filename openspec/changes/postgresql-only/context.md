# Контекст

Цель — единственный PostgreSQL-модуль без выбора между драйверами при поставке. Общий EF-адаптер сохраняет направление зависимостей и не подключает драйвер сам.

Пример: приложение вызывает AddAgentBridgePostgreSql(), задаёт Database.Provider=PostgreSql и строку подключения, затем AddAgentBridge. Maintenance по-прежнему подключается отдельно; требует pg_dump и явных backup-настроек.

Ограниченная история использует консервативную оценку 256 + 4 × сумма length текстовых полей. PostgreSQL считает Unicode-символы, каждый требует не более четырёх байт UTF-8 или UTF-16. Nullable поля дают ноль; trailing spaces учитываются. Это ограничение payload, не физического размера БД или managed heap.

Неизвестные/удалённые имена и числовые значения провайдера отклоняются до I/O. Значение PostgreSql=1 сохраняется. Старые SQLite/SQL Server комплекты и потребители несовместимы; миграция их данных не выполняется.

Snapshot переносится в существующую Migrations; namespace и содержимое generated-файла сохраняются. Новая миграция и изменение схемы не требуются.

Проверка: статические ссылки, локальный restore и compile-check конкретных проектов. Приложение, БД, Docker и скрипты поставки не запускаются. Новая бинарная поставка и runtime-приёмка выполняются отдельно.

## Фактическая проверка

Локальный restore из `C:/Users/Spike/.nuget/packages` и Debug compile-check с `--no-restore`, `GeneratePackageOnBuild=false` и отдельным `BaseOutputPath=./artifacts/compile-check/` выполнены для четырёх проектов: Persistence.EfCore.Tests, Hosting.Tests, Sub2Api.Tests и Delivery.Metadata.Tests. Все четыре сборки завершились без предупреждений и ошибок. После последней правки integration fixtures повторная сборка Persistence.EfCore.Tests также завершилась с 0 предупреждений и 0 ошибок.

Статически проверены XML solution/project references, отсутствие зависимостей удалённых провайдеров в csproj, синтаксис четырёх PowerShell-упаковщиков и кодировка затронутых текстовых файлов. Содержимое PostgreSQL snapshot совпадает с исходным; namespace, BOM и CRLF сохранены.

Крупные существующие maintenance/integration тестовые файлы менялись только для удаления провайдеров и обновления связанных проверок; самостоятельные новые обязанности в них не добавлялись. Поэтому их декомпозиция не включена в эту задачу.

Тесты, PostgreSQL/SQL, Docker, приложение, external binary consumers и скрипты упаковки не выполнялись. Новый DLL/NuGet-комплект не подготовлен. Strict OpenSpec CLI validation не запускалась; изменение не архивировано. Компиляция не подтверждает runtime, PostgreSQL model differ или backup/restore.
