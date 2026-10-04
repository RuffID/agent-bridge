## MODIFIED Requirements

### Requirement: Чтение настроек и выбор модели

AgentBridge MUST предоставлять безопасное представление текущих настроек конкретного диалога, включая effective модель, effort, лимиты, token истории и отдельную версию выбора из того же read snapshot. Выбор model/effort MUST сохраняться в БД AgentBridge для конкретного диалога. Приоритет MUST быть: override обращения → сохранённый выбор диалога → defaults приложения. Override MUST NOT менять сохранённый выбор. Выбор MUST проверяться текущим каталогом доступного ключа; недопустимые значения MUST NOT заменяться скрытым fallback. Секреты, инструкции, строки подключения, headers, raw envelope и canonical содержимое MUST NOT возвращаться в safe settings/status.

#### Scenario: Изменение усилия запроса

- **WHEN** приложение указывает допустимый effort конкретного обращения
- **THEN** он имеет приоритет над сохранённым effort диалога и default приложения
- **AND** выбранное значение используется в этом обращении без изменения уже выполняющихся запросов.

### Requirement: Раздельные миграции выбранного провайдера

AgentBridge MUST предоставлять независимые SQLite/PostgreSQL migrations assemblies и snapshots для одного общего AgentBridgeDbContext. Runtime и design-time MUST выбирать одну и ту же устойчивую identity по provider. Эти проекты MUST владеть только таблицами AgentBridge и MUST NOT добавлять host или зависимости в Domain/Application. Design-time factory MUST создавать контекст без открытия соединения, SQL, применения схемы или чтения секретов приложения.

Runtime и design-time MUST явно выбирать отдельную служебную историю `__AgentBridgeMigrationsHistory` и MUST NOT использовать общий ledger `__EFMigrationsHistory` подключающего приложения. Служебная история EF MUST оставаться отдельной от mapped таблиц диалога и MUST NOT добавляться как persistence entity в модель AgentBridge.

#### Scenario: Изолированная история миграций

- **WHEN** SQLite или PostgreSQL options создаются runtime регистрацией либо design-time factory
- **THEN** provider history repository получает имя __AgentBridgeMigrationsHistory
- **AND** выбор истории не меняет snapshot или схему mapped таблиц.

#### Scenario: Создание модели для выбранного провайдера

- **WHEN** tooling использует SQLite или PostgreSQL target/startup проект
- **THEN** factory создаёт общий AgentBridgeDbContext с выбранным provider и его отдельной migrations assembly
- **AND** runtime выбирает ту же assembly identity.

#### Scenario: Сохранение принятых границ схемы

- **WHEN** создаётся provider-specific модель
- **THEN** сохраняются только собственные таблицы, составные keys/FK, cascade, UTC ticks, BINARY/C collation и expiry/Id index
- **AND** owner-list index и таблицы подключающего приложения не добавляются.

## ADDED Requirements

### Requirement: Независимая версия выбора и снимок хода

DialogSettings MUST иметь собственную CAS Version, связанную с диалогом каскадным удалением. Запись MUST проверять owner, expiry, incarnation, исходную revision истории и expected settings version в коротком сценарном UoW через EFCoreLibrary. Она MUST NOT изменять revision истории, даты или ContentBytes содержимого. Устаревший token/version MUST давать Conflict без refresh/retry; неожиданные driver/commit/cleanup ошибки MUST NOT маскироваться Conflict. BeginWithSettings MUST атомарно сохранять immutable primitive snapshot model/effort/входного окна/порога/запаса. Активный ход MUST продолжать использовать его после нового выбора; новый выбор MUST действовать со следующего обращения. Historical turn settings и selected compact provenance MUST оставаться nullable; миграции MUST NOT выдумывать исходный выбор. Settings/snapshot/provenance MUST NOT учитываться как ContentBytes содержимого.

#### Scenario: Смена выбора при активном ходе

- **WHEN** после начала хода приложение сохраняет новый выбор с актуальной отдельной версией
- **THEN** текущий ход сохраняет прежние model/effort и действующий token истории
- **AND** следующий ход использует новый выбор, а запись со старой settings version получает Conflict.

#### Scenario: Исторические строки после Down и Up

- **WHEN** AddDialogSettings удаляется и применяется повторно на тестовой БД с историей
- **THEN** история, полный compact envelope, fixed dates и ContentBytes сохраняются
- **AND** удалённые settings/snapshot/selected provenance возвращаются null, без подмены server именем.

### Requirement: Подтверждение совместимости непрозрачного контекста

AgentBridge MUST проверять активное compact окно и непокрытый хвост перед сменой выбранной модели и перед началом хода. Наличие opaque MUST определяться отдельным model-independent inspector; ошибка tokenizer mapping MUST NOT служить доказательством opaque. Для opaque другой или неизвестной исходной selected модели MUST требоваться явное подтверждение порта приложения. Без подтверждения MUST возвращаться Unsupported без изменения выбора или истории. Порт MUST получать исходные selected model, фактическую server model при наличии и новый проверенный выбор раздельно. Равенство server/selected имён MUST NOT доказывать совместимость. Повторяющиеся canonical occurrences MUST сохраняться; report output MUST NOT проверяться повторно как input без своих server metadata.

#### Scenario: Историческое opaque окно

- **WHEN** selected provenance неизвестна и server model совпадает с новым выбором
- **THEN** без подтверждения приложения возвращается Unsupported
- **AND** исходный контекст и сохранённый выбор остаются неизменными.

#### Scenario: Только текст и неизвестное tokenizer mapping

- **WHEN** каталог разрешает модель и сохранённое содержимое полностью текстовое
- **THEN** выбор допустим без compatibility порта
- **AND** недоступная токенизация отдельно возвращается ошибкой оценки, без объявления текста opaque.

### Requirement: Безопасный статус сохранённого диалога

Статус MUST возвращать token, CreatedAtUtc/ExpiresAtUtc, истечение при fresh now >= expiry, ContentBytes, мягкий порог и достижение, число принятых compact, safe settings/сохранённый выбор, selected model/effort отдельно от последней server model генерации, known tokens, nullable full estimate и nullable достижение token threshold. Отказ каталога, несовместимость или неизвестная оценка MUST возвращаться отдельно от доступных метаданных и MUST запрещать CanContinue. Истёкший диалог до удаления MUST сохранять доступные метаданные статуса. Оценка MUST относиться только к сохранённому рабочему input без transient providers/new input/tools и MUST NOT подтверждать бюджет полного следующего запроса. Known tokens MUST NOT подменять неизвестную полную оценку. Мягкий порог MUST NOT удалять историю или запрещать запись.

#### Scenario: Истечение с неизвестным бюджетом

- **WHEN** чтение статуса достигает ExpiresAtUtc и содержит opaque без полной оценки
- **THEN** возвращаются срок, байты и known tokens, full estimate и threshold indicator остаются null
- **AND** IsExpired=true и CanContinue=false; история не очищается.

#### Scenario: Последний ответ без server model

- **WHEN** последний report генерации не содержит model
- **THEN** ServerModel=null независимо от выбранной модели и предыдущих report
- **AND** raw envelope и секреты не возвращаются.
