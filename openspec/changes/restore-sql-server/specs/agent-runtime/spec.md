## MODIFIED Requirements

### Requirement: Автономный комплект DLL

Поставка AgentBridge MUST содержать ядро, Integration facade, CodexLb-адаптер, общее EF-хранилище, migrations assembly выбранного PostgreSQL или SQL Server и полную SDK runtime closure для явно указанного RID, включая EFCoreLibrary/HttpClientLibrary, tokenizer data и native assets. Состав MUST фиксироваться manifest с версиями и SHA256. Поставка MUST NOT требовать путей к дереву исходников или NuGet-публикации AgentBridge.

#### Scenario: Компиляция бинарного потребителя

- **WHEN** .NET10-потребитель и комплект скопированы за пределы дерева исходников
- **THEN** потребитель компилируется только с бинарными ссылками на комплект, без ProjectReference и PackageReference
- **AND** runtime/native файлы доставляются в output стандартной сборкой; compile-check не объявляется доказательством их загрузки.

### Requirement: Платформенная поставка клиента

PostgreSQL и SQL Server комплекты MUST поддерживать win-x64/linux-x64/linux-arm64 как RID приложения-клиента с установленным .NET10 runtime. SDK-selected statically referenced managed/native dependencies и culture resources MUST сохраняться целиком без Windows fallback на Linux или flattening satellites.

#### Scenario: Linux ARM64 клиент

- **WHEN** комплект PostgreSQL или SQL Server linux-arm64 собирается и consumer компилируется на Windows
- **THEN** selected native assets имеют ELF64 AArch64, Windows assets отсутствуют
- **AND** actual Linux ARM64 runtime остаётся отдельной проверкой.

### Requirement: Границы compile-only поставки

Compile/metadata checks MUST NOT объявляться runtime/native loading, AOT/trimming/single-file или подтверждением PostgreSQL/SQL Server Linux runtime.

#### Scenario: Проверка ELF заголовка

- **WHEN** isolated test прочитал ELF64 AArch64 без загрузки библиотеки
- **THEN** подтверждена metadata-архитектура, actual native/runtime acceptance не заявляется.

### Requirement: Явные внешние требования поставки

Документация MUST описывать PostgreSQL и SQL Server, их отдельные migrations assemblies, RID/native runtime, .NET10 runtime и внешние PostgreSQL dump-утилиты. SQL Server MUST использовать отдельный server backup directory без требования pg_dump. Она MUST сохранять явную конфигурацию provider, SingleInitializer и отсутствие автоматического обслуживания при подключении DLL.

#### Scenario: PostgreSQL backup

- **WHEN** приложение выбирает PostgreSQL maintenance
- **THEN** оно отдельно предоставляет pg_dump с согласованным major, абсолютными путями и конечным cleanup timeout; наличие DLL не заменяет утилиту или права сервера.

### Requirement: Раздельные миграции выбранного провайдера

AgentBridge MUST предоставлять отдельные PostgreSQL и SQL Server migrations assemblies и snapshots для одного общего AgentBridgeDbContext. Runtime и design-time MUST выбирать одну и ту же устойчивую identity выбранного provider. Snapshot и migrations MUST размещаться непосредственно в папке Migrations проекта; перенос snapshot MUST сохранять содержимое, namespace и migration IDs. Эти проекты MUST владеть только таблицами AgentBridge и MUST NOT добавлять host или зависимости в Domain/Application. Design-time factory MUST создавать контекст без открытия соединения, SQL, применения схемы или чтения секретов приложения.

#### Scenario: Изолированная история миграций

- **WHEN** PostgreSQL или SQL Server options создаются runtime регистрацией либо design-time factory
- **THEN** provider history repository получает имя __AgentBridgeMigrationsHistory
- **AND** выбор истории не меняет snapshot или схему mapped таблиц.

#### Scenario: Создание модели для выбранного провайдера

- **WHEN** tooling использует PostgreSQL или SQL Server target/startup проект
- **THEN** factory создаёт общий AgentBridgeDbContext с выбранным provider и его отдельной migrations assembly
- **AND** runtime выбирает ту же assembly identity.

#### Scenario: Сохранение принятых границ схемы

- **WHEN** создаётся provider-specific модель
- **THEN** сохраняются только собственные таблицы, составные keys/FK, единственные cascade paths, UTC ticks, ordinal owner (collation C для PostgreSQL, varbinary(max) UTF-16 для SQL Server) и expiry/Id index
- **AND** owner-list index и таблицы подключающего приложения не добавляются.

#### Scenario: Проверка правила — Раздельные миграции выбранного провайдера

- **WHEN** design-time factory создаёт выбранный provider context
- **THEN** assembly identity совпадает с runtime, соединение и host не запускаются.

### Requirement: Настраиваемое хранение диалогов

AgentBridge MUST сохранять диалоги и необходимое состояние контекста в БД. Провайдер БД и параметры подключения MUST задаваться конфигурацией подключающего приложения. PostgreSQL и SQL Server MUST поддерживаться отдельными явно подключаемыми модулями. DatabaseProvider.PostgreSql MUST сохранять числовое значение 1, SqlServer — 2. SQLite=0 и неизвестные имена/значения MUST отклоняться до I/O без fallback. Ядро MUST NOT зависеть от конкретной БД.

#### Scenario: Выбор БД

- **WHEN** приложение настраивает PostgreSQL или SQL Server как хранилище AgentBridge
- **THEN** диалоги сохраняются через выбранный провайдер
- **AND** ядро не получает зависимости от конкретного provider.

### Requirement: Формат общего EF-хранилища

Persistence-модель MUST отделяться от доменного агрегата и MUST сохранять владельца, фиксированные даты, incarnation/revision, порядок обращений и items.

#### Scenario: Сохранение результата инструмента и envelope

- **WHEN** сериализуется история с function_call_output и отдельным результатом шага
- **THEN** полные неизвестные поля и call_id остаются в соответствующих канонических данных
- **AND** envelope и continuation остаются отдельно от следующего input.

#### Scenario: Выбор общего контекста

- **WHEN** приложение явно выбирает PostgreSQL или SQL Server
- **THEN** DI регистрирует один scoped-контекст через AddEfCoreContext и AddEfCoreBaseRepositories
- **AND** регистрация не открывает БД, не запускает migrations и не выбирает провайдер при отсутствии настройки.

#### Scenario: Проверка правила — Формат общего EF-хранилища

- **WHEN** агрегат сериализуется в persistence DTO
- **THEN** owner, fixed dates, incarnation/revision, порядок и items сохранены отдельно от Domain.

### Requirement: Явное подключение обслуживания AgentBridge

EF-адаптер AgentBridge MUST регистрировать scoped `IDatabaseMaintenance<AgentBridgeContextKey>` через общий coordinator EFCoreLibrary и подключать PostgreSQL либо SQL Server по DatabaseOptions без automatic fallback. Перегрузки AddAgentBridgeDatabaseMaintenance MUST принимать раздел IConfiguration либо Action<DatabaseBackupOptions> без аргумента режима. Приложение MUST обеспечивать единственного исполнителя обслуживания.

#### Scenario: Подключение без обслуживания

- **WHEN** приложение регистрирует и разрешает maintenance сервис в отдельном scope
- **THEN** доступен API InspectAsync/UpdateExistingAsync/InitializeNewAsync выбранного provider
- **AND** никакая операция обслуживания не начинается до явного вызова приложения.

#### Scenario: Подключение с текущим API EFCoreLibrary

- **WHEN** приложение выбрало PostgreSQL или SQL Server и передало настройки backup без аргумента режима
- **THEN** scoped maintenance зарегистрирован через общий coordinator с единым gate на root container.

### Requirement: Явные настройки backup

AgentBridge MUST требовать положительный backup retention без default. PostgreSQL MUST требовать абсолютный локальный backup directory. SQL Server MUST требовать отдельный абсолютный SqlServerBackupDirectory на сервере БД независимо от ОС приложения и MUST NOT требовать PostgreSQL backup-параметры. PostgreSQL MUST требовать абсолютный pg_dump path, server major10+ и конечный положительный cleanup timeout. Настройки MUST проверяться локально до I/O без раскрытия значений.

#### Scenario: Срок backup не выбран

- **WHEN** приложение не задало срок хранения backup
- **THEN** локальная валидация отклоняет настройки до обслуживания
- **AND** срок хранения диалогов не используется как запасное значение.

#### Scenario: Проверка правила — Явные настройки backup

- **WHEN** backup retention не задан или pg_dump settings недопустимы
- **THEN** локальная проверка отклоняет настройки до I/O без раскрытия значений.

### Requirement: Backup и migrations через EFCoreLibrary

AgentBridge MUST предоставлять вызываемую приложением операцию обслуживания схемы: check → backup существующей БД при pending migrations → migrate. Backup выбранного PostgreSQL или SQL Server MUST строиться на контракте EFCoreLibrary; собственный backup в AgentBridge MUST NOT служить заменой этой зависимости. Ошибка обязательного backup MUST останавливать migration. Подключение DLL MUST NOT само по себе запускать обслуживание.

#### Scenario: Обновление существующей БД

- **WHEN** обслуживание обнаруживает pending migrations схемы AgentBridge
- **THEN** успешный backup через EFCoreLibrary предшествует применению migrations
- **AND** ошибка backup не скрывается продолжением migration.

### Requirement: Подтверждение backup и владение ресурсами

EFCoreLibrary MUST предоставлять расширяемый реляционный контракт и отдельные optional реализации SQLite, PostgreSQL, SQL Server и MySQL. AgentBridge MUST подключать выбранный PostgreSQL или SQL Server модуль этой библиотеки.

#### Scenario: Неизвестное завершение backup

- **WHEN** остановка внешнего процесса или уже отправленной server command не подтверждена
- **THEN** библиотека не выдаёт подтверждённый receipt, запрещает migration и блокирует gate
- **AND** неподтверждённые процессы и используемые ими приватные файлы сохраняют владельца до явного recovery
- **AND** recovery временных ресурсов не сбрасывает блокировку обслуживания автоматически.

#### Scenario: Ошибка и отмена

- **WHEN** обслуживание завершается ошибкой или фактической отменой
- **THEN** наружу и в лог передаются безопасные коды без исходного driver message, credentials и connection string
- **AND** уже установленная typed failure не заменяется одновременно сработавшим cancellation/deadline
- **AND** вторичная неопределённость cleanup сохраняет безопасный первичный код и блокирует повтор.

#### Scenario: Проверка правила — Подтверждение backup и владение ресурсами

- **WHEN** приложение подключает AgentBridge provider
- **THEN** явно подключается выбранный PostgreSQL или SQL Server модуль EFCoreLibrary.

### Requirement: Явная bounded history materialization

Opt-in PostgreSQL или SQL Server history policy MUST задавать явные record/record-byte/query limits без defaults. Supplied reader и внутренние runner/write/lifecycle/recovery/compact loaders MUST применять scalar preflight и server payload predicate/Take до full materialization всех необходимых canonical/provenance rows.

#### Scenario: Отказ bounded materialization и strict capability

- **WHEN** применяется strict history policy либо её лимиты превышены
- **THEN** Overflow MUST возвращать safe refusal без truncated model history, изменения read state, automatic retry/refill/replay или implicit нового диалога. Strict runner MUST требовать registered capability до history/providers/model и сохранять actual full payload budget guard, authorization/root/profile/retention/tombstone/CAS/incarnation/fencing и function-pair/recovery semantics. Policy MUST документировать per-operation payload bounds отдельно от CLR heap, server scan/work и SQL/provider acceptance.

#### Scenario: Existing oversized history

- **WHEN** размер строк либо их число превосходит явно заданную policy
- **THEN** preflight ограничен scalar rows и payload этой query не загружается
- **AND** reader не выдаёт partial snapshot, canonical state остаётся неизменным, runner не делает replay/new chat.

#### Scenario: Payload изменился после preflight

- **WHEN** записи выросли между scalar и payload queries
- **THEN** payload сохраняет server byte/count bounds и mismatch возвращает conflict
- **AND** final root/profile checks не раскрывают изменившуюся историю; retry/refill отсутствуют.
