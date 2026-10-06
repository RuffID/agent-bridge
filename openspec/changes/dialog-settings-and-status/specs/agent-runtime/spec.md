## MODIFIED Requirements

### Requirement: Чтение настроек и выбор модели

AgentBridge MUST предоставлять безопасное представление текущих настроек конкретного диалога, включая effective модель, effort, лимиты, token истории и отдельную версию выбора из того же read snapshot. Выбор model/effort MUST сохраняться в БД AgentBridge для конкретного диалога.

#### Scenario: Изменение усилия запроса

- **WHEN** приложение указывает допустимый effort конкретного обращения
- **THEN** он имеет приоритет над сохранённым effort диалога и default приложения
- **AND** выбранное значение используется в этом обращении без изменения уже выполняющихся запросов.

#### Scenario: Проверка правила — Чтение настроек и выбор модели

- **WHEN** приложение читает настройки конкретного диалога
- **THEN** безопасный snapshot включает persisted model/effort и отдельную settings version.

### Requirement: Приоритет и проверка выбора модели

Приоритет MUST быть: override обращения → сохранённый выбор диалога → defaults приложения. Override MUST NOT менять сохранённый выбор. Выбор MUST проверяться текущим каталогом доступного ключа; недопустимые значения MUST NOT заменяться скрытым fallback.

#### Scenario: Проверка правила — Приоритет и проверка выбора модели

- **WHEN** обращение задаёт допустимый effort override
- **THEN** override выше persisted/default, но сохранённый выбор не меняется и fallback не используется.

### Requirement: Секреты вне settings и status

Секреты, инструкции, строки подключения, headers, raw envelope и canonical содержимое MUST NOT возвращаться в safe settings/status.

#### Scenario: Проверка правила — Секреты вне settings и status

- **WHEN** приложение запрашивает safe settings/status
- **THEN** секреты и canonical содержимое не возвращаются.

### Requirement: Раздельные миграции выбранного провайдера

AgentBridge MUST предоставлять независимые SQLite/PostgreSQL migrations assemblies и snapshots для одного общего AgentBridgeDbContext. Runtime и design-time MUST выбирать одну и ту же устойчивую identity по provider. Эти проекты MUST владеть только таблицами AgentBridge и MUST NOT добавлять host или зависимости в Domain/Application. Design-time factory MUST создавать контекст без открытия соединения, SQL, применения схемы или чтения секретов приложения.

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

#### Scenario: Проверка правила — Раздельные миграции выбранного провайдера

- **WHEN** design-time factory создаёт выбранный provider context
- **THEN** assembly identity совпадает с runtime, соединение и host не запускаются.

### Requirement: Отдельная служебная история миграций

Runtime и design-time MUST явно выбирать отдельную служебную историю `__AgentBridgeMigrationsHistory` и MUST NOT использовать общий ledger `__EFMigrationsHistory` подключающего приложения. Служебная история EF MUST оставаться отдельной от mapped таблиц диалога и MUST NOT добавляться как persistence entity в модель AgentBridge.

#### Scenario: Проверка правила — Отдельная служебная история миграций

- **WHEN** runtime и design-time выбирают ledger
- **THEN** используется __AgentBridgeMigrationsHistory отдельно от mapped сущностей и ledger приложения.

## ADDED Requirements

### Requirement: Независимая версия выбора и снимок хода

DialogSettings MUST иметь собственную CAS Version, связанную с диалогом каскадным удалением. Запись MUST проверять owner, expiry, incarnation, исходную revision истории и expected settings version в коротком сценарном UoW через EFCoreLibrary. Она MUST NOT изменять revision истории, даты или ContentBytes содержимого. Устаревший token/version MUST давать Conflict без refresh/retry; неожиданные driver/commit/cleanup ошибки MUST NOT маскироваться Conflict.

#### Scenario: Смена выбора при активном ходе

- **WHEN** после начала хода приложение сохраняет новый выбор с актуальной отдельной версией
- **THEN** текущий ход сохраняет прежние model/effort и действующий token истории
- **AND** следующий ход использует новый выбор, а запись со старой settings version получает Conflict.

#### Scenario: Исторические строки после Down и Up

- **WHEN** AddDialogSettings удаляется и применяется повторно на тестовой БД с историей
- **THEN** история, полный compact envelope, fixed dates и ContentBytes сохраняются
- **AND** удалённые settings/snapshot/selected provenance возвращаются null, без подмены server именем.

#### Scenario: Проверка правила — Независимая версия выбора и снимок хода

- **WHEN** выбор сохраняется со stale settings version
- **THEN** возвращается Conflict без retry и изменения revision истории, driver failure не маскируется.

### Requirement: Атомарный snapshot выполняющегося хода

BeginWithSettings MUST атомарно сохранять immutable primitive snapshot model/effort/входного окна/порога/запаса. Активный ход MUST продолжать использовать его после нового выбора; новый выбор MUST действовать со следующего обращения.

#### Scenario: Проверка правила — Атомарный snapshot выполняющегося хода

- **WHEN** UI меняет выбор после BeginWithSettings
- **THEN** активный ход сохраняет прежний snapshot, следующий получает новый выбор.

### Requirement: Nullable provenance и ContentBytes metadata

Historical turn settings и selected compact provenance MUST оставаться nullable; миграции MUST NOT выдумывать исходный выбор. Settings/snapshot/provenance MUST NOT учитываться как ContentBytes содержимого.

#### Scenario: Проверка правила — Nullable provenance и ContentBytes metadata

- **WHEN** исторический turn не имеет selected provenance
- **THEN** null сохраняется без выдуманного выбора, metadata не увеличивают ContentBytes.

### Requirement: Подтверждение совместимости непрозрачного контекста

AgentBridge MUST проверять активное compact окно и непокрытый хвост перед сменой выбранной модели и перед началом хода. Наличие opaque MUST определяться отдельным model-independent inspector; ошибка tokenizer mapping MUST NOT служить доказательством opaque.

#### Scenario: Историческое opaque окно

- **WHEN** selected provenance неизвестна и server model совпадает с новым выбором
- **THEN** без подтверждения приложения возвращается Unsupported
- **AND** исходный контекст и сохранённый выбор остаются неизменными.

#### Scenario: Только текст и неизвестное tokenizer mapping

- **WHEN** каталог разрешает модель и сохранённое содержимое полностью текстовое
- **THEN** выбор допустим без compatibility порта
- **AND** недоступная токенизация отдельно возвращается ошибкой оценки, без объявления текста opaque.

#### Scenario: Проверка правила — Подтверждение совместимости непрозрачного контекста

- **WHEN** неизвестный tokenizer mapping встречается при смене модели
- **THEN** отдельный inspector проверяет окно и хвост; ошибка mapping не доказывает opaque.

### Requirement: Подтверждение приложения для opaque модели

Для opaque другой или неизвестной исходной selected модели MUST требоваться явное подтверждение порта приложения. Без подтверждения MUST возвращаться Unsupported без изменения выбора или истории.

#### Scenario: Проверка правила — Подтверждение приложения для opaque модели

- **WHEN** opaque имеет другую или неизвестную исходную selected модель
- **THEN** без подтверждения возвращается Unsupported и выбор с историей не меняются.

### Requirement: Раздельная provenance и canonical occurrences

Порт MUST получать исходные selected model, фактическую server model при наличии и новый проверенный выбор раздельно. Равенство server/selected имён MUST NOT доказывать совместимость. Повторяющиеся canonical occurrences MUST сохраняться; report output MUST NOT проверяться повторно как input без своих server metadata.

#### Scenario: Проверка правила — Раздельная provenance и canonical occurrences

- **WHEN** server model совпадает с новой selected моделью
- **THEN** равенство не подтверждает совместимость; occurrences и report metadata сохраняются.

### Requirement: Безопасный статус сохранённого диалога

Статус MUST возвращать token, CreatedAtUtc/ExpiresAtUtc, истечение при fresh now >= expiry, ContentBytes, мягкий порог и достижение, число принятых compact, safe settings/сохранённый выбор, selected model/effort отдельно от последней server model генерации, known tokens, nullable full estimate и nullable достижение token threshold.

#### Scenario: Истечение с неизвестным бюджетом

- **WHEN** чтение статуса достигает ExpiresAtUtc и содержит opaque без полной оценки
- **THEN** возвращаются срок, байты и known tokens, full estimate и threshold indicator остаются null
- **AND** IsExpired=true и CanContinue=false; история не очищается.

#### Scenario: Последний ответ без server model

- **WHEN** последний report генерации не содержит model
- **THEN** ServerModel=null независимо от выбранной модели и предыдущих report
- **AND** raw envelope и секреты не возвращаются.

#### Scenario: Проверка правила — Безопасный статус сохранённого диалога

- **WHEN** приложение читает статус существующего диалога
- **THEN** snapshot возвращает safe metadata, known tokens и nullable full estimate раздельно.

### Requirement: Метаданные статуса при отказе и expiry

Отказ каталога, несовместимость или неизвестная оценка MUST возвращаться отдельно от доступных метаданных и MUST запрещать CanContinue. Истёкший диалог до удаления MUST сохранять доступные метаданные статуса.

#### Scenario: Проверка правила — Метаданные статуса при отказе и expiry

- **WHEN** диалог истёк либо каталог или оценка отказали
- **THEN** доступные метаданные сохраняются, CanContinue не разрешён.

### Requirement: Граница сохранённого status budget

Оценка MUST относиться только к сохранённому рабочему input без transient providers/new input/tools и MUST NOT подтверждать бюджет полного следующего запроса. Known tokens MUST NOT подменять неизвестную полную оценку. Мягкий порог MUST NOT удалять историю или запрещать запись.

#### Scenario: Проверка правила — Граница сохранённого status budget

- **WHEN** transient providers отсутствуют в сохранённом input
- **THEN** status estimate не доказывает полный следующий request, soft bytes не удаляет историю.
