## ADDED Requirements

### Requirement: Ограниченное исполнение инструментов приложения

Registry MUST хранить exact names, descriptions, полные schemas и scoped handler/validator registrations; duplicate names MUST отклоняться. Перед handler MUST проверяться completed lifecycle модели, complete object arguments, непустые call_id/name, разрешённый выбор инструмента и обязательный validator аргументов/прав приложения. Каждый invocation MUST получать отдельный DI scope, включая validator и handler; параллельные tasks MUST NOT использовать общий scoped DbContext. Metadata регистрации и handler MUST совпадать до действия.

#### Scenario: Неизвестный или запрещённый инструмент

- **WHEN** имя не зарегистрировано, не выбрано приложением или validator отказывает
- **THEN** handler не вызывается, результат явно отражает отказ и не раскрывает raw arguments/messages/secrets.

### Requirement: Идентичность попыток и ограниченный lifecycle tools

Сессия MUST фиксировать owner/dialog/incarnation/turn/agent, выбранные имена и expiry. Попытка MUST различаться StepId и исходной позицией function_call в output; call_id MUST оставаться только связью canonical пары, без глобальной дедупликации. Completed пары с повторным call_id MUST сохраняться; уже закрытые calls MUST NOT исполняться заново. Сессия MUST ограничивать tool steps, calls per step, общее cooperative время и параллельность; при now >= expiry действия MUST NOT начинаться. Начатые tasks MUST ожидаться и scopes освобождаться при всех исходах. Один step MUST NOT исполняться повторно внутри сессии; interruption/unknown MUST блокировать её следующие действия без retries.

Если приложение передало IToolExecutionCheckpoint, executor MUST ожидать подтверждённый успех checkpoint после validator и до handler. Отказ, исключение, отмена или неизвестный исход checkpoint MUST NOT разрешать handler/повтор в той же сессии. Checkpoint MUST создавать отдельный короткий scope/UoW на invocation; общий scoped DbContext между параллельными callbacks MUST NOT использоваться. Caller cancellation после подтверждённого результата MUST сохранять output и останавливать сессию. Primary exception и failure DisposeAsync MUST сохраняться вместе; ошибка cleanup MUST NOT удалять уже подтверждённый соседний результат.

#### Scenario: Повторяемый call_id

- **WHEN** разные завершённые шаги содержат function_call с одинаковым call_id
- **THEN** отдельные StepId/output positions допускают отдельные исполнения, сохраняя исходные ID результатов.

#### Scenario: Неопределённый исход

- **WHEN** начатый handler прерван либо выбросил неожиданное исключение
- **THEN** отчёт сохраняет успешные соседние результаты и Unknown для начатой попытки без выдуманного output
- **AND** неожиданное исключение/отмена распространяется после ожидания tasks; та же session не повторяет действия.

#### Scenario: Отказ checkpoint

- **WHEN** checkpoint не подтверждает запись начала или его ожидание отменено
- **THEN** handler не вызывается, attempted step блокируется в сессии, fake output отсутствует.

### Requirement: Canonical результаты и граница durable recovery

Успешный результат MUST представляться полным function_call_output с исходным call_id и сериализованным JSON output. Подтверждённый ServiceResult.Fail MUST сохраняться как явный безопасный error output; Timeout после начала handler MUST считаться Unknown. Исходные canonical calls/opaque items MUST NOT изменяться. Приложение MUST сохранять calls/outputs через existing short IDialogTurnWriter для последующего ContextBuilder. Этап19 session-memory MUST NOT объявляться restart protection. Durable запись начала до handler и запрет recovery незавершённой попытки MUST интегрироваться в этапе20; автоматического retry uncertain side effects MUST NOT быть.

#### Scenario: Последующий вопрос о заказе

- **WHEN** сохранённая история владельца содержит GetOrderStatus и matching output
- **THEN** actual ContextBuilder включает полную пару в следующий input того же владельца
- **AND** чужой owner не получает prepared request.
