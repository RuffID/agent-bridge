# Контекст проекта AgentBridge

## Назначение

AgentBridge позволяет подключить ИИ-агента к приложению на C# и .NET 10 и повторно использовать общую логику диалогов, контекста и инструментов в ASP.NET Core, WPF, Telegram-ботах и других SDK-style приложениях. Имя проекта и репозитория — `agent-bridge`.

AgentBridge — название разрабатываемой библиотеки. Нормативные требования находятся в [spec.md](spec.md).

Подробное описание поддерживается по разделам в [Documentation](../../../Documentation/README.md): [Business logic](<../../../Documentation/Business logic/README.md>) и [Technical documentation](<../../../Documentation/Technical documentation/README.md>).

## Согласованные границы

Библиотека распространяется обычными DLL. Подключающее приложение выбирает SQLite или PostgreSQL, задаёт подключение, мягкий порог байтов на диалог, срок хранения от создания и порог сжатия в токенах в собственной конфигурации. Начальные значения: 10 МиБ содержимого, 7 дней, 32 000 токенов. Поиск по старой истории не входит в текущий объём.

Ядро агента отделено от интеграций. Адаптер codex-lb использует HttpClientLibrary. Доступ к данным построен на EFCoreLibrary с приоритетом базовых операций чтения, создания, изменения и удаления. Спорные моменты развивающихся библиотек обсуждаются с пользователем до выбора решения.

Приложение может читать безопасные настройки, менять модель/effort и показывать `ExpiresAtUtc`. Индивидуальный ключ пользователя имеет приоритет, общий применяется при его отсутствии. Логирование проходит через Serilog приложения.

## Реализованная основа диагностики

Этап 03 предоставляет `AddAgentBridgeDiagnostics` и `AgentBridgeDiagnostics` через стандартный `ILogger<AgentBridgeDiagnostics>`. Это наблюдение, не AgentRunner и не отдельный logging framework: provider, Serilog logger, sinks, фильтры, enrichers и освобождение ресурсов выбирает приложение. В production нет зависимости от Serilog и обращения к `Log.Logger`. Связь с настоящим Serilog provider проверена изолированно с sink в памяти; автоматическая диагностика будущих HTTP/storage-сценариев ещё не подключена.

Имена операции берутся из закрытого enum; корреляция — непустой GUID. Каждое наблюдение получает отдельный OperationId и монотонно измеренную DurationMs. `Complete`/`Fail` дают единственное итоговое событие с Operation, OperationId, CorrelationId, Status, ErrorCode и DurationMs. Наличие в enum имён будущих операций не означает их реализации. Отсутствие явного завершения не выдаётся за успех.

`Fail` не передаёт исключение logger, не читает Message/ToString/Data/InnerException и не принимает URL, configuration objects или текст диалога. Отказ от произвольного payload предотвращает утечки через форматирование исключений; диагностический код ошибки выбирается из фиксированного набора. Приложение отвечает за свои ambient scopes/enrichers и журналы других компонентов. На этапе 03 HttpClientLibrary не изменялась; её последующее согласованное развитие описано ниже.

Для OperationCanceledException передаются два раздельных исходных токена: caller и локальный deadline, без linked token. Caller имеет приоритет, если сработали оба: Canceled/Information; только deadline — DeadlineExceeded/Warning; отсутствие подтверждённого источника — Failed/Error с UnattributedCancellation. Обычная ошибка, включая TimeoutException, не меняет причину только по имени типа или состоянию токенов. Наблюдение не отменяет работу и не заменяет выброшенное исключение. Владелец сценария сам повторно выбрасывает исходную ошибку.

Пример доступной операции: приложение начинает `ConfigurationValidation`, явно вызывает существующий `IStartupValidator.Validate()`, затем `Complete`; в catch вызывает `Fail(error)` и `throw`. Пример кода и контракт: [техническая документация](../../../Documentation/Technical%20documentation/07-tokenizer-and-settings.md#serilog), результаты 36 тестов ядра (18 новых): [этап 03](../../../Documentation/Plans/AgentBridge%20Initial%20Implementation/03-serilog-integration.md). Нормативный [spec.md](spec.md) не меняется: реализуется ранее согласованная диагностика, без новых бизнес-сценариев.

## Прочие согласованные границы

На этапе 04 пользователь разрешил изменения HttpClientLibrary: default-safe логирование, статусный Message, None/opt-in JsonStructure и отдельно ограниченный транспортный error contract. Выполнены проверки библиотеки на net8/net10; адаптер AgentBridge ещё не подключён. Подробности API и результаты: [HTTP-контракт](<../../../Documentation/Technical documentation/03-http-and-codex-lb.md>) и [этап 04](<../../../Documentation/Plans/AgentBridge Initial Implementation/04-httpclientlibrary-logging.md>).

Структурная сводка намеренно не содержит текст body, исходные имена и значения. Это позволяет исключать секретные значения без обещания универсального распознавания секретов в произвольном тексте. Например, `{"private_key":"secret","items":[1]}` даёт только виды узлов и числовые счётчики. Объём и структура остаются наблюдаемыми метаданными. Режим текста и app-owned sanitizer не входят в принятый объём.

Для будущих этапов 13–15 ошибка сохраняет raw headers и body до 65536 байтов по умолчанию с явной полнотой. Полный транспортный текст может оказаться невалидным JSON; prefix Truncated нельзя выдавать за complete envelope. Строгий UTF-8 предотвращает скрытую замену неверных байтов, но иные объявленные кодировки дают UnsupportedContent. Byte limit проверяется дополнительным байтом, поэтому чтение ошибки может длиться дольше прежнего 2000-char snippet; cancellation остаётся ответственностью вызывающего кода. API сохраняет старые конструкторы, но содержимое Message и формат логов меняются.

ErrorResponse и прежние свойства исключения могут содержать секреты: библиотека их не логирует, а приложение отвечает за свои enrichers, scopes и обработку результатов. Type/code/param, server correlation и retry-семантика остаются будущему адаптеру; отсутствие полного envelope не разрешает повтор.

Порядок check/backup/migrate взят как функциональный образец из AquaByte-Ledger. Этап 05 реализован и принят; запрещённые проверки пропущены. В EFCoreLibrary реализован общий coordinator и четыре optional maintenance-модуля SQLite/PostgreSQL/SQL Server/MySQL; commit `a1747388ab0eb2be6da3031535fd88e7533b8df1`. AgentBridge сохраняет только SQLite/PostgreSQL в options; подключение EF adapter и startup остаётся последующим этапам. Очистка истёкших диалогов использует базовые репозитории, расписанием вызовов владеет приложение.

Выбран явный SingleInitializer: приложение останавливает другие экземпляры, writes и DDL. Это позволяет не выдавать локальный SemaphoreSlim за distributed lock. EF connection удерживается между проверками, но приложению нужен стабильный прямой endpoint без failover/proxy routing и автоматических reconnect. Пример: приложение с существующей PostgreSQL БД явно вызывает `UpdateExistingAsync`; только при pending migrations выполняются pg_dump, подтверждение receipt и повторная сверка цели перед migration. `InitializeNewAsync` выбирается отдельно и никогда не служит fallback после ошибки пароля.

Scope ограничен реальным механизмом: SQLite — native backup обычной main без attachments; PostgreSQL — custom dump выбранной БД без global roles; SQL Server — full COPY_ONLY/CHECKSUM на сервере с HEADERONLY/VERIFYONLY, без server logins/внешних ключей; MySQL — InnoDB database/routines/triggers/events без accounts, tablespaces и replication topology. MySQL требует доступного и выключенного partial_revokes и прямых global grants для доказательства полноты metadata. Технический driver — MIT MySqlConnector; совместимый EF provider выбирает приложение, mandatory Oracle dependency нет.

Завершённые backup сохраняются до явной политики оператора; удаление диалога не стирает данные из старых копий. Credentials private и временные, но не удаляются, пока неизвестно, остановился ли использующий их процесс. CleanupUnconfirmed отравляет gate, singleton сохраняет ownership до explicit RetryCleanupAsync; recovery файлов/процесса не доказывает целостность схемы и не снимает gate. SQL Server после dispatch требует отдельной операторской проверки остановки. Restore не автоматизирован и не проверен реальным восстановлением. Конечные budgets кооперативны, без гарантии немедленного прерывания зависшего native/OS вызова.

Результаты адресной сборки, изолированных тестов и платформенные ограничения находятся в [этапе 05](<../../../Documentation/Plans/AgentBridge Initial Implementation/05-efcorelibrary-maintenance.md>); API и эксплуатационные предпосылки — в [техническом описании](<../../../Documentation/Technical documentation/06-database-maintenance.md>) и README EFCoreLibrary. Реальные БД, SQL, backup/restore и процессы в этих проверках не запускались.

Приложение владеет бизнес-данными, авторизацией и обработчиками инструментов. AgentBridge собирает контекст и координирует обращение к модели. codex-lb сохраняет свою роль upstream-шлюза: выбор аккаунта, маршрутизация и передача ответа.

## Прикладные контракты этапа 07

Этап 07 определяет независимые прикладные порты и принят; запрещённые проверки пропущены. [Описание API](<../../../Documentation/Technical documentation/09-application-ports.md>) отделяет канонические items от полного envelope, результатов шагов и continuation. Контейнеры используют независимый JsonElement.Clone без реализации JSON/SSE mapping. ModelAccess фиксирует уже выбранный ключ на вызов; выбор ключа остаётся этапу 13. Например, output reasoning содержит encrypted_content, а envelope того же шага — id/usage/unknown metadata; оба снимка сохраняются отдельно, envelope не отправляется как input item.

Storage ports выражают короткие атомарные операции, а не готовую persistence. Incarnation/revision получаются от хранилища, не из private lifetime Domain-объекта. После ожидания модели старое условие записи нельзя заменять свежим ради обхода конфликта. Пример отказа: ID и revision совпали после пересоздания, но incarnation другой — старый ответ не записывается. Этап 08 добавляет EF-модели/DI; CRUD/UoW, восстановление и реальная concurrency остаются этапам 09–10. Contract fakes доказывают только заменяемость и форму условий. Точная граница Responses items также остаётся последующим этапам. Проверены 93 теста ядра, включая 27 новых; запрещённые интеграции пропущены.

## Формат хранения этапа 08

Статус: **реализован и принят; запрещённые проверки пропущены**. Изменение [persistence-models](../../changes/persistence-models/proposal.md) остаётся неархивированным. Общий AgentBridgeDbContext использует локальный EFCoreLibrary, Microsoft EF/Relational/SQLite 10.0.11 и Npgsql provider 10.0.3. Domain не менялся: DTO отделены от агрегата, rehydration ещё не реализована.

Таблицы разделяют диалог, обращения, полные канонические items, результаты model steps и принятые compact. Turn ID локален диалогу, step ID — обращению; composite FK сохраняет родительское владение. Порядок задан Sequence/Version, не временем/порядком выдачи provider. Активный compact — максимальная принятая Version, старые состояния и история сохраняются. ThroughTurnSequence не фильтрует будущие outputs/tool-results.

Например, function_call_output с call_id хранится полным item; envelope того же model step с id/usage/unknown metadata сохраняется отдельно, как и opaque continuation. ModelResponseRecord сохраняет весь lifecycle/output, включая Failed/Incomplete/Canceled; compact имеет Completed constraint. FormatVersion=1, повреждённые или несовместимые данные отклоняются явно. ModelAccess/API keys не входят в persistence DTO, payload не логируется.

UTC ticks в INTEGER/bigint сохраняют точность и сортировку expiry на обоих providers. JSON text избегает provider-нормализации. OwnerId остаётся без нормализации и искусственного MaxLength; отдельного owner-индекса нет, поскольку текущие порты читают по ID. Индексы поддерживают порядок и ограниченную очистку ExpiresAtUtc/Id. Fixed-field и concurrency metadata не доказывают существование/owner/expiry/accessID/incarnation/revision guards; их атомарная реализация — этапы 09–10, вне ожидания сети.

Явный AddAgentBridgePersistence после AddDatabaseConfiguration регистрирует один scoped adapter/context-key и base repositories. Default SQLite и автоматическое обслуживание отсутствуют; sensitive data logging выключен. Проверены 34 persistence-теста (25 новых), две сборки без warnings/errors. Metadata/serialization/DI не доказывают relational enforcement или restart на БД. БД/SQL/migrations/backup/hosting/процессы пропущены по указанию пользователя; OpenSpec CLI отсутствует в PATH. [Фактический API](<../../../Documentation/Technical documentation/02-efcorelibrary.md#реализация-этапа-08>), [команды и ограничения](<../../../Documentation/Plans/AgentBridge Initial Implementation/08-persistence-models.md>).

## Поток одного обращения

1. Приложение передаёт сообщение и идентификаторы пользователя и диалога.
2. AgentBridge загружает доступную историю и состояние контекста из настроенной БД.
3. Зарегистрированные источники приложения предоставляют необходимый бизнес-контекст.
4. AgentBridge отправляет запрос через отдельный адаптер в codex-lb.
5. При запросе инструмента AgentBridge обращается к разрешённому обработчику приложения и передаёт результат модели.
6. AgentBridge сохраняет результат обращения и состояние диалога, затем возвращает ответ приложению.

## История и рабочее окно контекста

На этапе 02 реализован конфигурационный API: options ядра и адаптеров, групповые DI-расширения и локальная валидация. Пример: `DialogRetentionOptions.CalculateExpiresAtUtc(createdAtUtc)` при `RetentionPeriod = TimeSpan.FromDays(14)` возвращает дату создания плюс 14 дней. Этап 06 уже фиксирует вычисленные даты в доменном `Dialog`; сохранение в БД ещё не реализовано. Binding использует источник приложения, а сценарии будут получать конкретные options, без `IConfiguration`.

Отсутствующие адрес, модель, выбранный провайдер и строка подключения дают явные ошибки. Локальная проверка лимитов не заменяет проверку каталога и бюджета модели; effort остаётся строкой wire-контракта, дефолт `medium` не гарантирует поддержку. Ключи и подключение находятся во входных options приложения, которые нельзя использовать как безопасный UI-снимок или логировать. Settings service и выбор ключа остаются последующим этапам. Подробности фактического API: [конфигурация](../../../Documentation/Technical%20documentation/05-configuration-and-lifecycle.md).

История в БД нужна для продолжения диалога между запусками и управления сроком хранения. Рабочее окно — часть состояния, передаваемая модели на очередном шаге. Разделение этих обязанностей позволяет уменьшать рабочий контекст без немедленного удаления полной истории.

Сжатие не заменяет ограничения объёма и времени хранения БД. Эти ограничения учитывают также сохранённые результаты сжатия.

## Пример

Этап 06 реализован и принят, запрещённые проверки пропущены: чистый `Domain/Dialogs`, неизменяемые идентичности/дочерние состояния, read-only коллекции, порядок начала обращений, конечные статусы и версии контекста. На `nowUtc >= ExpiresAtUtc` домен отклоняет продолжение и поздние результаты. Настроенные 36 часов сохраняются после активности/compact/изменения options; новый диалог получает новую политику. Доменное удаление очищает дочерние состояния и инвалидирует снимки.

Пользователь согласовал покрытие контекста только префиксом terminal turns, включая 0; выполняющийся turn или дыра в префиксе не считаются покрытыми. Это предотвращает признание будущих outputs/tools уже покрытыми на уровне обращений. Канонический cutoff элементов Responses, payload и composition/compact остаются этапам 14–18; метаданные не заменяют эту реализацию.

Снимок `DialogStateVersion` содержит private случайную идентичность текущей жизни объекта и Revision. Поэтому он отклоняется другим заново созданным объектом даже с тем же ID/Revision. Restart/rehydration и persistent concurrency не реализованы: на этапах 07–10 потребуется сохраняемая идентичность жизни и атомарная проверка актуальности после повторной загрузки. Получение нового token не разрешает применить результат, построенный по старому контексту. В памяти агрегат не потокобезопасен и не выбирает очередь конкурентных обращений.

Проверки этапа 06: 66 passed / 0 failed / 0 skipped, включая 30 новых доменных; затронутые ядро/тестовый проект собраны без ошибок и предупреждений. Приложения, БД и реальные HTTP не запускались. [Фактический API и пример](<../../../Documentation/Technical documentation/08-dialog-domain-state.md>), [команды и пропуски](<../../../Documentation/Plans/AgentBridge Initial Implementation/06-dialog-domain-state.md>).

Пользователь Telegram-бота спрашивает: «Что с моим заказом?». Агент запрашивает инструмент `GetOrderStatus`. Обработчик приложения проверяет доступ пользователя к заказу и получает сведения через бизнес-сервис. AgentBridge передаёт результат модели и сохраняет ответ в диалог.

При следующем сообщении «Когда доставят?» AgentBridge восстанавливает контекст этого диалога. Пользователю не требуется заново указывать заказ, если необходимые сведения остаются доступны в рамках политики хранения.

