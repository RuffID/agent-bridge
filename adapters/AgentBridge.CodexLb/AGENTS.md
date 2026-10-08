# Адаптер codex-lb

- CodexLbModelSettingsReader.ReadWithAccessAsync проверяет exact выбор и каталог уже зафиксированным ModelAccess run без повторного resolver/source I/O. Standalone ReadAsync сохраняет primitive settings snapshot до resolver и исходные typed errors. Snapshot доступа не сохраняется; fake handler actual HttpClientLibrary проверяет смену источника между resolve и catalog.

## Ответственность и границы

- Проект `AgentBridge.CodexLb.csproj` реализует транспортные порты ядра; зависит от `AgentBridge/agent-bridge.csproj`. Обратная ссылка из ядра и зависимость от EF-хранилища запрещены.
- Здесь находятся wire DTO, преобразование JSON/SSE Responses, compact и транспортные ошибки. Доменные решения, права пользователя и выполнение инструментов остаются вне адаптера.
- Исходящий HTTP выполняется только через локальный ProjectReference HttpClientLibrary. Models содержит чтение `/v1/models`, проекцию capabilities, выбор доступа и чтение настроек. Responses реализует JSON/SSE GenerateAsync этапов 14–15; границы в `Responses/AGENTS.md`. CompactAsync реализован на этапе18 с отдельными CompactRequestWriter/CompactJsonReader и CompactTimeout.
- HTTP 2xx, text delta и EOF не заменяют подтверждённое terminal-состояние. Сохранять caller cancellation и владение потоком; не вводить скрытые retry или смену ключа/модели.
- Регистрация и параметры подключения принадлежат composition root приложения. Адаптер не создаёт host и не владеет жизненным циклом приложения.
- `Configuration/` содержит `CodexLbOptions` и `AddCodexLbConfiguration`: только binding и локальные проверки, без HTTP, выбора ключа или обращения к каталогу. Модель обязательна; effort не проверяется статическим списком. Общий ключ может отсутствовать при индивидуальных ключах. Options с секретом не передавать в UI или лог; ошибки локальной валидации не содержат значения настроек.
- BaseAddress/Model/ReasoningEffort/GenerationTimeout/CompactTimeout и KeySource обязательны без рабочих defaults. KeySource=Shared требует SharedApiKey, но сохраняет приоритет provided индивидуального источника; Individual использует только источник приложения и при null не применяет общий ключ. Ошибка индивидуального ключа/источника никогда не разрешает fallback. SafeOptionsBindingExtensions ядра ограждает standard Binder errors/reload без secret values/inner exception; Options Configure/PostConfigure остаются штатными.
- `AddCodexLbModelCatalog` отдельно регистрирует scoped actual HttpApiClient и Application ports. Приложение регистрирует IIndividualModelKeySource, logging/options и предоставляет принадлежащий ему HttpClient; фабрика не выполняет I/O. Timeout, handlers и освобождение HttpClient принадлежат приложению. Общий mutable Authorization/default headers, скрытые retries и общий кеш каталога запрещены.
- `AddCodexLbResponses` подключает тот же каталог/resolver/HttpApiClient и scoped IModelGateway; использовать вместо отдельного вызова AddCodexLbModelCatalog при подключении JSON/SSE генерации. GenerationTimeout ограничивает отправку и чтение JSON/SSE; app HttpClient/handlers не должны вводить retries или менять account. Локальные snapshots не доказывают live upstream compatibility.
- Только null индивидуального ключа допускает SharedApiKey. Пустой/непригодный для Bearer ключ, ошибка источника или HTTP-отказ не разрешают смену ключа. Каталог читает канонический base-prefix + `/v1/models` без client_version; API-key authentication и upstream routing принадлежат codex-lb.
- Metadata.input_context_window — единственный бюджет проверки настроек; неизвестный бюджет даёт Unsupported. Сохранять независимые списки и объявленные флаги, не публиковать raw/unknown metadata. ID/effort сравнивать точно. Пустой data допустим; нарушенная форма даёт безопасный Rejected, без статического fallback.
- HTTP-отказ маппится только по статусу: 401 Unauthorized, 403 Forbidden, остальные Rejected. Ошибка JSON/формы — Rejected. Raw headers/body/reason/Exception.Message не выдавать и не логировать; preview никогда не считать envelope. HttpClientLibrary использует 65536-byte capture и явную полноту. Caller cancellation и неожиданный I/O не маскировать. Settings reader сохраняет typed failure целиком перед проверкой поздней отмены.

## Документация и проверка

- `Auxiliary/` реализует optional IModelAuxiliaryGateway usage/files/images через тот же HttpClientLibrary; регистрация AddCodexLbAuxiliary отдельна от facade/Responses. Все пять auxiliary deadlines обязательны. Signed upload без Bearer; содержимое/URL/errors не логировать. Границы — Auxiliary/AGENTS.md и технический документ27.

- Самостоятельные типы и методы получают XML `<summary>` на русском; реализации интерфейсов — `<inheritdoc/>`. Wire-контракт сверять с текущими исходниками и [технической документацией](../../Documentation/Technical%20documentation/03-http-and-codex-lb.md).
- Изолированные проверки относятся к `tests/AgentBridge.CodexLb.Tests`: подставные ответы и локальные потоки, без живого шлюза, OpenAI и сети.
- Compile-check выполняется по конкретному проекту после проверки build-файлов; при изменении транспортной границы обновлять этот документ.
