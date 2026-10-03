# Общее EF-хранилище

## Ответственность и границы

- Проект `AgentBridge.Persistence.EfCore.csproj` зависит от корневого `agent-bridge.csproj` и реализует его узкие порты хранения. Ядро не ссылается на хранилище; зависимости от codex-lb здесь нет.
- Область владеет persistence-моделями, маппингом, адаптацией базовых репозиториев и сценарными Unit of Work. EF-типы, tracking и `IQueryable` не выходят в Application-контракты.
- Доступ к данным строится только через EFCoreLibrary, прежде всего её базовые CRUD. Локальный ProjectReference подключает `../../../work/EFCoreLibrary/EFCoreLibrary.csproj`; Microsoft EF Core/Relational/SQLite согласованы на 10.0.11, Npgsql provider — 10.0.3. Maintenance-модули не подключены; startup относится к этапу 12.
- Один сценарный scope использует общий контекст; параллельная работа с одним контекстом запрещена. Сохранение выполняется явно на границе сценария, вне ожидания HTTP.
- Выбор SQLite/PostgreSQL, регистрация и жизненный цикл принадлежат приложению. Общие репозитории не дублируются на каждый провайдер.
- Runtime задаёт MigrationsAssembly по AgentBridgeMigrationsAssemblies.SQLITE/POSTGRESQL. Отдельные provider проекты ссылаются сюда, обратных ProjectReference нет. При обслуживании схемы приложение поставляет DLL выбранного migrations проекта; отсутствие DLL не маскировать fallback на общий контекст. Design-time factories находятся в provider проектах; startup/maintenance здесь ещё не подключены.
- Runtime и обе factories явно задают AgentBridgeMigrationsHistory.TABLE_NAME (__AgentBridgeMigrationsHistory), чтобы не смешивать ledger с контекстом приложения. Это служебная таблица EF, не шестая mapped entity; provider history repository управляет ею при явном обслуживании. Смена history options не требует изменения generated модели или повторной генерации.
- `Configuration/` содержит `DatabaseOptions`, явный nullable `DatabaseProvider` и `AddDatabaseConfiguration`. Binding/validation не подключают EFCoreLibrary или provider и не открывают БД. Отсутствующий провайдер и неизвестное значение различаются; SQLite не выбирается автоматически. Строка подключения остаётся секретом приложения и не включается в ошибки локальной проверки или UI.
- Обслуживание вызывается явно через согласованный контракт EFCoreLibrary; подключение сборки не запускает migrations, backup или очистку. Не реализовывать отсутствующий backup API обходным SQL.

## Модели и регистрация этапа 08

- `AgentBridgeDbContext` маппит только изменяемые DTO из `Models`, не Domain. Не ослаблять доменные сеттеры ради EF/IEntity. `DialogRecord` хранит отдельные IncarnationId/Revision; private Domain lifetime не сохраняется. Создание новой incarnation и атомарные guards реализуют сценарии этапа 10; metadata контекста не заменяет их.
- `DialogTurnRecord` имеет ключ DialogId/Id, `ModelStepRecord` — DialogId/TurnId/Id. Эти ID локальны родителю; нельзя читать их одним baseById без predicate родителя. Канонические items имеют ключ DialogId/TurnId/Sequence. Индексы порядка и составные FK не разрешают присоединить item/step к чужому turn. Каскады удаляют согласованный набор зависимых строк.
- `DialogContextRecord` сохраняет все принятые версии; активна максимальная Version по DialogId. ThroughTurnSequence — terminal prefix, включая 0, не cutoff отдельных items. Проверку непрерывности и неубывания выполняет Domain внутри write UoW; FK/check constraint её не доказывает. Compact имеет только Completed status; прежняя история сохраняется.
- `ModelResponseRecord` — общий required complex payload шага/compact: lifecycle, полный output, отдельные envelope/continuation и семантическая ошибка. JSON хранится text без нормализации provider; function_call_output и call_id сохраняются как полные канонические items. FormatVersion=1, неизвестный формат/повреждённые данные отклоняются явно. Не сохранять ModelAccess/API keys и не логировать канонические payload.
- Даты конвертируются в точные UTC ticks (bigint/INTEGER), чтобы сортировка expiry не зависела от ограничений SQLite DateTimeOffset и точности PostgreSQL timestamp. Ненулевое смещение отклоняется. OwnerId сравнивается BINARY/C без нормализации, не имеет искусственного MaxLength/индекса: текущие порты читают по ID. Фиксированные owner/incarnation/created/expiry защищены AfterSaveBehavior.Throw; revision/incarnation/owner/expiry имеют concurrency metadata.
- После `AddDatabaseConfiguration` приложение явно вызывает `AddAgentBridgePersistence`. Метод регистрирует scoped контекст, `AddEfCoreContext<AgentBridgeDbContext, AgentBridgeContextKey>`, `AddEfCoreBaseRepositories<AgentBridgeContextKey>`, адаптеры `Repositories`, read ports и сценарные write UoW. Все base repositories используют этот scoped adapter; контекст не разделяется между параллельными задачами. SensitiveDataLogging выключен. Технические scope/session/gate также scoped; регистрация БД не открывает. Границы сохранения — в `UnitOfWork/AGENTS.md`.

## Адаптеры этапа 09

- `Repositories/*RecordQueries` используют base ById только для глобального dialog ID; turn/step predicates включают всех родителей. Сортировка Sequence/Version и expiry/ID задаётся через base include до take; custom query не нужен. Reads по умолчанию no-tracking, tracking для root/turn допускается только внутри короткого сценария записи.
- `RecordStaging<TEntity>` делегирует одиночные и пакетные create/update/delete базовым контрактам. Методы возвращают void и не вызывают SaveChanges/transactions. Это инфраструктурная адаптация DTO, не Application repository port или UoW. Не использовать её для будущих row mutations в обход Domain: этап 10 координирует проверки/доменные операции и общий scope.
- `Reading/DialogReader` проверяет owner ordinal до детей, фиксирует primitive owner/incarnation/revision/metadata и повторно читает root после последовательных base reads. Изменение owner/жизни/версии или удаление возвращает отказ без snapshot и без retry. Orphan items/steps и незавершённый принятый compact отклоняются явно; история не отбрасывается по prefix. Это защита чтения, не транзакционный снимок или atomic write guard.
- `ExpiredDialogReader` возвращает только ограниченные кандидаты с точным ExpiresAtUtc <= nowUtc. Удаление одного кандидата и повторная атомарная проверка реализованы этапом 10; batch orchestration остаётся этапу 22; расписание принадлежит приложению.

## Документация и проверка

- XML `<summary>` самостоятельных контрактов и методов — на русском; реализации интерфейсов используют `<inheritdoc/>`.
- [Проверенные контракты EFCoreLibrary](../../Documentation/Technical%20documentation/02-efcorelibrary.md) не заменяют повторную сверку исходников при подключении библиотеки.
- Изолированные проверки находятся в `tests/AgentBridge.Persistence.EfCore.Tests`: options, DI, настоящая EF metadata обоих providers, сериализация DTO и base repository fakes с управляемыми interleavings без открытия соединения. Они не доказывают relational enforcement, restart на БД или атомарность. БД, SQL, migrations, SQLite in-memory/EF InMemory и процессы не запускать.
- Все restore/build/test с ProjectReference EFCoreLibrary выполнять с `-p:GeneratePackageOnBuild=false`, без pack/publish. Проверять конкретные проекты, не весь solution.
- При изменении границ сохранения, зависимостей или регистрации обновлять этот документ.
