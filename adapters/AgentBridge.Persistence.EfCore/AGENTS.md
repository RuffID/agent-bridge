# Общее EF-хранилище

## Ответственность и границы

- Проект `AgentBridge.Persistence.EfCore.csproj` зависит от корневого `agent-bridge.csproj` и реализует его узкие порты хранения. Ядро не ссылается на хранилище; зависимости от codex-lb здесь нет.
- Область владеет persistence-моделями, маппингом, адаптацией базовых репозиториев и сценарными Unit of Work. EF-типы, tracking и `IQueryable` не выходят в Application-контракты.
- Доступ к данным строится только через EFCoreLibrary, прежде всего её базовые CRUD. Локальный ProjectReference подключает `../../../work/EFCoreLibrary/EFCoreLibrary.csproj`; Microsoft EF Core/Relational/SQLite согласованы на 10.0.11, Npgsql provider — 10.0.3. Maintenance-модули не подключены; startup относится к этапу 12.
- Один сценарный scope использует общий контекст; параллельная работа с одним контекстом запрещена. Сохранение выполняется явно на границе сценария, вне ожидания HTTP.
- Выбор SQLite/PostgreSQL, регистрация и жизненный цикл принадлежат приложению. Общие репозитории не дублируются на каждый провайдер.
- `Configuration/` содержит `DatabaseOptions`, явный nullable `DatabaseProvider` и `AddDatabaseConfiguration`. Binding/validation не подключают EFCoreLibrary или provider и не открывают БД. Отсутствующий провайдер и неизвестное значение различаются; SQLite не выбирается автоматически. Строка подключения остаётся секретом приложения и не включается в ошибки локальной проверки или UI.
- Обслуживание вызывается явно через согласованный контракт EFCoreLibrary; подключение сборки не запускает migrations, backup или очистку. Не реализовывать отсутствующий backup API обходным SQL.

## Модели и регистрация этапа 08

- `AgentBridgeDbContext` маппит только изменяемые DTO из `Models`, не Domain. Не ослаблять доменные сеттеры ради EF/IEntity. `DialogRecord` хранит отдельные IncarnationId/Revision; private Domain lifetime не сохраняется. Создание новой incarnation и атомарные guards принадлежат сценариям этапов 09–10; metadata контекста не заменяет их.
- `DialogTurnRecord` имеет ключ DialogId/Id, `ModelStepRecord` — DialogId/TurnId/Id. Эти ID локальны родителю; нельзя читать их одним baseById без predicate родителя. Канонические items имеют ключ DialogId/TurnId/Sequence. Индексы порядка и составные FK не разрешают присоединить item/step к чужому turn. Каскады удаляют согласованный набор зависимых строк.
- `DialogContextRecord` сохраняет все принятые версии; активна максимальная Version по DialogId. ThroughTurnSequence — terminal prefix, включая 0, не cutoff отдельных items. Проверку непрерывности и неубывания выполняет будущий сценарий; FK/check constraint её не доказывает. Compact имеет только Completed status; прежняя история сохраняется.
- `ModelResponseRecord` — общий required complex payload шага/compact: lifecycle, полный output, отдельные envelope/continuation и семантическая ошибка. JSON хранится text без нормализации provider; function_call_output и call_id сохраняются как полные канонические items. FormatVersion=1, неизвестный формат/повреждённые данные отклоняются явно. Не сохранять ModelAccess/API keys и не логировать канонические payload.
- Даты конвертируются в точные UTC ticks (bigint/INTEGER), чтобы сортировка expiry не зависела от ограничений SQLite DateTimeOffset и точности PostgreSQL timestamp. Ненулевое смещение отклоняется. OwnerId сравнивается BINARY/C без нормализации, не имеет искусственного MaxLength/индекса: текущие порты читают по ID. Фиксированные owner/incarnation/created/expiry защищены AfterSaveBehavior.Throw; revision/incarnation/owner/expiry имеют concurrency metadata.
- После `AddDatabaseConfiguration` приложение явно вызывает `AddAgentBridgePersistence`. Метод регистрирует scoped контекст, `AddEfCoreContext<AgentBridgeDbContext, AgentBridgeContextKey>` и `AddEfCoreBaseRepositories<AgentBridgeContextKey>`. Все base repositories используют этот scoped adapter; контекст не разделяется между параллельными задачами. SensitiveDataLogging выключен. Регистрация не реализует Application storage ports/UoW и не открывает БД.

## Документация и проверка

- XML `<summary>` самостоятельных контрактов и методов — на русском; реализации интерфейсов используют `<inheritdoc/>`.
- [Проверенные контракты EFCoreLibrary](../../Documentation/Technical%20documentation/02-efcorelibrary.md) не заменяют повторную сверку исходников при подключении библиотеки.
- Изолированные проверки находятся в `tests/AgentBridge.Persistence.EfCore.Tests`: options, DI, настоящая EF metadata обоих providers и сериализация DTO без открытия соединения. Они не доказывают relational enforcement, restart на БД или атомарность. БД, SQL, migrations, SQLite in-memory/EF InMemory и процессы не запускать.
- Все restore/build/test с ProjectReference EFCoreLibrary выполнять с `-p:GeneratePackageOnBuild=false`, без pack/publish. Проверять конкретные проекты, не весь solution.
- При изменении границ сохранения, зависимостей или регистрации обновлять этот документ.
