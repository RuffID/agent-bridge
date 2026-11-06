## MODIFIED Requirements

### Requirement: Явное подключение обслуживания AgentBridge

EF-адаптер AgentBridge MUST регистрировать scoped `IDatabaseMaintenance<AgentBridgeContextKey>` через общий coordinator EFCoreLibrary и выбирать SQLite/PostgreSQL/SQL Server по DatabaseOptions без automatic fallback. Перегрузки AddAgentBridgeDatabaseMaintenance MUST принимать раздел IConfiguration либо Action<DatabaseBackupOptions> без аргумента режима. Приложение MUST обеспечивать единственного исполнителя обслуживания.

#### Scenario: Подключение без обслуживания

- **WHEN** приложение регистрирует и разрешает maintenance сервис в отдельном scope
- **THEN** доступен API InspectAsync/UpdateExistingAsync/InitializeNewAsync выбранного provider
- **AND** никакая операция обслуживания не начинается до явного вызова приложения.

#### Scenario: Подключение с текущим API EFCoreLibrary

- **WHEN** приложение выбрало SQLite, PostgreSQL или SQL Server и передало настройки backup без аргумента режима
- **THEN** scoped maintenance зарегистрирован через общий coordinator с единым gate на root container.
