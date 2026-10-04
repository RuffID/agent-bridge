# AgentBridge

AgentBridge — C#-библиотека для добавления ИИ-агента в приложение на .NET 10. Она собирает контекст, обращается к модели через codex-lb, выполняет разрешённые инструменты приложения и сохраняет диалог в SQLite или PostgreSQL. Подключение — обычными DLL.

Ниже — пример для **ASP.NET Core сервера с PostgreSQL**: создать диалог и отправить текстовое сообщение. Пример использует общий ключ codex-lb, без бизнес-инструментов и дополнительных источников контекста. Авторизация, интерфейс и бизнес-данные остаются у приложения.

## 1. Подключить DLL к серверному проекту

Нужны .NET 10 SDK, доступный codex-lb и PostgreSQL. Для существующего комплекта поставки используется Windows x64: `artifacts/delivery/stage24-win-x64/PostgreSql`. Перенесите **весь комплект**, например в `vendor/AgentBridge/PostgreSql` серверного проекта:

```text
MyServer/
  MyServer.csproj
  vendor/AgentBridge/PostgreSql/
    AgentBridge.Delivery.props
    lib/       # Все DLL и XML из комплекта
    native/    # Native-файлы из комплекта
```

В `MyServer.csproj` подключите props:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AgentBridgeDeliveryRoot>$(MSBuildProjectDirectory)/vendor/AgentBridge/PostgreSql</AgentBridgeDeliveryRoot>
  </PropertyGroup>
  <Import Project="$(AgentBridgeDeliveryRoot)/AgentBridge.Delivery.props" />
</Project>
```

В существующем проекте добавьте только необходимые свойства и Import, сохранив его остальные настройки и зависимости. Props подключает managed DLL/XML и копирует native-файлы. Комплект содержит AgentBridge, оба адаптера, PostgreSQL migrations assembly, EFCoreLibrary и HttpClientLibrary. Отдельный NuGet-пакет AgentBridge не нужен.

Не переносите только `AgentBridge.dll`: ей нужны зависимости. Наличие SQLite native-файла в PostgreSQL-комплекте не переключает выбранную БД. Для Linux или другого RID требуется отдельная подготовка и проверка поставки; этот win-x64-комплект их не подтверждает. [Состав и ограничения DLL](<Documentation/Technical documentation/24-dll-delivery.md>).

## 2. Задать настройки и секреты

В `appsettings.json` приложения:

```json
{
  "AgentBridge": {
    "Agent": {
      "Instructions": "Отвечай по-русски, кратко и по существу.",
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
    "BaseAddress": "https://codex-lb.example.invalid/",
    "Model": "gpt-5",
    "ReasoningEffort": "medium",
    "GenerationTimeout": "00:03:00",
    "CompactTimeout": "00:03:00"
  },
  "Database": {
    "Provider": "PostgreSql"
  }
}
```

Адрес и модель — примеры. Укажите свой сервер и модель, доступную выбранному ключу. Точное имя модели и effort проверяются по каталогу codex-lb; встроенный tokenizer поддерживает ограниченный набор имён. Порог сжатия плюс запас должны помещаться во входной бюджет модели.

Ключ и строку подключения передайте через secret store приложения, User Secrets для разработки или переменные окружения. Имена переменных и форма значений:

```text
CodexLb__SharedApiKey=<ключ codex-lb>
Database__ConnectionString=Host=localhost;Port=5432;Database=agent_bridge;Username=agent_bridge_app;Password=<пароль>
```

Это шаблоны, не команды запуска. Рабочие секреты не сохраняйте в README или коммитимом appsettings. Параметры TLS и права PostgreSQL задаются вашим окружением.

Все числовые значения выше можно переопределить. Срок отсчитывается от создания диалога; сообщения и сжатие его не продлевают. Мягкий порог байтов выдаёт предупреждение, а не удаляет историю.

## 3. Зарегистрировать библиотеку в DI

Следующие типы — **код подключающего приложения**, а не дополнительные API AgentBridge. Разместите их в отдельных файлах.

`Integration/SharedKeyOnlySource.cs` явно сообщает, что индивидуальные ключи в этом примере не используются:

```csharp
using AgentBridge.Application.Ports;
using AgentBridge.Domain.Dialogs;

namespace MyServer.Integration;

/// <summary>Пример без индивидуальных ключей: применяется общий ключ приложения.</summary>
public class SharedKeyOnlySource : IIndividualModelKeySource
{
    /// <inheritdoc/>
    public Task<string?> GetKeyAsync(DialogOwnerId ownerId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }
}
```

Если нужны индивидуальные ключи, замените этот источник чтением вашего secret store по владельцу. Только `null` разрешает общий ключ; пустой или неверный индивидуальный ключ и ошибка источника не разрешают fallback.

`Integration/AgentBridgeRegistration.cs`:

```csharp
using System.Security.Claims;
using AgentBridge.Application;
using AgentBridge.Application.Ports;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Diagnostics;
using AgentBridge.Persistence.EfCore.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyServer.Integration;

/// <summary>Подключает текстовый агент и PostgreSQL без выполнения HTTP или операций БД.</summary>
public static class AgentBridgeRegistration
{
    /// <summary>Регистрирует options, адаптеры и сценарии; аутентификацию настраивает сервер.</summary>
    public static IServiceCollection AddServerAgentBridge(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAgentBridgeConfiguration(configuration.GetSection("AgentBridge"));
        services.AddCodexLbConfiguration(configuration.GetSection("CodexLb"));
        services.AddDatabaseConfiguration(configuration.GetSection("Database"));
        services.AddAgentBridgePersistence();
        services.AddAgentBridgeDiagnostics();

        services.AddScoped<IIndividualModelKeySource, SharedKeyOnlySource>();
        services.AddHttpClient("AgentBridgeCodexLb", client =>
        {
            // Таймаут generation/compact контролирует адаптер.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddCodexLbResponses(provider =>
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("AgentBridgeCodexLb"));

        services.AddScoped<ContextBuilder>(_ => new ContextBuilder([]));
        services.AddAgentBridgeTools();
        services.AddAgentBridgeTokenization();
        services.AddAgentBridgeCompaction();
        services.AddAgentBridgeRunner();
        services.AddAgentBridgeSettings();
        services.AddAgentBridgeDialogCleanup();

        // Пример политики приложения: допуск только по проверенному permission claim.
        services.AddAuthorization(options => options.AddPolicy("AgentBridgeChat", policy =>
            policy.RequireAuthenticatedUser()
                .RequireClaim(ClaimTypes.NameIdentifier)
                .RequireClaim("permission", "agentbridge.chat")));
        return services;
    }
}
```

Пустой `ContextBuilder` означает отсутствие дополнительных бизнес-источников; история диалога всё равно включается. Пустой registry инструментов намеренный: в следующем примере отправляется пустой список выбранных инструментов. Не добавляйте автоматические retries и общие изменяемые Authorization headers в HTTP pipeline.

В существующий `Program.cs` после создания builder и настройки аутентификации добавьте:

```csharp
using MyServer.Integration;

if (builder.Environment.IsDevelopment())
{
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });
}

builder.Services.AddServerAgentBridge(builder.Configuration);
```

В pipeline после `WebApplication app = builder.Build();`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapAgentBridgeChat();
// Остальные маршруты приложения.
app.Run();
```

Это фрагменты **существующего** сервера. Он должен заранее настроить свою authentication scheme: например, JWT bearer или cookie. Проверенная учётная запись должна давать стабильный `ClaimTypes.NameIdentifier`; в многопользовательской системе с несколькими арендаторами идентификатор должен быть уникален и между арендаторами. Claim `permission=agentbridge.chat` выдаёт доверенная система прав приложения. Не принимайте owner или permission из тела запроса.

Serilog и его integration с ILogger также настраивает приложение. `AddAgentBridgeDiagnostics` не создаёт Serilog logger и sinks. API ниже предполагает серверную обработку неожиданных исключений без выдачи stack trace, ключей и raw model payload клиенту.

## 4. Подготовить PostgreSQL отдельной административной операцией

До первого обращения таблицы AgentBridge должны существовать. DI-регистрация их не создаёт; не используйте `EnsureCreated` вместо migrations.

В административном composition root зарегистрируйте те же options/persistence и дополнительно:

```csharp
using AgentBridge.Persistence.EfCore.Configuration;
using EFCoreLibrary.Maintenance.Models;

services.AddAgentBridgeDatabaseMaintenance(backup =>
{
    backup.BackupDirectory = @"C:\MyServer\backups\agent-bridge";
    backup.BackupRetentionPeriod = TimeSpan.FromDays(30);
    backup.PostgreSqlDumpExecutablePath = @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe";
    backup.PostgreSqlServerMajorVersion = 18;
    backup.PostgreSqlCleanupTimeout = TimeSpan.FromSeconds(15);
}, MaintenanceExecutionMode.SingleInitializer);
```

Пути, major 18 и срок 30 дней — пример настроек оператора. `pg_dump` устанавливается отдельно, его major должен совпадать с сервером. Режим SingleInitializer требует остановить остальные экземпляры, записи и DDL на всё время обслуживания; локальная блокировка библиотеки этого не обеспечивает.

Пример **явно выбранной первой установки**, выполняемый административным кодом с его root provider и cancellation token:

```csharp
using AgentBridge.Persistence.EfCore;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;

await using AsyncServiceScope scope = root.CreateAsyncScope();
IDatabaseMaintenance<AgentBridgeContextKey> maintenance =
    scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<AgentBridgeContextKey>>();

DatabaseMaintenanceResult result = await maintenance.InitializeNewAsync(
    TimeSpan.FromMinutes(5), cancellationToken);
```

Для уже существующей БД вместо `InitializeNewAsync` явно выбирают `UpdateExistingAsync`; для просмотра состояния — `InspectAsync`. Не переключайтесь на создание БД при ошибке подключения. При pending migrations обновление существующей БД требует успешного backup; ошибка останавливает операцию. У административной учётной записи должны быть соответствующие права.

Эти операции не должны быть обычным публичным HTTP-маршрутом. Они не включены в запуск веб-сервера выше. Хранение/удаление backup и проверку восстановления организует приложение. [Подробный контракт обслуживания](<Documentation/Technical documentation/06-database-maintenance.md>).

## 5. Добавить маршруты создания диалога и отправки сообщения

`Models/ChatMessage.cs`:

```csharp
namespace MyServer.Models;

/// <summary>Новое сообщение; TurnId сохраняется клиентом для этой логической отправки.</summary>
public record ChatMessage(Guid TurnId, string Text);
```

`Integration/AgentBridgeEndpoints.cs`:

```csharp
using System.Security.Claims;
using System.Text.Json;
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using MyServer.Models;

namespace MyServer.Integration;

/// <summary>Пример HTTP-границы приложения: владелец только из проверенной учётной записи.</summary>
public static class AgentBridgeEndpoints
{
    /// <summary>Добавляет авторизованные маршруты одного текстового агента.</summary>
    public static IEndpointRouteBuilder MapAgentBridgeChat(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/agent/dialogs")
            .RequireAuthorization("AgentBridgeChat");
        group.MapPost("/", CreateAsync);
        group.MapPost("/{dialogId:guid}/messages", SendAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(HttpContext http, IDialogCreator creator,
        IOptionsSnapshot<DialogRetentionOptions> retention, TimeProvider time, CancellationToken ct)
    {
        string? userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

        DialogId id = DialogId.From(Guid.NewGuid());
        DateTimeOffset created = time.GetUtcNow();
        DateTimeOffset expires = retention.Value.CalculateExpiresAtUtc(created);
        ServiceResult<DialogWriteToken> result = await creator.CreateAsync(
            id, DialogOwnerId.From(userId), created, expires, ct);
        if (!result.Success)
            return Results.Json(new { errorCode = result.Error!.Type.ToString() },
                statusCode: ErrorStatus(result.Error.Type));
        return Results.Ok(new { dialogId = id.Value, expiresAtUtc = expires });
    }

    private static async Task<IResult> SendAsync(Guid dialogId, ChatMessage message,
        HttpContext http, AgentRunner runner, CancellationToken ct)
    {
        string? userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
        if (dialogId == Guid.Empty || message.TurnId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.Text))
            return Results.BadRequest(new { errorCode = "InvalidMessage" });

        ApplicationCallContext call = new(DialogId.From(dialogId),
            DialogOwnerId.From(userId), message.TurnId, "support");
        CanonicalModelItem input = new(JsonSerializer.SerializeToElement(new
        {
            type = "message",
            role = "user",
            content = new[] { new { type = "input_text", text = message.Text } }
        }));
        AgentRunRequest request = new(call, [input], [],
            new ToolExecutionLimits(1, 1, 1, TimeSpan.FromMinutes(5)));
        AgentRunResult result = await runner.RunAsync(request, cancellationToken: ct);

        bool completed = result.Status == AgentRunStatus.Completed && result.TerminalSaved;
        return Results.Json(new
        {
            turnId = message.TurnId,
            status = result.Status.ToString(),
            terminalSaved = result.TerminalSaved,
            completed,
            text = ReadVisibleText(result.LastResponse),
            errorCode = result.Error?.Type.ToString()
        }, statusCode: result.Error is null ? 200 : ErrorStatus(result.Error.Type));
    }

    // В HTTP DTO не выдаются envelope, reasoning, continuation или tool payload.
    private static string ReadVisibleText(ModelResponse? response)
    {
        List<string> text = [];
        if (response is null) return string.Empty;
        foreach (CanonicalModelItem item in response.Output)
        {
            JsonElement json = item.Content;
            if (!IsString(json, "type", "message") || !IsString(json, "role", "assistant") ||
                !json.TryGetProperty("content", out JsonElement parts) ||
                parts.ValueKind != JsonValueKind.Array) continue;
            foreach (JsonElement part in parts.EnumerateArray())
            {
                if (IsString(part, "type", "output_text") &&
                    part.TryGetProperty("text", out JsonElement value) &&
                    value.ValueKind == JsonValueKind.String)
                    text.Add(value.GetString()!);
            }
        }
        return string.Join("\n", text);
    }

    private static bool IsString(JsonElement json, string property, string expected) =>
        json.ValueKind == JsonValueKind.Object &&
        json.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String && value.GetString() == expected;

    // Это HTTP-политика примера приложения, а не контракт AgentBridge.
    private static int ErrorStatus(ServiceErrorType error) => error switch
    {
        ServiceErrorType.Validation => 400,
        ServiceErrorType.Forbidden => 403,
        ServiceErrorType.NotFound => 404,
        ServiceErrorType.Conflict => 409,
        ServiceErrorType.Expired => 410,
        ServiceErrorType.Unsupported => 422,
        ServiceErrorType.Timeout => 504,
        _ => 502
    };
}
```

В примере право `agentbridge.chat` разрешает агент `support`. При нескольких агентах приложение должно проверять доступ к каждому выбранному агенту. Отказ ключа codex-lb — проблема доступа сервера к модели, а не повод доверять другому owner; пример не превращает его в повторную попытку с другим ключом.

Порядок вызовов:

1. Авторизованный клиент отправляет `POST /api/agent/dialogs/` и получает `dialogId` и `expiresAtUtc`.
2. Для нового сообщения создаёт `turnId` один раз и отправляет `POST /api/agent/dialogs/{dialogId}/messages`:

```json
{
  "turnId": "771c966d-3502-4efa-96f2-a0c2a8047e0d",
  "text": "Привет! Чем ты можешь помочь?"
}
```

3. Проверяет `completed`, `status`, `terminalSaved` и `errorCode`, затем показывает `text`. HTTP 200 здесь означает получение отчёта; неполный или отменённый отчёт может иметь `completed=false`. Текст может быть частичным, а `LastResponse` не подтверждает сохранение.
4. Следующий новый вопрос использует тот же `dialogId` и новый `turnId`. Повтор доставки прежнего запроса сохраняет прежний `turnId`: существующее обращение не переисполняется. Это защита от replay, а не кеш ответа на повторную HTTP-отправку. Не меняйте ID автоматически ради обхода отказа.

Пример использует JSON-ответ модели. Для потоковой передачи нужен callback `AgentRunner.RunAsync` и отдельная HTTP streaming-граница приложения; [контракт SSE](<Documentation/Technical documentation/15-responses-sse-adapter.md>). Переданные лимиты инструментов — значения примера; сейчас выбранных инструментов нет.

## 6. Настройки диалога, инструменты и очистка

- `AgentSettingsService.ReadAsync/SelectAsync` читает и сохраняет модель/effort конкретного диалога с проверкой исходных версий. Разовый override запроса имеет приоритет над сохранённым выбором, затем идут defaults.
- `AgentSettingsService.GetStatusAsync` возвращает срок, объём, выбор модели и оценку сохранённого контекста. Это не гарантия бюджета следующего запроса.
- Бизнес-инструменты подключаются через `AddAgentBridgeTool<THandler,TValidator>`; приложение проверяет полную схему аргументов и актуальные права. [Пример инструмента](tests/Delivery/Consumer/AccountSummaryTool.cs) и [validator](tests/Delivery/Consumer/AccountSummaryValidator.cs).
- `ExpiredDialogCleanup.CleanupAsync(limit, ct)` вызывается приложением в отдельном scope по его расписанию и обрабатывает один ограниченный пакет. Регистрация не запускает фоновой очистки. Partial/Unknown/отмена не означают успешное удаление всех кандидатов.
- При неизвестном исходе внешнего действия библиотека не выполняет автоматический повтор. Проверку фактического состояния бизнес-системы организует приложение.

## Проверенность и документация

Сигнатуры примера выше сверены статически с текущими исходниками. **Новый ASP.NET Core пример не компилировался и не запускался**; PostgreSQL, migrations и живой codex-lb в этой задаче не проверялись. Ранее [бинарные примеры Consumer](tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj) компилировались с обоими комплектами, но их методы не исполнялись. Это отдельные исторические проверки, не подтверждение нового серверного примера.

Неизвестный полный размер opaque-контекста блокирует отправку встроенным budget guard; успешное сжатие само по себе не гарантирует возможность продолжения. Runtime/native загрузка комплекта и совместимость с конкретным приложением требуют отдельной проверки. [Ограничения и подробное руководство](<Documentation/Technical documentation/25-usage-guide.md>).

Первоначальная реализация 00–25 завершена в документированных границах; [карта доказательств](<Documentation/Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>). OpenSpec validation по прежним отчётам не выполнена, changes не архивированы. Новый аудит предназначен только для выявления проблем; его этапы пока не начаты.

[Бизнес-логика](<Documentation/Business logic/README.md>) · [Техническая документация](<Documentation/Technical documentation/README.md>) · [План аудита](<Documentation/Plans/AgentBridge Quality Audit/README.md>) · [Реестр проблем](<Documentation/Plans/AgentBridge Quality Audit/Findings.md>) · [OpenSpec](openspec/specs/agent-runtime/spec.md) · [Решение](agent-bridge.slnx)
