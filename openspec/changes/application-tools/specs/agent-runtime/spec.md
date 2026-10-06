## ADDED Requirements

### Requirement: Ограниченное исполнение инструментов приложения

Registry MUST хранить exact names, descriptions, полные schemas и scoped handler/validator registrations; duplicate names MUST отклоняться.

#### Scenario: Неизвестный или запрещённый инструмент

- **WHEN** имя не зарегистрировано, не выбрано приложением или validator отказывает
- **THEN** handler не вызывается, результат явно отражает отказ и не раскрывает raw arguments/messages/secrets.

#### Scenario: Проверка правила — Ограниченное исполнение инструментов приложения

- **WHEN** регистрируется второй инструмент с тем же exact name
- **THEN** регистрация отклоняется.

### Requirement: Проверка допуска инструмента до handler

Перед handler MUST проверяться completed lifecycle модели, complete object arguments, непустые call_id/name, разрешённый выбор инструмента и обязательный validator аргументов/прав приложения.

#### Scenario: Проверка правила — Проверка допуска инструмента до handler

- **WHEN** validator отказывает в аргументах или правах
- **THEN** handler не вызывается.

### Requirement: Изолированный scope invocation

Каждый invocation MUST получать отдельный DI scope, включая validator и handler; параллельные tasks MUST NOT использовать общий scoped DbContext. Metadata регистрации и handler MUST совпадать до действия.

#### Scenario: Проверка правила — Изолированный scope invocation

- **WHEN** два вызова выполняются параллельно
- **THEN** validator и handler каждого вызова используют собственный scope, metadata совпадают.

### Requirement: Идентичность попыток и ограниченный lifecycle tools

Сессия MUST фиксировать owner/dialog/incarnation/turn/agent, выбранные имена и expiry. Попытка MUST различаться StepId и исходной позицией function_call в output; call_id MUST оставаться только связью canonical пары, без глобальной дедупликации. Completed пары с повторным call_id MUST сохраняться; уже закрытые calls MUST NOT исполняться заново.

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

#### Scenario: Проверка правила — Идентичность попыток и ограниченный lifecycle tools

- **WHEN** два завершённых шага используют одинаковый call_id
- **THEN** StepId и output position различают попытки; уже закрытый вызов не повторяется.

### Requirement: Лимиты и завершение сессии инструментов

Сессия MUST ограничивать tool steps, calls per step, общее cooperative время и параллельность; при now >= expiry действия MUST NOT начинаться. Начатые tasks MUST ожидаться и scopes освобождаться при всех исходах. Один step MUST NOT исполняться повторно внутри сессии; interruption/unknown MUST блокировать её следующие действия без retries.

#### Scenario: Проверка правила — Лимиты и завершение сессии инструментов

- **WHEN** сессия прервана после начала step
- **THEN** начатые tasks ожидаются и scopes освобождаются; step не повторяется.

### Requirement: Подтверждённый checkpoint перед handler

Если приложение передало IToolExecutionCheckpoint, executor MUST ожидать подтверждённый успех checkpoint после validator и до handler. Отказ, исключение, отмена или неизвестный исход checkpoint MUST NOT разрешать handler/повтор в той же сессии. Checkpoint MUST создавать отдельный короткий scope/UoW на invocation; общий scoped DbContext между параллельными callbacks MUST NOT использоваться.

#### Scenario: Проверка правила — Подтверждённый checkpoint перед handler

- **WHEN** приложение передало checkpoint, но его исход неизвестен
- **THEN** handler не вызывается; callback использует отдельный короткий scope.

### Requirement: Результаты инструментов при отмене и cleanup

Caller cancellation после подтверждённого результата MUST сохранять output и останавливать сессию. Primary exception и failure DisposeAsync MUST сохраняться вместе; ошибка cleanup MUST NOT удалять уже подтверждённый соседний результат.

#### Scenario: Проверка правила — Результаты инструментов при отмене и cleanup

- **WHEN** результат подтверждён до caller cancellation и ошибки cleanup
- **THEN** output остаётся в отчёте, primary и cleanup failure сохраняются вместе.

### Requirement: Canonical результаты и граница durable recovery

Успешный результат MUST представляться полным function_call_output с исходным call_id и сериализованным JSON output. Подтверждённый ServiceResult.Fail MUST сохраняться как явный безопасный error output; Timeout после начала handler MUST считаться Unknown. Исходные canonical calls/opaque items MUST NOT изменяться.

#### Scenario: Последующий вопрос о заказе

- **WHEN** сохранённая история владельца содержит GetOrderStatus и matching output
- **THEN** actual ContextBuilder включает полную пару в следующий input того же владельца
- **AND** чужой owner не получает prepared request.

#### Scenario: Проверка правила — Canonical результаты и граница durable recovery

- **WHEN** handler возвращает подтверждённый ServiceResult.Fail
- **THEN** сохраняется безопасный error output с исходной canonical связью.

### Requirement: Сохранение canonical пар для композиции

Приложение MUST сохранять calls/outputs через existing short IDialogTurnWriter для последующего ContextBuilder.

#### Scenario: Проверка правила — Сохранение canonical пар для композиции

- **WHEN** приложение сохраняет call и output через IDialogTurnWriter
- **THEN** следующий ContextBuilder получает полную пару.

### Requirement: Граница restart protection инструментов

Этап19 session-memory MUST NOT объявляться restart protection. Durable запись начала до handler и запрет recovery незавершённой попытки MUST интегрироваться в этапе20; автоматического retry uncertain side effects MUST NOT быть.

#### Scenario: Проверка правила — Граница restart protection инструментов

- **WHEN** приложение перезапущено после незавершённого действия
- **THEN** session-memory19 не считается защитой; durable запись начала относится к этапу20 без retry.
