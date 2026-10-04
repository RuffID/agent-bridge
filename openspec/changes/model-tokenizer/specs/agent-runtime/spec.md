## ADDED Requirements

### Requirement: Полноценная токенизация подготовленного запроса

IContextTokenCounter MUST применять полноценный .NET BPE tokenizer с проверенным exact model→encoding mapping. Неизвестный ID, другой регистр или непроверенный suffix MUST возвращать безопасный Unsupported без произвольного fallback. Runtime tokenization MUST NOT требовать сети. Подсчёт MUST учитывать инструкции, всю prepared Input последовательность, известные function calls/results, names/descriptions/полные schemas tools и входные параметры формата/выбора tools. Model/effort и транспортные metadata MUST NOT объявляться input текстом.

KnownTokens MUST сохранять локально посчитанную известную часть. EstimatedInputTokens MUST быть nullable оценкой полного input, MUST NOT объявляться локально точным server/billing count. Встроенный offline ContextTokenCounter без подтверждённой внешней оценки MUST возвращать null estimate и HasOpaqueContent=true при opaque/multimodal/unknown input или скрытом continuation state; известный текст MUST оставаться посчитанным. Независимый порт MUST сохранять возможность обоснованной полной оценки отдельно от HasOpaqueContent; DI MUST сохранять явный counter приложения. Подсчёт MUST соблюдать caller cancellation, MUST NOT менять request, историю или opaque payload.

#### Scenario: Весь подготовленный запрос

- **WHEN** инструкции, provider/history/new message, tools и results входят в prepared ModelRequest
- **THEN** KnownTokens учитывает всю известную часть, включая полные schemas и input parameters.

#### Scenario: Неизвестное содержимое

- **WHEN** встроенный offline counter считает input с изображением, opaque reasoning/compaction, неизвестным полем либо continuation без подтверждённой полной оценки
- **THEN** KnownTokens видимого текста доступен отдельно, EstimatedInputTokens=null и HasOpaqueContent=true.

### Requirement: Проверка полного входного бюджета

Guard MUST использовать exact model/effort и положительный InputContextWindow из проверенного каталога, configured threshold и reserve. ContextWindow/MaxOutputTokens MUST NOT подменять входной лимит. Guard MUST отклонять неизвестную полную оценку, некорректные настройки и превышение без integer overflow; ошибка counter MUST передаваться тем же ServiceError. Reserve MUST применяться отдельно от KnownTokens; estimate+reserve == input_context_window MUST быть допустимо по оценке. Threshold MUST достигаться при estimate >= threshold; guard MUST NOT запускать compact, HTTP, DB или orchestration.

#### Scenario: Граница и неизвестный бюджет

- **WHEN** estimate с резервом равен входному лимиту
- **THEN** guard возвращает результат оценки и отдельный признак достижения threshold
- **AND** при null estimate вместо успеха возвращает Unsupported.
