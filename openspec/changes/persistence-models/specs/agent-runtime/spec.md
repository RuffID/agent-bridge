## ADDED Requirements

### Requirement: Формат общего EF-хранилища

Persistence-модель MUST отделяться от доменного агрегата и MUST сохранять владельца, фиксированные даты, incarnation/revision, порядок обращений и items. Полный результат каждого шага модели и принятого compact MUST сохраняться отдельно от канонических items истории, включая lifecycle, output, envelope, continuation и ожидаемую ошибку. Результат compact MUST иметь Completed status. Связи MUST исключать присоединение дочерних строк к обращению другого диалога и MUST каскадно удалять зависимые строки вместе с диалогом. Перечисленные concurrency metadata MUST NOT объявляться реализацией атомарных application guards. Per-call ModelAccess/API keys MUST NOT сохраняться в persistence-моделях.

#### Scenario: Сохранение результата инструмента и envelope

- **WHEN** сериализуется история с function_call_output и отдельным результатом шага
- **THEN** полные неизвестные поля и call_id остаются в соответствующих канонических данных
- **AND** envelope и continuation остаются отдельно от следующего input.

#### Scenario: Выбор общего контекста

- **WHEN** приложение явно выбирает SQLite или PostgreSQL
- **THEN** DI регистрирует один scoped-контекст через AddEfCoreContext и AddEfCoreBaseRepositories
- **AND** регистрация не открывает БД, не запускает migrations и не выбирает SQLite при отсутствии настройки.
