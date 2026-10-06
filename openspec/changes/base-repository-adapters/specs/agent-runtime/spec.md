## ADDED Requirements

### Requirement: Адаптация базовых репозиториев

Адаптеры AgentBridge MUST делегировать чтение и staging create/update/delete актуальным базовым репозиториям EFCoreLibrary. Поиск дочерней строки по локальному ID MUST включать всех родителей её локального ключа.

#### Scenario: Одинаковые локальные ID

- **GIVEN** разные диалоги содержат обращения с одинаковым ID, а разные обращения — шаги с одинаковым ID
- **WHEN** адаптер читает дочернюю строку
- **THEN** predicate включает DialogId и, для шага, TurnId
- **AND** чужая строка не возвращается.

#### Scenario: Защищённое полное чтение

- **WHEN** владелец читает существующий диалог, включая истёкший до физического удаления
- **THEN** возвращаются сохраняемый token, фиксированные даты, объём, вся упорядоченная история и максимальная версия compact
- **AND** lifecycle/output/envelope/continuation/error остаются полными и отдельными от items
- **AND** ThroughTurnSequence не отбрасывает историю.

#### Scenario: Отказ чтения

- **WHEN** диалог отсутствует либо владелец не совпадает ordinal
- **THEN** чтение возвращает соответственно NotFound или Forbidden без данных
- **AND** дочерние данные не читаются и staging не выполняется.

#### Scenario: Изменение root во время чтения

- **GIVEN** чтение зафиксировало primitive owner/incarnation/revision до загрузки детей
- **WHEN** повторное base-чтение root после детей обнаруживает удаление, смену владельца, incarnation или revision
- **THEN** возвращается NotFound, Forbidden или Conflict без snapshot
- **AND** новый token не подставляется к смешанным данным и скрытый retry не выполняется.

#### Scenario: Повреждённая история

- **WHEN** при стабильном root item или step не имеет родительского turn в прочитанной истории либо принятый compact не Completed
- **THEN** чтение явно отклоняет повреждённые данные без молчаливого отбрасывания.

#### Scenario: Несохранённые изменения

- **WHEN** Infrastructure ставит create/update/delete в scoped session
- **THEN** вызывается соответствующая базовая операция EFCoreLibrary
- **AND** успех атомарного изменяющего Application port не объявляется.

#### Scenario: Проверка правила — Адаптация базовых репозиториев

- **WHEN** два turn имеют одинаковый локальный ID в разных dialogs
- **THEN** base predicate включает родительские ключи и не возвращает чужую строку.

### Requirement: Порядок и bounded base queries

Коллекции MUST сортироваться по сохраняемым Sequence/Version; ограничение кандидатов очистки MUST применяться после сортировки ExpiresAtUtc/Id. Custom query MUST применяться только при недостаточности base predicate/include API.

#### Scenario: Проверка правила — Порядок и bounded base queries

- **WHEN** выбираются истёкшие кандидаты с limit
- **THEN** сначала выполняется сортировка ExpiresAtUtc/Id, custom query требует недостаточности base API.

### Requirement: Staging без подтверждения save

Staging MUST NOT выдавать подтверждение сохранённого успеха изменяющего Application port и MUST NOT выполнять SaveChanges или транзакции.

#### Scenario: Проверка правила — Staging без подтверждения save

- **WHEN** Infrastructure выполняет staging create/update/delete
- **THEN** SaveChanges и transaction не выполняются, port success не объявляется.
