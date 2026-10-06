# Подключение и использование AgentBridge

Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). Проверяемые исходники: [Consumer](../../tests/Delivery/Consumer/AgentBridge.BinaryConsumer.csproj); команды и границы подтверждения: [отчёт25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>). Это compile-only библиотека без entry point. Ни один метод примера при проверке не исполнялся.

## 1. Подключить комплект DLL

Для current API требуется комплект с `AgentBridge.Integration.dll`, ядром, обоими адаптерами, полной runtime closure и выбранной migrations DLL (основной provider — SQL Server). Подготовка MSSQL/win-x64/linux-x64/linux-arm64 и external binary compilation относятся к Audit Remediation15/16 и ещё не подтверждены. Не смешивайте версии, RID и migrations assemblies. [Facade/lifetimes](26-integration-registration.md).

Исторические комплекты `artifacts/delivery/stage24-win-x64/Sqlite` и `PostgreSql` содержали39 managed DLL,35 XML, native x64 SQLite, props и evidence/manifest для .NET10/win-x64/Debug. Они **не содержат новый facade** и не компилируют current UsageRegistration/SimpleRegistration. Ниже Import описывает прежний binary layout, а не готовый новый MSSQL kit; [поставка24](24-dll-delivery.md).

В SDK-style .NET10 проекте используйте бинарный Import, как в проверенном consumer:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <AgentBridgeDeliveryRoot>C:\MyApplication\vendor\AgentBridge\Sqlite</AgentBridgeDeliveryRoot>
</PropertyGroup>
<Import Project="$(AgentBridgeDeliveryRoot)/AgentBridge.Delivery.props" />
```

Путь здесь — пример абсолютного каталога приложения. Import подключает все managed DLL и доставляет XML/native стандартными SDK items. Для runtime нужен .NET10 и соответствующий хост приложения. PostgreSQL сервер/права/`pg_dump` в DLL не входят. Наличие SQLite native в PostgreSQL kit связано с общей статической dependency closure и не меняет выбранный provider. Для существующего приложения отдельно проверьте конфликты его package/runtime версий; compile-only consumer не доказывает их отсутствие.

## 2. Настроить options и зависимости приложения

[SimpleRegistration](../../tests/Delivery/Consumer/SimpleRegistration.cs) вызывает actual `services.AddAgentBridge(configuration, httpClientFactory)` из `AgentBridge.Integration`. Это одна standard composition: strict options13, scoped persistence/transport/context/runner/settings/cleanup, diagnostics/tools/tokenizer/guard. Базовый Shared режим работает без пустых business классов и сохраняет обычную историю.

[UsageRegistration.AddUsageGuide](../../tests/Delivery/Consumer/UsageRegistration.cs) показывает advanced app wrapper: individual source, explicit ordered ContextBuilder и scoped business service/tool регистрируются перед тем же фасадом. ILoggerFactory регистрируется приложением **до** wrapper/facade. Individual mode без заранее зарегистрированного источника отклоняется options validation, source не разрешается из root при проверке старта. [App-owned HTTP registration, disposal/lifetimes/overrides/повтор](26-integration-registration.md); app callback получает borrowed client, его lifetime обеспечивает приложение. Low-level модульные API остаются доступны для advanced composition, но не вызываются повторно после фасада.

Пример формы configuration (endpoint, модель и путь условные; доступность model/effort проверяется каталогом):

```json
{
  "AgentBridge": {
    "Agent": { "InstructionsSource": "Configuration", "Instructions": "Отвечай по разрешённым данным.", "MaxToolSteps": 8 },
    "Retention": { "RetentionPeriod": "7.00:00:00", "SoftContentLimitBytes": 10485760 },
    "Compaction": { "TokenThreshold": 32000, "InputTokenReserve": 4096, "MaxPasses": 3 }
  },
  "CodexLb": {
    "KeySource": "Shared",
    "BaseAddress": "https://codex-lb.example.invalid/",
    "Model": "gpt-5", "ReasoningEffort": "medium",
    "GenerationTimeout": "00:03:00", "CompactTimeout": "00:03:00"
  },
  "Database": { "Provider": "SqlServer" }
}
```

`Database:ConnectionString` для своего SQL Server с согласованными authentication/TLS приходит из secret configuration приложения; выбранная migrations DLL — SqlServer. SQLite/PostgreSQL остаются явными альтернативами с собственной configuration/migrations identity. `CodexLb:SharedApiKey` также приходит из secret store/configuration, а не из коммитимого примера. Строки подключения, ключи, raw headers/body и canonical payload не выводятся в logs/UI.

Все операционные значения задаются явно; пропуск больше не получает прежний default. Для миграции выберите InstructionsSource=Configuration/PerRequest и KeySource=Shared/Individual. В Shared общий ключ обязателен, индивидуальный сохраняет приоритет; Individual не применяет общий fallback. PerRequest требует инструкции каждого run. `RetentionPeriod` положителен и фиксирует expiry при создании; новая конфигурация не пересчитывает старые сроки. `SoftContentLimitBytes` положителен, даёт предупреждение при `bytes >= limit`, не запрещает запись и не вызывает cleanup. `TokenThreshold` положителен, explicit `InputTokenReserve >= 0`, `MaxPasses > 0`. Проверенный threshold+reserve должен укладываться в **input_context_window**. [Полный options API и breaking migration](05-configuration-and-lifecycle.md), [бюджет/tokenizer](07-tokenizer-and-settings.md).

Logging/Serilog provider и его redaction настраивает приложение. Фабрики DI не выполняют I/O, HttpClient/handlers принадлежат приложению; не добавляйте retry или замену ключа/аккаунта. Scoped зависимости не разделяются между параллельными tools. Для ASP.NET Core вызывайте групповую регистрацию в composition root; development DI validation остаётся включённой. В приложении без host явно проверяйте полученные options и lifetimes: compile-check не исполняет `ValidateOnStart` или контейнер.

## 3. Подготовить БД отдельной операцией

Регистрация не создаёт БД и не применяет migrations. Перед пользовательскими обращениями приложение подготавливает хранилище через [явный maintenance API](06-database-maintenance.md#подключение-agentbridge-этапа-12). Проверенный [BinaryContractProbe.Register](../../tests/Delivery/Consumer/BinaryContractProbe.cs) содержит `AddAgentBridgeDatabaseMaintenance(backup, MaintenanceExecutionMode.SingleInitializer)`; `UsageRegistration` намеренно оставляет maintenance отдельной процедурой приложения.

При явном подключении maintenance обязательны provider-specific backup destination и положительный retention. MSSQL использует `SqlServerBackupDirectory` на сервере БД, не путь ASP.NET Core host; [actual SQL Server API](12-sql-server-provider.md) и [compile-only SqlServerRegistration](../../tests/Delivery/Consumer/SqlServerRegistration.cs). SQLite использует абсолютный локальный backup directory; PostgreSQL — также installed toolchain/absolute pg_dump/matching major/конечный cleanup timeout. SingleInitializer требует остановки других экземпляров/writes/DDL приложением. В отдельном scope явно выбираются `InspectAsync`, `InitializeNewAsync` либо `UpdateExistingAsync` с timeout/отменой. Ошибка подключения не означает отсутствующую БД; автоматического restore/fallback нет. Cleanup диалогов и retention backup — разные операции приложения. При disabled maintenance его backup options/schedule не требуются фасадом.

## 4. Ключи, каталог и выбор model/effort

[IndividualKeySource](../../tests/Delivery/Consumer/IndividualKeySource.cs) — адаптер delegate к secret store приложения. Он возвращает исходный ключ: **только null** означает отсутствие и в режиме Shared разрешает shared key. В Individual общий fallback отсутствует. Пустая строка/ошибка заданного ключа/отказ источника не разрешают общий ключ. `ModelAccess` живёт только на вызов и не сохраняется.

[UsageFlow.ReadModelsAsync](../../tests/Delivery/Consumer/UsageFlow.cs) сначала вызывает `IModelAccessResolver.ResolveAsync(owner)`, затем `IModelCatalog.ReadAsync(access)` в scoped boundary. UI получает `ModelCatalogSnapshot.Models`: `Id`, `ReasoningEfforts`, nullable `InputContextWindow` и другие safe capabilities. Это динамический каталог выбранного ключа; не заменяйте его фиксированным списком или default effort при отказе.

Для сохранения выбора сначала `ReadSettingsAsync` → проверить `ServiceResult.Success` → отобразить `AgentSettingsSnapshot` → `SelectAsync` с **исходными** `Token` и `SelectionVersion` и выбранными exact model/effort. Не перечитывайте версии для автоматического принятия устаревшего UI. Conflict требует отдельного нового действия пользователя. Выбор сохраняется для конкретного диалога с независимой CAS version и не прерывает active run.

Приоритет обращения: явный override → saved dialog selection → defaults приложения. `UsageFlow.RunAsync` принимает nullable `modelOverride`/`effortOverride`; override не persist. Активный run сохраняет атомарный `TurnModelSettings`. Selected model и actual `ServerModel` различны; server name не доказывает совместимость. Opaque другой/неизвестной selected model требует доказательного `IContextModelCompatibility` приложения. В примере этот порт не подставлен: отсутствие proof даёт Unsupported, история сохраняется. Совместимость не доказывает token budget. [Подробный контракт21](21-settings-and-dialog-status.md).

## 5. Создать диалог и вызвать агента

Полный проверяемый C# исходник: [UsageFlow](../../tests/Delivery/Consumer/UsageFlow.cs). Порядок вызовов приложения:

1. Авторизовать пользователя и выбранный `AgentId`; `ApplicationCallContext` сам не является разрешением.
2. Создать `DialogId.From(Guid.NewGuid())`, `DialogOwnerId.From(authenticatedSubject)` в пространстве ID приложения. Вызвать `CreateAsync(root, dialogId, owner, ct)`. Оно использует `TimeProvider`, `DialogRetentionOptions.CalculateExpiresAtUtc` и `IDialogCreator.CreateAsync` в отдельном коротком scope. Проверить typed результат до продолжения; существующий ID даёт Conflict.
3. Создать `ApplicationCallContext(dialogId, owner, Guid.NewGuid(), authorizedAgentId)` для **нового** turn. Вызвать `RunAsync(root, call, text, onUpdate, ct)`. Проверенный исходник строит canonical user message через `JsonSerializer.SerializeToElement`, а не конкатенацией JSON; передаёт только новый input и exact tool names.
4. `onUpdate=null` выбирает JSON; non-null — SSE. Callback awaited, соблюдает токен отмены и передаёт UI только разрешённый видимый текст. `Item`, envelope и tool output могут содержать чувствительные данные; не логируйте их. Delta предварительна, не подтверждает terminal сохранение.
5. Проверить `AgentRunResult.Status`, `TerminalSaved`, `Error`, при необходимости сохранённый `Turn`. `LastResponse`/`LastTools` сами не доказывают принятие storage. Успешный ответ — Completed с подтверждённым terminal save.

`ToolExecutionLimits(8,16,2,1 minute)` в примере — явные limits приложения; `AgentOptions.MaxToolSteps` дополнительно ограничивает шаги. Timeout cooperative: handler/callback обязаны завершаться по cancellation, library ожидает начатые tasks/scopes.

Providers выполняются последовательно один раз на run и сами авторизуют свой вклад. Допустимы исходные canonical roles; инструкции находятся в `ModelRequest.Instructions`. Входной request состоит из providers, active context, непокрытой истории и нового input; reports/envelope/continuation не добавляются повторно. Runner не использует compact id как generation previous_response_id. Compact покрывает только непрерывный terminal prefix, включая0; InProgress не считается покрытым. История не удаляется ради бюджета.

## 6. Подключить свои инструменты

Проверенный пример read-only инструмента состоит из [AccountSummaryTool](../../tests/Delivery/Consumer/AccountSummaryTool.cs), [AccountSummaryValidator](../../tests/Delivery/Consumer/AccountSummaryValidator.cs) и [IAccountSummarySource](../../tests/Delivery/Consumer/IAccountSummarySource.cs).

`IAccountSummarySource` — **пример порта приложения**, не тип поставляемой AgentBridge. Реализацию scoped factory передаёт приложение: настоящие текущие права owner/agent, повторная авторизация внутри чтения и safe разрешённая сводка. Заглушка успешных DB/HTTP вызовов не поставляется. Validator проверяет всю простую schema: exact name, object без полей (`properties={}`, `required=[]`, `additionalProperties=false`), затем актуальные права. Handler повторно проверяет допуск до бизнес-чтения; metadata совпадают с регистрацией. Для собственной сложной схемы приложение обеспечивает полную schema validation, executor не является общим JSON Schema engine.

`AddAgentBridgeTool<THandler,TValidator>(definition)` даёт отдельный invocation scope. Selected names не являются авторизацией. Бизнес-I/O выполняется после durable Started и вне write transaction AgentBridge. Не передавайте рабочий DbContext нескольких tasks в singleton. Подтверждённый отказ возвращается `ServiceResult.Fail` с безопасным сообщением; исключение/timeout после начала не объявляйте подтверждённым отказом, результат может быть Unknown. [Tools19](19-application-tools.md), [durable runner20](20-agent-turn-orchestration.md).

## 7. Показать status/expiry и запланировать очистку

`UsageFlow.ReadStatusAsync` возвращает `ServiceResult<DialogStatus>` через `AgentSettingsService.GetStatusAsync`. UI отображает `ExpiresAtUtc` в своей timezone и `IsExpired`, bytes/soft warning, selected model/effort, actual server model и контекст. При `now >= expiry` запись запрещена, но lifecycle metadata доступны до физического удаления. Активность/compact/settings срок не продлевают. `ExpiresAtUtc` — граница доступности, **не гарантированное время физического удаления**: оно зависит от расписания cleanup приложения.

`ContextSize.KnownTokens` и nullable `EstimatedInputTokens` различаются. Status измеряет только сохранённое окно/историю без следующего input/providers/instructions/tools; `CanContinue` позволяет подготовку, не гарантирует полный generation budget. `CompactionThresholdReached=null` не означает false. Unknown/opaque не считается нулём. Exact offline mapping: o200k — gpt-5/gpt-4.1/gpt-4o/o1/o3/o4-mini; cl100k — gpt-4/gpt-3.5-turbo. Другие ID, включая gpt-6 и suffixed IDs, не поддержаны offline; server estimator не реализован. Guard откажет при неизвестной полной оценке.

`UsageFlow.CleanupAsync(root, limit, ct)` показывает отдельный caller scope и один явный bounded пакет. Limit положителен и определяется приложением; расписание и системная авторизация также у приложения. Read освобождается до последовательных delete с отдельными scopes/fresh UTC. Completed подтверждает только обработанный пакет, не отсутствие всех истёкших строк. Partial/Failed/Canceled/Interrupted и candidate Unknown не выдаются за успех. Если нужен прогресс после exception, выполняйте прямой вызов в собственном caller scope и читайте `cleanup.LastResult` **до его освобождения**, как в [контракте22](22-expired-dialog-cleanup.md); helper `UsageFlow.CleanupAsync` такого доступа вызывающему не предоставляет. Новый запланированный вызов может обработать следующую порцию; внутри сценария нет drain-loop/refresh/retry.

## Обработка ошибок и recovery

Typed ServiceResult failures передаются UI по semantic `ServiceError.Type` и безопасному сообщению, которое обязано обеспечивать и приложение. Conflict/Expired/Unsupported/Timeout не превращаются в новую попытку. Неожиданные exceptions и aggregate cleanup errors распространяются; диагностируйте только безопасные codes/counts, не `Exception.ToString()` вместе с driver/HTTP секретами.

Run `Canceled`, `Incomplete`, `Interrupted` не являются Completed. Поздняя отмена может дать `Canceled` с `TerminalSaved=true` и уже принятым `Turn.Completed`: статус caller и факт commit — разные сведения. Candidate/tool Unknown означает отсутствие acknowledgement, а не rollback или разрешение retry. Synthetic acknowledgement tests не доказывают потерю реальной сети.

Existing TurnId не исполняется повторно; restart не возобновляет Started tool автоматически. Известный function_call без output, включая partial arguments SSE, блокирует следующий запрос. Нельзя удалить историю, придумать output либо заменить TurnId ради повторного действия. Повторные завершённые пары с одинаковым call_id допустимы и не дедуплицируются глобально. Recovery/новое действие после диагностики определяет приложение, библиотека не предоставляет автоматический replay.

## Границы подтверждения

Current facade14/consumer source compilation и DI/options/fake HTTP evidence описаны в [Results14](<../Plans/AgentBridge Audit Remediation/14-simplified-registration.md#результаты>). Methods consumer не исполнялись, source compilation не является external binary kits15/16 или provider/runtime/live17–19. HTTP endpoints приложения ниже перенесены из прежнего README и проверены только статически; host/маршруты/auth не запускались. Следующие два абзаца фиксируют **историческое evidence этапа25**, включая тогдашнее отсутствие CLI; актуальный CLI checkpoint09 находится в [OpenSpec workflow](../../openspec/README.md).

Примеры скомпилированы с каждым kit вне репозитория,0 warnings/errors, по206 references=39 kit+167 framework; нет project/package references. Проверены копирование XML/native и metadata inheritdoc; generated XML хранит `<inheritdoc/>`, автоматическое разворачивание конкретной IDE не проверено. EFCoreLibrary CRUD не генерирует XML, три SQLitePCLRaw managed DLL также без XML.

Runtime/DI/options execution, SQLite native loading из комплекта, Release/другие RID/AOT/trimming/single-file не проверены. Предшествующие реальные SQLite/PostgreSQL tests относятся к своим исходникам и окружению, не к runtime kit. Fake HTTP actual HttpClientLibrary не доказывает live codex-lb/OpenAI compatibility. Live HTTP/Telegram/hosting/demo — **Пропущено по указанию пользователя**. OpenSpec CLI отсутствует, CLI validation не выполнена, changes не архивированы. [Карта evidence00–25](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>).

<details>
<summary>HTTP endpoints приложения: create dialog и JSON message</summary>

Это код ASP.NET Core приложения, не API фасада. Logging/HTTP/authorization policy `AgentBridgeChat` и owner/agent permissions настраивает приложение; маршруты не создаются AddAgentBridge. Код перенесён из прежнего README, его host/HTTP/auth не исполнялись и compilation серверного примера не заявлена.

## HTTP endpoints приложения

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

Пример использует JSON-ответ модели. Для потоковой передачи нужен callback `AgentRunner.RunAsync` и отдельная HTTP streaming-граница приложения; [контракт SSE](<15-responses-sse-adapter.md>). Переданные лимиты инструментов — значения примера; сейчас выбранных инструментов нет.

</details>
