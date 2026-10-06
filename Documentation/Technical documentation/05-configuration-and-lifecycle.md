# Конфигурация, жизненный цикл и проверка

SQL Server добавлен Audit Remediation12 как явный Database.Provider=SqlServer без default/fallback. Backup.SqlServerBackupDirectory задаёт серверный absolute path, Backup.BackupRetentionPeriod — положительный app-owned retention; host BackupDirectory не требуется для MSSQL. [Текущий provider API](12-sql-server-provider.md) не заменяет предстоящую строгую IConfiguration конфигурацию13 и отдельную facade14.

## Конфигурация

Привязка настроек выполняется в composition root подключающего приложения через групповые DI-расширения. Прикладные сценарии получают типизированные options, а не `IConfiguration`.

На этапе 02 реализованы следующие публичные группы. Имена свойств одновременно служат ключами binding; значения по умолчанию переопределяются приложением.

| Пространство имён / тип | Свойства и пробные значения |
| --- | --- |
| `AgentBridge.Configuration.AgentOptions` | `Instructions` (необязательно), `MaxToolSteps = 8` |
| `AgentBridge.Configuration.DialogRetentionOptions` | `RetentionPeriod = 7 дней`, `SoftContentLimitBytes = 10 485 760` |
| `AgentBridge.Configuration.ContextCompactionOptions` | `TokenThreshold = 32 000`, `InputTokenReserve = 4096`, `MaxPasses = 3` |
| `AgentBridge.CodexLb.Configuration.CodexLbOptions` | Обязательные `BaseAddress`, `Model`; `ReasoningEffort = "medium"`, необязательный секрет `SharedApiKey`, `GenerationTimeout` и `CompactTimeout` по 180 секунд |
| `AgentBridge.Persistence.EfCore.Configuration.DatabaseOptions` | Обязательные `Provider` и секрет `ConnectionString`; провайдер не задан по умолчанию |
| `AgentBridge.Persistence.EfCore.Configuration.DatabaseBackupOptions` | Обязательные абсолютный каталог и явный положительный `BackupRetentionPeriod` без default; PostgreSQL дополнительно требует dump path, major 10+ и конечный cleanup timeout |

`DatabaseProvider` содержит `SQLite` и `PostgreSql`; nullable-свойство отличает отсутствие выбора от неизвестного числового значения. Это конфигурационный контракт, не регистрация готового EF-провайдера.

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
    agent => agent.MaxToolSteps = 5,
    retention => retention.RetentionPeriod = TimeSpan.FromDays(14),
    compaction => compaction.TokenThreshold = 24_000);
```

Ни файл appsettings, ни ASP.NET Core, ни host не обязательны. `IConfiguration` не регистрируется расширениями как зависимость сценариев. Обработчики инструментов и источники контекста регистрируются программно на последующих этапах, а не именами в options.

### Валидация и применение

Options проверяются при получении `Value`/`CurrentValue`, включая новые scope и reload. Зарегистрирован стандартный `IStartupValidator` через `ValidateOnStart`; приложение без host может явно вызвать `serviceProvider.GetRequiredService<IStartupValidator>().Validate()` сразу после построения своего контейнера. Одна регистрация `IServiceCollection` или `BuildServiceProvider` сами по себе не запускают проверку значений.

Локальные ошибки дают `OptionsValidationException` с именами полей и причиной без значений. Ошибки формата binding (например, неизвестное имя enum или неверный TimeSpan) дают явный `InvalidOperationException` от Microsoft.Extensions. Обязательны модель, адрес, провайдер и непустая строка подключения. Инструкции и общий ключ необязательны: инструкции может сформировать приложение на обращение, а индивидуальные ключи предоставляются отдельно. Уже заданный пустой/пробельный общий ключ считается ошибкой.

Период, мягкий порог байтов, порог токенов и числа шагов/проходов должны быть положительными; запас токенов допускает ноль. Сумма порога и запаса проверяется на переполнение локального `int`, а не на бюджет модели. Deadline положительный и не превышает `4 294 967 294` миллисекунды (диапазон таймера .NET). Адрес — абсолютный HTTP(S), без userinfo, query и fragment; путь префикса разрешён.

Непустые модель и effort сохраняются без подмены. Wire-контракт codex-lb принимает effort как строку; этап 13 проверяет точный model/effort и threshold+reserve по input_context_window текущего каталога выбранного ключа. [Фактический API](13-model-catalog-and-keys.md). Успешная локальная проверка не обещает доступности модели или compact.

`IOptions<T>` фиксирует полученное значение; `IOptionsSnapshot<T>` фиксирует его на scope; `IOptionsMonitor<T>` получает обновления от источника конфигурации. Вызовы `Configure<T>` после binding позволяют приложению добавить override. ModelSettingsSnapshot этапа 13 безопасно фиксирует проверенную модель/effort/threshold/reserve; управление выбором, остальные settings/status и фиксация всего обращения остаются последующим этапам. Сами options с ключом или строкой подключения нельзя сериализовать для UI или логирования.

`ContextCompactionOptions` содержит порог в токенах, запас бюджета и максимальное число проходов на обращение. Объём в `DialogRetentionOptions` измеряется на диалог в байтах содержимого; его порог мягкий. Провайдер и подключение задаются через `DatabaseOptions`.

`DialogRetentionOptions.CalculateExpiresAtUtc(DateTimeOffset createdAtUtc)` уже вычисляет время создания плюс настроенный период, включая нестандартные 14 дней или 36 часов. Требуется нулевое UTC-смещение; неправильный UTC, неположительный период и переполнение даты дают явные ошибки. Метод не создаёт сущность, не меняет ранее вычисленную дату и не обращается к хранилищу.

На этапе 06 `Dialog.Create` уже фиксирует `CreatedAtUtc` и вычисленный из конфигурации `ExpiresAtUtc` в доменной сущности; сохранение через EFCoreLibrary/scenario UoW реализовано этапами08–10, actual SQLite/PostgreSQL evidence см. в [карте00–25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>). Начальный срок — 7 дней; активность и compact его не продлевают. UTC используется для хранения и сравнения, отображение в часовом поясе пользователя выполняет приложение. [Публичный доменный API и границы снимка версии](08-dialog-domain-state.md).

Чтение безопасных настроек, выбор модели/effort, ключи и Serilog описаны в [отдельном разделе](07-tokenizer-and-settings.md).

Изменение параметров через конфигурацию не требует правки и пересборки бизнес-логики. Настройки проверяются до соответствующей операции; некорректные лимиты, адрес или отсутствующая обязательная зависимость приводят к явной ошибке.

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
