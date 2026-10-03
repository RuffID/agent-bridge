## ADDED Requirements

### Requirement: Раздельные миграции выбранного провайдера

AgentBridge MUST предоставлять независимые SQLite/PostgreSQL migrations assemblies и snapshots для одного общего AgentBridgeDbContext. Runtime и design-time MUST выбирать одну и ту же устойчивую identity по provider. Эти проекты MUST владеть только таблицами AgentBridge и MUST NOT добавлять host или зависимости в Domain/Application. Design-time factory MUST создавать контекст без открытия соединения, SQL, применения схемы или чтения секретов приложения.

Runtime и design-time MUST явно выбирать отдельную служебную историю `__AgentBridgeMigrationsHistory` и MUST NOT использовать общий ledger `__EFMigrationsHistory` подключающего приложения. Служебная история EF MUST оставаться отдельной от пяти mapped таблиц диалога и MUST NOT добавляться как persistence entity в модель AgentBridge.

#### Scenario: Изолированная история миграций

- **WHEN** SQLite или PostgreSQL options создаются runtime регистрацией либо design-time factory
- **THEN** provider history repository получает имя __AgentBridgeMigrationsHistory
- **AND** выбор истории не меняет snapshot или схему пяти mapped таблиц.

#### Scenario: Создание модели для SQLite

- **WHEN** tooling использует SQLite target/startup проект
- **THEN** factory создаёт общий AgentBridgeDbContext с SQLite и соответствующей migrations assembly
- **AND** модель содержит только собственные persistence DTO AgentBridge.

#### Scenario: Создание модели для PostgreSQL

- **WHEN** tooling использует PostgreSQL target/startup проект
- **THEN** factory создаёт общий AgentBridgeDbContext с PostgreSQL и отдельной migrations assembly
- **AND** общий runtime выбирает эту же assembly identity.

#### Scenario: Сохранение принятых границ схемы

- **WHEN** создаётся provider-specific модель
- **THEN** сохраняются составные keys/FK, cascade, UTC ticks, BINARY/C collation и expiry/Id index
- **AND** owner-list index и таблицы подключающего приложения не добавляются.
