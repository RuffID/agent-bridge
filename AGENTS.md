# agent-bridge

## Назначение

C#-библиотека AgentBridge для SDK-style приложений на .NET 10. Имя папки, проекта и GitHub-репозитория — `agent-bridge`. Подключение — обычными DLL, без публикации NuGet-пакетов.

## Источник требований

- `openspec/specs/agent-runtime/spec.md` — согласованные проверяемые требования.
- `Documentation/README.md` — общий навигационный документ.
- `Documentation/Business logic/` — бизнес-логика по разделам.
- `Documentation/Technical documentation/` — технические решения и обязанности типов; планируемые типы явно отличать от реализованных.
- `Documentation/Plans/AgentBridge Initial Implementation/README.md` — навигация по небольшим этапам реализации. Имена файлов и папок на английском, содержимое на русском. Создание плана не является разрешением начать кодирование.
- Бизнес-описание и техничку не объединять в один `Technical requirements.md`.
- Открытые вопросы и несогласованные решения не добавлять в документацию без просьбы пользователя.

## Структура решения

- `agent-bridge.slnx` — решение .NET 10.
- `agent-bridge.csproj` — ядро и прикладной слой в корне решения: SDK-style библиотека `net10.0`, сборка `AgentBridge.dll`, пространство имён `AgentBridge`.
- `Configuration/` — типизированные настройки ядра и групповая регистрация options; локальные границы в `Configuration/AGENTS.md`.
- `Diagnostics/` — безопасные структурированные события через ILogger приложения; локальные границы и контракт отмены в `Diagnostics/AGENTS.md`.
- `Domain/` — независимое состояние диалога, фиксированный срок и проверка актуальности; границы в `Domain/AGENTS.md`.
- `Application/` — независимые порты модели, контекста, инструментов, tokenizer и коротких сценариев хранения, ContextBuilder16, ContextCompactor18, registry/executor tools19 и AgentRunner20; границы в `Application/AGENTS.md`. Durable checkpoint/recovery20 использует явный journal и короткие scopes; standalone session-memory tools не защищает restart.
- `Tokenization/` — offline BPE реализация IContextTokenCounter этапа17; проверенный exact mapping, known/nullable estimate, embedded словари и границы в `Tokenization/AGENTS.md`.
- `adapters/AgentBridge.CodexLb/` — отдельный адаптер транспорта; правила в локальном `AGENTS.md`.
- `adapters/AgentBridge.Integration/` — отдельный facade net10.0: AddAgentBridge с IConfiguration и app-owned HTTP factory, required app logger/source modes; ближайшие границы в его AGENTS. Ядро не зависит от facade/adapters/ASP.NET Core.
- `adapters/AgentBridge.Persistence.EfCore/` — общее EF-хранилище; правила в локальном `AGENTS.md`.
- Оба адаптера ссылаются на ядро и используют общие Microsoft.Extensions options/DI. EF-адаптер подключает локальный EFCoreLibrary и SQLite/PostgreSQL/SQL Server; codex-lb адаптер подключает локальный HttpClientLibrary для каталога этапа 13 и JSON/SSE Responses этапов 14–15. Compact реализован на этапе18; прикладная граница — ContextCompactor и existing IDialogContextWriter. Read/write ports разделены; сценарные UoW используют общий scope. Generated миграции SQLite/PostgreSQL/SQL Server созданы. Явное maintenance подключение этапа 12 находится в Configuration EF-адаптера; регистрация не запускает операции, host отсутствует.
- `adapters/AgentBridge.Persistence.Migrations.Sqlite/` и `adapters/AgentBridge.Persistence.Migrations.PostgreSql/` — отдельные library target/startup проекты со своими design-time factories общего DbContext. Локальные AGENTS определяют tooling; обратных ссылок из общего адаптера нет. Генерация требует отдельного согласования точных команд этапа 11.
- `tests/` — три отдельных проекта изолированных проверок ядра и адаптеров; карта и ограничения в `tests/AGENTS.md`.
- `adapters/AgentBridge.Persistence.Migrations.SqlServer/` — отдельный library target/startup MSSQL с собственными AGENTS/factory/initial six-table schema. Основной сценарий MSSQL выбирается явно; SQLite/PostgreSQL сохранены. Server backup destination и EngineEdition2/3/4 определены existing EFCoreLibrary module, actual provider/runtime проверка отдельно в Audit Remediation17/18.
- `tests/Delivery/` — SDK closure SQL Server DLL-комплектов win-x64/linux-x64/linux-arm64, сохранённые SQLite/PostgreSQL win-x64, external compile-only consumer и PE/ELF/XML/manifest проверки; правила в `tests/Delivery/AGENTS.md`. Комплекты только в ignored artifacts, runtime проверяется отдельно.
- `Documentation/Technical documentation/25-usage-guide.md` — руководство с исходниками в `tests/Delivery/Consumer/`; примеры compile-only собираются вне всех repo с пятью current variants (SQL Server три RID и SQLite/PostgreSQL win-x64), методы не исполняются. App factories/authorization/business ports не являются API AgentBridge. Evidence хранится в плане, не в корневом README.
- Корневой compile glob исключает `adapters`, `tests`, `test`, а также все вложенные `bin`, `obj`, `artifacts`. Новые самостоятельные проекты не должны попадать в компиляцию ядра.

## Правила ядра

- Конкретные адаптеры codex-lb, HTTP и хранения размещаются в отдельных проектах и зависят от контрактов ядра.
- Не добавлять в ядро зависимости ASP.NET Core, WPF, Telegram, HttpClientLibrary, EFCoreLibrary или конкретных провайдеров БД.
- Подключающее приложение отвечает за конфигурацию, DI и жизненный цикл; ядро не запускает хост и фоновые службы самостоятельно.
- Проверять затронутые конкретные `.csproj` согласно `csharp-project-rules`, без запуска приложения и внешних интеграций. XML-документация генерируется при сборке; nullable включён во всех проектах.

## Архитектурные границы

- Ядро независимо от ASP.NET Core, WPF, Telegram, codex-lb и конкретной БД.
- Интеграция с codex-lb находится в отдельном адаптере.
- Бизнес-данные, авторизация пользователя и обработчики инструментов принадлежат подключающему приложению.
- Провайдер БД, подключение и ограничения хранения задаются подключающим приложением.
- SQLite и PostgreSQL выбираются опционально через конфигурацию; SQLite не является обязательным провайдером.
- Вся работа с БД строится на `EFCoreLibrary` из `../work/EFCoreLibrary`. В первую очередь использовать базовые read/create/update/delete репозитории; custom query применять только при недостаточности базовых операций.
- Контракты библиотек проверять по текущему исходному коду, а не только по README или старым примерам. Спорные моменты EFCoreLibrary согласовывать с пользователем; не обходить библиотеку прямым EF/SQL или собственной заменой.
- Исходящий HTTP строится на `HttpClientLibrary` из `../work/HttpClientLibrary`. Недостающие возможности развивать после согласования, без параллельного HTTP-клиента в обход библиотеки.
- Логирование через Serilog приложения и `ILogger<T>`. HttpClientLibrary 0.0.0.5 предоставляет безопасные HTTP-метаданные и opt-in JsonStructure без исходных имён/значений; raw error headers/body не логируются. Каталог/JSON/SSE Responses подключают pipeline с пределом ошибки 64 КиБ. Responses сохраняет canonical протокол и bound continuation; обязанности в Responses/AGENTS.md адаптера.
- `../TelegramCodexRelayBot` — источник наработок для выборочного переиспользования после проверки текущего API codex-lb. Telegram-зависимости и несовместимые решения не переносить.
- Объём содержимого считается в байтах на диалог; порог мягкий, исходная история удаляется только по сроку или явному удалению пользователя. Сжатие рабочего контекста не ограничивает физический размер БД.
- Срок хранения отсчитывается от создания, `ExpiresAtUtc` доступен приложению. Активность и compact срок не продлевают. Начальные значения и настройки находятся в документации, переопределяются конфигурацией.
- Сжатие запускается по числу токенов; применяется соответствующий модели tokenizer, порог задаётся конфигурацией. Циклы сжатия ограничены и не гарантируют локального подсчёта opaque-состояния.
- Приложение может безопасно читать настройки, выбирать модель и effort. Ключ пользователя имеет приоритет; общий ключ применяется только при отсутствии индивидуального, а не при его ошибке.
- Этап21 сохраняет выбор для конкретного диалога в отдельной DialogSettings с независимой версией: активный AgentRunner сохраняет свой atomic turn snapshot. Safe settings/status API, shape/compatibility границы — Application/AGENTS.md; provenance и шестая таблица — EF adapter/AGENTS.md. Срок и история не меняются от выбора, секреты не сохраняются.
- EFCoreLibrary предоставляет общий relational check/backup/migrate и optional SQLite/PostgreSQL/SQL Server/MySQL модули (этап 05 принят, проверка изолированная; запрещённые интеграции пропущены). AgentBridge выбирает SQLite/PostgreSQL/SQL Server; этап 08 подключает CRUD-библиотеку, модели и общий контекст, startup-интеграция относится к этапу 12. SingleInitializer, остановка writes/DDL/других экземпляров и retention backup принадлежат приложению; прямой SQL в обход библиотеки не добавлять.
- Поиск по старым сообщениям не входит в текущий объём проекта.
- Сжатие рабочего контекста и удаление данных по политике хранения — отдельные операции.
- ExpiredDialogCleanup22 в Application явно обрабатывает один bounded пакет existing read/deletion ports с отдельными short scopes/fresh UTC; AddAgentBridgeDialogCleanup не запускает операции. Расписание/авторизация принадлежат приложению, partial/unknown/cancel не являются успехом. Границы отчёта и проверок — Application/AGENTS.md и техническая документация22.

## Работа с файлами

- Создание минимального каркаса разрешено пользователем. Реализацию сценариев, адаптеров, migrations и следующих этапов начинать только по отдельной явной просьбе.
- Анализ и обсуждение не разрешают изменения файлов; нужна явная просьба пользователя.
- Ручные изменения делать через `apply_patch`, сохраняя кодировку и окончания строк существующих файлов.
- Для C# применять скилл `csharp-project-rules`; для ASP.NET Core-интеграции дополнительно `aspnetcore-project-rules`, для доступа к данным — `backend-uow-repositories`.
- Не запускать приложение, проектные скрипты, внешние интеграции, БД и SQL без явного разрешения. Compile-check и изолированные тесты выполнять по применимым скиллам и пользовательским ограничениям.
- Не выполнять Git-операции без явной просьбы о соответствующей операции.

## Документирование кода

- При создании проектов и архитектурных областей добавлять ближайший `AGENTS.md` с назначением, зависимостями, границами, инвариантами и проверками. Родительский файл хранит карту, дочерний — только правила своей области; generated/output-папки исключены.
- Классы, интерфейсы и методы документировать XML comments на русском языке. Для самостоятельных контрактов и типов использовать `<summary>`; параметры, результаты и значимые ошибки пояснять, когда это помогает понять контракт.
- Реализующие интерфейс классы и соответствующие методы использовать с `<inheritdoc/>`, чтобы не дублировать описания интерфейса. При неоднозначности указывать `cref`; специфичные детали реализации добавлять через `<remarks>`. Собственные методы без наследуемого контракта получают отдельный `<summary>`.
- При изменении обязанностей или устойчивых правил одновременно обновлять ближайший `AGENTS.md`. Не создавать инструкции ради количества файлов.
- Корневой `README.md` содержит очень краткое назначение и поток работы, затем более подробные инструкции подключения и использования. Примеры API должны соответствовать фактическому коду; до реализации не выдавать вымышленные сигнатуры за существующие.
