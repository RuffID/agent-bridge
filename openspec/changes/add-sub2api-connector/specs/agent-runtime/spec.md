## ADDED Requirements

### Requirement: Выбор коннектора модели при подключении

AgentBridge MUST позволять приложению выбрать один согласованный коннектор codex-lb либо sub2api при регистрации DI. Выбор MUST распространяться на gateway, каталог, доступ и settings. Неоднозначный выбор MUST отклоняться до внешних операций.

#### Scenario: Явный выбор sub2api

- **WHEN** приложение регистрирует sub2api-коннектор и затем стандартный facade
- **THEN** запросы и каталог обслуживаются sub2api без обязательного фиктивного раздела CodexLb.

#### Scenario: Конфликт регистрации

- **WHEN** приложение пытается выбрать два встроенных коннектора либо изменить выбор после facade
- **THEN** регистрация завершается явной ошибкой без HTTP или частично выбранного второго коннектора.

### Requirement: Сохранение прежнего прикладного подключения

AgentBridge MUST сохранять действующую сигнатуру AddAgentBridge, существующие public CodexLb точки входа и бизнес-контракты диалогов, tools, настроек и runner. Без нового выбора MUST сохраняться codex-lb и прежняя конфигурация. Documented app overrides и владение ресурсами MUST сохраняться.

#### Scenario: Прежнее приложение

- **WHEN** прежний consumer подключает обновлённую поставку без изменения бизнес-кода
- **THEN** он собирается и использует codex-lb через прежний AddAgentBridge.
- **AND** прежние public types, constructors и assembly identity доступны в заявленной бинарной поверхности.

#### Scenario: Регистрация без побочных операций

- **WHEN** приложение регистрирует любой встроенный коннектор
- **THEN** AgentBridge не открывает соединения, не запускает host, не вызывает HTTP factory и не создаёт ServiceProvider.

### Requirement: Доступ sub2api и подтверждённые возможности

Коннектор sub2api MUST использовать выбранный ключ sub2api для Bearer-авторизации и сохранять existing приоритет индивидуального источника без fallback при ошибке. Каталог MUST преобразовываться в общий контракт без выдуманных model IDs, effort и input budget. Неподтверждённые возможности MUST возвращать Unsupported.

#### Scenario: Ключ sub2api

- **WHEN** приложение передало действительный по локальному формату ключ sub2api
- **THEN** коннектор использует его без запроса OpenAI upstream credentials у приложения.
- **AND** ключ не появляется в safe snapshots, persistence, ошибках или логах.

#### Scenario: Неполный каталог

- **WHEN** выбранная модель видна, но effort не подтверждён либо входной бюджет неизвестен без явного разрешения серверной проверки неизвестного окна
- **THEN** запуск не получает выдуманные defaults и завершается явным Unsupported.

### Requirement: Явная серверная проверка неизвестного входного окна

AgentBridge MUST разрешать неизвестный InputContextWindow только при явно выбранной политике ServerValidationWithUnknownWindow в settings и guard. Порог сжатия MUST оставаться локальным ориентиром, резерв MUST NOT выдаваться за обеспеченный при неизвестном окне. Exact model/effort и объявленная API-доступность MUST проверяться по каталогу; context_window, effective percent и локальные оценки MUST NOT подменять входное окно. Прежние строгий режим и ServerValidation MUST сохранять требование известного окна.

#### Scenario: Неизвестное окно и явный режим

- **WHEN** manifest существующего sub2api объявляет точную модель, API-доступность и effort, но входной бюджет неизвестен, а приложение явно выбрало новый режим
- **THEN** settings и assessment сохраняют неизвестное окно и признак обязательной серверной проверки даже для полностью текстового input.
- **AND** это не доказывает upstream input budget или совместимость compact; generation сохраняет отдельную принятую canonical границу.

#### Scenario: Сохранение безопасного snapshot

- **WHEN** обращение использует неизвестное окно
- **THEN** TurnModelSettings сохраняется в version 2 с null окном и явной политикой; version 1 с известным окном продолжает читаться без defaults.
- **AND** старые публичные сигнатуры для известного окна сохраняются, неизвестный лимит не возвращается числом через legacy API.

### Requirement: Адаптация к существующему sub2api

AgentBridge MUST адаптироваться к существующим sub2api routes и поведению без доработки sub2api и без обязательных новых server headers/fields. Запрет клиентских retries MUST NOT трактоваться как запрет всех внутренних server retries. Коннектор MUST сохранять canonical данные при отправке sub2api и получении его ответа; история AgentBridge MUST сохранять исходные items. Удаление reasoning.content и encrypted reasoning при existing server normalization/recovery MAY происходить внутри sub2api; коннектор MUST NOT объявлять неизменную upstream передачу доказанной. Коннектор MUST NOT выдумывать budget/capabilities или выдавать requested model echo за доказанную actual upstream identity. Один guessed flag или успешная fixture MUST NOT снимать неподтверждённые ограничения compact.

#### Scenario: Чужая continuation при вручную переданном запросе

- **WHEN** встроенному sub2api gateway передан запрос с continuation, включая codex-lb anchor/turn-state
- **THEN** generation отклоняется до HTTP factory и callbacks, без изменения canonical input и без отправки anchors.

#### Scenario: Сервер изменяет reasoning при обработке

- **WHEN** ordinary/passthrough shared normalizer либо recovery удаляет reasoning внутри sub2api
- **THEN** AgentBridge сохраняет исходный отправленный контекст и полученный canonical ответ, не выполняя клиентский повтор или повтор business tool.
- **AND** успешный ответ не объявляется доказательством сохранности reasoning upstream.

#### Scenario: Отсутствие достоверной upstream model metadata

- **WHEN** публичный ответ содержит только клиентское model echo
- **THEN** коннектор сохраняет его только как объявленное значение и не заявляет независимо подтверждённую upstream identity.
- **AND** сценарий, которому такая гарантия обязательна, остаётся Unsupported без смены ключа/модели/коннектора или повторного выполнения business tool.

### Requirement: HTTP Responses sub2api с полным рабочим контекстом

Первая версия sub2api-коннектора MUST поддерживать existing JSON/SSE generation API через HttpClientLibrary с полным рабочим контекстом без исходящего previous_response_id и legacy turn-state. Canonical items, reasoning и пары tools MUST сохраняться на клиентской границе; серверные преобразования допустимы согласно требованию адаптации. Отказ MUST NOT запускать скрытый retry, смену модели, ключа или коннектора. Возвращённый response id MUST NOT превращаться в continuation sub2api.

#### Scenario: SSE вместо non-stream JSON

- **WHEN** приложение не передало callback, а сервер вернул text/event-stream
- **THEN** тот же HTTP-ответ читается как SSE без повторной отправки; EOF и [DONE] не подтверждают Completed.

#### Scenario: Несколько шагов инструмента

- **WHEN** модель вернула function_call и приложение сохранило и выполнило инструмент
- **THEN** следующий запрос содержит нужный контекст вызова и его результат.
- **AND** ошибка HTTP не приводит к повторному выполнению бизнес-инструмента.

#### Scenario: Прерванный поток

- **WHEN** SSE оборвался после части output без подтверждённого terminal success
- **THEN** возвращённый lifecycle сохраняет известный output и не объявляет операцию успешно завершённой.
- **AND** caller cancellation, timeout, callback и cleanup failures сохраняют установленные границы.

### Requirement: Первая версия sub2api без compact

Первая версия sub2api-коннектора MUST явно отключать автоматическое сжатие и MUST NOT создавать или активировать новое compact-окно. Ручной/force CompactAsync MUST возвращать Unsupported до HTTP factory, отправки и сохранения нового окна. История MUST сохраняться без удаления или обрезания ради прохождения запроса; compact MUST NOT подменяться обычной генерацией.

Достижение локального threshold MUST NOT само по себе останавливать generation: existing полный рабочий контекст MUST проходить отдельный budget guard и передаваться без обрезания при допустимой policy. Превышение известного локального лимита либо отказ сервера по размеру MUST возвращаться явно. Отказ generation MUST NOT вызывать retry, смену выбора или повтор бизнес-инструмента. Прежние коннекторы MUST сохранять автоматическое сжатие по умолчанию.

#### Scenario: Порог достигнут при отключённом compact

- **WHEN** полный рабочий контекст sub2api достиг threshold, автоматический compact отключён и отдельный guard допускает запрос
- **THEN** generation получает полный контекст без compact dispatch/save и без активации нового окна.
- **AND** HTTP-отказ по размеру возвращается явно, без обрезания и повторной отправки; известный локальный лимит продолжает проверяться до HTTP.

#### Scenario: Ручной или принудительный compact

- **WHEN** приложение запрашивает ручной либо force compact через sub2api
- **THEN** возвращается Unsupported до HTTP factory и отправки; новое окно не сохраняется и не активируется.
- **AND** полная история и прежнее состояние остаются сохранными.

#### Scenario: Входной бюджет исчерпан без compact

- **WHEN** полный контекст превышает известный локальный лимит либо сервер отклоняет его по размеру при разрешённом неизвестном окне
- **THEN** приложение получает явную ошибку без обрезания истории, нового compact, повторной отправки, смены выбора или повторного бизнес-инструмента.

### Requirement: Безопасность сохранённого контекста между коннекторами

Встроенные коннекторы MUST отклонять неподтверждённое использование чужого или неизвестного opaque-контекста до HTTP. Совпадение model ID MUST NOT само по себе подтверждать совместимость. Локальное происхождение MUST связывать коннектор, диалог, владельца, агента, endpoint и доступ без сохранения ключа. Оно MUST храниться отдельно от canonical envelope/output и transport continuation и MUST NOT передаваться upstream. Durable fencing, owner/expiry, pinned settings/access и no-replay MUST сохраняться.

Хранение результатов модели MUST использовать единственный новый JSON-формат с отдельными canonical envelope и metadata происхождения, без совместимого чтения прежнего JSON и без выбора v1/v2. Сохранение уже записанных старых диалогов не требуется; несовместимые записи MUST отклоняться явно без автоматического backfill, удаления или fallback. Прежние публичные сигнатуры MUST сохраняться; metadata MAY добавляться отдельными свойствами и методами.

#### Scenario: Смена шлюза при той же модели

- **WHEN** сохранённое opaque-окно или непокрытый output создан другим коннектором
- **THEN** запрос явно отклоняется без удаления истории и без отправки чужих transport anchors.

#### Scenario: Возобновление родного диалога

- **WHEN** приложение пересоздало DI и продолжает диалог через прежний коннектор
- **THEN** полный рабочий контекст нового формата восстанавливается с прежними guards и проверкой происхождения.
- **AND** recovery-required или неизвестный исход инструмента не разрешает его автоматический replay.

#### Scenario: Неизвестное происхождение при той же модели

- **WHEN** сохранённый opaque output или compact-окно не содержит подтверждённого локального происхождения, хотя model ID совпадает
- **THEN** встроенный коннектор отклоняет продолжение до HTTP без изменения исходного контекста.

#### Scenario: Старый JSON результата модели

- **WHEN** хранилище содержит прежний JSON без структуры нового формата
- **THEN** чтение возвращает явный отказ без совместимого преобразования или автоматической перезаписи.

## MODIFIED Requirements

### Requirement: Проверка полного входного бюджета

Guard MUST использовать exact model/effort, configured threshold/reserve и объявленный InputContextWindow. Прежние RequireLocalEstimate/ServerValidation MUST требовать положительное окно каталога. Только явный ServerValidationWithUnknownWindow одновременно в settings и guard MAY разрешать null окно; assessment MUST сохранять null и RequiresServerValidation=true даже при известной текстовой оценке. ContextWindow/MaxOutputTokens MUST NOT подменять входной лимит. Default RequireLocalEstimate MUST отклонять неизвестную полную оценку. Explicit ServerValidation MAY разрешать opaque input с null estimate и допустимой KnownTokens+reserve, MUST сохранять null estimate и RequiresServerValidation=true, MUST NOT объявлять серверный приём гарантированным. Некорректные настройки и превышение известной части MUST отклоняться без integer overflow; ошибка counter MUST передаваться тем же ServiceError.

#### Scenario: Граница и неизвестный бюджет

- **WHEN** estimate с резервом равен входному лимиту
- **THEN** guard возвращает результат оценки и отдельный признак достижения threshold
- **AND** при null estimate в default RequireLocalEstimate вместо успеха возвращает Unsupported.

#### Scenario: Проверка правила — Проверка полного входного бюджета

- **WHEN** полная оценка неизвестна в default policy или каталог не даёт допустимый input window без явного ServerValidationWithUnknownWindow
- **THEN** guard отклоняет проверку без подмены лимита или ServiceError.

### Requirement: Резерв и порог полного бюджета

При известном окне Reserve MUST применяться отдельно от KnownTokens; estimate+reserve == input_context_window MUST быть допустимо по оценке. Threshold MUST достигаться при estimate >= threshold (при explicit server policy с null estimate — по KnownTokens); guard MUST NOT запускать compact, HTTP, DB или orchestration.

#### Scenario: Явная серверная проверка opaque бюджета

- **WHEN** приложение выбрало ServerValidation, input opaque и full estimate неизвестна
- **THEN** guard проверяет KnownTokens+reserve против input window и возвращает RequiresServerValidation=true при допустимой известной части
- **AND** estimate остаётся null; превышение известной части даёт Rejected, HTTP-отказ не повторяется и не меняет доступ.

#### Scenario: Проверка правила — Резерв и порог полного бюджета

- **WHEN** estimate плюс reserve равны input_context_window
- **THEN** оценка допустима; threshold проверяется отдельно без запуска compact или I/O.

#### Scenario: Неизвестное окно не подменяется порогом

- **WHEN** ServerValidationWithUnknownWindow разрешён, объявленное окно null, а локальная оценка достигла threshold
- **THEN** assessment сообщает достижение локального порога и обязательную серверную проверку, не обещая допустимость input или соблюдение reserve.

### Requirement: Явная оценка моделей без подтверждённой кодировки

Optional `TokenizationOptions.UnknownModelEstimateEncoding` MAY явно задавать `o200k_base`/`cl100k_base` для приблизительного BPE подсчёта ID без подтверждённого mapping; default null MUST сохранять Unsupported. Подтверждённый mapping MUST иметь приоритет. Результат MUST содержать IsApproximateEncoding=true и MUST NOT объявлять кодировку модели подтверждённой. KnownTokens в таком результате является приблизительным, opaque/continuation MUST сохранять null full estimate. Настройка MUST валидироваться безопасно и фиксироваться при создании singleton.

Guard/compactor MUST допускать такую оценку только с explicit ServerValidation либо ServerValidationWithUnknownWindow после catalog validation exact модели/effort/API-доступности и бюджета согласно выбранной policy. Строгий режим MUST запрещать approximation до generation/compact dispatch и до candidate save, включая force. Порог автоматического сжатия MAY использовать приблизительный текстовый count; это MUST NOT объявляться серверным подсчётом. Runner MUST сохранять происхождение последнего успешного generation assessment и сбрасывать его перед следующим шагом; status MUST различать approximate count и strict refusal.

#### Scenario: Новая catalog модель с неподтверждённой кодировкой

- **WHEN** модель/effort/input budget подтверждены каталогом, включены UnknownModelEstimateEncoding и ServerValidation
- **THEN** отправка и автоматическое сжатие используют явную приблизительную оценку без новой exact записи; окончательный приём бюджета решает сервер.

#### Scenario: Приблизительность не разрешает недоступную модель

- **WHEN** модель отсутствует в каталоге либо effort не поддерживается
- **THEN** отказ сохраняется до dispatch независимо от включённого оценочного словаря.
