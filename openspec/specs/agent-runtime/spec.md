# Agent Runtime

## Назначение

Проверяемые требования к AgentBridge, согласованные на этапе проектирования. Имя папки, проекта и GitHub-репозитория — `agent-bridge`. Навигация по описанию сценариев и техническим решениям: [Documentation](../../../Documentation/README.md).

## Требования

### Requirement: Проверяемое руководство бинарного потребителя

Руководство AgentBridge MUST предоставлять короткий вход и последовательность бинарного подключения, DI/options, создания диалога, нового run, инструментов, exact model/effort, shared/individual keys, status/expiry и bounded cleanup. Примеры C# MUST компилироваться с поставленными SQLite и PostgreSQL DLL вне репозитория без ProjectReference/PackageReference. Обязанности приложения, настраиваемые retention/soft bytes/token thresholds и ограничения typed failure/cancellation/Unknown/no-replay MUST быть явными. Compile-only методы MUST NOT исполняться ради этой проверки или объявляться runtime evidence.

#### Scenario: Потребитель читает пример нового обращения

- **WHEN** потребитель открывает руководство и исходники примера
- **THEN** доступны реальные public типы и явные регистрации зависимостей приложения
- **AND** успешная компиляция обоих бинарных вариантов отделена от исполнения модели, инструментов, БД и native runtime.

### Requirement: Честное закрытие первоначального плана

Карта этапов00–25 MUST ссылаться на фактические отчёты и различать реализованное поведение, выполненные проверки и непроверенные окружения. Пересекающиеся исторические suites MUST NOT суммироваться как общий итог. Актуальный checkpoint MUST отделять завершённую реализацию от приёмки/коммита и отсутствующей OpenSpec CLI validation; непроверенные changes MUST NOT архивироваться.

#### Scenario: Проверка завершения при недоступном CLI

- **WHEN** реализация и адресные проверки закончены, но OpenSpec CLI отсутствует
- **THEN** отчёт явно фиксирует невыполненную CLI validation и неархивированные changes
- **AND** статические проверки не называются CLI validation.

### Requirement: Автономный комплект DLL

Поставка AgentBridge MUST содержать ядро, CodexLb-адаптер, общее EF-хранилище, выбранную SQLite/PostgreSQL migrations assembly и полную runtime closure для явно указанного RID, включая обязательные EFCoreLibrary/HttpClientLibrary, tokenizer data и native assets. Состав MUST фиксироваться manifest с версиями и SHA256. Поставка MUST NOT требовать путей к дереву исходников или NuGet-публикации AgentBridge.

#### Scenario: Компиляция бинарного потребителя

- **WHEN** .NET10-потребитель и комплект скопированы за пределы дерева исходников
- **THEN** потребитель компилируется только с бинарными ссылками на комплект, без ProjectReference и PackageReference
- **AND** runtime/native файлы доставляются в output стандартной сборкой; compile-check не объявляется доказательством их загрузки.

### Requirement: Документация бинарных контрактов

Поставка MUST включать сгенерированные XML-файлы AgentBridge рядом с соответствующими DLL и доступные XML зависимостей. Проверка MUST подтверждать наличие описаний публичных контрактов и metadata-связь inheritdoc реализации с документированным контрактом. Неразвёрнутый inheritdoc MUST NOT объявляться готовым текстом для любой IDE. Generated XML MUST NOT редактироваться вручную.

#### Scenario: Реализация интерфейса в отдельной сборке

- **WHEN** потребитель читает metadata CodexLb-реализации и XML из комплекта
- **THEN** доступны inheritdoc реализации и русский summary соответствующего интерфейса ядра без исходников.

### Requirement: Явные внешние требования поставки

Документация MUST различать SQLite/PostgreSQL, выбранную migrations assembly, RID/native runtime, .NET10 runtime и внешние PostgreSQL dump-утилиты. Она MUST сохранять явную конфигурацию provider, SingleInitializer и отсутствие автоматического обслуживания при подключении DLL.

#### Scenario: PostgreSQL backup

- **WHEN** приложение выбирает PostgreSQL maintenance
- **THEN** оно отдельно предоставляет pg_dump с согласованным major, абсолютными путями и конечным cleanup timeout; наличие DLL не заменяет утилиту или права сервера.

### Requirement: Сквозное подтверждение завершённых сценариев

Проверка AgentRunner MUST проходить actual CodexLb adapter и HttpClientLibrary с fake handler/local streams совместно с actual SQLite/PostgreSQL через базовые CRUD и сценарные UoW EFCoreLibrary. Evidence MUST отличать реальные persistence проверки от isolated doubles и fake HTTP от live compatibility. Проверка MUST сохранять действующие canonical, fixed expiry, pinned access/settings и durable no-replay инварианты без автоматических retries.

#### Scenario: Повторяющиеся вызовы и перезапуск

- **WHEN** два завершённых шага модели используют один call_id через actual JSON/SSE transport
- **THEN** сохраняются отдельные attempts и полные canonical пары
- **AND** новый root не повторяет существующее обращение после подтверждённого либо неизвестного commit.

#### Scenario: Неполное состояние или конкурентная очистка

- **WHEN** сохранён partial function call, opaque compact без полной оценки либо диалог удалён во время действия
- **THEN** следующий неподдержанный запрос/запись отклоняется без fake output, повторного эффекта или восстановления удалённых данных.

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

### Requirement: Отдельный JSON compact transport

Compact gateway MUST отправлять canonical base-prefix POST /v1/responses/compact через HttpClientLibrary с per-call ModelAccess и конечным CompactTimeout. MUST сохранять exact model/effort, instructions, ordered input и unknown/opaque поля. MUST NOT подменять compact генерацией, повторять запрос или менять доступ. Continuation и неподдержанные параметры MUST отклоняться до HTTP. Compact MUST NOT создавать generation continuation из id ответа.

#### Scenario: Каноническое окно без status

- **WHEN** JSON object discriminator после trim начинается с response.compact, status отсутствует/null, output является массивом объектов и error отсутствует/null
- **THEN** gateway возвращает Completed с полными output/envelope и null continuation.

#### Scenario: Явный отказ или неизвестный lifecycle

- **WHEN** ответ содержит error либо status=failed
- **THEN** gateway сохраняет output/envelope как Failed с безопасной ошибкой
- **AND** иной неподтверждённый status или отсутствующий output даёт Incomplete; некорректная JSON-форма отклоняется.

### Requirement: Ошибки и отмена compact

Compact MUST сохранять правила безопасных HTTP ошибок и caller/deadline JSON transport. Explicit failure MUST иметь приоритет над поздней отменой; caller cancellation после полного отчёта MUST сохранять output/envelope в Canceled. Неожиданный I/O MUST распространяться. HTTP request/response MUST освобождаться на всех путях. Raw headers/body/reason/exception message MUST NOT публиковаться или логироваться как ошибка.

#### Scenario: Поздняя отмена

- **WHEN** caller отменён после полного compact JSON
- **THEN** известный результат сохраняется как Canceled, если ранее не установлен explicit failure.

### Requirement: Сохраняемый префикс и фиксированный полный запрос

Прикладной compact MUST использовать только активное окно и следующий непрерывный terminal prefix, включая0. InProgress и последующий хвост, provider items и новый несохранённый input MUST оставаться вне сохраняемого окна. Полный request MUST учитывать их при threshold/budget и после замены окна; providers MUST вызываться один раз на сценарий. Instructions/tools/controls MUST сохраняться в полном generation request. Compact MUST использовать отдельную проекцию поддержанных controls без tools. Известные неполные function pairs MUST отклоняться до отправки, без удаления данных.

#### Scenario: Завершённая история с текущим хвостом

- **WHEN** первый turn terminal, второй InProgress, а приложение добавило provider и новый input
- **THEN** compact получает только активное окно и ещё не покрытый первый turn
- **AND** следующий полный request содержит provider, новый compact output, второй turn и новый input ровно по одному разу.

### Requirement: Ограниченное принятие compact

Сценарий MUST проверять exact settings, считать полный request и запускать compact при estimate >= threshold. Unknown estimate MUST NOT заменяться KnownTokens либо прошлым usage. Число проходов MUST ограничиваться MaxPasses; known non-reduction MUST останавливать проходы без принятия увеличенного окна. Валидный Completed кандидат с unknown full estimate MUST сохраняться и возвращать UnknownBudget без дальнейших проходов или разрешения генерации. Пустой output при непустой compact history MUST отклоняться без сохранения. Успех SaveAsync MUST предшествовать активации. Save MUST получать исходный token либо результат предыдущего save и свежий UTC; now >= expiry, stale token, incomplete/failed/canceled и ошибки MUST сохранять последнее успешно принятое окно без retry. Внешний I/O MUST завершаться вне write UoW. Исходная история и fixed expiry MUST сохраняться. Compact payload MUST проходить отдельную проверку input budget перед HTTP. Статус отчёта MUST NOT объявляться разрешением generation без отдельного full-request guard.

#### Scenario: Неизвестный opaque бюджет

- **WHEN** Completed output имеет opaque состояние и full counter возвращает null estimate
- **THEN** version-aware save принимает окно и сценарий возвращает UnknownBudget
- **AND** guard17 по-прежнему отклоняет генерацию без полной оценки.

#### Scenario: Сбой второго прохода

- **WHEN** первый проход сохранён, а второй завершился ошибкой либо потерял актуальность
- **THEN** активным остаётся окно первого прохода, ошибка сохраняется без автоматического повторения.

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

### Requirement: Полная детерминированная композиция контекста

Composition MUST сохранять инструкции в ModelRequest.Instructions и объединять input в порядке: вклады разрешённых провайдеров приложения, Items активного StoredDialogContext, Items всех обращений после ThroughTurnSequence, новый ещё не сохранённый input. При отсутствии окна MUST включаться вся история. Порядок провайдеров и элементов MUST сохраняться. Tools, exact model/effort, parameters и явно переданный continuation MUST сохраняться в подготовленном ModelRequest. Envelope/continuation MUST NOT становиться input. StoredDialogTurn.Items MUST быть единственным источником элементов истории; output из StoredModelStep MUST NOT включаться повторно. Unknown/opaque поля и исходные роли MUST сохраняться без текстовой сводки или нормализации.

#### Scenario: Сжатое окно и хвост

- **WHEN** принятое окно покрывает первые два terminal обращения и третье ещё выполняется
- **THEN** input содержит Items окна, все Items третьего обращения и новый input
- **AND** output третьего обращения не дублируется из ModelSteps.

#### Scenario: Пустой префикс

- **WHEN** ThroughTurnSequence равен 0
- **THEN** все обращения включаются после Items активного окна без item cutoff.

### Requirement: Исходные роли и полные пары функций

Провайдер приложения MUST отвечать за авторизацию своего вклада. Composition MUST принимать его canonical items с исходными ролями без повышения до системной роли или ограничения набора ролей. Проверка известных function_call/function_call_output MUST выполняться по call_id во всей подготовленной последовательности, включая границы вкладов/окна/хвоста/new input. Каждый output MUST сопоставляться с предшествующим ещё не закрытым call того же ID; call_id MUST допускать повторное использование в разных парах. Известный function_call без последующего результата MUST давать явный безопасный отказ до возврата ModelRequest, включая сохранённый call с partial arguments после обрыва. Некорректная известная пара MUST отклоняться без выдумывания результата, удаления или изменения истории. Opaque/unknown элементы MUST сохраняться без попытки проверки скрытых внутри них вызовов; arguments/output MUST NOT переписываться.

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

### Requirement: Защищённая композиция и последовательные провайдеры

Composition MUST проверять соответствие dialog/owner прочитанного snapshot и явный UTC срок до вызова провайдеров. nowUtc >= ExpiresAtUtc MUST отклонять подготовку. Повреждённый порядок обращений, отсутствующая часть prefix, InProgress внутри покрытого prefix или непринятый compact MUST отклоняться явно без исправления данных. Composition MUST NOT менять snapshot, фиксированные даты, token или выдавать разрешение записи. Провайдеры MUST получать actual ApplicationCallContext и новый input, MUST вызываться последовательно с caller token, без fan-out. Ожидаемая ошибка MUST передаваться тем же ServiceError без частичного запроса; неожиданные exceptions MUST распространяться без fallback. Отмена MUST соблюдаться до провайдера и после его успешного завершения.

#### Scenario: Другой владелец или точная граница срока

- **WHEN** owner не совпадает либо nowUtc равен ExpiresAtUtc
- **THEN** запрос не возвращается и провайдеры не вызываются.

#### Scenario: Отказ второго провайдера

- **WHEN** первый провайдер успешен, а второй возвращает ожидаемую ошибку
- **THEN** composition передаёт ту же ошибку без запроса и не вызывает следующих провайдеров.

### Requirement: Потоковый Responses gateway

GenerateAsync MUST выбирать stream=true при наличии onUpdate и MUST сохранять JSON stream=false при null. SSE MUST читаться через HttpStreamResponseResult actual HttpClientLibrary. Parser MUST поддерживать строгий UTF-8 fragmentation, optional начальный BOM, LF/CRLF/CR, comments и многострочные data. [DONE], HTTP2xx, delta и EOF MUST NOT подтверждать Completed; незакрытый frame на EOF MUST NOT dispatch. Completed MUST требовать response.completed с completed response без error и canonical output либо собранными item events. Непустой response.output MUST быть авторитетным и заменять collected items; absent/empty MUST допускать backfill. Failed/incomplete MUST сохранять известные output/envelope; unknown/opaque поля и output order MUST сохраняться. Function arguments и текстовые delta MUST сохраняться в неполных items при EOF. Continuation MUST применять binding/allowlist/id rules JSON adapter.

#### Scenario: EOF после tool arguments

- **WHEN** поток содержит function_call и фрагменты arguments, но не terminal event
- **THEN** отчёт Incomplete сохраняет call_id/arguments/unknown поля без фиктивного completion.

#### Scenario: Авторитетный terminal output

- **WHEN** collected items содержат больше элементов, чем непустой terminal output
- **THEN** итоговый output точно соответствует terminal array и не содержит старых extra items.

### Requirement: Владение streaming lifecycle

Callbacks MUST вызываться последовательно и ожидаться, MUST NOT вызываться после возврата. Callback exceptions MUST распространяться неизменными и MUST NOT становиться server/JSON/timeout errors. Поток/обёртка MUST освобождаться при любом выходе. Caller cancellation до данных MUST распространяться с исходным token, после полученного отчёта MUST сохранять данные в Canceled. Deadline MUST возвращать typed Timeout; при наличии данных MUST сохранять их в Failed. Explicit typed failure MUST иметь приоритет над поздней отменой; caller MUST иметь приоритет над deadline. Unexpected I/O MUST распространяться без retries/fallback.

#### Scenario: Callback бросает JsonException

- **WHEN** callback бросает JsonException
- **THEN** вызывающий получает тот же exception после освобождения потока без synthetic server error и следующих callbacks.

### Requirement: Каноническая JSON генерация Responses

JSON gateway MUST отправлять canonical base-prefix POST /v1/responses через HttpClientLibrary с per-call ModelAccess и stream=false/store=false. Model/instructions/exact effort, ordered canonical input и полные function definitions MUST сохраняться. Поддержанные параметры MUST иметь независимый снимок; mandatory fields и effort MUST NOT переопределяться. Unknown top-level controls MUST давать Unsupported до HTTP, malformed/duplicate controls — Validation. Default include reasoning.encrypted_content MUST добавляться только при отсутствии explicit include. Output MUST сохранять порядок/unknown/opaque поля отдельно от полного envelope/continuation. Completed MUST требовать status=completed, output array и отсутствие explicit error; HTTP 2xx/видимый текст MUST NOT заменять это подтверждение. Failed/incomplete/unknown lifecycle MUST сохранять известный output/envelope.

#### Scenario: Нет видимого текста

- **WHEN** completed JSON содержит только function_call/reasoning/compaction
- **THEN** gateway сохраняет весь ordered output и полный независимый envelope как Completed.

#### Scenario: Нет canonical output

- **WHEN** JSON содержит completed, но не содержит output
- **THEN** gateway возвращает Incomplete с полным envelope, без фиктивного completion.

### Requirement: Безопасные ошибки и ограниченный JSON вызов

JSON gateway MUST нормализовать HTTP error по status и закрытым известным безопасным type/code/param только из Complete valid error envelope. Raw body/headers/reason/message/exception MUST NOT попадать в публичную ошибку/logger. Truncated/invalid/unsupported body MUST NOT трактоваться как complete envelope. Вызов MUST иметь конечный GenerationTimeout на отправку/чтение. До получения полного отчёта caller cancellation MUST распространяться OCE с исходным token; после полного отчёта MUST возвращаться Canceled с сохранёнными output/envelope/continuation. Explicit typed HTTP/model/JSON failure MUST сохранять приоритет над поздней отменой. Caller MUST иметь приоритет над deadline. Неожиданный I/O MUST распространяться без retry. Request/response MUST освобождаться на успехе/отказе/JSON error/отмене. Compact gateway MUST использовать отдельный compact-контракт, определённый требованием «Отдельный JSON compact transport».

#### Scenario: Поздняя отмена

- **WHEN** caller отменяется после полного получения canonical JSON
- **THEN** отчёт Canceled сохраняет output/envelope/continuation
- **AND** explicit typed failure не подменяется отменой.

#### Scenario: Error prefix и секретные values

- **WHEN** HTTP error body неполный, либо error type/code/param содержит неизвестный текст
- **THEN** public error сохраняет status и только известные безопасные поля
- **AND** raw message не публикуется, retry отсутствует.

### Requirement: Продолжение только своего JSON вызова

Continuation JSON adapter MUST связывать previous_response_id/x-codex-turn-state с dialog/owner/agent, endpoint и отпечатком выбранного ключа без сохранения ключа. Несовпадение либо unknown format MUST отклоняться до HTTP. Unknown metadata MUST сохраняться, но MUST NOT становиться input, произвольными headers или разрешением retry/смены account. Новый envelope без пригодного id MUST удалять старый previous_response_id; исходный id MUST сохраняться в envelope. Upstream ownership MUST оставаться ответственностью codex-lb.

#### Scenario: Другой контекст или ключ

- **WHEN** continuation передано с другим dialog/owner/agent/key/endpoint
- **THEN** gateway отказывает до HTTP без fallback.

#### Scenario: Новый ответ без anchor

- **WHEN** новый JSON не содержит пригодного id
- **THEN** новый continuation не отправляет previous_response_id старого ответа.

### Requirement: Динамический каталог выбранного ключа

AgentBridge MUST читать канонический `/v1/models` через HttpClientLibrary с per-call ModelAccess и без client_version. Каталог MUST NOT заменяться статическим списком, прошлым снимком другого ключа или скрытым retry. Снимки MUST сохранять необходимые объявленные budgets, усилия и флаги независимо от исходного JSON. Отсутствующий metadata MUST оставаться неизвестным.

#### Scenario: Изменение возможностей

- **WHEN** повторное чтение каталога получает другой input budget или набор effort
- **THEN** проверка нового выбора использует новый ответ
- **AND** прежний снимок остаётся неизменным.

### Requirement: Явная проверка модельных настроек

AgentBridge MUST проверять точный ID модели, supported_in_api и точный effort по выбранному каталогу. Сумма положительного TokenThreshold и неотрицательного InputTokenReserve MUST NOT превышать положительный metadata.input_context_window. Неизвестный budget MUST давать явный Unsupported; недопустимый выбор MUST NOT скрыто заменяться. Проверка MUST NOT объявляться токенизацией реального запроса или подтверждением compact/Responses поддержки.

#### Scenario: Граница бюджета

- **WHEN** threshold плюс reserve равны input_context_window
- **THEN** проверка бюджета успешна
- **AND** превышение на один токен отклоняется.

### Requirement: Источник индивидуального ключа без скрытой замены

Приложение MUST предоставлять источник индивидуального ключа по идентичности владельца. Только null MUST обозначать отсутствие. Заданный пустой или некорректный ключ, ошибка источника и HTTP-отказ MUST NOT разрешать общий ключ. Выбранный ModelAccess MUST оставаться per-call; безопасные снимки и ошибки MUST NOT включать ключи, адрес, подключения, raw error details или exception message.

#### Scenario: Неверный индивидуальный ключ

- **GIVEN** индивидуальный ключ задан и общий также настроен
- **WHEN** каталог отвечает отказом аутентификации
- **THEN** возвращается Unauthorized без данных
- **AND** повтор с общим ключом отсутствует.

### Requirement: Раздельные миграции выбранного провайдера

AgentBridge MUST предоставлять независимые SQLite/PostgreSQL migrations assemblies и snapshots для одного общего AgentBridgeDbContext. Runtime и design-time MUST выбирать одну и ту же устойчивую identity по provider. Эти проекты MUST владеть только таблицами AgentBridge и MUST NOT добавлять host или зависимости в Domain/Application. Design-time factory MUST создавать контекст без открытия соединения, SQL, применения схемы или чтения секретов приложения.

Runtime и design-time MUST явно выбирать отдельную служебную историю `__AgentBridgeMigrationsHistory` и MUST NOT использовать общий ledger `__EFMigrationsHistory` подключающего приложения. Служебная история EF MUST оставаться отдельной от mapped таблиц диалога и MUST NOT добавляться как persistence entity в модель AgentBridge.

#### Scenario: Изолированная история миграций

- **WHEN** SQLite или PostgreSQL options создаются runtime регистрацией либо design-time factory
- **THEN** provider history repository получает имя __AgentBridgeMigrationsHistory
- **AND** выбор истории не меняет snapshot или схему mapped таблиц.

#### Scenario: Создание модели для выбранного провайдера

- **WHEN** tooling использует SQLite или PostgreSQL target/startup проект
- **THEN** factory создаёт общий AgentBridgeDbContext с выбранным provider и его отдельной migrations assembly
- **AND** runtime выбирает ту же assembly identity.

#### Scenario: Сохранение принятых границ схемы

- **WHEN** создаётся provider-specific модель
- **THEN** сохраняются только собственные таблицы, составные keys/FK, cascade, UTC ticks, BINARY/C collation и expiry/Id index
- **AND** owner-list index и таблицы подключающего приложения не добавляются.

### Requirement: Короткое атомарное сохранение

Write ports MUST выполнять проверку существования, владельца, явного UTC срока и исходного incarnation/revision в одной транзакционной границе с записью root и детей через EFCoreLibrary. Изменение MUST повышать revision. Read ports MUST оставаться отдельными. Сеть и инструменты MUST NOT выполняться внутри transaction. Один scoped-контекст MUST NOT использоваться параллельно; tracked state MUST очищаться до возврата из операции.

#### Scenario: Неактуальный результат

- **WHEN** исходный token не совпадает с состоянием root
- **THEN** операция возвращает Conflict без сохранения и без подмены token

#### Scenario: Частичная ошибка

- **WHEN** staging или save завершается ошибкой до commit
- **THEN** транзакция откатывается и tracker очищается
- **AND** неожиданная ошибка не превращается в ожидаемый Conflict и автоматический retry не выполняется

#### Scenario: Неизвестный исход commit

- **WHEN** commit или cleanup завершается исключением
- **THEN** операция не сообщает успех или ожидаемый Conflict
- **AND** текущий scope больше не допускает операции

### Requirement: Валидированное восстановление доменного состояния

Domain MUST восстанавливать фиксированные даты, порядок, revision, времена изменений и все версии контекста без фиктивных mutations. Нарушения локальных инвариантов MUST отклоняться до выдачи агрегата. Локальная идентичность экземпляра MUST NOT заменять сохраняемый incarnation.

#### Scenario: Повреждённый terminal prefix

- **WHEN** сохранённая версия контекста покрывает дыру, незавершённое или завершённое позже неё обращение
- **THEN** восстановление отклоняет состояние без исправления или отбрасывания истории

### Требование: Независимые прикладные порты

Application MUST предоставлять независимые контракты шлюза модели, источника контекста, инструмента и подсчёта полного подготовленного запроса. Контракты MUST NOT содержать EF/DbContext/IQueryable/Expression, типы HTTP-библиотеки, Web/MVC или wire DTO. Ожидаемый отказ MUST передаваться семантической ошибкой без данных; успех с данными MUST содержать ненулевые данные. Получение lifecycle-отчёта MUST отличаться от подтверждённого завершения модели; неполный, ошибочный и отменённый отчёт MUST сохранять известный канонический output. Неожиданные исключения MUST NOT маскироваться успехом или ожидаемым отказом.

Канонические items, полный envelope и метаданные продолжения MUST сохранять неизвестные и opaque-поля независимо от срока жизни исходных данных. Результаты отдельных шагов модели и compact MUST иметь путь записи/чтения полного envelope отдельно от канонических input/output items. Снимок выбранного ключа MUST передаваться на вызов, а MUST NOT храниться в изменяемом глобальном состоянии шлюза или раскрывать секрет обычным строковым представлением. Локально известный token count MUST отличаться от оценки полного бюджета с opaque-содержимым.

Read-only порты MUST NOT требовать изменяющий UoW. Изменяющие порты MUST выражать короткие атомарные сценарии: существование, владелец, срок и сохраняемые incarnation/revision MUST проверяться в одной границе с записью. Сеть MUST NOT удерживать транзакцию БД. Доменный object-lifetime snapshot MUST NOT объявляться переносимым persistent token. Отказ MUST NOT изменять данные, а поздняя запись MUST NOT заново создавать удалённый диалог. Terminal-prefix metadata MUST NOT подменять границу отдельных протокольных items.

#### Сценарий: Независимость канонических данных

- **WHEN** исходный документ или список уничтожен либо изменён после создания прикладного снимка
- **THEN** снимок сохраняет полные канонические данные, порядок и неизвестные поля
- **AND** полный envelope результата остаётся отдельным от элементов следующего input.

#### Сценарий: Поздняя запись в новую жизнь диалога

- **GIVEN** диалог был удалён и создан заново с тем же публичным ID
- **WHEN** приходит результат со старым сохраняемым incarnation или revision
- **THEN** атомарный изменяющий сценарий отклоняет результат без изменения новой истории.

### Требование: Подключение к .NET-приложениям

AgentBridge MUST предоставлять C#-библиотеку для SDK-style приложений на .NET 10 (`net10.0`) с подключением обычных DLL. Ядро MUST быть независимо от ASP.NET Core, WPF и Telegram.

#### Сценарий: Подключение из разных приложений

- **WHEN** ASP.NET Core-приложение, WPF-приложение или Telegram-бот подключает AgentBridge
- **THEN** логика диалога и управления контекстом доступна через общее ядро
- **AND** интеграция с интерфейсом пользователя остаётся в подключающем приложении.

### Требование: Отдельный адаптер codex-lb

Интеграция с codex-lb MUST находиться в отдельном адаптере. Адаптер MUST передавать запросы генерации через Responses API codex-lb. Ядро MUST NOT зависеть от реализации этого адаптера.

#### Сценарий: Запрос к модели

- **WHEN** AgentBridge обрабатывает сообщение с выбранным адаптером codex-lb
- **THEN** адаптер передаёт подготовленный запрос в codex-lb
- **AND** codex-lb выполняет upstream-маршрутизацию
- **AND** AgentBridge получает результат через адаптер.

### Требование: Контекст и инструменты приложения

Подключающее приложение MUST определять доступные источники бизнес-контекста, разрешённые инструменты и права пользователя. AgentBridge MUST объединять инструкции агента, историю диалога, необходимый бизнес-контекст и новое сообщение. Запрошенные моделью инструменты MUST выполняться через зарегистрированные обработчики приложения с проверкой разрешений.

#### Сценарий: Уточнение статуса заказа

- **WHEN** пользователь спрашивает о своём заказе
- **AND** агент запрашивает разрешённый инструмент `GetOrderStatus`
- **THEN** AgentBridge вызывает обработчик приложения и передаёт его результат модели
- **AND** следующий вопрос пользователя об ожидаемой доставке продолжает тот же диалог.

### Требование: Настраиваемое хранение диалогов

AgentBridge MUST сохранять диалоги и необходимое состояние контекста в БД. Провайдер БД и параметры подключения MUST задаваться конфигурацией подключающего приложения. SQLite и PostgreSQL MUST поддерживаться как опциональные провайдеры общего хранилища; SQLite MUST NOT быть обязательным выбором. Ядро MUST NOT зависеть от конкретной БД.

#### Сценарий: Выбор БД

- **WHEN** приложение настраивает SQLite или PostgreSQL как хранилище AgentBridge
- **THEN** диалоги сохраняются через выбранный провайдер
- **AND** замена провайдера не требует изменения бизнес-логики агента.

### Требование: Формат общего EF-хранилища

Persistence-модель MUST отделяться от доменного агрегата и MUST сохранять владельца, фиксированные даты, incarnation/revision, порядок обращений и items. Полный результат каждого шага модели и принятого compact MUST сохраняться отдельно от канонических items истории, включая lifecycle, output, envelope, continuation и ожидаемую ошибку. Результат compact MUST иметь Completed status. Связи MUST исключать присоединение дочерних строк к обращению другого диалога и MUST каскадно удалять зависимые строки вместе с диалогом. Перечисленные concurrency metadata MUST NOT объявляться реализацией атомарных application guards. Per-call ModelAccess/API keys MUST NOT сохраняться в persistence-моделях.

#### Сценарий: Сохранение результата инструмента и envelope

- **WHEN** сериализуется история с function_call_output и отдельным результатом шага
- **THEN** полные неизвестные поля и call_id остаются в соответствующих канонических данных
- **AND** envelope и continuation остаются отдельно от следующего input.

#### Сценарий: Выбор общего контекста

- **WHEN** приложение явно выбирает SQLite или PostgreSQL
- **THEN** DI регистрирует один scoped-контекст через AddEfCoreContext и AddEfCoreBaseRepositories
- **AND** регистрация не открывает БД, не запускает migrations и не выбирает SQLite при отсутствии настройки.

### Требование: Адаптация базовых репозиториев

Адаптеры AgentBridge MUST делегировать чтение и staging create/update/delete актуальным базовым репозиториям EFCoreLibrary. Поиск дочерней строки по локальному ID MUST включать всех родителей её локального ключа. Коллекции MUST сортироваться по сохраняемым Sequence/Version; ограничение кандидатов очистки MUST применяться после сортировки ExpiresAtUtc/Id. Custom query MUST применяться только при недостаточности base predicate/include API. Staging MUST NOT выдавать подтверждение сохранённого успеха изменяющего Application port и MUST NOT выполнять SaveChanges или транзакции.

#### Сценарий: Одинаковые локальные ID

- **GIVEN** разные диалоги содержат обращения с одинаковым ID, а разные обращения — шаги с одинаковым ID
- **WHEN** адаптер читает дочернюю строку
- **THEN** predicate включает DialogId и, для шага, TurnId
- **AND** чужая строка не возвращается.

#### Сценарий: Защищённое полное чтение

- **WHEN** владелец читает существующий диалог, включая истёкший до физического удаления
- **THEN** возвращаются сохраняемый token, фиксированные даты, объём, вся упорядоченная история и максимальная версия compact
- **AND** lifecycle/output/envelope/continuation/error остаются полными и отдельными от items
- **AND** ThroughTurnSequence не отбрасывает историю.

#### Сценарий: Отказ чтения

- **WHEN** диалог отсутствует либо владелец не совпадает ordinal
- **THEN** чтение возвращает соответственно NotFound или Forbidden без данных
- **AND** дочерние данные не читаются и staging не выполняется.

#### Сценарий: Изменение root во время чтения

- **GIVEN** чтение зафиксировало primitive owner/incarnation/revision до загрузки детей
- **WHEN** повторное base-чтение root после детей обнаруживает удаление, смену владельца, incarnation или revision
- **THEN** возвращается NotFound, Forbidden или Conflict без snapshot
- **AND** новый token не подставляется к смешанным данным и скрытый retry не выполняется.

#### Сценарий: Повреждённая история

- **WHEN** при стабильном root item или step не имеет родительского turn в прочитанной истории либо принятый compact не Completed
- **THEN** чтение явно отклоняет повреждённые данные без молчаливого отбрасывания.

#### Сценарий: Несохранённые изменения

- **WHEN** Infrastructure ставит create/update/delete в scoped session
- **THEN** вызывается соответствующая базовая операция EFCoreLibrary
- **AND** успех атомарного изменяющего Application port не объявляется.

### Требование: Продолжение диалога

AgentBridge MUST хранить историю и состояние, необходимые для продолжения диалога после перезапуска приложения. Диалоги разных пользователей MUST быть изолированы. Сохранение состояния Responses MUST учитывать элементы инструментов, reasoning и compaction, необходимые для последующего продолжения, а не только видимый текст ответа.

#### Сценарий: Продолжение после перезапуска

- **WHEN** приложение перезапущено
- **AND** пользователь продолжает сохранённый диалог в пределах политики хранения
- **THEN** AgentBridge восстанавливает доступный контекст из БД
- **AND** не использует историю другого пользователя.

### Требование: Ограничения хранения

Подключающее приложение MUST задавать мягкий порог объёма содержимого на диалог в байтах и срок хранения от создания диалога. Байты сообщений, результатов инструментов и состояний контекста MUST входить в метрику; физические индексы и overhead провайдера MUST NOT выдаваться за объём содержимого. Превышение мягкого порога MUST NOT само по себе удалять историю или блокировать диалог. Политика срока хранения MUST распространяться также на производные состояния контекста и результаты сжатия.

#### Сценарий: Превышение мягкого порога

- **WHEN** содержимое диалога превысило настроенный порог байтов
- **THEN** приложение получает предупреждение и текущий объём
- **AND** история сохраняется до истечения срока или явного удаления
- **AND** диалог может продолжаться при допустимом входном бюджете модели.

#### Сценарий: Истечение срока хранения

- **WHEN** срок хранения данных диалога истёк
- **THEN** AgentBridge применяет настроенную политику удаления
- **AND** удалённые сведения не сохраняются бессрочно только потому, что были включены в производный контекст.

#### Сценарий: Активность не продлевает срок

- **GIVEN** при создании диалог получил срок истечения
- **WHEN** в диалоге появляются новые сообщения или выполняется compact
- **THEN** срок истечения не переносится на дату последней активности.

### Требование: Дата истечения диалога

AgentBridge MUST сохранять `CreatedAtUtc` и `ExpiresAtUtc`, вычисленный из времени создания и настроенного срока хранения при создании. Пробное значение 7 дней MUST быть переопределяемым через конфигурацию и MUST NOT подменять настроенный срок в логике истечения. Приложение MUST иметь возможность получить эту дату и признак истечения. Просроченный диалог MUST NOT использоваться для нового обращения даже до физической очистки строк.

#### Сценарий: Отображение срока в интерфейсе

- **WHEN** приложение запрашивает состояние диалога
- **THEN** оно получает `ExpiresAtUtc` и признак истечения
- **AND** может показать пользователю срок без самостоятельного вычисления политики.

#### Сценарий: Срок хранения из конфигурации

- **GIVEN** приложение настроило срок хранения 14 дней вместо пробных 7
- **WHEN** создаётся новый диалог
- **THEN** его срок истечения равен времени создания плюс 14 дней
- **AND** логика очистки использует этот срок, а не пробное значение.

### Требование: Сжатие рабочего контекста

AgentBridge MUST управлять ограниченным рабочим окном контекста отдельно от хранения полной истории. Сжатие MUST запускаться по числу токенов рабочего контекста; порог MUST задаваться конфигурацией подключающего приложения. При проверке порога MUST учитываться подготовленный контекст обращения, включая инструкции, бизнес-данные, историю, новое сообщение и необходимые инструменты. Сжатие MUST сохранять новое состояние контекста, связанное с исходным диалогом, и MUST NOT само по себе означать удаление исходной истории из БД.

Размер текстового содержимого MUST определяться tokenizer соответствующей известной кодировки модели. Непрозрачное reasoning/compaction MUST NOT объявляться точно посчитанным локальным текстовым tokenizer. Повторное сжатие MUST быть ограничено числом проходов на обращение и прекращаться при отсутствии уменьшения; количество сжатий за жизнь диалога MUST NOT само по себе приводить к автоматическому созданию нового диалога.

#### Сценарий: Продолжение со сжатым контекстом

- **WHEN** рабочий контекст диалога сжимается
- **THEN** AgentBridge сохраняет результат сжатия как новое состояние контекста
- **AND** использует его для последующих обращений
- **AND** исходная история остаётся под управлением отдельной политики хранения.

#### Сценарий: Изменение порога сжатия

- **WHEN** приложение меняет настроенный порог в токенах
- **THEN** проверка сжатия использует новое значение после применения конфигурации
- **AND** изменение не требует правки исходного кода бизнес-логики AgentBridge.

### Требование: Использование EFCoreLibrary

Вся работа AgentBridge с БД MUST строиться на EFCoreLibrary. Базовые read/create/update/delete репозитории MUST иметь приоритет над custom query. Недостаточность или спорный контракт библиотеки MUST обсуждаться с пользователем до выбора обходной реализации; AgentBridge MUST NOT заменять библиотеку прямым EF/SQL или собственным параллельным слоем.

#### Сценарий: Удаление истории

- **WHEN** сценарий удаляет историю диалога
- **THEN** используются базовые delete-операции EFCoreLibrary и сценарная граница сохранения
- **AND** зависимые сохраняемые состояния обрабатываются вместе с историей.

### Требование: Использование HttpClientLibrary

Исходящие HTTP-запросы адаптера codex-lb MUST выполняться через HttpClientLibrary. Требуемое развитие её контракта MUST согласовываться с пользователем; отдельный HTTP pipeline для обхода библиотеки MUST NOT создаваться.

#### Сценарий: HTTP Responses

- **WHEN** адаптер отправляет запрос в `/v1/responses`
- **THEN** HTTP-запрос выполняется через HttpClientLibrary
- **AND** интерпретация JSON/SSE Responses остаётся в адаптере.

### Требование: Безопасное логирование HTTP

Логирование AgentBridge MUST проходить через Serilog, подключённый приложением, и `ILogger<T>`. По умолчанию диагностика HTTP MUST содержать статус и безопасные метаданные. Логирование содержимого MUST требовать явной настройки. Секреты MUST NOT включаться в диагностические сообщения.

#### Сценарий: Ошибка HTTP без включённого содержимого

- **WHEN** HTTP-запрос завершился ошибкой
- **AND** логирование содержимого явно не включено
- **THEN** body и response snippet не записываются в журнал.

#### Сценарий: Явно включённая структура HTTP-ошибки

- **WHEN** приложение явно включает JsonStructure
- **THEN** диагностика MUST включать только фиксированные признаки структуры полного валидного JSON и счётчики узлов
- **AND** исходные имена полей и значения MUST NOT записываться
- **AND** неполное или невалидное содержимое MUST NOT разбираться как полный JSON
- **AND** успешный SSE MUST NOT предварительно читаться ради диагностики.

### Требование: Транспортные данные HTTP-ошибки

HttpClientLibrary MUST сохранять HTTP-статус и независимый снимок response/content headers ошибки. Тело MUST иметь настраиваемый предел, по умолчанию 65536 байтов, и явное состояние Empty/Complete/Truncated/UnsupportedContent/InvalidEncoding. Truncated MUST сохранять корректно декодируемый ограниченный prefix без замены незавершённого символа; реальные неверные байты MUST обозначаться InvalidEncoding. Raw error details MUST NOT логироваться. Message исключения MUST содержать только `HTTP <status>.`. Интерпретация error envelope MUST оставаться обязанностью адаптера.

#### Сценарий: Ошибка превышает предел тела

- **WHEN** HTTP-ошибка содержит тело больше настроенного предела
- **THEN** чтение MUST ограничиваться пределом плюс одним проверочным байтом
- **AND** состояние MUST обозначаться Truncated для корректно декодируемого prefix
- **AND** preview MUST NOT интерпретироваться как полный error envelope.

### Требование: Чтение настроек и выбор модели

AgentBridge MUST предоставлять безопасное представление текущих настроек конкретного диалога, включая effective модель, effort, лимиты, token истории и отдельную версию выбора из того же read snapshot. Выбор model/effort MUST сохраняться в БД AgentBridge для конкретного диалога. Приоритет MUST быть: override обращения → сохранённый выбор диалога → defaults приложения. Override MUST NOT менять сохранённый выбор. Выбор MUST проверяться текущим каталогом доступного ключа; недопустимые значения MUST NOT заменяться скрытым fallback. Секреты, инструкции, строки подключения, headers, raw envelope и canonical содержимое MUST NOT возвращаться в safe settings/status.

#### Сценарий: Изменение усилия запроса

- **WHEN** приложение указывает допустимый effort конкретного обращения
- **THEN** он имеет приоритет над сохранённым effort диалога и default приложения
- **AND** выбранное значение используется в этом обращении без изменения уже выполняющихся запросов.

### Requirement: Независимая версия выбора и снимок хода

DialogSettings MUST иметь собственную CAS Version, связанную с диалогом каскадным удалением. Запись MUST проверять owner, expiry, incarnation, исходную revision истории и expected settings version в коротком сценарном UoW через EFCoreLibrary. Она MUST NOT изменять revision истории, даты или ContentBytes содержимого. Устаревший token/version MUST давать Conflict без refresh/retry; неожиданные driver/commit/cleanup ошибки MUST NOT маскироваться Conflict. BeginWithSettings MUST атомарно сохранять immutable primitive snapshot model/effort/входного окна/порога/запаса. Активный ход MUST продолжать использовать его после нового выбора; новый выбор MUST действовать со следующего обращения. Historical turn settings и selected compact provenance MUST оставаться nullable; миграции MUST NOT выдумывать исходный выбор. Settings/snapshot/provenance MUST NOT учитываться как ContentBytes содержимого.

#### Scenario: Смена выбора при активном ходе

- **WHEN** после начала хода приложение сохраняет новый выбор с актуальной отдельной версией
- **THEN** текущий ход сохраняет прежние model/effort и действующий token истории
- **AND** следующий ход использует новый выбор, а запись со старой settings version получает Conflict.

#### Scenario: Исторические строки после Down и Up

- **WHEN** AddDialogSettings удаляется и применяется повторно на тестовой БД с историей
- **THEN** история, полный compact envelope, fixed dates и ContentBytes сохраняются
- **AND** удалённые settings/snapshot/selected provenance возвращаются null, без подмены server именем.

### Requirement: Подтверждение совместимости непрозрачного контекста

AgentBridge MUST проверять активное compact окно и непокрытый хвост перед сменой выбранной модели и перед началом хода. Наличие opaque MUST определяться отдельным model-independent inspector; ошибка tokenizer mapping MUST NOT служить доказательством opaque. Для opaque другой или неизвестной исходной selected модели MUST требоваться явное подтверждение порта приложения. Без подтверждения MUST возвращаться Unsupported без изменения выбора или истории. Порт MUST получать исходные selected model, фактическую server model при наличии и новый проверенный выбор раздельно. Равенство server/selected имён MUST NOT доказывать совместимость. Повторяющиеся canonical occurrences MUST сохраняться; report output MUST NOT проверяться повторно как input без своих server metadata.

#### Scenario: Историческое opaque окно

- **WHEN** selected provenance неизвестна и server model совпадает с новым выбором
- **THEN** без подтверждения приложения возвращается Unsupported
- **AND** исходный контекст и сохранённый выбор остаются неизменными.

#### Scenario: Только текст и неизвестное tokenizer mapping

- **WHEN** каталог разрешает модель и сохранённое содержимое полностью текстовое
- **THEN** выбор допустим без compatibility порта
- **AND** недоступная токенизация отдельно возвращается ошибкой оценки, без объявления текста opaque.

### Requirement: Безопасный статус сохранённого диалога

Статус MUST возвращать token, CreatedAtUtc/ExpiresAtUtc, истечение при fresh now >= expiry, ContentBytes, мягкий порог и достижение, число принятых compact, safe settings/сохранённый выбор, selected model/effort отдельно от последней server model генерации, known tokens, nullable full estimate и nullable достижение token threshold. Отказ каталога, несовместимость или неизвестная оценка MUST возвращаться отдельно от доступных метаданных и MUST запрещать CanContinue. Истёкший диалог до удаления MUST сохранять доступные метаданные статуса. Оценка MUST относиться только к сохранённому рабочему input без transient providers/new input/tools и MUST NOT подтверждать бюджет полного следующего запроса. Known tokens MUST NOT подменять неизвестную полную оценку. Мягкий порог MUST NOT удалять историю или запрещать запись.

#### Scenario: Истечение с неизвестным бюджетом

- **WHEN** чтение статуса достигает ExpiresAtUtc и содержит opaque без полной оценки
- **THEN** возвращаются срок, байты и known tokens, full estimate и threshold indicator остаются null
- **AND** IsExpired=true и CanContinue=false; история не очищается.

#### Scenario: Последний ответ без server model

- **WHEN** последний report генерации не содержит model
- **THEN** ServerModel=null независимо от выбранной модели и предыдущих report
- **AND** raw envelope и секреты не возвращаются.

### Требование: Индивидуальный и общий ключи

Заданный индивидуальный ключ пользователя MUST иметь приоритет над общим ключом приложения. Общий ключ MUST использоваться при отсутствии индивидуального. Ошибка заданного индивидуального ключа MUST NOT приводить к скрытой повторной отправке с общим ключом.

#### Сценарий: У пользователя нет ключа

- **WHEN** приложение не предоставило индивидуальный ключ пользователя
- **THEN** запрос использует настроенный общий ключ.

### Requirement: Явное подключение обслуживания AgentBridge

EF-адаптер AgentBridge MUST регистрировать scoped `IDatabaseMaintenance<AgentBridgeContextKey>` через общий coordinator EFCoreLibrary и выбирать только SQLite/PostgreSQL по DatabaseOptions. SingleInitializer MUST задаваться явно. Регистрация и разрешение сервисов MUST NOT открывать соединение, создавать файлы, запускать backup, migrations, процессы или фоновые задачи. Singleton gate MUST оставаться общим на root container и MUST NOT захватывать scoped context. Результаты, безопасные ошибки, отмена и диагностика стадий MUST сохранять контракт EFCoreLibrary без автоматического retry или fallback initialization.

#### Scenario: Подключение без обслуживания

- **WHEN** приложение регистрирует и разрешает maintenance сервис в отдельном scope
- **THEN** доступен API InspectAsync/UpdateExistingAsync/InitializeNewAsync выбранного provider
- **AND** никакая операция обслуживания не начинается до явного вызова приложения.

### Requirement: Явные настройки backup

AgentBridge MUST требовать абсолютный backup directory и явно заданный положительный срок хранения backup без значения по умолчанию. Для PostgreSQL MUST требоваться абсолютный путь pg_dump, явный major сервера 10+ и конечный положительный cleanup timeout. Настройки MUST проверяться локально до maintenance I/O без раскрытия значений в ошибках. Backup retention MUST оставаться обязанностью приложения и MUST NOT запускать purge или подменяться expiry диалогов. Format, scope, receipt, private workspace и защита от перезаписи MUST делегироваться EFCoreLibrary.

#### Scenario: Срок backup не выбран

- **WHEN** приложение не задало срок хранения backup
- **THEN** локальная валидация отклоняет настройки до обслуживания
- **AND** срок хранения диалогов не используется как запасное значение.

### Требование: Backup и migrations через EFCoreLibrary

AgentBridge MUST предоставлять вызываемую приложением операцию обслуживания схемы: check → backup существующей БД при pending migrations → migrate. Backup-возможности SQLite/PostgreSQL MUST строиться на развиваемом контракте EFCoreLibrary; прямой SQL Server backup в AgentBridge MUST NOT служить заменой этой зависимости. Ошибка обязательного backup MUST останавливать migration. Подключение DLL MUST NOT само по себе запускать обслуживание.

#### Сценарий: Обновление существующей БД

- **WHEN** обслуживание обнаруживает pending migrations схемы AgentBridge
- **THEN** успешный backup через EFCoreLibrary предшествует применению migrations
- **AND** ошибка backup не скрывается продолжением migration.

### Требование: Явный и единоличный режим обслуживания

Контракт обслуживания EFCoreLibrary MUST разделять inspection, обновление существующей БД и явную первую установку. Missing MUST подтверждаться механизмом provider; ошибки аутентификации, прав, подключения и проверки EF target MUST NOT автоматически разрешать CREATE. Режим SingleInitializer MUST выбираться явно; приложение MUST исключать другие экземпляры, writes и DDL на весь период обслуживания. Локальный gate MUST NOT объявляться распределённой блокировкой. Транзакции вызывающего кода и автоматические EF retry strategies MUST отклоняться.

#### Сценарий: Первая установка

- **WHEN** приложение явно выбирает initialization и provider подтверждает Missing
- **THEN** библиотека создаёт БД, проверяет цель и применяет migrations зарегистрированного контекста без фиктивного backup
- **AND** отказ после начала установки запрещает последующее обслуживание тем же gate до внешнего recovery.

### Требование: Подтверждение backup и владение ресурсами

EFCoreLibrary MUST предоставлять расширяемый реляционный контракт и отдельные optional реализации SQLite, PostgreSQL, SQL Server и MySQL. Набор providers AgentBridge MUST оставаться SQLite/PostgreSQL. Receipt MUST связывать operation, фактический target, provider, format, scope, время операции и подтверждённый артефакт до migrations; target MUST повторно проверяться. При отсутствии pending migrations существующей БД backup MUST NOT создаваться. Внешние dump-инструменты MUST запускаться только при явном вызове обслуживания, без shell и произвольных CLI arguments. Успешные backup MUST NOT удаляться автоматически.

#### Сценарий: Неизвестное завершение backup

- **WHEN** остановка внешнего процесса или уже отправленной server command не подтверждена
- **THEN** библиотека не выдаёт подтверждённый receipt, запрещает migration и блокирует gate
- **AND** неподтверждённые процессы и используемые ими приватные файлы сохраняют владельца до явного recovery
- **AND** recovery временных ресурсов не сбрасывает блокировку обслуживания автоматически.

#### Сценарий: Ошибка и отмена

- **WHEN** обслуживание завершается ошибкой или фактической отменой
- **THEN** наружу и в лог передаются безопасные коды без исходного driver message, credentials и connection string
- **AND** уже установленная typed failure не заменяется одновременно сработавшим cancellation/deadline
- **AND** вторичная неопределённость cleanup сохраняет безопасный первичный код и блокирует повтор.

### Требование: Очистка через базовые репозитории

AgentBridge MUST предоставлять вызываемую приложением очистку истёкших диалогов. Чтение и удаление MUST использовать базовые read/delete операции EFCoreLibrary и сценарную границу сохранения. Приложение MUST владеть расписанием вызова очистки. Один вызов MUST читать не более одного пакета с явным положительным limit и MUST NOT выполнять повторную выборку или automatic retry. Каждый read/delete MUST получать отдельный короткий async DI scope; удаления MUST выполняться последовательно со свежим UTC непосредственно перед вызовом порта. Истечение MUST включать равенство nowUtc = ExpiresAtUtc. Existing incarnation/revision/expiry guards MUST повторно проверяться атомарным deletion port; DialogSettings и все зависимые данные MUST удаляться каскадно.

Отчёт MUST отдельно сохранять подтверждённые удаления, ожидаемые отказы, неизвестные исходы и не начатые кандидаты. Ожидаемый отказ одного удаления MUST сохраняться в отчёте и MUST NOT препятствовать обработке остальных кандидатов прочитанного пакета. Отмена MUST останавливать следующие операции, сохраняя принятые результаты. Неожиданные exceptions MUST распространяться с доступным последним отчётом без raw exception в DTO; primary и scope cleanup failures MUST сохраняться вместе. Подтверждённый port success MUST сохраняться в отчёте даже при последующей ошибке DisposeAsync. Успешная обработка пакета MUST NOT означать отсутствие других истёкших строк в БД. Мягкий bytes threshold MUST NOT запускать очистку.

#### Сценарий: Очистка по сроку

- **WHEN** приложение вызывает очистку с limit2 в момент точного истечения трёх диалогов
- **THEN** не более двух кандидатов обрабатываются через EFCoreLibrary
- **AND** третий остаётся до отдельного вызова приложения
- **AND** мягкий порог байтов не подменяет условие истечения срока.

#### Сценарий: Частичный отказ пакета

- **WHEN** первое удаление подтверждено, второе возвращает Conflict и третье подтверждено
- **THEN** отчёт содержит два Deleted и один Failed с исходной semantic error
- **AND** отказавший кандидат не перечитывается и не повторяется.

#### Сценарий: Отмена или неизвестный исход

- **WHEN** после принятого удаления следующая операция прерывается отменой либо неожиданным исключением
- **THEN** принятое удаление остаётся Deleted, начатая неподтверждённая операция — Unknown, следующие кандидаты — NotAttempted
- **AND** никакой неизвестный исход не объявляется успешным.

### Требование: Подтверждённое завершение Responses

Потоковый ответ MUST NOT считаться успешным только по полученному тексту или HTTP 2xx. EOF без подтверждённого завершения MUST отличаться от завершённого результата. Частичные данные MUST NOT сохраняться как успешное завершение диалога.

#### Сценарий: Обрыв SSE после text delta

- **WHEN** поток возвращает text delta и затем закрывается без terminal completion
- **THEN** результат классифицируется как незавершённый
- **AND** не становится успешным завершённым ответом только из-за непустого текста.

### Требование: Согласование удаления и выполняющегося обращения

Удаление и сохранение результата MUST учитывать актуальность состояния диалога. Результат обращения, начатого до удаления или очистки, MUST NOT восстанавливать удалённую историю и контекст.

#### Сценарий: Удаление во время ожидания модели

- **WHEN** диалог очищен или удалён во время ожидания upstream-ответа
- **THEN** позднее сохранение ответа не восстанавливает удалённые сообщения и состояние.

### Требование: Отсутствие поиска по старой истории

Текущая версия AgentBridge MUST NOT предоставлять поиск по старым сообщениям или автоматически возвращать их в контекст через такой поиск.

#### Сценарий: Продолжение после сжатия

- **WHEN** пользователь продолжает диалог со сжатым состоянием
- **THEN** AgentBridge использует актуальное состояние и доступные последующие сообщения
- **AND** не выполняет поиск по старой истории.

### Требование: Защищённое доменное состояние диалога

Доменное состояние MUST быть независимо от HTTP, EF и идентичностей конкретного интерфейса. Идентичность, владелец и фиксированные даты MUST NOT изменяться обычным прикладным присваиванием. Обращения MUST иметь явный порядок начала и отличать выполняющееся состояние от конечного успешного, ошибочного, отменённого и неполного результата. Конечный статус MUST NOT переписываться повторным завершением. Доменные операции MUST получать время явно в UTC. При `nowUtc >= ExpiresAtUtc` новое обращение и принятие позднего результата MUST отклоняться. Принятие контекста или результата по устаревшему снимку MUST NOT менять актуальное состояние.

#### Сценарий: Независимое владение и порядок

- **WHEN** переданная идентичность пользователя не совпадает с владельцем
- **THEN** домен отклоняет операции над диалогом без изменения состояния
- **AND** обращения допустимого владельца упорядочиваются по началу, независимо от одинаковых дат и порядка завершения.

#### Сценарий: Точная граница доступности

- **GIVEN** диалог создан с настроенным сроком истечения
- **WHEN** переданное время равно `ExpiresAtUtc`
- **THEN** новое обращение, позднее завершение и принятие compact отклоняются
- **AND** ранее зафиксированный срок не продлевается.

#### Сценарий: Устаревший результат контекста

- **GIVEN** операция получила снимок версии диалога
- **WHEN** диалог изменился или был удалён до принятия результата
- **THEN** старый снимок не разрешает изменить обращения или активный контекст.

### Требование: Покрытие контекста завершёнными обращениями

Покрытие истории на уровне обращений MUST представлять только непрерывный префикс обращений с конечными статусами. Значение 0 MUST означать отсутствие покрытых обращений. Выполняющееся обращение MUST NOT считаться полностью покрытым. Покрытие принятой следующей версии MUST NOT возвращаться назад и MUST NOT выходить за существующую историю. Повторное сжатие того же префикса MUST быть допустимо без удаления обращений. Эти метаданные MUST NOT заменять точную границу отдельных элементов Responses или служить основанием пропустить будущие результаты текущего обращения.

#### Сценарий: Пустой префикс и повторное сжатие

- **WHEN** ни одно обращение ещё не покрыто контекстом
- **THEN** допустимо принять версию с покрытием 0
- **AND** повторное принятие того же префикса создаёт следующую версию, сохраняя историю.

#### Сценарий: Незавершённое обращение внутри префикса

- **GIVEN** первое обращение выполняется, а второе уже имеет конечный статус
- **WHEN** результат compact объявляет покрытым префикс до второго обращения
- **THEN** домен отклоняет покрытие без изменения предыдущего контекста
- **AND** будущие ответы и результаты инструментов первого обращения не считаются уже покрытыми.

### Requirement: Полный ход агента

AgentRunner MUST фиксировать owner, dialog incarnation/revision, настройки модели, инструкции и tool selection в начале run. Providers MUST вызываться один раз на сценарий. Генерация MUST проходить полный ContextBudgetGuard после любого compact outcome и tool шага. Stream updates MUST оставаться предварительными; Completed MUST возвращаться только после подтверждённого model completion и успешного terminal save. Canonical output и полный ModelResponse MUST сохраняться без нормализации неизвестных данных.

#### Scenario: Модель вызывает инструмент

- **WHEN** Completed model step содержит pending function calls
- **THEN** полный step и calls сохраняются до исполнения
- **AND** после confirmed outputs следующий полный request проходит guard заново.

### Requirement: Durable защита действий инструмента

Перед handler AgentRunner MUST успешно сохранить Started для owner/dialog/incarnation/turn/agent + StepId + исходная output position. Каждая запись MUST использовать отдельный короткий scope/UoW, завершённый до внешнего I/O. Checkpoint writes и token updates одного run MUST сериализоваться. Unknown commit, conflict, восстановленный Started и повтор попытки MUST NOT разрешать handler или retry. Outcomes и confirmed outputs MUST сохраняться атомарно. Unknown MUST NOT получать выдуманный function_call_output; повторные completed пары call_id MUST оставаться допустимыми.

#### Scenario: Restart после действия

- **GIVEN** Started сохранён и процесс остановился без outcome
- **WHEN** приложение повторяет run для того же turn
- **THEN** handler не запускается повторно
- **AND** сохранённая canonical история остаётся без изменения.

### Requirement: Честное завершение и актуальность

AgentRunner MUST сохранять partial reports и confirmed соседние tool outputs, включая LastResult после exception/cancel, и MUST ожидать все начатые tasks/scopes. Отмена и partial/Unknown MUST NOT объявляться успехом. Каждая запись MUST получать fresh UTC и original либо successful-save token. Expiry/delete/cleanup/conflict MUST отклонять late writes без recreation, refresh или automatic retry. ModelAccess MUST NOT сохраняться.

#### Scenario: Поздний результат

- **WHEN** инструмент завершился после expiry либо удаления диалога
- **THEN** запись отклоняется
- **AND** итог не сообщает Completed или подтверждённое сохранение.
