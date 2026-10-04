# Первоначальная реализация AgentBridge

## Checkpoint24 — принят координатором

Блок19–23 реализован, проверен, принят и закоммичен. HEAD23=`7e9d533d80393bd85b08e1b17567038e12ec8a16`, parent22=`89c28e839bf12584027b81494c31557c91279935`,21=`6482d595cb1f89eeb30ec33696026aa6c1bde1cc`,20=`95c28fae3773efa9e8e4d0281282340f5ac9e6c0`,19=`de58342f5c80e46279e1d7b59fe0665c893c5d4b`. [Отчёт23](23-cross-component-verification.md) сохраняет историческую передачу; пауза24 снята ранее, пользователь поручил координатору проверить24 и продолжить25; координатор независимо принял24. [Отчёт24 и полный manifest](24-dll-delivery.md): DLL SQLite/PostgreSQL для win-x64, два внешних бинарных compile-check,8 isolated metadata/XML tests passed. **24 принят координатором; локальный commit manifest24 разрешён, hash — в git log и итоговом ответе.25 этому исполнителю не поручен.** OpenSpec CLI отсутствует, validation не выполнена, changes не архивированы.

Статус: **00–23 приняты и закоммичены;24 реализован, проверен в пределах compile/metadata и принят координатором;25 передаётся отдельному исполнителю**. Платформа: **.NET 10**, подключение **DLL**. Evidence23:32 actual DB (28 новых cross-component +4 existing rollback/start acknowledgement),25 isolated metadata/DI/design-time;0 failed/0 skipped. Эти наборы в24 не повторялись и с8 новыми metadata cases не суммируются. Runtime/native загрузка поставки24 не выполнялась. Production/schema/root csproj не менялись; в slnx24 добавлен только новый документ в solution items.

План описывает реализацию согласованной библиотеки агента небольшими этапами. Создание плана не разрешает писать код. Спорные контракты библиотек обсуждаются с пользователем до выбора обходного решения или изменения соседней библиотеки.

Текущая EFCoreLibrary — `0.0.5`: удалён `ICopyable<TEntity>`, maintenance namespace соответствуют папкам. Импорты production, test doubles и Integration приведены к текущим исходникам; [контракты](<../../Technical documentation/02-efcorelibrary.md>) и [примеры maintenance](<../../Technical documentation/06-database-maintenance.md>) обновлены. Версия `0.0.4` в отчёте предыдущего интеграционного запуска ниже историческая; результаты того запуска не подтверждают версию `0.0.5`. В этапе14 DB checks не повторялись: изменён только JSON transport/Application parameters/docs. На историческом checkpoint14 были разрешены приёмка и локальный коммит; следующие этапы тогда ещё не начинались. Текущий статус18 приведён выше.

## Проверка совместимости с EFCoreLibrary 0.0.5

Проверка 2026-10-04: конкретные проекты `AgentBridge.Persistence.EfCore`, `AgentBridge.Persistence.Migrations.Sqlite`, `AgentBridge.Persistence.Migrations.PostgreSql` и `AgentBridge.Persistence.EfCore.Tests` собраны в Debug без предупреждений и ошибок. Test build включает исходники Integration. Соседние исходники и generated migrations не изменены; ProjectReference, CRUD-регистрация, Domain/Application, транзакционные границы и concurrency сохранены.

Из корня agent-bridge выполнены:

```powershell
dotnet restore tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -p:GeneratePackageOnBuild=false -p:NuGetAudit=false --source https://api.nuget.org/v3/index.json --verbosity minimal
# Для каждого из четырёх конкретных .csproj выше, без сборки solution:
dotnet build <project.csproj> -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ --filter 'Dependency!=Database' --logger 'trx;LogFileName=efcorelibrary-005-isolated.trx' --results-directory artifacts\test-results\efcorelibrary-005 --verbosity minimal
```

По фактическим class traits обе Integration test classes имеют `Dependency=Database`; filtered discovery не включает их. Изолированный набор: **164 passed / 0 failed / 0 skipped**. `dotnet format style --no-restore --verify-no-changes --diagnostics IDE0005 --severity info` проверил все C#-файлы EF-адаптера и persistence-тестов с обращениями к EFCoreLibrary; неиспользуемых using нет. Старые maintenance imports отсутствуют, изменённые файлы сохраняют UTF-8 без BOM и LF.

Интеграционные тесты, приложения, БД/SQL, Docker, native backup, dump-процессы, pack/publish и Git-операции не выполнялись. Исторический интеграционный отчёт ниже относится к предыдущему запуску и не заменяет эту проверку версии 0.0.5.

## Источники

- [Бизнес-логика](<../../Business logic/README.md>)
- [Техническая документация](<../../Technical documentation/README.md>)
- [Нормативные требования](../../../openspec/specs/agent-runtime/spec.md)
- [Инструкции проекта](../../../AGENTS.md)

## Навигация по этапам

| Этап | Результат | Зависимости |
| --- | --- | --- |
| [00 — Проверка исходных контрактов](00-contract-baseline.md) | Проверенные API и контракты зависимостей | — |
| [01 — Основа решения и правила документации](01-solution-foundation.md) | Границы проектов .NET 10 и локальные инструкции | 00 |
| [02 — Конфигурация и начальные значения](02-configuration-and-defaults.md) | Типизированные настройки и проверенные пробные значения | 01 |
| [03 — Подключение Serilog](03-serilog-integration.md) | Безопасная диагностика операций и DI-связь с pipeline приложения; завершён и принят | 01, 02 |
| [04 — Логирование HttpClientLibrary](04-httpclientlibrary-logging.md) | Завершён и принят; запрещённые проверки пропущены. Default-safe/JsonStructure, ограниченный error contract; 44 теста на каждом net8/net10 | 00, 03 |
| [05 — Обслуживание через EFCoreLibrary](05-efcorelibrary-maintenance.md) | Реализовано и принято; запрещённые проверки пропущены. Общий контракт и SQLite/PostgreSQL/SQL Server/MySQL; 89 изолированных тестов | 00 |
| [06 — Доменное состояние диалога](06-dialog-domain-state.md) | Реализован и принят; запрещённые проверки пропущены: владение, фиксированный срок, статусы, версии; 66 тестов ядра, 30 новых | 01, 02 |
| [07 — Контракты прикладного слоя](07-application-ports.md) | Реализован и принят; запрещённые проверки пропущены: независимые порты, lifecycle, canonical snapshots и storage guards; 93 теста ядра, 27 новых | 06 |
| [08 — Модели хранения и контекст](08-persistence-models.md) | Реализован и принят; запрещённые проверки пропущены: persistence DTO/EF metadata, полный payload и DI; 34 теста; без БД | 05, 06, 07 |
| [09 — Адаптеры базовых репозиториев](09-base-repository-adapters.md) | Реализован и принят; запрещённые проверки пропущены: base read/staging CRUD, полный защищённый read path; 71 тест | 08 |
| [10 — Сценарные Unit of Work](10-scenario-unit-of-work.md) | Реализован и принят; запрещённые проверки пропущены. Write UoW, Domain Restore, 107 core/108 persistence tests | 09 |
| [11 — Первые миграции провайдеров](11-initial-provider-migrations.md) | Реализован и принят; запрещённые проверки пропущены. Generated артефакты созданы; 114 persistence tests до history isolation, после — 6 адресных тестов | 08, 10 |
| [12 — Инициализация БД и бэкап](12-database-startup-and-backup.md) | Реализован и принят; запрещённые проверки пропущены. Явный EFCoreLibrary API, backup options; 164 persistence tests, 50 новых | 05, 11 |
| [13 — Каталог моделей и выбор ключа](13-model-catalog-and-keys.md) | Реализован и принят; 121 core + 61 CodexLb tests. Историческая пауза после приёмки13 сохранена | 02, 04, 07 |
| [14 — JSON-адаптер Responses](14-responses-json-adapter.md) | Реализован и принят координатором; локальный коммит разрешён, CLI validation не выполнена; 136 CodexLb + 121 core tests | 04, 07, 13 |
| [15 — SSE-адаптер Responses](15-responses-sse-adapter.md) | Реализован и принят координатором; локальный коммит разрешён. Transport191/0/0, cleanup regression7/0/0; CLI validation не выполнена | 14 |
| [16 — Сборка контекста](16-context-composition.md) | Реализован и проверен, принят координатором: ordered ModelRequest, исходные роли/полные пары, prefix/tail/guards; 86 affected tests | 07, 14 |
| [17 — Токенизация для модели](17-model-tokenizer.md) | Реализован и принят координатором: offline BPE/exact mapping/known-null estimate/guard; 189 affected tests, 71 новых; локальный коммит31 файлов разрешён | 13, 16 |
| [18 — Сжатие контекста](18-context-compaction.md) | Реализовано и принято координатором: ограниченное сжатие terminal prefix, version-aware save, UnknownBudget | 10, 14, 17 |
| [19 — Инструменты приложения](19-application-tools.md) | Принят и закоммичен de58342f5c80e46279e1d7b59fe0665c893c5d4b; durable recovery реализуется20 | 07, 14, 16 |
| [20 — Координация обращения к агенту](20-agent-turn-orchestration.md) | Принят и закоммичен 95c28fae3773efa9e8e4d0281282340f5ac9e6c0 | 10, 15, 18, 19 |
| [21 — Настройки и состояние диалога](21-settings-and-dialog-status.md) | Реализован, принят и закоммичен6482d595: per-dialog CAS, pinned snapshot, safe status, opaque compatibility | 13, 17, 20 |
| [22 — Очистка истёкших диалогов](22-expired-dialog-cleanup.md) | Принят и закоммичен89c28e8: bounded app вызов, separate scopes, honest partial/cancel/unknown, cascade без revival | 10, 20, 21 |
| [23 — Проверка взаимодействия компонентов](23-cross-component-verification.md) | Принят и закоммичен7e9d533;32 DB/25 isolated, пауза снята только для24 | 12, 20, 21, 22 |
| [24 — Поставка DLL](24-dll-delivery.md) | Подготовлен и проверен: два комплекта/compile-only потребителя,8 metadata tests; принят координатором, локальный commit разрешён | 23 |
| [25 — Руководство и завершение плана](25-usage-guide-and-closure.md) | НЕ НАЧАТ; проверенные примеры README и итоговая документация | 24 |

## Правила работы

- Ограничивать изменения одним этапом и его указанными предварительными работами.
- Добавлять или обновлять ближайший владеющий `AGENTS.md` при создании областей проекта или изменении архитектурных границ.
- Документировать классы, интерфейсы и методы XML comments на русском. Реализации интерфейсных контрактов используют `<inheritdoc/>`; при неоднозначности указывать `cref`.
- Использовать базовые операции чтения/создания/изменения/удаления EFCoreLibrary прежде custom query. Исходящий HTTP выполнять через HttpClientLibrary.
- Обновлять затронутые спецификации, бизнес-документацию и техническую документацию при изменении реализованного поведения.
- Проверять каждый этап на соответствующей границе продукта, включая ошибки и отмену. Не добавлять проверки, которые только повторяют детали реализации.
- Соблюдать разрешения на выполнение. Статический анализ и изолированные проверки проводить там, где это разрешено; БД, бэкап провайдера, процессы, приложения и реальные сетевые проверки требуют явного разрешения.
- Не отмечать этап завершённым только потому, что код написан. Записывать, что проверено и что осталось непроверенным.

## Дополнительный интеграционный запуск 2026-10-04

Дата: **2026-10-04, Asia/Novosibirsk**. Пользователь отдельно разрешил тестовые БД, Docker/PostgreSQL, SQL, migrations/rollback, реальные backup/restore, необходимые тестовые процессы, изменения тестов/плана и локальный коммит. Это проверка уже реализованных этапов 00–13, а не возобновление реализации или начало этапа 23. Исторические отчёты и первоначальные пропуски ниже в файлах этапов сохранены; их строки «этап N не начат» относятся к дате первоначального отчёта. Таблица навигации выше сохраняет сведения первоначальной приёмки; текущие дополнительные доказательства находятся в этом разделе и датированных дополнениях этапов.

### Инвентаризация пропусков и применимость

До запуска прочитаны отчёты 00–13, применимые AGENTS.md, нормативный spec, фактические registration/models/read/write/migrations/maintenance/catalog исходники и контракты соседних библиотек. codex-lb, TelegramCodexRelayBot и обслуживание AquaByte-Ledger использованы только как источники для статической сверки; их приложения не запускались.

| Исторически пропущенная граница | Применимость к 00–13 | Дополнительный фактический результат |
| --- | --- | --- |
| DLL/DI/options/Serilog и чистый Domain/Application | 00–03, 06–07 | Повторная сборка текущих цепочек и 121 тест ядра; 0 warnings/errors |
| HTTP и библиотечная диагностика | 04, 13: только каталог реализован | 61 тест адаптера через настоящий HttpClientLibrary и локальный HttpMessageHandler; библиотека: 44 net8 + 44 net10. Живой сервер не проверялся |
| Реальный SQLite engine / PostgreSQL provider / query translation / CRUD / restart | 08–10 | Полный payload всех четырёх lifecycle, новая root-DI регистрация, порядок, bytes, fixed expiry, FK/unique/check/NOT NULL, cascade и локальные IDs проверены на обоих провайдерах |
| Owner/expiry/incarnation/revision guards и CAS | 06–10 | Публичные порты отклоняют чужого владельца, точный expiry, stale token, удалённый/пересозданный ID; настоящий root UPDATE с устаревшим original revision откатывает детей |
| Relational transactions / rollback / conflicts | 10 | Отказ после настоящего SQL SaveChanges до commit оставляет схему/данные неизменными и очищает tracker. Два отдельных Serializable контекста не сохраняют двух победителей; driver busy/serialization не подменяются ожидаемым Conflict |
| Generated migrations Up/Down и ledger isolation | 11 | Реальные Up → Down до 0 → Up; host __EFMigrationsHistory сохраняется, AgentBridge ledger содержит только migration выбранной assembly; HasPendingModelChanges=false |
| Initialize/update/inspect/native backup/pg_dump/restore | 05, 12 | Missing/initialize/already-exists/no-pending, backup до DDL, полный backup, отдельная restore-БД, сопоставление схемы и всех данных, реальные отказы backup/DDL/auth/major/executable, общий poisoned gate |
| Responses/SSE, composition/tokenizer/tools/AgentRunner/cleanup scheduling/DLL delivery | Реализации 14–25 отсутствуют | Не запускались; основная реализация остаётся на паузе |
| SQL Server/MySQL, живой codex-lb/OpenAI/Telegram/hosting | Не входят в разрешённую инфраструктуру этого запуска | Не проверялись; SQLite/PostgreSQL и HTTP handler не являются их подтверждением |

### Окружение и зависимости

- Windows `10.0.26200`, `win-x64`; .NET SDK `10.0.401`, MSBuild `18.9.11`, .NET runtime `10.0.12`; net8 runner использует установленный .NET 8. Restore — официальный `https://api.nuget.org/v3/index.json`.
- Существующие root csproj/slnx сохранены. Проверены конкретные csproj, ancestor Directory.Build/Directory.Packages/NuGet/lock и package imports: custom executable hooks нет; HttpClientLibrary Directory.Build.props задаёт только тестовые output/intermediate paths. Адресные Build, без Rebuild/pack/publish. **Во всех restore/build/test передано `-p:GeneratePackageOnBuild=false`.**
- Фактические ProjectReference: `../work/EFCoreLibrary` (CRUD `0.0.4`, общий maintenance и SQLite/PostgreSQL модули); `../work/HttpClientLibrary` (FileVersion `0.0.0.5`, net8/net10). EF/Relational/SQLite adapter `10.0.11`, Npgsql provider `10.0.3`. Никаких замен этих библиотек тестовой реализацией доступа к БД или HTTP.
- Docker Desktop `4.93.0`, Engine `29.8.1`, `desktop-linux`; официальный `postgres:18`, сервер **18.6 (Debian 18.6-1.pgdg13+2)**. Полученный digest: `sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722`.
- Утилиты найдены через Windows uninstall registry в **`D:\Programs\PostgreSQL\18\bin`**, хотя PATH/Program Files их не содержали. `pg_dump.exe --version`, `pg_restore.exe --version`, `psql.exe --version` вернули **18.6**. Они совместимы с major 18; установка программ и изменение библиотек не понадобились.
- Контейнер **`abverify-d0dd4eb11c6e4129b46f714a89adefd1`**, label `agentbridge-verification=abverify_d0dd4eb11c6e4129b46f714a89adefd1`. Публикация **`127.0.0.1:63752 → 5432`**. Данные — новый tmpfs `/var/lib/postgresql`, 1 GiB. Docker volumes, bind mounts и собственные сети не создавались; чужие ресурсы не изменялись.
- Уникальные PostgreSQL БД/пользователь `abverify_<GUID>` и случайный пароль только этой задачи. SQLite — отдельные обычные файлы на каждый case, pooling=false, FK=true. Конфигурация/credentials и дампы размещались только в игнорируемом `artifacts/integration-abverify_d0dd4eb11c6e4129b46f714a89adefd1`, затем удалены.

### Фактические команды

Рабочий каталог: `D:\Media\User\source\repos\agent-bridge`. Инфраструктурные команды (переменные указывали только на созданные задачей значения; секреты не публикуются):

```powershell
docker version --format '{{json .}}'
docker info --format '{{.OSType}} {{.ServerVersion}}'
dotnet --info
Get-Command docker,dotnet,openspec,pg_dump,pg_restore,psql -ErrorAction SilentlyContinue
& 'D:\Programs\PostgreSQL\18\bin\pg_dump.exe' --version
& 'D:\Programs\PostgreSQL\18\bin\pg_restore.exe' --version
& 'D:\Programs\PostgreSQL\18\bin\psql.exe' --version
docker pull postgres:18
docker run --detach --name $container --label "agentbridge-verification=$runId" --env-file $envFile --publish '127.0.0.1::5432' --tmpfs '/var/lib/postgresql:rw,size=1073741824' postgres:18
docker exec abverify-d0dd4eb11c6e4129b46f714a89adefd1 pg_isready -U postgres -d postgres
```

Первый pg_isready сразу после создания ещё не видел сервер; повторный вернул `accepting connections`. Стартовый probe не выдаётся за проверку БД-функциональности.

Для каждого `<project>` из таблицы ниже **последовательно** выполнены эти точные restore/build команды; solution не собирался:

```powershell
dotnet restore <project> -p:GeneratePackageOnBuild=false --source https://api.nuget.org/v3/index.json --verbosity minimal
dotnet build <project> -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --verbosity minimal
```

| `<project>` | Restore / итоговый compile-check |
| --- | --- |
| `tests\AgentBridge.Tests\AgentBridge.Tests.csproj` | Успех / 0 warnings, 0 errors |
| `tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj` | Успех / 0 warnings, 0 errors |
| `tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj` | Успех / 0 warnings, 0 errors; обе migrations DLL и реальные EFCoreLibrary references собраны |
| `D:\Media\User\source\repos\work\EFCoreLibrary\tests\EFCoreLibrary.Maintenance.Tests\EFCoreLibrary.Maintenance.Tests.csproj` | Успех / 0 warnings, 0 errors; семь проектов |
| `D:\Media\User\source\repos\work\HttpClientLibrary\HttpClientLibrary.Tests\HttpClientLibrary.Tests.csproj` | Успех / 0 warnings, 0 errors; оба TFM |

Точные runner-команды:

```powershell
dotnet test tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --logger 'trx;LogFileName=core.trx' --results-directory artifacts\test-results\integration-20261004 --verbosity minimal
dotnet test tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --logger 'trx;LogFileName=codexlb.trx' --results-directory artifacts\test-results\integration-20261004 --verbosity minimal
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'Dependency!=Database' --logger 'trx;LogFileName=persistence-unit.trx' --results-directory artifacts\test-results\integration-20261004 --verbosity minimal
dotnet test 'D:\Media\User\source\repos\work\EFCoreLibrary\tests\EFCoreLibrary.Maintenance.Tests\EFCoreLibrary.Maintenance.Tests.csproj' -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --logger 'trx;LogFileName=efcorelibrary-unit.trx' --results-directory artifacts\test-results\integration-20261004 --verbosity minimal
dotnet test 'D:\Media\User\source\repos\work\HttpClientLibrary\HttpClientLibrary.Tests\HttpClientLibrary.Tests.csproj' -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -p:TestTfmsInParallel=false -m:1 --logger 'trx;LogFilePrefix=httpclientlibrary-unit' --results-directory artifacts\test-results\integration-20261004 --verbosity minimal
```

В отдельном shell для интеграций прочитан созданный задачей `run.json` (после очистки отсутствует) и установлены только эти test env vars:

```powershell
$run = Get-Content -Raw -Encoding UTF8 'artifacts\integration-abverify_d0dd4eb11c6e4129b46f714a89adefd1\run.json' | ConvertFrom-Json
$env:AGENTBRIDGE_INTEGRATION = '1'
$env:AGENTBRIDGE_INTEGRATION_ROOT = $run.Root
$env:AGENTBRIDGE_POSTGRES_CONNECTION = $run.Connection
$env:AGENTBRIDGE_PG_DUMP = 'D:\Programs\PostgreSQL\18\bin\pg_dump.exe'
$env:AGENTBRIDGE_PG_RESTORE = 'D:\Programs\PostgreSQL\18\bin\pg_restore.exe'
$env:AGENTBRIDGE_PG_MAJOR = '18'
dotnet test tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts\compile-check\ -m:1 --filter 'Dependency=Database' --logger 'trx;LogFileName=integration-final.trx' --results-directory artifacts\test-results\integration-20261004 --verbosity minimal
```

Установка env vars не заменяет разрешение на интеграции. Helpers fail-fast проверяют отдельный loopback endpoint/пользователя/порт/каталог; при включении набора отсутствующая конфигурация не даёт скрытый skip.

### Результаты и обнаруженные ограничения

| Финальный набор | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| AgentBridge core | 121 | 0 | 0 |
| AgentBridge CodexLb (local handler) | 61 | 0 | 0 |
| AgentBridge persistence isolated | 164 | 0 | 0 |
| Новые integration: SQLite 18 + PostgreSQL 21 | 39 | 0 | 0 |
| EFCoreLibrary maintenance isolated | 89 | 0 | 0 |
| HttpClientLibrary net8 | 44 | 0 | 0 |
| HttpClientLibrary net10 | 44 | 0 | 0 |
| **Итого финальных наборов** | **562** | **0** | **0** |

Не суммируются повторные запуски одних и тех же cases. В первом integration run `integration-first.trx`: **32 passed / 4 failed / 0 skipped**, 36 cases. Ошибки находились в тестовой организации/ожиданиях: общий двух-reader барьер несовместим с SQLite BEGIN IMMEDIATE; PostgreSQL transient/serialization error имеет дополнительную EF exception-обёртку; maintenance после CRUD в том же scope нарушает контракт выделенного maintenance scope и получает ConnectionFailed после сокрытия пароля Npgsql connection; отсутствующий executable даёт Configuration до запуска процесса. Production-код и библиотеки не исправлялись. Исправлены только тесты: SQLite конкурирует на двух worker tasks без read-barrier, PostgreSQL barrier сохраняется; проверяется реальный вложенный SQLSTATE; обслуживание восстановленной БД выполняется в отдельном scope; expected error согласован с фактическим API.

Адресный повтор после исправления (те же test options, TRX `integration-repaired.trx`, filter `FullyQualifiedName~SimultaneousWritersCannotBothCommit|FullyQualifiedName~FullBackupRestoresSchemaAndDataIntoSeparateDatabase|FullyQualifiedName~PostgreSqlMissingExecutableStopsUpdate`) — **5 passed / 0 failed / 0 skipped**. После добавления реальных root-PK/fixed-fields и corrupted restore проверок выполнен полный финальный набор — **39/39**. Ошибок production в пределах проверенных поддержанных сценариев не обнаружено; это не доказательство отсутствия дефектов за их границами.

Schema/data fingerprints сравнивают live каталоги колонок/индексов/check/FK и все строки всех пользовательских/служебных таблиц отдельно восстановленной БД, а не только факт открытия или receipt. Полный backup дополнительно читается через IDialogReader; SHA-256 и длина опубликованного артефакта проверяются физически. SQLite backup/restore использует настоящий native API EFCoreLibrary; PostgreSQL dump — её provider/PGPASSFILE/process pipeline, restore — штатный pg_restore через её process runner. Для Down применяется штатный IMigrator из DatabaseFacade библиотечного IUnitOfWorkContext: coordinator downgrade API не предоставляет. Технический SQL наблюдения/подготовки отказов использует IDatabaseCommands; он не заменяет прикладные CRUD/UoW.

### Непроведённые проверки

- **SQL Server/MySQL:** реальные серверы/backup/restore не запускались; в разрешении инфраструктуры указан только PostgreSQL и временные SQLite-файлы. 89 fake-boundary tests EFCoreLibrary не являются runtime-проверкой этих провайдеров.
- **Живые HTTP/codex-lb/OpenAI/Telegram и hosting:** явно не разрешены. 61 адаптерный + 88 библиотечных HTTP cases используют local handler/streams, без сервера, аккаунтов и токенов.
- **Этапы 14–25:** реализации отсутствуют или не начаты; общий agent turn, Responses/SSE/compact transport, tokenizer, batch cleanup, delivery и приложения не проверялись и не начали реализовываться.
- **OpenSpec CLI:** `Get-Command openspec -ErrorAction SilentlyContinue` не нашёл CLI. Валидация не выполнена и не отмечается успешной; установка не предпринималась, существующие changes не архивировались.
- **Дополнительные deployment/fault условия:** PostgreSQL VerifyFull/CA, Unix ACL, multi-instance SingleInitializer, реальная потеря соединения во время commit, disk-full/OS permission failures и неподтверждённое убийство dump-процесса не проверены этим локальным запуском. Их controlled failure cases остаются изолированными; Windows loopback/tmpfs не воспроизводит эти условия. Backup retention/purge и расписание принадлежат приложению и не реализованы библиотекой. Восстановимость подтверждена только для созданных здесь тестовых БД/данных.

### Очистка и локальная фиксация

Все fixtures завершили cleanup: перед удалением контейнера `SELECT count(*) FROM pg_database WHERE datname LIKE 'abverify_%'` вернул **0**; подкаталогов fixtures/SQLite-файлов/дампов не осталось. После проверки label/digest выполнены:

```powershell
docker rm --force abverify-d0dd4eb11c6e4129b46f714a89adefd1
docker image rm postgres:18
Remove-Item -LiteralPath 'D:\Media\User\source\repos\agent-bridge\artifacts\integration-abverify_d0dd4eb11c6e4129b46f714a89adefd1\container.env','D:\Media\User\source\repos\agent-bridge\artifacts\integration-abverify_d0dd4eb11c6e4129b46f714a89adefd1\run.json'
Remove-Item -LiteralPath 'D:\Media\User\source\repos\agent-bridge\artifacts\integration-abverify_d0dd4eb11c6e4129b46f714a89adefd1'
```

Контейнер и скачанный этой задачей образ удалены, временный каталог отсутствует (`Test-Path=False`); фильтры Docker по task label и `postgres:18` пусты. Рекурсивная shell-очистка была отклонена автоматической проверкой; вместо неё успешно удалены два явно перечисленных собственных файла и уже пустой каталог. Чужие контейнеры/сети/volumes не изменялись. TRX и compile outputs оставлены в игнорируемом artifacts, в Git не добавляются.

Изменения этой задачи ограничены тестами, ближайшими тестовыми инструкциями и существующими файлами этого плана 00–13/README. Сразу после тестов соседние библиотеки имели чистый git status. При заключительной проверке в EFCoreLibrary появились сторонние незакоммиченные изменения: 42 maintenance-файла перемещены с побайтово неизменным содержимым относительно HEAD `a1747388ab0eb2be6da3031535fd88e7533b8df1`, `Abstractions/Entity/ICopyable.cs` удалён; эти изменения не выполнялись этой задачей и в её коммит не включаются. HttpClientLibrary остаётся чистой. Финальный адресный persistence compile-check после последних правок тестовых комментариев успешен: 0 warnings/errors, команда та же, что в таблице выше. Результаты TRX относятся к исходникам на момент выполненных тестов; новый общий аудит сторонних изменений не проводился.

Production AgentBridge, generated migrations, root csproj/slnx, branch `master`, Git identity и статус паузы сохранены. Обычный/staged diff проверяются перед локальным коммитом; его фактический hash возвращается в итоговом сообщении, собственный будущий hash не записывается заранее в план. Remotes, отправка кода/артефактов, push/PR и архивирование changes не выполняются.
