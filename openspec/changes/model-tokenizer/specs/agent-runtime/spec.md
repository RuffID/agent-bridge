## ADDED Requirements

### Requirement: Полноценная токенизация подготовленного запроса

IContextTokenCounter MUST применять полноценный .NET BPE tokenizer с проверенным exact model→encoding mapping. Неизвестный ID, другой регистр или непроверенный suffix MUST возвращать безопасный Unsupported без произвольного fallback. Runtime tokenization MUST NOT требовать сети.

#### Scenario: Весь подготовленный запрос

- **WHEN** инструкции, provider/history/new message, tools и results входят в prepared ModelRequest
- **THEN** KnownTokens учитывает всю известную часть, включая полные schemas и input parameters.

#### Scenario: Неизвестное содержимое

- **WHEN** встроенный offline counter считает input с изображением, opaque reasoning/compaction, неизвестным полем либо continuation без подтверждённой полной оценки
- **THEN** KnownTokens видимого текста доступен отдельно, EstimatedInputTokens=null и HasOpaqueContent=true.

#### Scenario: Проверка правила — Полноценная токенизация подготовленного запроса

- **WHEN** передан неизвестный ID или другой регистр модели
- **THEN** counter возвращает Unsupported без fallback и сетевого поиска.

### Requirement: Состав токенизируемого input

Подсчёт MUST учитывать инструкции, всю prepared Input последовательность, известные function calls/results, names/descriptions/полные schemas tools и входные параметры формата/выбора tools. Model/effort и транспортные metadata MUST NOT объявляться input текстом.

#### Scenario: Проверка правила — Состав токенизируемого input

- **WHEN** запрос содержит instructions, input, tool schemas и transport metadata
- **THEN** входное содержимое учитывается целиком, model/effort не выдаются за input текст.

### Requirement: Известные токены и непрозрачная оценка

KnownTokens MUST сохранять локально посчитанную известную часть. EstimatedInputTokens MUST быть nullable оценкой полного input, MUST NOT объявляться локально точным server/billing count. Встроенный offline ContextTokenCounter без подтверждённой внешней оценки MUST возвращать null estimate и HasOpaqueContent=true при opaque/multimodal/unknown input или скрытом continuation state; известный текст MUST оставаться посчитанным.

#### Scenario: Проверка правила — Известные токены и непрозрачная оценка

- **WHEN** input содержит видимый текст и opaque item без внешней оценки
- **THEN** KnownTokens остаются доступны, полная оценка null и HasOpaqueContent=true.

### Requirement: Counter приложения и неизменность запроса

Независимый порт MUST сохранять возможность обоснованной полной оценки отдельно от HasOpaqueContent; DI MUST сохранять явный counter приложения. Подсчёт MUST соблюдать caller cancellation, MUST NOT менять request, историю или opaque payload.

#### Scenario: Проверка правила — Counter приложения и неизменность запроса

- **WHEN** приложение зарегистрировало собственный counter и отменило вызов
- **THEN** DI сохраняет counter, отмена соблюдается, request и история не меняются.

### Requirement: Проверка полного входного бюджета

Guard MUST использовать exact model/effort и положительный InputContextWindow из проверенного каталога, configured threshold и reserve. ContextWindow/MaxOutputTokens MUST NOT подменять входной лимит. Guard MUST отклонять неизвестную полную оценку, некорректные настройки и превышение без integer overflow; ошибка counter MUST передаваться тем же ServiceError.

#### Scenario: Граница и неизвестный бюджет

- **WHEN** estimate с резервом равен входному лимиту
- **THEN** guard возвращает результат оценки и отдельный признак достижения threshold
- **AND** при null estimate вместо успеха возвращает Unsupported.

#### Scenario: Проверка правила — Проверка полного входного бюджета

- **WHEN** полная оценка неизвестна или каталог не даёт допустимый input window
- **THEN** guard отклоняет проверку без подмены лимита или ServiceError.

### Requirement: Резерв и порог полного бюджета

Reserve MUST применяться отдельно от KnownTokens; estimate+reserve == input_context_window MUST быть допустимо по оценке. Threshold MUST достигаться при estimate >= threshold; guard MUST NOT запускать compact, HTTP, DB или orchestration.

#### Scenario: Проверка правила — Резерв и порог полного бюджета

- **WHEN** estimate плюс reserve равны input_context_window
- **THEN** оценка допустима; threshold проверяется отдельно без запуска compact или I/O.
