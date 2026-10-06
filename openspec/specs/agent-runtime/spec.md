# Agent Runtime

## Purpose

Проверяемые требования к AgentBridge, согласованные на этапе проектирования. Имя папки, проекта и GitHub-репозитория — `agent-bridge`. Навигация по описанию сценариев и техническим решениям: [Documentation](../../../Documentation/README.md).

## Requirements

### Requirement: Проверяемое руководство бинарного потребителя

Руководство AgentBridge MUST предоставлять короткий вход и последовательность бинарного подключения, DI/options, создания диалога, нового run, инструментов, exact model/effort, shared/individual keys, status/expiry и bounded cleanup.

#### Scenario: Потребитель читает пример нового обращения

- **WHEN** потребитель открывает руководство и исходники примера
- **THEN** доступны реальные public типы и явные регистрации зависимостей приложения
- **AND** успешная компиляция обоих бинарных вариантов отделена от исполнения модели, инструментов, БД и native runtime.

#### Scenario: Проверка правила — Проверяемое руководство бинарного потребителя

- **WHEN** потребитель подключает библиотеку и начинает run
- **THEN** руководство показывает последовательность подключения и основных сценариев.

### Requirement: Компиляция примеров руководства

Примеры C# MUST компилироваться с поставленными SQLite и PostgreSQL DLL вне репозитория без ProjectReference/PackageReference.

#### Scenario: Проверка правила — Компиляция примеров руководства

- **WHEN** примеры вынесены за пределы репозитория
- **THEN** они компилируются с SQLite и PostgreSQL DLL без project/package references.

### Requirement: Явные ограничения примеров

Обязанности приложения, настраиваемые retention/soft bytes/token thresholds и ограничения typed failure/cancellation/Unknown/no-replay MUST быть явными. Compile-only методы MUST NOT исполняться ради этой проверки или объявляться runtime evidence.

#### Scenario: Проверка правила — Явные ограничения примеров

- **WHEN** потребитель оценивает compile-only проверку
- **THEN** обязанности приложения и ограничения видны; методы не исполняются и runtime успех не заявлен.

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

Прикладной compact MUST использовать только активное окно и следующий непрерывный terminal prefix, включая0. InProgress и последующий хвост, provider items и новый несохранённый input MUST оставаться вне сохраняемого окна. Полный request MUST учитывать их при threshold/budget и после замены окна; providers MUST вызываться один раз на сценарий.

#### Scenario: Завершённая история с текущим хвостом

- **WHEN** первый turn terminal, второй InProgress, а приложение добавило provider и новый input
- **THEN** compact получает только активное окно и ещё не покрытый первый turn
- **AND** следующий полный request содержит provider, новый compact output, второй turn и новый input ровно по одному разу.

#### Scenario: Проверка правила — Сохраняемый префикс и фиксированный полный запрос

- **WHEN** история имеет terminal prefix и InProgress хвост
- **THEN** compact получает только сохраняемый prefix, полный budget учитывает хвост и фиксированные providers.

### Requirement: Проекция controls и проверка compact input

Instructions/tools/controls MUST сохраняться в полном generation request. Compact MUST использовать отдельную проекцию поддержанных controls без tools. Известные неполные function pairs MUST отклоняться до отправки, без удаления данных.

#### Scenario: Проверка правила — Проекция controls и проверка compact input

- **WHEN** полный generation request содержит tools и неполную function pair
- **THEN** tools остаются в generation projection, compact input с неполной парой отклоняется.

### Requirement: Ограниченное принятие compact

Сценарий MUST проверять exact settings, считать полный request и запускать compact при estimate >= threshold. Unknown estimate MUST NOT заменяться KnownTokens либо прошлым usage. Число проходов MUST ограничиваться MaxPasses; known non-reduction MUST останавливать проходы без принятия увеличенного окна.

#### Scenario: Неизвестный opaque бюджет

- **WHEN** Completed output имеет opaque состояние и full counter возвращает null estimate
- **THEN** version-aware save принимает окно и сценарий возвращает UnknownBudget
- **AND** guard17 по-прежнему отклоняет генерацию без полной оценки.

#### Scenario: Сбой второго прохода

- **WHEN** первый проход сохранён, а второй завершился ошибкой либо потерял актуальность
- **THEN** активным остаётся окно первого прохода, ошибка сохраняется без автоматического повторения.

#### Scenario: Проверка правила — Ограниченное принятие compact

- **WHEN** known estimate достиг threshold, а проход не уменьшил окно
- **THEN** число проходов ограничено MaxPasses, увеличенный кандидат не принимается.

### Requirement: Принятие compact при неизвестном бюджете

Валидный Completed кандидат с unknown full estimate MUST сохраняться и возвращать UnknownBudget без дальнейших проходов или разрешения генерации. Пустой output при непустой compact history MUST отклоняться без сохранения.

#### Scenario: Проверка правила — Принятие compact при неизвестном бюджете

- **WHEN** Completed кандидат имеет unknown full estimate
- **THEN** он сохраняется как UnknownBudget без нового прохода и разрешения генерации; пустой кандидат для непустой истории отвергается.

### Requirement: Сохранение и активация compact окна

Успех SaveAsync MUST предшествовать активации. Save MUST получать исходный token либо результат предыдущего save и свежий UTC; now >= expiry, stale token, incomplete/failed/canceled и ошибки MUST сохранять последнее успешно принятое окно без retry. Внешний I/O MUST завершаться вне write UoW. Исходная история и fixed expiry MUST сохраняться.

#### Scenario: Проверка правила — Сохранение и активация compact окна

- **WHEN** первое окно сохранено, следующий кандидат стал stale или expired
- **THEN** сохранённое окно остаётся активным, история и fixed expiry не меняются.

### Requirement: Исходная FIFO association при compact

Сжатие MUST связывать известные результаты с первым незакрытым вызовом того же call_id. Каждая retained известная пара MUST сохранять полный canonical call/output и исходный порядок occurrences. Подмена результата, ID rewriting и global dedup по call_id MUST NOT допускаться. Неопределимая association MUST давать явный отказ до save без потери последнего принятого окна/token/history/fixed expiry и без replay handler.

#### Scenario: Различимые повторные ID

- **WHEN** исходная история содержит call(x,argsA), call(x,argsB), output(x,resultA), output(x,resultB)
- **THEN** compact subset может сохранить callA/resultA либо callB/resultB, но не callB/resultA
- **AND** следующий builder/executor сохраняет закрытые occurrences без повторения handler.

### Requirement: Однозначность retained compact occurrences

Баланс известных пар и первое совпадение содержимого MUST NOT считаться доказательством исходной association. Retained пары MUST иметь единственное ordered сопоставление с исходными полными call/output occurrences; неоднозначный subset MUST отклоняться. Полная одинаковая последовательность и одинаковые calls с различимыми outputs MUST оставаться допустимыми при единственном сопоставлении. Скрытая opaque association MUST NOT выдумываться.

#### Scenario: Неоднозначный subset одинаковых пар

- **WHEN** compact оставляет одну известную пару из двух полностью одинаковых исходных occurrences с одним call_id
- **THEN** сценарий отказывает до save, сохраняя последнее принятое состояние.

#### Scenario: Ошибка association второго прохода

- **WHEN** первый проход сохранён, а известные пары второго прохода потеряли исходную association
- **THEN** сохраняются окно и token первого save без retry или replay.

### Requirement: Отдельный бюджет compact и генерации

Compact payload MUST проходить отдельную проверку input budget перед HTTP. Статус отчёта MUST NOT объявляться разрешением generation без отдельного full-request guard.

#### Scenario: Проверка правила — Отдельный бюджет compact и генерации

- **WHEN** compact payload укладывается в input budget
- **THEN** это не заменяет full-request guard перед generation.

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

### Requirement: Потоковый Responses gateway

GenerateAsync MUST выбирать stream=true при наличии onUpdate и MUST сохранять JSON stream=false при null. SSE MUST читаться через HttpStreamResponseResult actual HttpClientLibrary.

#### Scenario: EOF после tool arguments

- **WHEN** поток содержит function_call и фрагменты arguments, но не terminal event
- **THEN** отчёт Incomplete сохраняет call_id/arguments/unknown поля без фиктивного completion.

#### Scenario: Авторитетный terminal output

- **WHEN** collected items содержат больше элементов, чем непустой terminal output
- **THEN** итоговый output точно соответствует terminal array и не содержит старых extra items.

#### Scenario: Проверка правила — Потоковый Responses gateway

- **WHEN** GenerateAsync получает onUpdate callback
- **THEN** выбирается SSE через HttpStreamResponseResult; без callback остаётся JSON.

### Requirement: SSE framing и неподтверждённый EOF

Parser MUST поддерживать строгий UTF-8 fragmentation, optional начальный BOM, LF/CRLF/CR, comments и многострочные data. [DONE], HTTP2xx, delta и EOF MUST NOT подтверждать Completed; незакрытый frame на EOF MUST NOT dispatch.

#### Scenario: Проверка правила — SSE framing и неподтверждённый EOF

- **WHEN** поток заканчивается незакрытым frame без terminal event
- **THEN** frame не dispatch, EOF или DONE не подтверждают Completed.

### Requirement: Подтверждённый canonical SSE output

Completed MUST требовать response.completed с completed response без error и canonical output либо собранными item events. Непустой response.output MUST быть авторитетным и заменять collected items; absent/empty MUST допускать backfill.

#### Scenario: Проверка правила — Подтверждённый canonical SSE output

- **WHEN** terminal completed содержит непустой output
- **THEN** terminal array заменяет collected items; absent или empty допускает backfill.

### Requirement: Сохранение неполного SSE и continuation

Failed/incomplete MUST сохранять известные output/envelope; unknown/opaque поля и output order MUST сохраняться. Function arguments и текстовые delta MUST сохраняться в неполных items при EOF. Continuation MUST применять binding/allowlist/id rules JSON adapter.

#### Scenario: Проверка правила — Сохранение неполного SSE и continuation

- **WHEN** EOF приходит после function arguments delta
- **THEN** неполные items и envelope сохраняются; continuation соблюдает JSON binding rules.

### Requirement: Владение streaming lifecycle

Callbacks MUST вызываться последовательно и ожидаться, MUST NOT вызываться после возврата. Callback exceptions MUST распространяться неизменными и MUST NOT становиться server/JSON/timeout errors. Поток/обёртка MUST освобождаться при любом выходе.

#### Scenario: Callback бросает JsonException

- **WHEN** callback бросает JsonException
- **THEN** вызывающий получает тот же exception после освобождения потока без synthetic server error и следующих callbacks.

#### Scenario: Проверка правила — Владение streaming lifecycle

- **WHEN** callback бросает исключение
- **THEN** тот же exception распространяется после cleanup, следующие callbacks не вызываются.

### Requirement: Приоритет отмены и deadline SSE

Caller cancellation до данных MUST распространяться с исходным token, после полученного отчёта MUST сохранять данные в Canceled. Deadline MUST возвращать typed Timeout; при наличии данных MUST сохранять их в Failed. Explicit typed failure MUST иметь приоритет над поздней отменой; caller MUST иметь приоритет над deadline. Unexpected I/O MUST распространяться без retries/fallback.

#### Scenario: Проверка правила — Приоритет отмены и deadline SSE

- **WHEN** получен явный failed report и затем caller отменён
- **THEN** typed failure сохраняется; caller имеет приоритет над deadline, известные данные не теряются.

### Requirement: Ошибки освобождения HTTP

При primary и cleanup failure владеющая граница MUST пытаться выполнить remaining cleanup и распространять тот же primary exception с сохранённым stack. `Exception.Data["HttpClientLibrary.CleanupExceptions"]` MUST содержать immutable список наблюдённых ошибок cleanup, отличных от самого primary, в порядке наблюдения. Повторения одного secondary на разных boundaries допустимы; повторный throw самого primary MUST NOT создавать self-reference.

#### Scenario: Primary и secondary

- **WHEN** callback либо чтение JSON/SSE бросает primary, а cleanup бросает другой exception
- **THEN** primary identity/stack сохраняются, secondary доступен immutable снимком и не исчезает при typed normalization.

#### Scenario: Standalone cleanup и повторная попытка

- **WHEN** cleanup-only exception имеет тип OCE/JsonException/HttpRequestFailedException
- **THEN** его identity сохраняется независимо caller/deadline; повторный wrapper disposal не повторяет failed cleanup и не подтверждает освобождение.

#### Scenario: Проверка правила — Ошибки освобождения HTTP

- **WHEN** чтение бросает primary и remaining cleanup бросает secondary
- **THEN** primary identity и stack сохранены, immutable Data содержит secondary без self-reference.

### Requirement: Cleanup origin и безопасный payload

Presence списка, включая empty standalone cleanup origin, MUST исключать нормализацию этой ошибки в ServiceResult/отмену/Timeout для JSON/SSE/compact. Cleanup-only MUST распространяться исходным объектом независимо типа/token. Исключения/Data MUST NOT публиковаться как safe UI/log payload.

#### Scenario: Проверка правила — Cleanup origin и безопасный payload

- **WHEN** cleanup-only OCE возникает при отменённом caller
- **THEN** исходный объект не нормализуется в Canceled, Exception/Data не публикуются.

### Requirement: Однократная попытка wrapper disposal

Повторный Dispose/DisposeAsync wrapper после первой попытки MUST ничего не делать, включая повтор после отказа; no-op MUST NOT означать успешное освобождение.

#### Scenario: Проверка правила — Однократная попытка wrapper disposal

- **WHEN** после неуспешного Dispose вызывается DisposeAsync
- **THEN** новая попытка cleanup не выполняется; no-op не подтверждает release.

### Requirement: Каноническая JSON генерация Responses

JSON gateway MUST отправлять canonical base-prefix POST /v1/responses через HttpClientLibrary с per-call ModelAccess и stream=false/store=false. Model/instructions/exact effort, ordered canonical input и полные function definitions MUST сохраняться.

#### Scenario: Нет видимого текста

- **WHEN** completed JSON содержит только function_call/reasoning/compaction
- **THEN** gateway сохраняет весь ordered output и полный независимый envelope как Completed.

#### Scenario: Нет canonical output

- **WHEN** JSON содержит completed, но не содержит output
- **THEN** gateway возвращает Incomplete с полным envelope, без фиктивного completion.

#### Scenario: Проверка правила — Каноническая JSON генерация Responses

- **WHEN** JSON GenerateAsync получает canonical items и exact effort
- **THEN** POST через HttpClientLibrary сохраняет per-call доступ и ordered input.

### Requirement: Проверка и снимок JSON controls

Поддержанные параметры MUST иметь независимый снимок; mandatory fields и effort MUST NOT переопределяться. Unknown top-level controls MUST давать Unsupported до HTTP, malformed/duplicate controls — Validation. Default include reasoning.encrypted_content MUST добавляться только при отсутствии explicit include.

#### Scenario: Проверка правила — Проверка и снимок JSON controls

- **WHEN** controls переопределяют mandatory field или имеют duplicate
- **THEN** запрос отклоняется до HTTP; default include не заменяет explicit include.

### Requirement: Проверка известных вложенных controls

JSON/SSE и compact MUST отклонять неверные типы и повторы известных поддержанных nested fields с Validation до HTTP. Reasoning.summary MUST быть string/null; override effort MUST NOT допускаться. Generation text.verbosity MUST быть string/null, format — object/null; format.type/name — string/null, strict — boolean/null, schema — произвольный JSON. Повтор schema MUST отклоняться. Top-level allowlists и запрет null reasoning/text MUST сохраняться.

#### Scenario: Неверный summary до HTTP

- **WHEN** JSON/SSE либо compact получает reasoning.summary=42 или два поля summary
- **THEN** возвращается безопасный Validation без HTTP вызова.

#### Scenario: Nullable known controls

- **WHEN** generation получает text.format=null или type/name/strict/schema=null внутри format
- **THEN** запрос допускается без изменения nullable полей.

### Requirement: Сохранение неизвестных nested controls

Writer MUST сохранять unknown nested fields, их повторы, порядок и произвольное содержимое schema без удаления, переименования или нормализации. Known validation MUST ограничиваться подтверждёнными путями и MUST NOT рекурсивно применяться внутри schema, unknown JSON или untyped tool_choice object. Статические enum/model ограничения MUST NOT вводиться без подтверждённого контракта.

#### Scenario: Opaque вложенные данные

- **WHEN** schema или unknown object содержит summary=42, повтор type либо иной opaque JSON
- **THEN** эти данные сохраняются полностью и допускаются по прежнему top-level контракту.

### Requirement: Canonical output и JSON completion

Output MUST сохранять порядок/unknown/opaque поля отдельно от полного envelope/continuation. Completed MUST требовать status=completed, output array и отсутствие explicit error; HTTP 2xx/видимый текст MUST NOT заменять это подтверждение. Failed/incomplete/unknown lifecycle MUST сохранять известный output/envelope.

#### Scenario: Проверка правила — Canonical output и JSON completion

- **WHEN** HTTP2xx содержит visible text, но нет completed output
- **THEN** Completed не заявлен; известные output и envelope сохраняются раздельно.

### Requirement: Безопасные ошибки и ограниченный JSON вызов

JSON gateway MUST нормализовать HTTP error по status и закрытым известным безопасным type/code/param только из Complete valid error envelope. Raw body/headers/reason/message/exception MUST NOT попадать в публичную ошибку/logger. Truncated/invalid/unsupported body MUST NOT трактоваться как complete envelope.

#### Scenario: Поздняя отмена

- **WHEN** caller отменяется после полного получения canonical JSON
- **THEN** отчёт Canceled сохраняет output/envelope/continuation
- **AND** explicit typed failure не подменяется отменой.

#### Scenario: Error prefix и секретные values

- **WHEN** HTTP error body неполный, либо error type/code/param содержит неизвестный текст
- **THEN** public error сохраняет status и только известные безопасные поля
- **AND** raw message не публикуется, retry отсутствует.

#### Scenario: Проверка правила — Безопасные ошибки и ограниченный JSON вызов

- **WHEN** error envelope усечён или содержит неизвестный секретный type
- **THEN** публичная ошибка не публикует raw данные и не считает prefix полным envelope.

### Requirement: Конечный JSON вызов и приоритет отмены

Вызов MUST иметь конечный GenerationTimeout на отправку/чтение. До получения полного отчёта caller cancellation MUST распространяться OCE с исходным token; после полного отчёта MUST возвращаться Canceled с сохранёнными output/envelope/continuation. Explicit typed HTTP/model/JSON failure MUST сохранять приоритет над поздней отменой. Caller MUST иметь приоритет над deadline.

#### Scenario: Проверка правила — Конечный JSON вызов и приоритет отмены

- **WHEN** caller отменён после полного отчёта без explicit failure
- **THEN** Canceled сохраняет output, envelope и continuation; deadline не подменяет caller.

### Requirement: Cleanup и граница compact

Неожиданный I/O MUST распространяться без retry. Request/response MUST освобождаться на успехе/отказе/JSON error/отмене. Compact gateway MUST использовать отдельный compact-контракт, определённый требованием «Отдельный JSON compact transport».

#### Scenario: Проверка правила — Cleanup и граница compact

- **WHEN** JSON запрос завершился ошибкой чтения
- **THEN** request/response освобождаются без I/O retry; compact регулируется своим действующим контрактом.

### Requirement: Продолжение только своего JSON вызова

Continuation JSON adapter MUST связывать previous_response_id/x-codex-turn-state с dialog/owner/agent, endpoint и отпечатком выбранного ключа без сохранения ключа. Несовпадение либо unknown format MUST отклоняться до HTTP. Unknown metadata MUST сохраняться, но MUST NOT становиться input, произвольными headers или разрешением retry/смены account.

#### Scenario: Другой контекст или ключ

- **WHEN** continuation передано с другим dialog/owner/agent/key/endpoint
- **THEN** gateway отказывает до HTTP без fallback.

#### Scenario: Новый ответ без anchor

- **WHEN** новый JSON не содержит пригодного id
- **THEN** новый continuation не отправляет previous_response_id старого ответа.

#### Scenario: Проверка правила — Продолжение только своего JSON вызова

- **WHEN** continuation передано с другим owner, agent, endpoint или ключом
- **THEN** отказ происходит до HTTP; unknown metadata не превращается в headers или retry.

### Requirement: Новый anchor и upstream ownership

Новый envelope без пригодного id MUST удалять старый previous_response_id; исходный id MUST сохраняться в envelope. Upstream ownership MUST оставаться ответственностью codex-lb.

#### Scenario: Проверка правила — Новый anchor и upstream ownership

- **WHEN** новый JSON ответ не содержит пригодного id
- **THEN** старый previous_response_id удалён, raw новый id остаётся в envelope; ownership проверяет codex-lb.

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

AgentBridge MUST предоставлять независимые SQLite/PostgreSQL/SQL Server migrations assemblies и snapshots для одного общего AgentBridgeDbContext. Runtime и design-time MUST выбирать одну и ту же устойчивую identity по provider. Эти проекты MUST владеть только таблицами AgentBridge и MUST NOT добавлять host или зависимости в Domain/Application. Design-time factory MUST создавать контекст без открытия соединения, SQL, применения схемы или чтения секретов приложения.

#### Scenario: Изолированная история миграций

- **WHEN** SQLite, PostgreSQL или SQL Server options создаются runtime регистрацией либо design-time factory
- **THEN** provider history repository получает имя __AgentBridgeMigrationsHistory
- **AND** выбор истории не меняет snapshot или схему mapped таблиц.

#### Scenario: Создание модели для выбранного провайдера

- **WHEN** tooling использует SQLite, PostgreSQL или SQL Server target/startup проект
- **THEN** factory создаёт общий AgentBridgeDbContext с выбранным provider и его отдельной migrations assembly
- **AND** runtime выбирает ту же assembly identity.

#### Scenario: Сохранение принятых границ схемы

- **WHEN** создаётся provider-specific модель
- **THEN** сохраняются только собственные таблицы, составные keys/FK, единственные cascade paths, UTC ticks, ordinal owner (BINARY/C для SQLite/PostgreSQL либо varbinary UTF-16 code units для SQL Server) и expiry/Id index
- **AND** owner-list index и таблицы подключающего приложения не добавляются.

#### Scenario: Проверка правила — Раздельные миграции выбранного провайдера

- **WHEN** design-time factory создаёт выбранный provider context
- **THEN** assembly identity совпадает с runtime, соединение и host не запускаются.

### Requirement: Отдельная служебная история миграций

Runtime и design-time MUST явно выбирать отдельную служебную историю `__AgentBridgeMigrationsHistory` и MUST NOT использовать общий ledger `__EFMigrationsHistory` подключающего приложения. Служебная история EF MUST оставаться отдельной от mapped таблиц диалога и MUST NOT добавляться как persistence entity в модель AgentBridge.

#### Scenario: Проверка правила — Отдельная служебная история миграций

- **WHEN** runtime и design-time выбирают ledger
- **THEN** используется __AgentBridgeMigrationsHistory отдельно от mapped сущностей и ledger приложения.

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

### Requirement: Независимые прикладные порты

Application MUST предоставлять независимые контракты шлюза модели, источника контекста, инструмента и подсчёта полного подготовленного запроса. Контракты MUST NOT содержать EF/DbContext/IQueryable/Expression, типы HTTP-библиотеки, Web/MVC или wire DTO.

#### Scenario: Независимость канонических данных

- **WHEN** исходный документ или список уничтожен либо изменён после создания прикладного снимка
- **THEN** снимок сохраняет полные канонические данные, порядок и неизвестные поля
- **AND** полный envelope результата остаётся отдельным от элементов следующего input.

#### Scenario: Поздняя запись в новую жизнь диалога

- **GIVEN** диалог был удалён и создан заново с тем же публичным ID
- **WHEN** приходит результат со старым сохраняемым incarnation или revision
- **THEN** атомарный изменяющий сценарий отклоняет результат без изменения новой истории.

#### Scenario: Проверка правила — Независимые прикладные порты

- **WHEN** приложение подключает Application ports
- **THEN** контракты не содержат HTTP, EF, IQueryable или UI типов.

### Requirement: Lifecycle и семантические ошибки портов

Ожидаемый отказ MUST передаваться семантической ошибкой без данных; успех с данными MUST содержать ненулевые данные. Получение lifecycle-отчёта MUST отличаться от подтверждённого завершения модели; неполный, ошибочный и отменённый отчёт MUST сохранять известный канонический output. Неожиданные исключения MUST NOT маскироваться успехом или ожидаемым отказом.

#### Scenario: Проверка правила — Lifecycle и семантические ошибки портов

- **WHEN** gateway возвращает неполный report или неожиданное исключение
- **THEN** известный canonical output сохраняется, exception не маскируется typed успехом.

### Requirement: Независимые canonical snapshots и envelope

Канонические items, полный envelope и метаданные продолжения MUST сохранять неизвестные и opaque-поля независимо от срока жизни исходных данных. Результаты отдельных шагов модели и compact MUST иметь путь записи/чтения полного envelope отдельно от канонических input/output items.

#### Scenario: Проверка правила — Независимые canonical snapshots и envelope

- **WHEN** исходный JSON уничтожен после создания snapshot
- **THEN** unknown items и полный envelope остаются независимыми и раздельными.

### Requirement: Per-call снимок выбранного ключа

Снимок выбранного ключа MUST передаваться на вызов, а MUST NOT храниться в изменяемом глобальном состоянии шлюза или раскрывать секрет обычным строковым представлением.

#### Scenario: Проверка правила — Per-call снимок выбранного ключа

- **WHEN** два вызова gateway используют разные ключи
- **THEN** ключи передаются на вызовы, не сохраняются глобально и не раскрываются строковым представлением.

### Requirement: Известный счёт и полный бюджет порта

Локально известный token count MUST отличаться от оценки полного бюджета с opaque-содержимым.

#### Scenario: Проверка правила — Известный счёт и полный бюджет порта

- **WHEN** подготовленный запрос содержит opaque данные
- **THEN** known count отличается от оценки полного бюджета.

### Requirement: Короткие атомарные write ports

Read-only порты MUST NOT требовать изменяющий UoW. Изменяющие порты MUST выражать короткие атомарные сценарии: существование, владелец, срок и сохраняемые incarnation/revision MUST проверяться в одной границе с записью. Сеть MUST NOT удерживать транзакцию БД.

#### Scenario: Проверка правила — Короткие атомарные write ports

- **WHEN** внешняя модель ещё выполняет сетевой запрос
- **THEN** write UoW не удерживается; read-only port не требует изменяющей транзакции.

### Requirement: Persistent guards и terminal-prefix metadata

Доменный object-lifetime snapshot MUST NOT объявляться переносимым persistent token. Отказ MUST NOT изменять данные, а поздняя запись MUST NOT заново создавать удалённый диалог. Terminal-prefix metadata MUST NOT подменять границу отдельных протокольных items.

#### Scenario: Проверка правила — Persistent guards и terminal-prefix metadata

- **WHEN** диалог удалён до получения результата
- **THEN** старый snapshot не разрешает recreation или подмену протокольной границы.

### Requirement: Подключение к .NET-приложениям

AgentBridge MUST предоставлять C#-библиотеку для SDK-style приложений на .NET 10 (`net10.0`) с подключением обычных DLL. Ядро MUST быть независимо от ASP.NET Core, WPF и Telegram.

#### Scenario: Подключение из разных приложений

- **WHEN** ASP.NET Core-приложение, WPF-приложение или Telegram-бот подключает AgentBridge
- **THEN** логика диалога и управления контекстом доступна через общее ядро
- **AND** интеграция с интерфейсом пользователя остаётся в подключающем приложении.

### Requirement: Отдельный адаптер codex-lb

Интеграция с codex-lb MUST находиться в отдельном адаптере. Адаптер MUST передавать запросы генерации через Responses API codex-lb. Ядро MUST NOT зависеть от реализации этого адаптера.

#### Scenario: Запрос к модели

- **WHEN** AgentBridge обрабатывает сообщение с выбранным адаптером codex-lb
- **THEN** адаптер передаёт подготовленный запрос в codex-lb
- **AND** codex-lb выполняет upstream-маршрутизацию
- **AND** AgentBridge получает результат через адаптер.

### Requirement: Контекст и инструменты приложения

Подключающее приложение MUST определять доступные источники бизнес-контекста, разрешённые инструменты и права пользователя. AgentBridge MUST объединять инструкции агента, историю диалога, необходимый бизнес-контекст и новое сообщение. Запрошенные моделью инструменты MUST выполняться через зарегистрированные обработчики приложения с проверкой разрешений.

#### Scenario: Уточнение статуса заказа

- **WHEN** пользователь спрашивает о своём заказе
- **AND** агент запрашивает разрешённый инструмент `GetOrderStatus`
- **THEN** AgentBridge вызывает обработчик приложения и передаёт его результат модели
- **AND** следующий вопрос пользователя об ожидаемой доставке продолжает тот же диалог.

### Requirement: Настраиваемое хранение диалогов

AgentBridge MUST сохранять диалоги и необходимое состояние контекста в БД. Провайдер БД и параметры подключения MUST задаваться конфигурацией подключающего приложения. SQL Server MUST поддерживаться как основной сценарий внедрения при явном выборе; SQLite и PostgreSQL MUST сохраняться как опциональные провайдеры общего хранилища; SQLite MUST NOT быть обязательным выбором. Ядро MUST NOT зависеть от конкретной БД.

#### Scenario: Выбор БД

- **WHEN** приложение настраивает SQLite, PostgreSQL или SQL Server как хранилище AgentBridge
- **THEN** диалоги сохраняются через выбранный провайдер
- **AND** замена провайдера не требует изменения бизнес-логики агента.

### Requirement: Unicode и ordinal mapping SQL Server

Для SQL Server строковые payload/journal/settings/provenance MUST использовать Unicode nvarchar(max) без нормализации. OwnerId MUST сохранять ordinal identity как обратимые UTF-16 code units в varbinary(max), включая trailing spaces и непарные суррогаты, без MaxLength/trim/replacement. UTC MUST храниться bigint ticks. Provider-specific checks MUST сохранять nullable lifecycle и nonempty semantics без зависимости от QUOTED_IDENTIFIER или SQL Server string padding.

#### Scenario: Ordinal owner SQL Server

- **WHEN** owner отличается case, завершающим пробелом либо UTF-16 code unit
- **THEN** converter и binary token сохраняют различие без нормализации
- **AND** disconnected metadata/parameter проверки не объявляются доказательством server equality/CAS.

### Requirement: Формат общего EF-хранилища

Persistence-модель MUST отделяться от доменного агрегата и MUST сохранять владельца, фиксированные даты, incarnation/revision, порядок обращений и items.

#### Scenario: Сохранение результата инструмента и envelope

- **WHEN** сериализуется история с function_call_output и отдельным результатом шага
- **THEN** полные неизвестные поля и call_id остаются в соответствующих канонических данных
- **AND** envelope и continuation остаются отдельно от следующего input.

#### Scenario: Выбор общего контекста

- **WHEN** приложение явно выбирает SQLite, PostgreSQL или SQL Server
- **THEN** DI регистрирует один scoped-контекст через AddEfCoreContext и AddEfCoreBaseRepositories
- **AND** регистрация не открывает БД, не запускает migrations и не выбирает SQLite при отсутствии настройки.

#### Scenario: Проверка правила — Формат общего EF-хранилища

- **WHEN** агрегат сериализуется в persistence DTO
- **THEN** owner, fixed dates, incarnation/revision, порядок и items сохранены отдельно от Domain.

### Requirement: Хранение полного model и compact report

Полный результат каждого шага модели и принятого compact MUST сохраняться отдельно от канонических items истории, включая lifecycle, output, envelope, continuation и ожидаемую ошибку. Результат compact MUST иметь Completed status.

#### Scenario: Проверка правила — Хранение полного model и compact report

- **WHEN** сохраняется принятый compact
- **THEN** report Completed, envelope и continuation хранятся отдельно от canonical истории.

### Requirement: Связи и безопасные persistence metadata

Связи MUST исключать присоединение дочерних строк к обращению другого диалога и MUST каскадно удалять зависимые строки вместе с диалогом. Перечисленные concurrency metadata MUST NOT объявляться реализацией атомарных application guards. Per-call ModelAccess/API keys MUST NOT сохраняться в persistence-моделях.

#### Scenario: Проверка правила — Связи и безопасные persistence metadata

- **WHEN** дочерняя строка принадлежит другому turn или dialog
- **THEN** связь не допускается, cascade сохраняет границы, ModelAccess и keys не записываются.

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

### Requirement: Продолжение диалога

AgentBridge MUST хранить историю и состояние, необходимые для продолжения диалога после перезапуска приложения. Диалоги разных пользователей MUST быть изолированы. Сохранение состояния Responses MUST учитывать элементы инструментов, reasoning и compaction, необходимые для последующего продолжения, а не только видимый текст ответа.

#### Scenario: Продолжение после перезапуска

- **WHEN** приложение перезапущено
- **AND** пользователь продолжает сохранённый диалог в пределах политики хранения
- **THEN** AgentBridge восстанавливает доступный контекст из БД
- **AND** не использует историю другого пользователя.

### Requirement: Ограничения хранения

Подключающее приложение MUST задавать мягкий порог объёма содержимого на диалог в байтах и срок хранения от создания диалога. Байты сообщений, результатов инструментов и состояний контекста MUST входить в метрику; физические индексы и overhead провайдера MUST NOT выдаваться за объём содержимого. Превышение мягкого порога MUST NOT само по себе удалять историю или блокировать диалог. Политика срока хранения MUST распространяться также на производные состояния контекста и результаты сжатия.

#### Scenario: Превышение мягкого порога

- **WHEN** содержимое диалога превысило настроенный порог байтов
- **THEN** приложение получает предупреждение и текущий объём
- **AND** история сохраняется до истечения срока или явного удаления
- **AND** диалог может продолжаться при допустимом входном бюджете модели.

#### Scenario: Истечение срока хранения

- **WHEN** срок хранения данных диалога истёк
- **THEN** AgentBridge применяет настроенную политику удаления
- **AND** удалённые сведения не сохраняются бессрочно только потому, что были включены в производный контекст.

#### Scenario: Активность не продлевает срок

- **GIVEN** при создании диалог получил срок истечения
- **WHEN** в диалоге появляются новые сообщения или выполняется compact
- **THEN** срок истечения не переносится на дату последней активности.

### Requirement: Дата истечения диалога

AgentBridge MUST сохранять `CreatedAtUtc` и `ExpiresAtUtc`, вычисленный из времени создания и настроенного срока хранения при создании. Пробное значение 7 дней MUST быть переопределяемым через конфигурацию и MUST NOT подменять настроенный срок в логике истечения. Приложение MUST иметь возможность получить эту дату и признак истечения. Просроченный диалог MUST NOT использоваться для нового обращения даже до физической очистки строк.

#### Scenario: Отображение срока в интерфейсе

- **WHEN** приложение запрашивает состояние диалога
- **THEN** оно получает `ExpiresAtUtc` и признак истечения
- **AND** может показать пользователю срок без самостоятельного вычисления политики.

#### Scenario: Срок хранения из конфигурации

- **GIVEN** приложение настроило срок хранения 14 дней вместо пробных 7
- **WHEN** создаётся новый диалог
- **THEN** его срок истечения равен времени создания плюс 14 дней
- **AND** логика очистки использует этот срок, а не пробное значение.

### Requirement: Сжатие рабочего контекста

AgentBridge MUST управлять ограниченным рабочим окном контекста отдельно от хранения полной истории. Сжатие MUST запускаться по числу токенов рабочего контекста; порог MUST задаваться конфигурацией подключающего приложения. При проверке порога MUST учитываться подготовленный контекст обращения, включая инструкции, бизнес-данные, историю, новое сообщение и необходимые инструменты.

#### Scenario: Продолжение со сжатым контекстом

- **WHEN** рабочий контекст диалога сжимается
- **THEN** AgentBridge сохраняет результат сжатия как новое состояние контекста
- **AND** использует его для последующих обращений
- **AND** исходная история остаётся под управлением отдельной политики хранения.

#### Scenario: Изменение порога сжатия

- **WHEN** приложение меняет настроенный порог в токенах
- **THEN** проверка сжатия использует новое значение после применения конфигурации
- **AND** изменение не требует правки исходного кода бизнес-логики AgentBridge.

#### Scenario: Проверка правила — Сжатие рабочего контекста

- **WHEN** полный prepared контекст достигает configured token threshold
- **THEN** порог учитывает инструкции, business input, историю, сообщение и инструменты.

### Requirement: Сохранение окна без удаления истории

Сжатие MUST сохранять новое состояние контекста, связанное с исходным диалогом, и MUST NOT само по себе означать удаление исходной истории из БД.

#### Scenario: Проверка правила — Сохранение окна без удаления истории

- **WHEN** compact создаёт новую версию контекста
- **THEN** версия связана с диалогом, полная история не удаляется операцией сжатия.

### Requirement: Токенизация и ограниченные compact циклы

Размер текстового содержимого MUST определяться tokenizer соответствующей известной кодировки модели. Непрозрачное reasoning/compaction MUST NOT объявляться точно посчитанным локальным текстовым tokenizer. Повторное сжатие MUST быть ограничено числом проходов на обращение и прекращаться при отсутствии уменьшения; количество сжатий за жизнь диалога MUST NOT само по себе приводить к автоматическому созданию нового диалога.

#### Scenario: Проверка правила — Токенизация и ограниченные compact циклы

- **WHEN** opaque окно не имеет полной локальной оценки
- **THEN** точный счёт не выдумывается, проходы ограничены и не создают новый диалог автоматически.

### Requirement: Использование EFCoreLibrary

Вся работа AgentBridge с БД MUST строиться на EFCoreLibrary. Базовые read/create/update/delete репозитории MUST иметь приоритет над custom query. Недостаточность или спорный контракт библиотеки MUST обсуждаться с пользователем до выбора обходной реализации; AgentBridge MUST NOT заменять библиотеку прямым EF/SQL или собственным параллельным слоем.

#### Scenario: Удаление истории

- **WHEN** сценарий удаляет историю диалога
- **THEN** используются базовые delete-операции EFCoreLibrary и сценарная граница сохранения
- **AND** зависимые сохраняемые состояния обрабатываются вместе с историей.

### Requirement: Использование HttpClientLibrary

Исходящие HTTP-запросы адаптера codex-lb MUST выполняться через HttpClientLibrary. Требуемое развитие её контракта MUST согласовываться с пользователем; отдельный HTTP pipeline для обхода библиотеки MUST NOT создаваться.

#### Scenario: HTTP Responses

- **WHEN** адаптер отправляет запрос в `/v1/responses`
- **THEN** HTTP-запрос выполняется через HttpClientLibrary
- **AND** интерпретация JSON/SSE Responses остаётся в адаптере.

### Requirement: Безопасное логирование HTTP

Логирование AgentBridge MUST проходить через Serilog, подключённый приложением, и `ILogger<T>`. По умолчанию диагностика HTTP MUST содержать статус и безопасные метаданные. Логирование содержимого MUST требовать явной настройки. Секреты MUST NOT включаться в диагностические сообщения.

#### Scenario: Ошибка HTTP без включённого содержимого

- **WHEN** HTTP-запрос завершился ошибкой
- **AND** логирование содержимого явно не включено
- **THEN** body и response snippet не записываются в журнал.

#### Scenario: Явно включённая структура HTTP-ошибки

- **WHEN** приложение явно включает JsonStructure
- **THEN** диагностика MUST включать только фиксированные признаки структуры полного валидного JSON и счётчики узлов
- **AND** исходные имена полей и значения MUST NOT записываться
- **AND** неполное или невалидное содержимое MUST NOT разбираться как полный JSON
- **AND** успешный SSE MUST NOT предварительно читаться ради диагностики.

### Requirement: Транспортные данные HTTP-ошибки

HttpClientLibrary MUST сохранять HTTP-статус и независимый снимок response/content headers ошибки. Тело MUST иметь настраиваемый предел, по умолчанию 65536 байтов, и явное состояние Empty/Complete/Truncated/UnsupportedContent/InvalidEncoding. Truncated MUST сохранять корректно декодируемый ограниченный prefix без замены незавершённого символа; реальные неверные байты MUST обозначаться InvalidEncoding.

#### Scenario: Ошибка превышает предел тела

- **WHEN** HTTP-ошибка содержит тело больше настроенного предела
- **THEN** чтение MUST ограничиваться пределом плюс одним проверочным байтом
- **AND** состояние MUST обозначаться Truncated для корректно декодируемого prefix
- **AND** preview MUST NOT интерпретироваться как полный error envelope.

#### Scenario: Проверка правила — Транспортные данные HTTP-ошибки

- **WHEN** HTTP error больше configured byte limit
- **THEN** статус и headers сохраняются, prefix отмечен Truncated с корректной кодировкой.

### Requirement: Безопасное исключение HTTP и adapter mapping

Raw error details MUST NOT логироваться. Message исключения MUST содержать только `HTTP <status>.`. Интерпретация error envelope MUST оставаться обязанностью адаптера.

#### Scenario: Проверка правила — Безопасное исключение HTTP и adapter mapping

- **WHEN** HTTP error содержит raw body и driver details
- **THEN** они не логируются, Message ограничено HTTP status, envelope интерпретирует adapter.

### Requirement: Чтение настроек и выбор модели

AgentBridge MUST предоставлять безопасное представление текущих настроек конкретного диалога, включая effective модель, effort, лимиты, token истории и отдельную версию выбора из того же read snapshot. Выбор model/effort MUST сохраняться в БД AgentBridge для конкретного диалога.

#### Scenario: Изменение усилия запроса

- **WHEN** приложение указывает допустимый effort конкретного обращения
- **THEN** он имеет приоритет над сохранённым effort диалога и default приложения
- **AND** выбранное значение используется в этом обращении без изменения уже выполняющихся запросов.

#### Scenario: Проверка правила — Чтение настроек и выбор модели

- **WHEN** приложение читает настройки конкретного диалога
- **THEN** безопасный snapshot включает persisted model/effort и отдельную settings version.

### Requirement: Приоритет и проверка выбора модели

Приоритет MUST быть: override обращения → сохранённый выбор диалога → defaults приложения. Override MUST NOT менять сохранённый выбор. Выбор MUST проверяться текущим каталогом доступного ключа; недопустимые значения MUST NOT заменяться скрытым fallback.

#### Scenario: Проверка правила — Приоритет и проверка выбора модели

- **WHEN** обращение задаёт допустимый effort override
- **THEN** override выше persisted/default, но сохранённый выбор не меняется и fallback не используется.

### Requirement: Секреты вне settings и status

Секреты, инструкции, строки подключения, headers, raw envelope и canonical содержимое MUST NOT возвращаться в safe settings/status.

#### Scenario: Проверка правила — Секреты вне settings и status

- **WHEN** приложение запрашивает safe settings/status
- **THEN** секреты и canonical содержимое не возвращаются.

### Requirement: Независимая версия выбора и снимок хода

DialogSettings MUST иметь собственную CAS Version, связанную с диалогом каскадным удалением. Запись MUST проверять owner, expiry, incarnation, исходную revision истории и expected settings version в коротком сценарном UoW через EFCoreLibrary. Она MUST NOT изменять revision истории, даты или ContentBytes содержимого. Устаревший token/version MUST давать Conflict без refresh/retry; неожиданные driver/commit/cleanup ошибки MUST NOT маскироваться Conflict.

#### Scenario: Смена выбора при активном ходе

- **WHEN** после начала хода приложение сохраняет новый выбор с актуальной отдельной версией
- **THEN** текущий ход сохраняет прежние model/effort и действующий token истории
- **AND** следующий ход использует новый выбор, а запись со старой settings version получает Conflict.

#### Scenario: Исторические строки после Down и Up

- **WHEN** AddDialogSettings удаляется и применяется повторно на тестовой БД с историей
- **THEN** история, полный compact envelope, fixed dates и ContentBytes сохраняются
- **AND** удалённые settings/snapshot/selected provenance возвращаются null, без подмены server именем.

#### Scenario: Проверка правила — Независимая версия выбора и снимок хода

- **WHEN** выбор сохраняется со stale settings version
- **THEN** возвращается Conflict без retry и изменения revision истории, driver failure не маскируется.

### Requirement: Атомарный snapshot выполняющегося хода

BeginWithSettings MUST атомарно сохранять immutable primitive snapshot model/effort/входного окна/порога/запаса. Активный ход MUST продолжать использовать его после нового выбора; новый выбор MUST действовать со следующего обращения.

#### Scenario: Проверка правила — Атомарный snapshot выполняющегося хода

- **WHEN** UI меняет выбор после BeginWithSettings
- **THEN** активный ход сохраняет прежний snapshot, следующий получает новый выбор.

### Requirement: Nullable provenance и ContentBytes metadata

Historical turn settings и selected compact provenance MUST оставаться nullable; миграции MUST NOT выдумывать исходный выбор. Settings/snapshot/provenance MUST NOT учитываться как ContentBytes содержимого.

#### Scenario: Проверка правила — Nullable provenance и ContentBytes metadata

- **WHEN** исторический turn не имеет selected provenance
- **THEN** null сохраняется без выдуманного выбора, metadata не увеличивают ContentBytes.

### Requirement: Подтверждение совместимости непрозрачного контекста

AgentBridge MUST проверять активное compact окно и непокрытый хвост перед сменой выбранной модели и перед началом хода. Наличие opaque MUST определяться отдельным model-independent inspector; ошибка tokenizer mapping MUST NOT служить доказательством opaque.

#### Scenario: Историческое opaque окно

- **WHEN** selected provenance неизвестна и server model совпадает с новым выбором
- **THEN** без подтверждения приложения возвращается Unsupported
- **AND** исходный контекст и сохранённый выбор остаются неизменными.

#### Scenario: Только текст и неизвестное tokenizer mapping

- **WHEN** каталог разрешает модель и сохранённое содержимое полностью текстовое
- **THEN** выбор допустим без compatibility порта
- **AND** недоступная токенизация отдельно возвращается ошибкой оценки, без объявления текста opaque.

#### Scenario: Проверка правила — Подтверждение совместимости непрозрачного контекста

- **WHEN** неизвестный tokenizer mapping встречается при смене модели
- **THEN** отдельный inspector проверяет окно и хвост; ошибка mapping не доказывает opaque.

### Requirement: Подтверждение приложения для opaque модели

Для opaque другой или неизвестной исходной selected модели MUST требоваться явное подтверждение порта приложения. Без подтверждения MUST возвращаться Unsupported без изменения выбора или истории.

#### Scenario: Проверка правила — Подтверждение приложения для opaque модели

- **WHEN** opaque имеет другую или неизвестную исходную selected модель
- **THEN** без подтверждения возвращается Unsupported и выбор с историей не меняются.

### Requirement: Раздельная provenance и canonical occurrences

Порт MUST получать исходные selected model, фактическую server model при наличии и новый проверенный выбор раздельно. Равенство server/selected имён MUST NOT доказывать совместимость. Повторяющиеся canonical occurrences MUST сохраняться; report output MUST NOT проверяться повторно как input без своих server metadata.

#### Scenario: Проверка правила — Раздельная provenance и canonical occurrences

- **WHEN** server model совпадает с новой selected моделью
- **THEN** равенство не подтверждает совместимость; occurrences и report metadata сохраняются.

### Requirement: Безопасный статус сохранённого диалога

Статус MUST возвращать token, CreatedAtUtc/ExpiresAtUtc, истечение при fresh now >= expiry, ContentBytes, мягкий порог и достижение, число принятых compact, safe settings/сохранённый выбор, selected model/effort отдельно от последней server model генерации, known tokens, nullable full estimate и nullable достижение token threshold.

#### Scenario: Истечение с неизвестным бюджетом

- **WHEN** чтение статуса достигает ExpiresAtUtc и содержит opaque без полной оценки
- **THEN** возвращаются срок, байты и known tokens, full estimate и threshold indicator остаются null
- **AND** IsExpired=true и CanContinue=false; история не очищается.

#### Scenario: Последний ответ без server model

- **WHEN** последний report генерации не содержит model
- **THEN** ServerModel=null независимо от выбранной модели и предыдущих report
- **AND** raw envelope и секреты не возвращаются.

#### Scenario: Проверка правила — Безопасный статус сохранённого диалога

- **WHEN** приложение читает статус существующего диалога
- **THEN** snapshot возвращает safe metadata, known tokens и nullable full estimate раздельно.

### Requirement: Метаданные статуса при отказе и expiry

Отказ каталога, несовместимость или неизвестная оценка MUST возвращаться отдельно от доступных метаданных и MUST запрещать CanContinue. Истёкший диалог до удаления MUST сохранять доступные метаданные статуса.

#### Scenario: Проверка правила — Метаданные статуса при отказе и expiry

- **WHEN** диалог истёк либо каталог или оценка отказали
- **THEN** доступные метаданные сохраняются, CanContinue не разрешён.

### Requirement: Граница сохранённого status budget

Оценка MUST относиться только к сохранённому рабочему input без transient providers/new input/tools и MUST NOT подтверждать бюджет полного следующего запроса. Known tokens MUST NOT подменять неизвестную полную оценку. Мягкий порог MUST NOT удалять историю или запрещать запись.

#### Scenario: Проверка правила — Граница сохранённого status budget

- **WHEN** transient providers отсутствуют в сохранённом input
- **THEN** status estimate не доказывает полный следующий request, soft bytes не удаляет историю.

### Requirement: Индивидуальный и общий ключи

Заданный индивидуальный ключ пользователя MUST иметь приоритет над общим ключом приложения. Общий ключ MUST использоваться при отсутствии индивидуального. Ошибка заданного индивидуального ключа MUST NOT приводить к скрытой повторной отправке с общим ключом.

#### Scenario: У пользователя нет ключа

- **WHEN** приложение не предоставило индивидуальный ключ пользователя
- **THEN** запрос использует настроенный общий ключ.

### Requirement: Явное подключение обслуживания AgentBridge

EF-адаптер AgentBridge MUST регистрировать scoped `IDatabaseMaintenance<AgentBridgeContextKey>` через общий coordinator EFCoreLibrary и выбирать SQLite/PostgreSQL/SQL Server по DatabaseOptions без automatic fallback. SingleInitializer MUST задаваться явно.

#### Scenario: Подключение без обслуживания

- **WHEN** приложение регистрирует и разрешает maintenance сервис в отдельном scope
- **THEN** доступен API InspectAsync/UpdateExistingAsync/InitializeNewAsync выбранного provider
- **AND** никакая операция обслуживания не начинается до явного вызова приложения.

#### Scenario: Проверка правила — Явное подключение обслуживания AgentBridge

- **WHEN** приложение выбрало SQLite, PostgreSQL или SQL Server и SingleInitializer
- **THEN** scoped maintenance зарегистрирован через общий coordinator.

### Requirement: Регистрация maintenance без побочных операций

Регистрация и разрешение сервисов MUST NOT открывать соединение, создавать файлы, запускать backup, migrations, процессы или фоновые задачи. Singleton gate MUST оставаться общим на root container и MUST NOT захватывать scoped context.

#### Scenario: Проверка правила — Регистрация maintenance без побочных операций

- **WHEN** root разрешает сервис обслуживания
- **THEN** соединения, backup и фоновые задачи не запускаются, общий gate не захватывает scoped context.

### Requirement: Единый контракт maintenance результатов

Результаты, безопасные ошибки, отмена и диагностика стадий MUST сохранять контракт EFCoreLibrary без автоматического retry или fallback initialization.

#### Scenario: Проверка правила — Единый контракт maintenance результатов

- **WHEN** coordinator возвращает безопасный отказ
- **THEN** адаптер сохраняет error/cancellation контракт без retry или fallback initialization.

### Requirement: Явные настройки backup

AgentBridge MUST требовать положительный backup retention без default. SQLite/PostgreSQL MUST требовать абсолютный локальный backup directory. PostgreSQL MUST требовать абсолютный pg_dump path, server major10+ и конечный положительный cleanup timeout. Настройки MUST проверяться локально до I/O без раскрытия значений.

#### Scenario: Срок backup не выбран

- **WHEN** приложение не задало срок хранения backup
- **THEN** локальная валидация отклоняет настройки до обслуживания
- **AND** срок хранения диалогов не используется как запасное значение.

#### Scenario: Проверка правила — Явные настройки backup

- **WHEN** backup retention не задан или pg_dump settings недопустимы
- **THEN** локальная проверка отклоняет настройки до I/O без раскрытия значений.

### Requirement: Серверный каталог SQL Server backup

SQL Server MUST требовать отдельный SqlServerBackupDirectory на сервере БД независимо от ОС приложения. Локальный BackupDirectory MUST NOT подменять серверный destination. Absolute Unix/Windows drive/UNC формы MUST проверяться без файловой системы app host; доступ service account, retention и cleanup MUST обеспечиваться приложением/оператором.

#### Scenario: Linux server backup из Windows приложения

- **WHEN** Windows приложение задаёт абсолютный Unix SqlServerBackupDirectory
- **THEN** options не требуют существования этого пути на app host
- **AND** server доступность и восстановимость не заявляются по локальной форме или receipt.

### Requirement: Владение backup retention и артефактом

Backup retention MUST оставаться обязанностью приложения и MUST NOT запускать purge или подменяться expiry диалогов. Format, scope, receipt, private workspace и защита от перезаписи MUST делегироваться EFCoreLibrary.

#### Scenario: Проверка правила — Владение backup retention и артефактом

- **WHEN** приложение завершило backup
- **THEN** retention остаётся приложению, receipt и защита артефакта делегируются EFCoreLibrary без автоматического purge.

### Requirement: Backup и migrations через EFCoreLibrary

AgentBridge MUST предоставлять вызываемую приложением операцию обслуживания схемы: check → backup существующей БД при pending migrations → migrate. Backup-возможности SQLite/PostgreSQL/SQL Server MUST строиться на развиваемом контракте EFCoreLibrary; прямой SQL Server backup в AgentBridge MUST NOT служить заменой этой зависимости. Ошибка обязательного backup MUST останавливать migration. Подключение DLL MUST NOT само по себе запускать обслуживание.

#### Scenario: Обновление существующей БД

- **WHEN** обслуживание обнаруживает pending migrations схемы AgentBridge
- **THEN** успешный backup через EFCoreLibrary предшествует применению migrations
- **AND** ошибка backup не скрывается продолжением migration.

### Requirement: Явный и единоличный режим обслуживания

Контракт обслуживания EFCoreLibrary MUST разделять inspection, обновление существующей БД и явную первую установку. Missing MUST подтверждаться механизмом provider; ошибки аутентификации, прав, подключения и проверки EF target MUST NOT автоматически разрешать CREATE.

#### Scenario: Первая установка

- **WHEN** приложение явно выбирает initialization и provider подтверждает Missing
- **THEN** библиотека создаёт БД, проверяет цель и применяет migrations зарегистрированного контекста без фиктивного backup
- **AND** отказ после начала установки запрещает последующее обслуживание тем же gate до внешнего recovery.

#### Scenario: Проверка правила — Явный и единоличный режим обслуживания

- **WHEN** provider проверка завершилась ошибкой прав вместо Missing
- **THEN** CREATE не разрешается, режимы inspection/update/initialization остаются отдельными.

### Requirement: SingleInitializer и границы gate

Режим SingleInitializer MUST выбираться явно; приложение MUST исключать другие экземпляры, writes и DDL на весь период обслуживания. Локальный gate MUST NOT объявляться распределённой блокировкой. Транзакции вызывающего кода и автоматические EF retry strategies MUST отклоняться.

#### Scenario: Проверка правила — SingleInitializer и границы gate

- **WHEN** приложение запускает maintenance при внешней transaction
- **THEN** вызов отклоняется; локальный gate не заменяет остановку других экземпляров, writes и DDL.

### Requirement: Подтверждение backup и владение ресурсами

EFCoreLibrary MUST предоставлять расширяемый реляционный контракт и отдельные optional реализации SQLite, PostgreSQL, SQL Server и MySQL. Набор providers AgentBridge MUST включать SQLite/PostgreSQL/SQL Server. SQL Server maintenance MUST использовать существующий optional module EFCoreLibrary с EngineEdition2/3/4 и прямым endpoint/ConnectRetryCount=0; Azure SQL/MI/Synapse и Engine ARM64 MUST NOT объявляться поддержанными по этому контракту.

#### Scenario: Неизвестное завершение backup

- **WHEN** остановка внешнего процесса или уже отправленной server command не подтверждена
- **THEN** библиотека не выдаёт подтверждённый receipt, запрещает migration и блокирует gate
- **AND** неподтверждённые процессы и используемые ими приватные файлы сохраняют владельца до явного recovery
- **AND** recovery временных ресурсов не сбрасывает блокировку обслуживания автоматически.

#### Scenario: Ошибка и отмена

- **WHEN** обслуживание завершается ошибкой или фактической отменой
- **THEN** наружу и в лог передаются безопасные коды без исходного driver message, credentials и connection string
- **AND** уже установленная typed failure не заменяется одновременно сработавшим cancellation/deadline
- **AND** вторичная неопределённость cleanup сохраняет безопасный первичный код и блокирует повтор.

#### Scenario: Проверка правила — Подтверждение backup и владение ресурсами

- **WHEN** приложение подключает AgentBridge provider
- **THEN** явно выбирается SQLite/PostgreSQL/SQL Server из расширяемых optional модулей EFCoreLibrary.

### Requirement: Подтверждённый receipt перед migration

Receipt MUST связывать operation, фактический target, provider, format, scope, время операции и подтверждённый артефакт до migrations; target MUST повторно проверяться. При отсутствии pending migrations существующей БД backup MUST NOT создаваться.

#### Scenario: Проверка правила — Подтверждённый receipt перед migration

- **WHEN** существующая БД не имеет pending migrations
- **THEN** backup отсутствует; при pending receipt связывает target и подтверждённый артефакт.

### Requirement: Явный запуск dump и сохранение backup

Внешние dump-инструменты MUST запускаться только при явном вызове обслуживания, без shell и произвольных CLI arguments. Успешные backup MUST NOT удаляться автоматически.

#### Scenario: Проверка правила — Явный запуск dump и сохранение backup

- **WHEN** обслуживание явно вызывает dump
- **THEN** нет shell или произвольных arguments, успешная копия не удаляется автоматически.

### Requirement: Очистка через базовые репозитории

AgentBridge MUST предоставлять вызываемую приложением очистку истёкших диалогов. Чтение и удаление MUST использовать базовые read/delete операции EFCoreLibrary и сценарную границу сохранения. Приложение MUST владеть расписанием вызова очистки. Один вызов MUST читать не более одного пакета с явным положительным limit и MUST NOT выполнять повторную выборку или automatic retry.

#### Scenario: Очистка по сроку

- **WHEN** приложение вызывает очистку с limit2 в момент точного истечения трёх диалогов
- **THEN** не более двух кандидатов обрабатываются через EFCoreLibrary
- **AND** третий остаётся до отдельного вызова приложения
- **AND** мягкий порог байтов не подменяет условие истечения срока.

#### Scenario: Частичный отказ пакета

- **WHEN** первое удаление подтверждено, второе возвращает Conflict и третье подтверждено
- **THEN** отчёт содержит два Deleted и один Failed с исходной semantic error
- **AND** отказавший кандидат не перечитывается и не повторяется.

#### Scenario: Отмена или неизвестный исход

- **WHEN** после принятого удаления следующая операция прерывается отменой либо неожиданным исключением
- **THEN** принятое удаление остаётся Deleted, начатая неподтверждённая операция — Unknown, следующие кандидаты — NotAttempted
- **AND** никакой неизвестный исход не объявляется успешным.

#### Scenario: Проверка правила — Очистка через базовые репозитории

- **WHEN** приложение вызывает cleanup с положительным limit
- **THEN** обрабатывается один bounded пакет через base CRUD без повторной выборки.

### Requirement: Fresh UTC и атомарные guards удаления

Каждый read/delete MUST получать отдельный короткий async DI scope; удаления MUST выполняться последовательно со свежим UTC непосредственно перед вызовом порта. Истечение MUST включать равенство nowUtc = ExpiresAtUtc. Existing incarnation/revision/expiry guards MUST повторно проверяться атомарным deletion port; DialogSettings и все зависимые данные MUST удаляться каскадно.

#### Scenario: Проверка правила — Fresh UTC и атомарные guards удаления

- **WHEN** кандидат достигает точного ExpiresAtUtc во время пакета
- **THEN** отдельный scope проверяет fresh expiry и root guards, удаление каскадное.

### Requirement: Отдельные исходы кандидатов cleanup

Отчёт MUST отдельно сохранять подтверждённые удаления, ожидаемые отказы, неизвестные исходы и не начатые кандидаты. Ожидаемый отказ одного удаления MUST сохраняться в отчёте и MUST NOT препятствовать обработке остальных кандидатов прочитанного пакета.

#### Scenario: Проверка правила — Отдельные исходы кандидатов cleanup

- **WHEN** второй кандидат возвращает ожидаемый Conflict
- **THEN** отказ сохранён в отчёте, остальные кандидаты пакета продолжают обработку.

### Requirement: Принятые удаления при отмене и cleanup failure

Отмена MUST останавливать следующие операции, сохраняя принятые результаты. Неожиданные exceptions MUST распространяться с доступным последним отчётом без raw exception в DTO; primary и scope cleanup failures MUST сохраняться вместе. Подтверждённый port success MUST сохраняться в отчёте даже при последующей ошибке DisposeAsync.

#### Scenario: Проверка правила — Принятые удаления при отмене и cleanup failure

- **WHEN** port подтвердил удаление перед ошибкой DisposeAsync
- **THEN** подтверждённый исход остаётся в отчёте, primary/cleanup failure не теряются.

### Requirement: Граница завершённого пакета cleanup

Успешная обработка пакета MUST NOT означать отсутствие других истёкших строк в БД. Мягкий bytes threshold MUST NOT запускать очистку.

#### Scenario: Проверка правила — Граница завершённого пакета cleanup

- **WHEN** первый bounded пакет обработан успешно
- **THEN** отсутствие других expired строк не заявляется, bytes threshold не запускает cleanup.

### Requirement: Подтверждённое завершение Responses

Потоковый ответ MUST NOT считаться успешным только по полученному тексту или HTTP 2xx. EOF без подтверждённого завершения MUST отличаться от завершённого результата. Частичные данные MUST NOT сохраняться как успешное завершение диалога.

#### Scenario: Обрыв SSE после text delta

- **WHEN** поток возвращает text delta и затем закрывается без terminal completion
- **THEN** результат классифицируется как незавершённый
- **AND** не становится успешным завершённым ответом только из-за непустого текста.

### Requirement: Согласование удаления и выполняющегося обращения

Удаление и сохранение результата MUST учитывать актуальность состояния диалога. Результат обращения, начатого до удаления или очистки, MUST NOT восстанавливать удалённую историю и контекст.

#### Scenario: Удаление во время ожидания модели

- **WHEN** диалог очищен или удалён во время ожидания upstream-ответа
- **THEN** позднее сохранение ответа не восстанавливает удалённые сообщения и состояние.

### Requirement: Отсутствие поиска по старой истории

Текущая версия AgentBridge MUST NOT предоставлять поиск по старым сообщениям или автоматически возвращать их в контекст через такой поиск.

#### Scenario: Продолжение после сжатия

- **WHEN** пользователь продолжает диалог со сжатым состоянием
- **THEN** AgentBridge использует актуальное состояние и доступные последующие сообщения
- **AND** не выполняет поиск по старой истории.

### Requirement: Защищённое доменное состояние диалога

Доменное состояние MUST быть независимо от HTTP, EF и идентичностей конкретного интерфейса. Идентичность, владелец и фиксированные даты MUST NOT изменяться обычным прикладным присваиванием.

#### Scenario: Независимое владение и порядок

- **WHEN** переданная идентичность пользователя не совпадает с владельцем
- **THEN** домен отклоняет операции над диалогом без изменения состояния
- **AND** обращения допустимого владельца упорядочиваются по началу, независимо от одинаковых дат и порядка завершения.

#### Scenario: Точная граница доступности

- **GIVEN** диалог создан с настроенным сроком истечения
- **WHEN** переданное время равно `ExpiresAtUtc`
- **THEN** новое обращение, позднее завершение и принятие compact отклоняются
- **AND** ранее зафиксированный срок не продлевается.

#### Scenario: Устаревший результат контекста

- **GIVEN** операция получила снимок версии диалога
- **WHEN** диалог изменился или был удалён до принятия результата
- **THEN** старый снимок не разрешает изменить обращения или активный контекст.

#### Scenario: Проверка правила — Защищённое доменное состояние диалога

- **WHEN** прикладной код обращается к состоянию Domain
- **THEN** owner и fixed dates не доступны обычному изменяющему присваиванию, зависимости UI/HTTP/EF отсутствуют.

### Requirement: Порядок и terminal lifecycle обращений

Обращения MUST иметь явный порядок начала и отличать выполняющееся состояние от конечного успешного, ошибочного, отменённого и неполного результата. Конечный статус MUST NOT переписываться повторным завершением.

#### Scenario: Проверка правила — Порядок и terminal lifecycle обращений

- **WHEN** два обращения имеют одинаковое время и разные статусы
- **THEN** порядок начала сохраняется, terminal статус повторно не меняется.

### Requirement: UTC и актуальность доменной мутации

Доменные операции MUST получать время явно в UTC. При `nowUtc >= ExpiresAtUtc` новое обращение и принятие позднего результата MUST отклоняться. Принятие контекста или результата по устаревшему снимку MUST NOT менять актуальное состояние.

#### Scenario: Проверка правила — UTC и актуальность доменной мутации

- **WHEN** now достиг ExpiresAtUtc или snapshot stale
- **THEN** новый turn и late result не принимаются без изменения текущего состояния.

### Requirement: Покрытие контекста завершёнными обращениями

Покрытие истории на уровне обращений MUST представлять только непрерывный префикс обращений с конечными статусами. Значение 0 MUST означать отсутствие покрытых обращений. Выполняющееся обращение MUST NOT считаться полностью покрытым.

#### Scenario: Пустой префикс и повторное сжатие

- **WHEN** ни одно обращение ещё не покрыто контекстом
- **THEN** допустимо принять версию с покрытием 0
- **AND** повторное принятие того же префикса создаёт следующую версию, сохраняя историю.

#### Scenario: Незавершённое обращение внутри префикса

- **GIVEN** первое обращение выполняется, а второе уже имеет конечный статус
- **WHEN** результат compact объявляет покрытым префикс до второго обращения
- **THEN** домен отклоняет покрытие без изменения предыдущего контекста
- **AND** будущие ответы и результаты инструментов первого обращения не считаются уже покрытыми.

#### Scenario: Проверка правила — Покрытие контекста завершёнными обращениями

- **WHEN** первое обращение InProgress, а второе terminal
- **THEN** prefix до второго не принимается;0 означает отсутствие покрытия.

### Requirement: Монотонный prefix и протокольная граница

Покрытие принятой следующей версии MUST NOT возвращаться назад и MUST NOT выходить за существующую историю. Повторное сжатие того же префикса MUST быть допустимо без удаления обращений. Эти метаданные MUST NOT заменять точную границу отдельных элементов Responses или служить основанием пропустить будущие результаты текущего обращения.

#### Scenario: Проверка правила — Монотонный prefix и протокольная граница

- **WHEN** compact повторно покрывает тот же terminal prefix
- **THEN** новая версия допустима без удаления истории или пропуска будущих protocol items.

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

### Requirement: Явные обязательные настройки приложения

AgentBridge MUST получать настройки из выбранного IConfiguration приложения или явных programmatic callbacks. Agent/Retention/Compaction/CodexLb/Database MUST требовать каждый обязательный для выбранного режима ключ без рабочих defaults. Предшествующий Configure MUST NOT скрывать отсутствующий configuration key.

#### Scenario: Отсутствие обязательного ключа

- **WHEN** выбранный configuration section не содержит обязательный ключ при ранее заполненных options
- **THEN** startup/options validation отклоняет настройку с безопасным path без исходного значения.

### Requirement: Диапазоны явных настроек

Отсутствующий раздел, blank, malformed, invalid range, undefined enum и overflow MUST отклоняться до первого соответствующего сценария. Explicit reserve=0 и значения прежних defaults MUST приниматься.

#### Scenario: Явный нулевой запас

- **WHEN** все обязательные ключи заданы, а InputTokenReserve явно равен нулю
- **THEN** локальная validation принимает reserve; отсутствие этого ключа даёт отказ.

### Requirement: Безопасный стандартный options pipeline

Binding MUST сохранять обычный IConfiguration provider precedence, Configure/PostConfigure, IOptions/IOptionsSnapshot/IOptionsMonitor и reload. Ошибки Binder MUST NOT раскрывать исходные values или unsafe inner exception. IStartupValidator MUST быть доступен приложению без host; registration MUST NOT строить ServiceProvider, запускать host, HTTP, DB или file logger. IConfiguration MUST NOT передаваться runtime-сервисам; собственного config loader/options store MUST NOT быть.

#### Scenario: Проверка без hosting

- **WHEN** приложение явно вызывает IStartupValidator после построения своего контейнера
- **THEN** missing/malformed настройки отклоняются до операций, а merged providers и explicit overrides сохраняют обычный приоритет.

### Requirement: Явные источники ключа и инструкций

InstructionsSource MUST явно выбирать Configuration или PerRequest. Configuration MUST требовать непустые options instructions и сохранять request override; PerRequest MUST требовать instructions обращения без options/string.Empty fallback.

#### Scenario: Инструкции обращения отсутствуют

- **WHEN** выбран PerRequest, но инструкции обращения не предоставлены
- **THEN** run отклоняется до первого I/O без options fallback.

### Requirement: Явный источник ключа

KeySource MUST явно выбирать Shared или Individual. Shared MUST требовать общий ключ и сохранять приоритет provided индивидуального источника; только null разрешает общий. Individual MUST NOT использовать общий fallback. Ошибка индивидуального ключа/источника MUST NOT переключать account. Local validation MUST NOT обещать catalog capabilities без HTTP.

#### Scenario: Индивидуальный источник без ключа

- **WHEN** Individual возвращает null при наличии общего ключа
- **THEN** доступ отклоняется без общего fallback и без HTTP.

### Requirement: Владение расписанием и logger конфигурацией

Приложение MUST выбирать и валидировать cleanup schedule/bounded batch только в своём включённом режиме очистки и явно вызывать CleanupAsync в short scope. При disabled cleanup schedule MUST NOT требоваться библиотекой. AgentBridge MUST NOT создавать собственный scheduler.

#### Scenario: Очистка отключена

- **WHEN** приложение не включает cleanup scheduler
- **THEN** библиотека не требует schedule и не запускает background job.

### Requirement: Владение конфигурацией logging и maintenance

Logging sinks/path/rotation/retention MUST принадлежать приложению; file mode MUST требовать явный путь в регистрации logging приложения. Console/custom ILogger MUST NOT требовать file path. AgentBridge MUST NOT создавать собственный logger. Optional maintenance MUST требовать provider-specific backup settings только при его явном подключении.

#### Scenario: Отключённое обслуживание приложения

- **WHEN** приложение не подключает maintenance и использует console logger без cleanup scheduler
- **THEN** Backup, schedule и file path не становятся обязательными AgentBridge options.
