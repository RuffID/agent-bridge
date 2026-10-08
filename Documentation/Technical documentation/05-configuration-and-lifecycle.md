# Конфигурация, жизненный цикл и проверка

SQL Server добавлен Audit Remediation12 как явный Database.Provider=SqlServer без default/fallback. Backup.SqlServerBackupDirectory задаёт серверный absolute path, Backup.BackupRetentionPeriod — положительный app-owned retention; host BackupDirectory не требуется для MSSQL. Строгая configuration boundary Audit Remediation13 описана ниже; [отдельная facade14](26-integration-registration.md) предоставляет стандартную composition. [Provider API](12-sql-server-provider.md).

## Конфигурация

Привязка настроек выполняется в composition root подключающего приложения через групповые DI-расширения. Прикладные сценарии получают типизированные options, а не `IConfiguration`.

**Breaking behavior13:** обязательные параметры больше не получают рабочие defaults. Типы и сигнатуры прежних свойств сохранены; добавлены nullable enum InstructionsSource и KeySource. Пустые callbacks и частичная configuration теперь отклоняются. Для миграции явно перенесите нужную политику: например, 8 шагов, 7 дней/10 MiB, 32000/4096/3, medium/180 секунд, и выберите оба source mode. Явное значение, равное прежнему default, допустимо; пропуск поля — ошибка. Initial zero и reserve=-1 не являются рабочими настройками.

Все значения в таблице принадлежат приложению. Пути относительны выбранному library root; адаптеры получают непосредственно CodexLb/Database/Backup section. Library owner проверки — соответствующая options registration. Безопасная ошибка содержит путь и причину без значения: missing/blank configuration — required, malformed/overflow Binder — invalid_type, неверный programmatic scalar/диапазон ядра — required_or_range. Секции и ключи проверяются независимо от ранее зарегистрированного Configure; valid configuration затем допускает Configure/PostConfigure overrides.

| Путь | Тип / диапазон | Обязательность / безопасная причина |
| --- | --- | --- |
| Agent.InstructionsSource | AgentInstructionsSource?: Configuration / PerRequest | Всегда; required / required_or_invalid |
| Agent.Instructions | Непустая строка | Configuration: обязательно; PerRequest: обязательны instructions обращения без options fallback; required |
| Agent.MaxToolSteps | int > 0 | Всегда; required / required_or_range |
| Retention.RetentionPeriod | TimeSpan?; null либо > 0 | Опционален; отсутствие/null — бессрочно; период применяется ко всем CreatedAtUtc |
| Retention.SoftContentLimitBytes | long > 0, байты | Всегда; required / required_or_range |
| Compaction.TokenThreshold | int > 0, токены | Всегда; required / required_or_range |
| Compaction.InputTokenReserve | int >= 0, токены | Всегда, включая explicit zero; required / required_or_range |
| Compaction.MaxPasses | int > 0 | Всегда; required / required_or_range |
| Compaction.TokenThreshold + InputTokenReserve | Сумма <= int.MaxValue | Всегда; overflow; input budget проверяется каталогом отдельно |
| CodexLb.BaseAddress | Абсолютная HTTP(S) строка без userinfo/query/fragment | Всегда; required / ошибка формы адреса |
| CodexLb.Model | Непустой exact ID | Всегда; required; доступность локально не подтверждается |
| CodexLb.ReasoningEffort | Непустая exact строка | Всегда; required / ошибка пустого effort; статического списка capabilities нет |
| CodexLb.KeySource | ModelKeySourceMode?: Shared / Individual | Всегда; required / required_or_invalid |
| CodexLb.SharedApiKey | Secret, непустая строка без whitespace/control chars | Shared: обязательно; Individual: необязательно, не fallback; required / invalid_key |
| CodexLb.GenerationTimeout | TimeSpan > 0, <=4294967294 ms | Всегда; required / ошибка диапазона таймера |
| CodexLb.CompactTimeout | TimeSpan > 0, <=4294967294 ms | Всегда; required / ошибка диапазона таймера |
| Database.Provider | DatabaseProvider?: SQLite / PostgreSql / SqlServer | Всегда; required / ошибка неизвестного провайдера |
| Database.ConnectionString | Secret, непустая строка с синтаксисом DbConnectionStringBuilder | Всегда; required / invalid_format; provider keys/server/auth/TLS этой границей не проверяются |
| Backup.BackupDirectory | Абсолютный локальный путь | Включённое SQLite/PostgreSQL maintenance; ошибка абсолютного пути |
| Backup.SqlServerBackupDirectory | Абсолютный серверный Unix/Windows/UNC путь | Включённое SQL Server maintenance; ошибка серверного пути |
| Backup.BackupRetentionPeriod | TimeSpan > 0 | Любое включённое maintenance; ошибка required/положительного retention |
| Backup.PostgreSqlDumpExecutablePath | Абсолютный локальный путь | Включённое PostgreSQL maintenance; ошибка абсолютного пути |
| Backup.PostgreSqlServerMajorVersion | int >= 10 | Включённое PostgreSQL maintenance; ошибка required/major диапазона |
| Backup.PostgreSqlCleanupTimeout | TimeSpan > 0, <=4294967294 ms | Включённое PostgreSQL maintenance; ошибка required/диапазона таймера |

Без вызова AddAgentBridgeDatabaseMaintenance Backup не требуется. При включении SingleInitializer задаётся явно; приложение применяет backup retention само. Наличие unused backup поля не меняет выбранного провайдера; malformed переданный scalar отклоняется стандартным binding. Валидация не открывает соединение или пути.

Этап 12 отдельно подключает `AddAgentBridgeDatabaseMaintenance` после `AddDatabaseConfiguration`/`AddAgentBridgePersistence`, с явным SingleInitializer. Backup options проверяются локально без I/O; фактические maintenance методы вызываются приложением в отдельном scope после остановки writes/DDL/других экземпляров. Retention backup исполняет приложение. [Сигнатуры, binding и ошибки](06-database-maintenance.md#подключение-agentbridge-этапа-12).

Фактические расширения composition root:

```csharp
using AgentBridge.Configuration;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// configuration и services принадлежат приложению.
services.AddAgentBridgeConfiguration(configuration.GetSection("AgentBridge"));
services.AddCodexLbConfiguration(configuration.GetSection("AgentBridge:CodexLb"));
services.AddDatabaseConfiguration(configuration.GetSection("AgentBridge:Database"));
```

Первое расширение привязывает подразделы `Agent`, `Retention`, `Compaction`; два других получают непосредственно раздел своих настроек. Все расширения имеют программные перегрузки с `Action<TOptions>`. В ядре обязательный callback для `AgentOptions` и необязательные callbacks для хранения и compact; например:

```csharp
services.AddAgentBridgeConfiguration(
    agent => { agent.InstructionsSource = AgentInstructionsSource.PerRequest; agent.MaxToolSteps = 5; },
    retention => { retention.RetentionPeriod = TimeSpan.FromDays(14); retention.SoftContentLimitBytes = 10_485_760; },
    compaction => { compaction.TokenThreshold = 24_000; compaction.InputTokenReserve = 0; compaction.MaxPasses = 3; });
```

Ни файл appsettings, ни ASP.NET Core, ни host не обязательны. `IConfiguration` не регистрируется расширениями как зависимость сценариев. Обработчики инструментов и источники контекста регистрируются программно на последующих этапах, а не именами в options.

### Валидация и применение

Исходник полной programmatic регистрации: [StrictConfigurationRegistration.cs](../../tests/Delivery/Consumer/StrictConfigurationRegistration.cs). Он компилируется как linked source в isolated persistence tests без исполнения; внешние binary kits и runtime относятся к15/16/18.

Options проверяются при получении `Value`/`CurrentValue`, включая новые scope и reload. Зарегистрирован стандартный `IStartupValidator` через `ValidateOnStart`; приложение без host может явно вызвать `serviceProvider.GetRequiredService<IStartupValidator>().Validate()` сразу после построения своего контейнера. Одна регистрация `IServiceCollection` или `BuildServiceProvider` сами по себе не запускают проверку значений.

Локальные ошибки, включая malformed enum/TimeSpan/overflow Binder, дают OptionsValidationException без исходного значения и unsafe inner exception. IStartupValidator объединяет несколько отказов в AggregateException. SafeOptionsBindingExtensions ограждает стандартный ConfigurationBinder внутри Configure и регистрирует стандартный ConfigurationChangeTokenSource; собственного loader/options store нет. Приложение передаёт merged root, subsection либо отдельный IConfiguration и само выбирает environment/secret providers и их приоритет. Библиотека не открывает config файлы и не передаёт IConfiguration runtime-сервисам.

Shared требует общий ключ, но сохраняет приоритет provided индивидуального ключа; только null индивидуального источника разрешает общий. Individual допускает отсутствие общего ключа, null источника даёт Unauthorized без fallback. Ошибка/пустой индивидуальный ключ/HTTP отказ не переключают account. Источник приложения регистрируется в обоих режимах (для shared-only он явно возвращает null). Configuration instructions допускают request override; PerRequest требует непустые request instructions до первого I/O, без options/string.Empty fallback. Ручной Options.Create без registration не является строгим configuration API.

Период, мягкий порог байтов, порог токенов и числа шагов/проходов должны быть положительными; запас токенов допускает ноль. Сумма порога и запаса проверяется на переполнение локального `int`, а не на бюджет модели. Deadline положительный и не превышает `4 294 967 294` миллисекунды (диапазон таймера .NET). Адрес — абсолютный HTTP(S), без userinfo, query и fragment; путь префикса разрешён.

Непустые модель и effort сохраняются без подмены. Wire-контракт codex-lb принимает effort как строку; этап 13 проверяет точный model/effort и threshold+reserve по input_context_window текущего каталога выбранного ключа. [Фактический API](13-model-catalog-and-keys.md). Успешная локальная проверка не обещает доступности модели или compact.

`IOptions<T>` фиксирует полученное значение; `IOptionsSnapshot<T>` фиксирует его на scope; `IOptionsMonitor<T>` получает обновления от источника конфигурации. Вызовы `Configure<T>` после binding позволяют приложению добавить override. ModelSettingsSnapshot этапа 13 безопасно фиксирует проверенную модель/effort/threshold/reserve; управление выбором, остальные settings/status и фиксация всего обращения остаются последующим этапам. Сами options с ключом или строкой подключения нельзя сериализовать для UI или логирования.

`ContextCompactionOptions` содержит порог в токенах, запас бюджета и максимальное число проходов на обращение. Объём в `DialogRetentionOptions` измеряется на диалог в байтах содержимого; его порог мягкий. Провайдер и подключение задаются через `DatabaseOptions`.

`DialogRetentionOptions.CalculateExpiresAtUtc(DateTimeOffset createdAtUtc)` возвращает nullable срок: null для бессрочного хранения, иначе создание плюс положительный период. Неверный UTC, неположительный заданный период и переполнение дают ошибки. Scoped DialogRetentionPolicy фиксирует текущие options и применяется reader/guards ко всем ранее созданным строкам без их переписывания. Регистрация не выполняет I/O.

`Dialog.Create` фиксирует `CreatedAtUtc` и nullable `ExpiresAtUtc` в доменной сущности; при чтении существующего диалога срок вычисляется по политике текущего scope. Сохраняемое обязательное поле expiry остаётся метаданной создания; новые бессрочные строки используют DateTimeOffset.MaxValue, миграция схемы не требуется. Активность и compact не продлевают срок. UTC используется для хранения и сравнения, отображение в часовом поясе пользователя выполняет приложение. [Публичный доменный API](08-dialog-domain-state.md).

Чтение безопасных настроек, выбор модели/effort, ключи и Serilog описаны в [отдельном разделе](07-tokenizer-and-settings.md).

Изменение параметров через конфигурацию не требует правки и пересборки бизнес-логики. Настройки проверяются до соответствующей операции; некорректные лимиты, адрес или отсутствующая обязательная зависимость приводят к явной ошибке.

## Настройки расписания и логирования приложения

Cleanup schedule, bounded batch и logger принадлежат приложению и читаются им из его IConfiguration. Это не поля AgentBridge options; приложение выбирает их имена/пути и валидатор. Например, собственные `Cleanup.Enabled`, `Cleanup.Schedule`, `Cleanup.BatchLimit` и `Logging.Mode`, `Logging.FilePath`, `Logging.Rotation`, `Logging.Retention` могут иметь следующий контракт:

| Пример app path / owner | Тип / обязательность выбранного app режима | Безопасная ошибка app |
| --- | --- | --- |
| Cleanup.Enabled / приложение | bool, явный выбор при настройке scheduler | Только path/код режима |
| Cleanup.Schedule / приложение | Непустое допустимое расписание, только Enabled=true | Только path/код required/invalid_schedule |
| Cleanup.BatchLimit / приложение | Положительный bounded int в пределах собственного лимита приложения, только Enabled=true | Только path/код required/range |
| Logging.Mode / приложение | Выбор Serilog/ILogger provider/sinks | Только path/код invalid_mode |
| Logging.FilePath / приложение | Явный путь только для файлового sink | Только path/код required/invalid_path |
| Logging.Rotation, Logging.Retention / приложение | Положительные app policy параметры, если нужны выбранному файловому sink | Только path/код required/range |

Эти имена — пример configuration приложения, а не новый API библиотеки. При отключённом cleanup schedule/batch не обязательны; console/custom ILogger не требует file path. При обязательном файловом режиме приложение отклоняет missing path в своей регистрации logging до работы. AgentBridge получает ILogger приложения без собственного logger/file writer/sinks. Валидация13 не запускает file logger и не открывает файл.

Приложение само вызывает ExpiredDialogCleanup.CleanupAsync в short scope с bounded batch по своему расписанию; AddAgentBridgeDialogCleanup не запускает scheduler/host/background job. Заданный RetentionPeriod вычисляет expiry диалога от создания; null отключает автоматическую очистку. Compaction limits задают рабочий token budget; они не определяют расписание, backup retention или физический размер БД.

## Жизненный цикл данных

Фактическая реализация коротких write ports и общего scoped gate описана в [сценарных Unit of Work](10-scenario-unit-of-work.md). Этап 10 реализован и принят; запрещённые проверки пропущены. Cleanup tracker завершается до возврата из начатой write transaction; неизвестный begin/commit/cleanup требует нового DI scope.

Один EF DbContext не используется одновременно несколькими задачами. Короткие операции чтения и сохранения выполняются в управляемых scope. Сетевой ответ не ожидается внутри открытой DB-транзакции.

При сохранении ответа или результата compact проверяется, что диалог всё ещё существует и его состояние не было очищено. Удаление не должно приводить к восстановлению истории поздним результатом старого обращения.

Связь с конкретной версией диалога и покрываемым диапазоном истории нужна для последовательного применения compact и политики хранения. Сохранение нового состояния и выбор его активным должны согласовываться в одной транзакционной границе.

Физическое удаление выполняется базовыми delete-репозиториями EFCoreLibrary через сценарный UoW. Работа с зависимыми данными учитывает FK и согласованное изменение активного контекста.

Startup check/backup/migrate и очистка доступны приложению как операции библиотеки. Приложение владеет моментом запуска и расписанием. Детали: [обслуживание БД](06-database-maintenance.md).

## Жизненный цикл HTTP

HTTP-клиент и его handlers управляются приложением и DI. Не создаётся новый независимый HttpClient для каждого сообщения.

Отмена приложения или вызывающего кода передаётся библиотеке. Deadline одной операции не превращается в бесконечное ожидание ответа или завершения stream. Полученные ресурсы закрываются после успеха, ошибки или отмены.

Сетевой failure после dispatch не означает, что upstream не выполнил запрос. AgentBridge не дублирует потенциально изменяющий инструмент или модельный turn автоматически.

## Проверка будущей реализации

После начала реализации проверяются .NET 10 compile-check и изолированные тесты. Для HTTP используются локальные заглушки; для протокола — записанные или синтетические JSON/SSE fixtures.

Значимые сценарии проверки:

- Сохранение function call/output и непрозрачного состояния compact.
- EOF SSE без terminal completion и явный terminal error.
- Caller cancellation отдельно от локального deadline.
- Порог токенов с учётом всего подготовленного контекста.
- Разделение сжатия и удаления истории.
- Сохранение результата, одновременно с которым диалог был удалён или очищен.
- Изоляция диалогов разных пользователей.

Проверки реальных SQLite/PostgreSQL и живого codex-lb требуют отдельного разрешения. На этапе документации сборка, приложения, тесты и подключение к БД не выполнялись.
