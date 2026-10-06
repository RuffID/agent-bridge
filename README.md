# AgentBridge

AgentBridge — C#-библиотека для приложений на **.NET 10**. Она ведёт диалог с моделью через **codex-lb**, сохраняет историю в БД, собирает и сжимает рабочий контекст, а при необходимости вызывает инструменты вашего приложения.

Библиотека подключается обычными DLL. Основной провайдер хранения — **SQL Server**; также доступны **SQLite** и **PostgreSQL**. Её можно использовать в ASP.NET Core, настольном приложении или другом SDK-style проекте: интерфейс пользователя и запуск приложения остаются у вас.

Обычный путь работы: **подключить DLL → настроить зависимости → подготовить БД → создать диалог → отправлять сообщения**.

## Что понадобится

- .NET 10 SDK для сборки и подходящий .NET 10 runtime на машине приложения.
- Полный комплект DLL для выбранной БД, операционной системы и архитектуры.
- Адрес работающего codex-lb, ключ доступа и доступная этому ключу модель.
- Собственная БД и строка подключения к ней.
- Настроенные конфигурация, логирование и DI-контейнер приложения.

Для первого подключения достаточно общего ключа и обычного текстового диалога. Свои инструменты и источники дополнительного контекста можно добавить позже.

## 1. Подключите DLL к проекту

Выберите комплект, соответствующий **машине приложения**, и скопируйте его целиком, например в `vendor/AgentBridge/SqlServer/win-x64`.

| База данных | Подготовленные варианты |
| --- | --- |
| SQL Server | `win-x64`, `linux-x64`, `linux-arm64` |
| SQLite | `win-x64` |
| PostgreSQL | `win-x64` |

В текущем локальном checkout комплекты находятся в `artifacts/delivery/stage16/{Provider}/{RID}`, где `Provider` — `SqlServer`, `Sqlite` или `PostgreSql`. Это игнорируемые сборочные артефакты: при клонировании репозитория они не появятся. Порядок подготовки и состав комплектов описаны в [руководстве по поставке DLL](<Documentation/Technical documentation/24-dll-delivery.md>).

Добавьте в `.csproj` приложения свойства и импорт комплекта. Пример для Windows x64 и SQL Server:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <AgentBridgeDeliveryRoot>$(MSBuildProjectDirectory)/vendor/AgentBridge/SqlServer/win-x64</AgentBridgeDeliveryRoot>
</PropertyGroup>
<Import Project="$(AgentBridgeDeliveryRoot)/AgentBridge.Delivery.props" />
```

Для существующего WPF-проекта сохраните его `net10.0-windows`. Для Linux замените RID и путь на соответствующий вариант. `RuntimeIdentifier` должен совпадать с комплектом.

Импорт подключает DLL и копирует сопутствующие XML-документы, ресурсы и нативные библиотеки. **Не копируйте только `AgentBridge.dll` и не смешивайте файлы разных комплектов.** Бинарное подключение не требует публикации NuGet-пакета; совместимость зависимостей с уже установленными пакетами приложения проверяется отдельно.

Linux ARM64 — вариант клиентского приложения, подключающегося к серверу БД. Запуск SQL Server Engine на ARM64 в этот объём не входит.

## 2. Задайте настройки

AgentBridge получает готовый `IConfiguration` вашего приложения. Он должен содержать разделы `AgentBridge`, `CodexLb` и `Database`. Приложение само выбирает источники: JSON, переменные окружения или хранилище секретов.

Пример `appsettings.json` для SQL Server и общего ключа:

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

Замените условный адрес на свой codex-lb и выберите модель и effort из его каталога. Значения лимитов в примере — отправная точка для настройки вашего приложения.

Секреты передайте отдельно через те же источники конфигурации:

| Ключ конфигурации | Значение |
| --- | --- |
| `CodexLb:SharedApiKey` | Общий ключ доступа к codex-lb |
| `Database:ConnectionString` | Строка подключения с вашим сервером, БД, способом аутентификации и TLS |

При использовании стандартного провайдера переменных окружения .NET им соответствуют `CodexLb__SharedApiKey` и `Database__ConnectionString`. Не сохраняйте реальные ключи и пароли в репозитории.

| Настройка | За что отвечает |
| --- | --- |
| `Instructions` | Инструкции поведения агента |
| `MaxToolSteps` | Предел шагов модели с вызовами инструментов |
| `RetentionPeriod` | Срок доступности диалога от момента создания; в примере — 7 дней |
| `SoftContentLimitBytes` | Порог предупреждения об объёме содержимого; в примере — 10 МиБ |
| `TokenThreshold` | Порог запуска сжатия рабочего контекста |
| `InputTokenReserve` | Запас токенов для следующего ввода |
| `MaxPasses` | Максимальное число проходов сжатия |
| `GenerationTimeout` / `CompactTimeout` | Бюджеты времени генерации и сжатия |

**Обязательные настройки задаются явно.** Пропуск или неверное значение вызывает ошибку валидации, а не подстановку рабочего значения по умолчанию.

В режиме `InstructionsSource=PerRequest` инструкции передаются с каждым обращением. В режиме `KeySource=Individual` приложение заранее регистрирует `IIndividualModelKeySource`. Индивидуальный ключ имеет приоритет и в Shared; ошибка этого ключа не приводит к повтору с общим ключом. [Все настройки и правила перехода](<Documentation/Technical documentation/05-configuration-and-lifecycle.md>).

## 3. Зарегистрируйте библиотеку

Общая регистрация находится в `AgentBridge.Integration.dll`. Сначала настройте логирование и HTTP-клиент приложения, затем вызовите `AddAgentBridge`.

Для ASP.NET Core добавьте в приложение класс `ApplicationAgentHttpExtensions.cs`:

```csharp
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Настраивает принадлежащий приложению HTTP-клиент для AgentBridge.</summary>
public static class ApplicationAgentHttpExtensions
{
    /// <summary>Подключает фабрику обработчиков и освобождаемый вместе со scope клиент.</summary>
    public static IServiceCollection AddApplicationAgentHttp(this IServiceCollection services)
    {
        services.AddHttpClient("AgentBridgeCodexLb", client =>
            client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddScoped<HttpClient>(provider =>
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("AgentBridgeCodexLb"));
        return services;
    }
}
```

В `Program.cs`, до построения приложения:

```csharp
using System.Net.Http;
using AgentBridge.Integration;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddApplicationAgentHttp();
builder.Services.AddAgentBridge(builder.Configuration,
    provider => provider.GetRequiredService<HttpClient>());
```

`builder` здесь — ваш `WebApplicationBuilder`; он уже предоставляет `ILoggerFactory`. Приложение выбирает Serilog или другой провайдер логирования, уровни, место хранения и ротацию файлов. Для приложения без ASP.NET Core сначала зарегистрируйте logging через `services.AddLogging(...)` или свою фабрику, затем передайте фасаду свой `IConfiguration` и HTTP-фабрику.

В примере DI освобождает `HttpClient` при завершении scope, а `IHttpClientFactory` управляет обработчиками. Время операций ограничивается настройками AgentBridge; поэтому собственный общий таймаут клиента отключён. Не добавляйте автоматические повторы запросов, способные повторить действие инструмента.

Регистрация подключает хранение, транспорт, работу с контекстом и прикладные сценарии. Она **не обращается к БД или codex-lb и не запускает миграции**. Подробности владения зависимостями — в [руководстве регистрации](<Documentation/Technical documentation/26-integration-registration.md>).

## 4. Подготовьте базу данных

До первого диалога приложение должно явно подготовить схему выбранной БД. Комплект содержит соответствующую сборку миграций, но вызов `AddAgentBridge` их не применяет.

Для этого предусмотрена отдельная регистрация `AddAgentBridgeDatabaseMaintenance` и явные операции инициализации или обновления. Первую установку и обновление существующей БД приложение выбирает самостоятельно. На время обслуживания оно останавливает записи и другие экземпляры, а для обновления задаёт параметры резервного копирования.

[Подготовка SQL Server и ограничения](<Documentation/Technical documentation/12-sql-server-provider.md>) · [Пример регистрации SQL Server](tests/Delivery/Consumer/SqlServerRegistration.cs) · [Порядок обслуживания БД](<Documentation/Technical documentation/06-database-maintenance.md>).

## 5. Создайте диалог и отправьте сообщение

После подготовки БД используйте два сценария:

1. **Создание диалога:** `IDialogCreator.CreateAsync` получает новый `DialogId`, идентификатор авторизованного владельца и даты создания/истечения срока. Дату истечения вычисляет `DialogRetentionOptions.CalculateExpiresAtUtc`.
2. **Отправка сообщения:** `AgentRunner.RunAsync` получает `AgentRunRequest` с диалогом, владельцем, агентом, `TurnId` и новым содержимым.

Следующий фрагмент помещается внутри асинхронного метода вашего приложения. `root` — его DI-контейнер; `authorizedOwner`, `agentId` и право на вызов проверены приложением. `dialogId` создаётся один раз для нового диалога, а `turnId` — один раз для этой логической отправки; приложение сохраняет оба ID. `ct` — токен отмены операции.

```csharp
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Новый диалог создаётся в коротком scope, до обращения к модели.
await using (AsyncServiceScope createScope = root.CreateAsyncScope())
{
    IServiceProvider services = createScope.ServiceProvider;
    DateTimeOffset created = services.GetRequiredService<TimeProvider>().GetUtcNow();
    DialogRetentionOptions retention = services
        .GetRequiredService<IOptionsSnapshot<DialogRetentionOptions>>().Value;
    ServiceResult<DialogWriteToken> createdDialog = await services.GetRequiredService<IDialogCreator>()
        .CreateAsync(dialogId, authorizedOwner, created,
            retention.CalculateExpiresAtUtc(created), ct);
    if (!createdDialog.Success)
    {
        // Передайте безопасную ошибку своему вызывающему коду.
        // Отправка сообщения при отказе создания не продолжается.
        throw new InvalidOperationException($"Диалог не создан: {createdDialog.Error!.Type}");
    }
}

CanonicalModelItem input = new(JsonSerializer.SerializeToElement(new
{
    type = "message",
    role = "user",
    content = new[] { new { type = "input_text", text = "Привет!" } }
}));
ApplicationCallContext call = new(dialogId, authorizedOwner, turnId, agentId);
AgentRunRequest request = new(call, [input], [],
    new ToolExecutionLimits(1, 1, 1, TimeSpan.FromMinutes(1)));

await using AsyncServiceScope runScope = root.CreateAsyncScope();
AgentRunResult result = await runScope.ServiceProvider.GetRequiredService<AgentRunner>()
    .RunAsync(request, cancellationToken: ct);
bool completed = result.Status == AgentRunStatus.Completed && result.TerminalSaved;
// Возвращайте результат вызывающему коду с учётом completed и result.Error.
```

В этом примере инструменты не выбраны: `selectedToolNames` передан как `[]`. Для следующего сообщения пропустите создание диалога и выполните новый запрос с тем же `dialogId`. Типы идентификаторов — `DialogId` и `DialogOwnerId` из `AgentBridge.Domain.Dialogs`; используйте их `From(...)` для значений приложения.

Полные методы создания и вызовов показаны в [UsageFlow.cs](tests/Delivery/Consumer/UsageFlow.cs): `CreateAsync` и `RunAsync`. В его `RunAsync` дополнительно выбран демонстрационный инструмент `AccountSummaryTool`. [Пошаговое руководство и пример HTTP-маршрутов](<Documentation/Technical documentation/25-usage-guide.md>).

Приложение проверяет право пользователя на диалог и выбранного агента **до вызова**. Идентификатор владельца берётся из проверенной учётной записи. Сам `ApplicationCallContext` не доказывает авторизацию.

Для каждого нового сообщения создавайте новый `TurnId` и сохраняйте его для этой отправки. При повторной доставке сохраняйте прежний ID: существующее обращение не исполняется заново. Все сообщения одного диалога используют прежний `DialogId`; историю повторно в input передавать не нужно.

Ответ считается завершённым, когда `AgentRunResult.Status == AgentRunStatus.Completed` и `TerminalSaved == true`. Проверяйте также `Error`; наличие текста в `LastResponse` само по себе не подтверждает сохранение. Для потокового ответа передайте callback в `RunAsync`: промежуточный текст ещё не означает успешное завершение.

## Возможности вашего приложения

| Задача | Подключение |
| --- | --- |
| Свои инструменты | `AddAgentBridgeTool<THandler,TValidator>`; приложение реализует обработчик, проверку аргументов и прав |
| Дополнительный контекст | Реализации `IContextProvider` |
| Ключи отдельных пользователей | `IIndividualModelKeySource`, зарегистрированный до `AddAgentBridge` |
| Выбор модели и effort, чтение статуса | `AgentSettingsService` |
| Удаление истёкших диалогов | Явный вызов `ExpiredDialogCleanup.CleanupAsync` по расписанию приложения |

Логирование, авторизация, бизнес-данные, маршруты API и расписание принадлежат приложению. AgentBridge не создаёт свой хост или фоновую службу.

Срок диалога отсчитывается от создания: сообщения, смена модели и сжатие его не продлевают. Физическое удаление зависит от вызовов очистки. Сжатие сокращает рабочий контекст модели, **сохраняя исходную историю**; мягкий порог объёма также её не удаляет.

## Текущее состояние проверок

Можно начинать подключение в своём проекте. Проверены изолированные сценарии и сборка бинарных потребителей с пятью комплектами. На Windows также проверены DI, загрузка DLL и нативных библиотек, а также локальная токенизация без HTTP и БД.

Реальные проверки SQL Server, Linux runtime, live codex-lb и восстановления после аварии остаются открытыми. Сборка Linux-комплекта на Windows не подтверждает работу на Linux. Перед рабочим использованием проверьте свой состав зависимостей и реальные интеграции. Точные результаты и ограничения находятся в [итоге плана исправлений](<Documentation/Plans/AgentBridge Audit Remediation/20-final-acceptance.md>).

## Документация

- [Подключение и использование: полный маршрут с примерами](<Documentation/Technical documentation/25-usage-guide.md>)
- [Регистрация, HTTP-клиент и времена жизни зависимостей](<Documentation/Technical documentation/26-integration-registration.md>)
- [Конфигурация](<Documentation/Technical documentation/05-configuration-and-lifecycle.md>)
- [DLL-комплекты и платформы](<Documentation/Technical documentation/24-dll-delivery.md>)
- [SQL Server](<Documentation/Technical documentation/12-sql-server-provider.md>)
- [Техническая документация](<Documentation/Technical documentation/README.md>) · [Бизнес-логика](<Documentation/Business logic/README.md>)
- [Действующая спецификация](openspec/specs/agent-runtime/spec.md) · [План исправлений](<Documentation/Plans/AgentBridge Audit Remediation/README.md>) · [Решение](agent-bridge.slnx)
