## ADDED Requirements

### Requirement: Полная детерминированная композиция контекста

Composition MUST сохранять инструкции в ModelRequest.Instructions и объединять input в порядке: вклады разрешённых провайдеров приложения, Items активного StoredDialogContext, Items всех обращений после ThroughTurnSequence, новый ещё не сохранённый input. При отсутствии окна MUST включаться вся история. Порядок провайдеров и элементов MUST сохраняться.

#### Scenario: Сжатое окно и хвост

- **WHEN** принятое окно покрывает первые два terminal обращения и третье ещё выполняется
- **THEN** input содержит Items окна, все Items третьего обращения и новый input
- **AND** output третьего обращения не дублируется из ModelSteps.

#### Scenario: Пустой префикс

- **WHEN** ThroughTurnSequence равен 0
- **THEN** все обращения включаются после Items активного окна без item cutoff.

#### Scenario: Проверка правила — Полная детерминированная композиция контекста

- **WHEN** есть активное окно, хвост истории и новый input
- **THEN** композиция сохраняет инструкции и установленный порядок источников.

### Requirement: Metadata подготовленного ModelRequest

Tools, exact model/effort, parameters и явно переданный continuation MUST сохраняться в подготовленном ModelRequest. Envelope/continuation MUST NOT становиться input.

#### Scenario: Проверка правила — Metadata подготовленного ModelRequest

- **WHEN** приложение передало tools, exact settings и continuation
- **THEN** они сохраняются в ModelRequest, envelope и continuation не становятся input.

### Requirement: Единственный источник истории композиции

StoredDialogTurn.Items MUST быть единственным источником элементов истории; output из StoredModelStep MUST NOT включаться повторно. Unknown/opaque поля и исходные роли MUST сохраняться без текстовой сводки или нормализации.

#### Scenario: Проверка правила — Единственный источник истории композиции

- **WHEN** StoredModelStep повторяет output из StoredDialogTurn.Items
- **THEN** output включается только из Items; opaque поля и исходные роли сохраняются.

### Requirement: Исходные роли и полные пары функций

Провайдер приложения MUST отвечать за авторизацию своего вклада. Composition MUST принимать его canonical items с исходными ролями без повышения до системной роли или ограничения набора ролей.

#### Scenario: Вызов без результата

- **WHEN** текущий хвост содержит function_call с частичными arguments и без function_call_output
- **THEN** composition возвращает явный отказ без ModelRequest
- **AND** исходные Items и lifecycle отчёт остаются неизменными.

#### Scenario: Пара на границе источников

- **WHEN** известный function_call находится в Items окна, а соответствующий function_call_output в хвосте
- **THEN** проверка использует полную последовательность и сохраняет оба элемента в исходном порядке.

#### Scenario: Повторное использование call_id

- **WHEN** два обращения содержат отдельные полные пары с одним call_id
- **THEN** композиция сохраняет обе пары без отказа по глобальной уникальности ID.

#### Scenario: Проверка правила — Исходные роли и полные пары функций

- **WHEN** авторизованный provider возвращает canonical item с исходной ролью
- **THEN** роль сохраняется без повышения до system.

### Requirement: Сопоставление известных function pairs

Проверка известных function_call/function_call_output MUST выполняться по call_id во всей подготовленной последовательности, включая границы вкладов/окна/хвоста/new input. Каждый output MUST сопоставляться с предшествующим ещё не закрытым call того же ID; call_id MUST допускать повторное использование в разных парах.

#### Scenario: Проверка правила — Сопоставление известных function pairs

- **WHEN** calls и outputs находятся на границе вкладов и повторяют call_id
- **THEN** каждый output связывается с предшествующим незакрытым call без глобальной уникальности.

### Requirement: Отказ неполной пары без изменения истории

Известный function_call без последующего результата MUST давать явный безопасный отказ до возврата ModelRequest, включая сохранённый call с partial arguments после обрыва. Некорректная известная пара MUST отклоняться без выдумывания результата, удаления или изменения истории. Opaque/unknown элементы MUST сохраняться без попытки проверки скрытых внутри них вызовов; arguments/output MUST NOT переписываться.

#### Scenario: Проверка правила — Отказ неполной пары без изменения истории

- **WHEN** сохранён partial call без output и рядом opaque item
- **THEN** подготовка явно отклоняется без fake output, изменения arguments или раскрытия opaque.

### Requirement: Защищённая композиция и последовательные провайдеры

Composition MUST проверять соответствие dialog/owner прочитанного snapshot и явный UTC срок до вызова провайдеров. nowUtc >= ExpiresAtUtc MUST отклонять подготовку. Повреждённый порядок обращений, отсутствующая часть prefix, InProgress внутри покрытого prefix или непринятый compact MUST отклоняться явно без исправления данных. Composition MUST NOT менять snapshot, фиксированные даты, token или выдавать разрешение записи.

#### Scenario: Другой владелец или точная граница срока

- **WHEN** owner не совпадает либо nowUtc равен ExpiresAtUtc
- **THEN** запрос не возвращается и провайдеры не вызываются.

#### Scenario: Отказ второго провайдера

- **WHEN** первый провайдер успешен, а второй возвращает ожидаемую ошибку
- **THEN** composition передаёт ту же ошибку без запроса и не вызывает следующих провайдеров.

#### Scenario: Проверка правила — Защищённая композиция и последовательные провайдеры

- **WHEN** snapshot чужой, expired или содержит повреждённый prefix
- **THEN** подготовка отклоняется до providers без изменения snapshot или выдачи права записи.

### Requirement: Последовательный вызов context providers

Провайдеры MUST получать actual ApplicationCallContext и новый input, MUST вызываться последовательно с caller token, без fan-out. Ожидаемая ошибка MUST передаваться тем же ServiceError без частичного запроса; неожиданные exceptions MUST распространяться без fallback. Отмена MUST соблюдаться до провайдера и после его успешного завершения.

#### Scenario: Проверка правила — Последовательный вызов context providers

- **WHEN** второй provider возвращает ошибку или вызывающий отменён
- **THEN** ошибка передаётся без частичного запроса и fallback, последовательность соблюдает caller token.
