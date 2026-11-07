# Открытые вопросы

## Вопросы клиенту

Открытых вопросов нет. Требования: отдельный коннектор, ключ sub2api, выбор через DI и сохранение прикладного API. Ниже — проверки разработчика, а не запрос нового бизнес-решения.

## Технические вопросы разработчику — внутренний раздел

После частичного этапа03 все T01–T05 остаются открытыми. [Результаты03](03-sub2api/results.md) подтверждают доступ, ordinary каталог ID, fail-closed settings и безопасный запрет generation/compact. [Блокеры/необходимый контракт](03-sub2api/contract-blockers.md) проверены по актуальным исходникам0.2.19. Unsupported и isolated tests не закрывают capability/OAuth/server-profile вопросы; исторические [результаты02](02-composition/results.md) сохраняются.

### T01. Подтверждённый публичный входной бюджет и provenance возможностей

На03 проверены28 sub2api sources по описи01: без изменений; публичный versioned budget/provenance не найден. Implemented ordinary catalogue намеренно не читает неподтверждённые metadata/effort; settings остаётся Unsupported. Для продолжения нужны перечисленные в [контракте препятствия](03-sub2api/contract-blockers.md) version/profile, семантика input budget и применимость возможностей к generation pool. Это серверное расширение/доказательство существующего контракта, не обход HttpClientLibrary или validator.

В [матрице01.01](01-contracts/wire-matrix.md) установлено: ordinary list не обещает metadata; Codex manifest смешивает upstream и локальные шаблоны. Публичный контракт input_context_window / доказанная формула входного бюджета не найдены. context_window, max_context_window и effective percent=95 не доказывают доступный input budget; внутренний UpstreamModelMetadata.MaxOutputTokens не гарантированно публикуется в manifest. При этом settings ядра требуют подтверждённый входной бюджет.

До03.01 требуется получить versioned контракт целевого sub2api-профиля: точное публичное поле/семантику input budget, источник output limit и происхождение effort для реально выбираемых аккаунтов. Например, manifest context_window=272000 для локально дополненной модели не разрешает передать InputContextWindow=272000 или258400. Пока нет доказательства — null и Unsupported, без таблицы guessed defaults и без admin API. Если нужен новый контракт сервера/конфигурации либо соседней библиотеки, вынести адресное расширение отдельно. Это блокирует пригодный для запуска settings path, но не закрытую статическую фиксацию01.01.

### T02. Полный контекст с OAuth и инструментами

На03 повторно прослежены обе forwarding ветки/non-stream conversion. Actual Sub2Api JSON/SSE/tool transport не включён до T05; linked fixtures/codex-lb regression не закрывают T02. После снятия блокировки нужны isolated gateway tests и отдельно live chain.

На01.01 ограничение подтверждено в Responses handler: HTTP previous_response_id пропускает OAuth/SetupToken аккаунты и ищет OpenAI API-key account. Подготовлены [образцы W03–W06](01-contracts/wire-samples.md), но это не live-трафик. На03.02/03.03 и05.03 проверить пользовательское сообщение → function_call → durable function_call_output → итоговый ответ → следующий turn с полным рабочим контекстом без поля previous_response_id и без legacy turn-state. Проверить обе forwarding ветки, reasoning/encrypted_content, compact discriminator/output, смену выбранного upstream account и Content-Type при JSON/SSE/compact. OAuth принудительно stream=true upstream и удаляет max_output_tokens; client cancel может оставить server drain/биллинг.

Нужны isolated parser/transport результаты и отдельно разрешённая live OAuth цепочка с подтверждёнными T01 capabilities. Если она не работает, не подменять стратегию retry/WebSocket; назвать препятствие и согласовать расширение. Runtime/OAuth-приёмка остаётся открытой.

### T03. Provenance и legacy opaque-состояние

Continuation уже имеет adapter/binding, но проверка совместимости контекста учитывает прежде всего модель. На 04.01 определить место хранения identity коннектора для результатов и compact, учитывая legacy записи без identity. Пример: одинаковый model ID у двух шлюзов не подтверждает совместимость encrypted reasoning. Сначала проверить достаточность versioned metadata существующих JSON-контрактов. Если нужна схема БД или изменение публичного контракта, остановить эту часть и согласовать адресное расширение; не добавлять migration в рамках обычной адаптации незаметно.

### T04. Новая поставка против сохранённой прежней бинарной поверхности

На02 выполнено PE/API subset comparison AgentBridge/CodexLb/Integration: прежние declaring types/assembly identity и декодированные public/protected сигнатуры/constraints/defaults сохранены. Legacy probe компилируется как source-linked код, без выполнения. Это не external consumer и не проверка CLR resolution старых binaries; custom attributes/nullable и identities полного referenced closure не покрыты этим comparator. Перечисленные ниже проверки05.02 сохраняются.

На01.02 source fingerprints текущего kit совпали, PE identities/API зафиксированы, kit и две frozen consumer DLL сохранены вне репозиториев. Оба исходных compile-only consumers собраны против baseline DLL, без исполнения. [Опись и методика](01-contracts/compatibility-checks.md) содержит точные пути/команды/hashes.

На05.02 нужны fresh новая поставка, source rebuild неизменённого consumer, semantic API comparison и resolution TypeRef/MemberRef/MethodSpec **неперекомпилированных** consumer DLL против нового closure. Такого old→new verifier результата пока нет; повторная компиляция не заменяет ABI. Проверить все заявленные provider/RID, отдельно Windows DI/runtime. Facade по исходникам и PE имеет ссылку на CodexLb; разделение csproj не оправдывает удаление legacy DLL/типов или breaking change.

### T05. Гарантии на границе AgentBridge и внутренние повторы sub2api

На03 подтверждены lossy invalid-encrypted-content retry, rejected-field recovery, compact fallback и обратная замена model в JSON/SSE обеих веток. Actual server profile не предоставлен; response.model/один успешный HTTP-ответ недостаточны. Generation/compact блокируются до HTTP даже с custom settings. [Необходимые профиль/гарантии/metadata](03-sub2api/contract-blockers.md) описаны конкретно; это не клиентская опция, которая могла бы сама отключить серверные transformations. Provenance зависимость зафиксирована без начала04.

На01.01 найдены server account failover, HTTP recovery после invalid_encrypted_content с изменением reasoning, compact-only mapping и Gateway.OpenAICompactModel fallback. OAuth также нормализует tools/IDs/instructions. Поэтому один запрос клиента и успешный ответ не доказывают неизменность upstream модели/контекста.

До03.02/03.03 определить проверяемый серверный профиль и возможность обеспечить требуемые opaque/model guarantees: какие ветки используются, какие mappings/retries включены, по каким response metadata видно фактическую обработку. Например, requested model A и successful compact ещё не доказывают, что compact выполнен моделью A. На05.03 подтвердить на целевой поставке или явно заблокировать приёмку. Требование отсутствия клиентского retry сохраняется; скрывать server transformations или менять sub2api в рамках этого плана нельзя. Если strict upstream guarantee требует изменения требований/сервера, нужно отдельное согласование этой конкретной границы.
