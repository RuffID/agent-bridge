## ADDED Requirements

### Requirement: Полный ход агента

AgentRunner MUST фиксировать owner, dialog incarnation/revision, настройки модели, инструкции и tool selection в начале run. Providers MUST вызываться один раз на сценарий.

#### Scenario: Модель вызывает инструмент

- **WHEN** Completed model step содержит pending function calls
- **THEN** полный step и calls сохраняются до исполнения
- **AND** после confirmed outputs следующий полный request проходит guard заново.

#### Scenario: Проверка правила — Полный ход агента

- **WHEN** AgentRunner начинает обращение
- **THEN** owner/version/settings зафиксированы, providers вызываются один раз.

### Requirement: Guard и подтверждённое завершение run

Генерация MUST проходить полный ContextBudgetGuard после любого compact outcome и tool шага. Stream updates MUST оставаться предварительными; Completed MUST возвращаться только после подтверждённого model completion и успешного terminal save. Canonical output и полный ModelResponse MUST сохраняться без нормализации неизвестных данных.

#### Scenario: Проверка правила — Guard и подтверждённое завершение run

- **WHEN** модель прислала stream update перед terminal save
- **THEN** update остаётся предварительным, Completed требует model completion и успешного save с canonical данными.

### Requirement: Durable защита действий инструмента

Перед handler AgentRunner MUST успешно сохранить Started для owner/dialog/incarnation/turn/agent + StepId + исходная output position. Каждая запись MUST использовать отдельный короткий scope/UoW, завершённый до внешнего I/O. Checkpoint writes и token updates одного run MUST сериализоваться.

#### Scenario: Restart после действия

- **GIVEN** Started сохранён и процесс остановился без outcome
- **WHEN** приложение повторяет run для того же turn
- **THEN** handler не запускается повторно
- **AND** сохранённая canonical история остаётся без изменения.

#### Scenario: Проверка правила — Durable защита действий инструмента

- **WHEN** AgentRunner собирается вызвать handler
- **THEN** Started сохранён отдельным scope до внешнего I/O, checkpoint writes сериализованы.

### Requirement: Durable отказ повторения неопределённой попытки

Unknown commit, conflict, восстановленный Started и повтор попытки MUST NOT разрешать handler или retry. Outcomes и confirmed outputs MUST сохраняться атомарно. Unknown MUST NOT получать выдуманный function_call_output; повторные completed пары call_id MUST оставаться допустимыми.

#### Scenario: Проверка правила — Durable отказ повторения неопределённой попытки

- **WHEN** restart восстановил Started без outcome
- **THEN** handler и retry запрещены, Unknown не получает fake output; отдельные completed пары call_id допустимы.

### Requirement: Честное завершение и актуальность

AgentRunner MUST сохранять partial reports и confirmed соседние tool outputs, включая LastResult после exception/cancel, и MUST ожидать все начатые tasks/scopes. Отмена и partial/Unknown MUST NOT объявляться успехом. Каждая запись MUST получать fresh UTC и original либо successful-save token. Expiry/delete/cleanup/conflict MUST отклонять late writes без recreation, refresh или automatic retry. ModelAccess MUST NOT сохраняться.

#### Scenario: Поздний результат

- **WHEN** инструмент завершился после expiry либо удаления диалога
- **THEN** запись отклоняется
- **AND** итог не сообщает Completed или подтверждённое сохранение.
