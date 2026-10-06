# AgentBridge

AgentBridge — C#-библиотека для .NET 10, подключаемая обычными DLL. Она собирает контекст, вызывает модель через codex-lb, выполняет разрешённые инструменты приложения и сохраняет диалоги через явно выбранный SQL Server, SQLite или PostgreSQL.

## Подключение

Стандартное подключение предоставляет `AgentBridge.Integration.dll`. Приложение настраивает logging и управляемый HTTP client, затем вызывает одну регистрацию:

```csharp
using AgentBridge.Integration;

builder.Services.AddAgentBridge(builder.Configuration,
    provider => provider.GetRequiredService<HttpClient>());
```

Это actual API библиотеки. `ILoggerFactory` должен быть зарегистрирован **до** вызова: ASP.NET Core builder уже предоставляет его, приложение отдельно выбирает provider/Serilog sinks и уровни. AgentBridge не создаёт logger или log-файл. Файловый путь, ротацию и срок хранения проверяет logging-регистрация приложения; console/custom sink не требует пути.

Пример предполагает заранее зарегистрированный scoped `HttpClient` приложения из его IHttpClientFactory. HTTP callback вызывается внутри scope и не выполняет I/O при создании. Приложение владеет client, handlers, timeout и освобождением; DI приложения освобождает scoped client. Нельзя добавлять скрытые retries или смену аккаунта. [Полная регистрация app HTTP-группы и lifetimes](<Documentation/Technical documentation/26-integration-registration.md>).

Базовый Shared сценарий работает без пустых key-source, context-provider и tool классов. Для Individual зарегистрируйте scoped `IIndividualModelKeySource` **до** фасада; отсутствие источника отклоняется options validation. Заданный индивидуальный ключ имеет приоритет и в Shared; ошибка источника/ключа не разрешает общий fallback.

## Конфигурация MSSQL

Передайте root либо выбранный раздел `IConfiguration`, содержащий `AgentBridge`, `CodexLb`, `Database`. Источники JSON/environment/secret store и их порядок задаёт приложение; библиотека не открывает config-файлы.

```json
{
  "AgentBridge": {
    "Agent": {
      "InstructionsSource": "Configuration",
      "Instructions": "Отвечай по разрешённым данным.",
      "MaxToolSteps": 8
    },
    "Retention": {
      "RetentionPeriod": "7.00:00:00",
      "SoftContentLimitBytes": 10485760
    },
    "Compaction": {
      "TokenThreshold": 32000,
      "InputTokenReserve": 4096,
      "MaxPasses": 3
    }
  },
  "CodexLb": {
    "KeySource": "Shared",
    "BaseAddress": "https://codex-lb.example.invalid/",
    "Model": "gpt-5",
    "ReasoningEffort": "medium",
    "GenerationTimeout": "00:03:00",
    "CompactTimeout": "00:03:00"
  },
  "Database": { "Provider": "SqlServer" }
}
```

Адрес/model/effort — примеры, их доступность подтверждает каталог. `CodexLb:SharedApiKey` и `Database:ConnectionString` передайте через конфигурацию секретов приложения. Строка MSSQL должна явно задавать ваш сервер, БД, authentication и TLS. Все операционные параметры обязательны; скрытых рабочих defaults нет. `InstructionsSource=PerRequest` требует инструкции каждого run; `KeySource=Individual` не использует общий fallback. [Настройки и breaking migration](<Documentation/Technical documentation/05-configuration-and-lifecycle.md>).

## Использование и границы

После отдельной подготовки БД приложение авторизует owner/agent, создаёт диалог и вызывает `AgentRunner.RunAsync` в scoped boundary. Обычная registration не выполняет migrations, maintenance, compact, HTTP или cleanup. Расписание и размер пакета `ExpiredDialogCleanup`, business tools/providers, authentication и endpoints принадлежат приложению. Срок отсчитывается от создания диалога; активность/compact его не продлевают. Мягкий порог байтов не удаляет историю.

[Руководство с consumer исходниками и HTTP/admin примерами](<Documentation/Technical documentation/25-usage-guide.md>) · [SQL Server и maintenance](<Documentation/Technical documentation/12-sql-server-provider.md>) · [Техническая документация](<Documentation/Technical documentation/README.md>) · [Бизнес-логика](<Documentation/Business logic/README.md>).

Фасад проверяется в локальной DI/options границе с fake HTTP без БД/hosting. Новые MSSQL DLL kits для win-x64/linux-x64/linux-arm64 и их external binary compilation относятся к этапам15/16; provider/runtime/live — к17–19. Старые SQLite/PostgreSQL kits не содержат новую Integration DLL. Linux ARM64 означает приложение-клиент, а не локальный SQL Server Engine.

[План исправлений и evidence](<Documentation/Plans/AgentBridge Audit Remediation/README.md>) · [Текущая спецификация](openspec/specs/agent-runtime/spec.md) · [Решение](agent-bridge.slnx)
