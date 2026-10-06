## ADDED Requirements

### Requirement: Формат общего EF-хранилища

Persistence-модель MUST отделяться от доменного агрегата и MUST сохранять владельца, фиксированные даты, incarnation/revision, порядок обращений и items.

#### Scenario: Сохранение результата инструмента и envelope

- **WHEN** сериализуется история с function_call_output и отдельным результатом шага
- **THEN** полные неизвестные поля и call_id остаются в соответствующих канонических данных
- **AND** envelope и continuation остаются отдельно от следующего input.

#### Scenario: Выбор общего контекста

- **WHEN** приложение явно выбирает SQLite или PostgreSQL
- **THEN** DI регистрирует один scoped-контекст через AddEfCoreContext и AddEfCoreBaseRepositories
- **AND** регистрация не открывает БД, не запускает migrations и не выбирает SQLite при отсутствии настройки.

#### Scenario: Проверка правила — Формат общего EF-хранилища

- **WHEN** агрегат сериализуется в persistence DTO
- **THEN** owner, fixed dates, incarnation/revision, порядок и items сохранены отдельно от Domain.

### Requirement: Хранение полного model и compact report

Полный результат каждого шага модели и принятого compact MUST сохраняться отдельно от канонических items истории, включая lifecycle, output, envelope, continuation и ожидаемую ошибку. Результат compact MUST иметь Completed status.

#### Scenario: Проверка правила — Хранение полного model и compact report

- **WHEN** сохраняется принятый compact
- **THEN** report Completed, envelope и continuation хранятся отдельно от canonical истории.

### Requirement: Связи и безопасные persistence metadata

Связи MUST исключать присоединение дочерних строк к обращению другого диалога и MUST каскадно удалять зависимые строки вместе с диалогом. Перечисленные concurrency metadata MUST NOT объявляться реализацией атомарных application guards. Per-call ModelAccess/API keys MUST NOT сохраняться в persistence-моделях.

#### Scenario: Проверка правила — Связи и безопасные persistence metadata

- **WHEN** дочерняя строка принадлежит другому turn или dialog
- **THEN** связь не допускается, cascade сохраняет границы, ModelAccess и keys не записываются.
