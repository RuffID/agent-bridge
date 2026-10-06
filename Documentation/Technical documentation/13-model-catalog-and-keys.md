# Каталог моделей и ключ доступа

Статус: **Реализован и принят; запрещённые проверки пропущены**. Пауза после13 — исторический checkpoint; реализация14–25 завершена в документированных границах. [Актуальный checkpoint и evidence](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>). Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md). [Отчёт и команды этапа 13](<../Plans/AgentBridge Initial Implementation/13-model-catalog-and-keys.md>).

## Фактические контракты

| API | Назначение |
| --- | --- |
| `IIndividualModelKeySource.GetKeyAsync(DialogOwnerId, CancellationToken)` | Приложение возвращает string?; только null означает отсутствие. Источник обеспечивает доступ к секрету и права пользователя, не логирует ключ |
| `IModelAccessResolver.ResolveAsync(DialogOwnerId, CancellationToken)` | Индивидуальный ключ, иначе SharedApiKey из scoped options; результат ServiceResult<ModelAccess> |
| `IModelCatalog.ReadAsync(ModelAccess, CancellationToken)` | Текущий каталог конкретного выбранного доступа; ServiceResult<ModelCatalogSnapshot> |
| `ModelSelectionValidator.Validate(catalog, model, effort, tokenThreshold, inputTokenReserve)` | Точная проверка выбора и настроенного input budget; ServiceResult<ModelSettingsSnapshot> |
| `IModelSettingsReader.ReadAsync(ownerId, model = null, effort = null, ct = default)` | Чтение defaults/overrides без сохранения выбора и без запуска агента |
| `AddCodexLbModelCatalog(services, httpClientFactory, loggingOptions = null)` | Scoped HttpApiClient библиотеки, resolver/catalog/reader; без отправки при регистрации/разрешении |

Порты и снимки находятся в `AgentBridge.Application.Ports`/`Models`, validator — `AgentBridge.Application`. Реализации — `AgentBridge.CodexLb.Models`, DI — `AgentBridge.CodexLb.Configuration`. Ядро не зависит от HTTP/EF. Adapter ProjectReference использует текущую HttpClientLibrary 0.0.0.5; приложение поставляет её DLL вместе с зависимостями.

Приложение предоставляет реализацию источника даже при использовании только общего ключа: тогда источник возвращает null. Заданный пустой, пробельный или непригодный для Bearer ключ отклоняется Validation без fallback. Отсутствие обоих — Unauthorized. Исключение источника не маскируется и не разрешает общий ключ. ModelAccess не сериализует секрет публичными свойствами, его ToString безопасен; RevealApiKey только для авторизации транспорта. Сохранение и логирование запрещены.

## Подключение

Фрагмент composition root: `services`, принадлежащий приложению `applicationHttpClient` и `individualKeys` (реализация IIndividualModelKeySource) уже созданы приложением. HttpClient factory не выполняет I/O; приложение владеет timeout, handlers и освобождением клиента. Не добавлять handlers со скрытым retry или подменой Authorization.

```csharp
using AgentBridge.Application.Ports;
using AgentBridge.Application.Models;
using AgentBridge.Application.Results;
using AgentBridge.CodexLb.Configuration;
using AgentBridge.Configuration;
using AgentBridge.Domain.Dialogs;
using Microsoft.Extensions.DependencyInjection;

services.AddLogging(); // Serilog provider и фильтры задаёт приложение
services.AddAgentBridgeConfiguration(
    options => { options.InstructionsSource = AgentInstructionsSource.PerRequest; options.MaxToolSteps = 8; },
    options => { options.RetentionPeriod = TimeSpan.FromDays(7); options.SoftContentLimitBytes = 10_485_760; },
    options => { options.TokenThreshold = 32_000; options.InputTokenReserve = 4_096; options.MaxPasses = 3; });
services.AddCodexLbConfiguration(options =>
{
    options.BaseAddress = "https://gateway.example/proxy/";
    options.Model = selectedModel;
    options.ReasoningEffort = selectedEffort;
    options.SharedApiKey = applicationSharedKey;
    options.KeySource = ModelKeySourceMode.Shared;
    options.GenerationTimeout = TimeSpan.FromSeconds(180);
    options.CompactTimeout = TimeSpan.FromSeconds(180);
});
services.AddScoped<IIndividualModelKeySource>(_ => individualKeys);
services.AddCodexLbModelCatalog(_ => applicationHttpClient);

// В принадлежащем приложению DI scope:
IModelSettingsReader reader = scope.ServiceProvider.GetRequiredService<IModelSettingsReader>();
ServiceResult<ModelSettingsSnapshot> result = await reader.ReadAsync(DialogOwnerId.From(applicationOwnerId), effort: requestedEffort, ct: ct);
```

Общий API-ключ обязателен в KeySource=Shared, необязателен в Individual. В Shared provided индивидуальный ключ сохраняет priority; Individual при null не применяет общий fallback. Nullable override использует явные defaults приложения; пустой override не подменяется. IOptionsSnapshot фиксирует настройки на scope, reader копирует строки и лимиты до await. Для последующего scope доступны обновлённые options. Reader не записывает выбор; AgentRunner фиксирует ModelAccess и settings на обращение. Повторное чтение UI не является разрешением run. [Строгая configuration/migration boundary13](05-configuration-and-lifecycle.md).

## Каталог и бюджет

Адрес строится как base-prefix + `/v1/models`: пример даёт `/proxy/v1/models`. BaseAddress задаётся адресом сервера/префикса без `/v1` endpoint. client_version/query отсутствуют; Codex-native форма `{models:...}` не принимается за OpenAI-compatible `{object:"list",data:[...]}`. Каталог читается заново для каждого вызова; created не является версией, кеша/статического списка нет.

ModelCapabilities сохраняет ID, наличие metadata, ContextWindow/InputContextWindow/MaxOutputTokens, точные усилия/default, модальности, SupportedInApi и объявленные flags summaries/parallel tools/verbosity/websockets. Коллекции независимы и read-only. Raw metadata, описания, headers, URL и ключ не входят в снимок. Неизвестные поля не переносятся в публичный settings snapshot. Каталог предназначен для объявленных возможностей, не подтверждает Responses/compact или фактическую модель ответа.

Текущий `_to_model_metadata` codex-lb передаёт разрешённый входной бюджет в input_context_window; `_resolved_context_window` учитывает операторские overrides и backend ceiling. Validator проверяет threshold+reserve в long, без переполнения и повторного вычитания max_output_tokens. Например, 32000+4096 допустимы при 36096; 36095 даёт Validation. Отсутствующий/нулевой input budget, metadata или SupportedInApi=false — Unsupported. ContextWindow не заменяет неизвестный input budget. Точный неподдержанный model/effort — Unsupported; default effort не используется для исправления выбора.

Это проверка настроенных порога/запаса. Tokenizer, подсчёт полного запроса и opaque-состояния, проверка фактического input и compact остаются последующим этапам. Аутентификационная политика codex-lb может отключать обязательную проверку ключа; адаптер отправляет Bearer, а не меняет серверную политику или доказывает действительность ключа.

## Ошибки и проверки

401 → Unauthorized; 403 → Forbidden; прочие HTTP-отказы → Rejected. Неверный JSON/форма, duplicate ID или ошибочный тип metadata — Rejected. Пустой data — успешное чтение, выбор отсутствующей модели — Unsupported. Сообщения фиксированные, без raw body/headers/reason/Exception.Message. HttpClientLibrary захватывает не более 65536+1 байтов ошибки и явно различает полноту; адаптер не разбирает preview как envelope. По умолчанию content logging выключен, opt-in JsonStructure сохраняет только структуру через ILogger приложения.

Caller cancellation и неожиданные I/O/source исключения сохраняются. Уже установленный typed catalog failure передаётся тем же ServiceError до проверки поздней отмены; успешный каталог при отменённом caller не разрешает settings snapshot. Автоматического retry или смены ключа/модели нет. Ответы/потоки освобождает actual HttpClientLibrary.

Изолированные проверки используют настоящий DI и HttpApiClient, fake handler/local streams. Они не подтверждают живой codex-lb, upstream, серверную authentication policy или реальную поддержку модели. БД/SQL/hosting/сеть/внешние процессы — **Пропущено по указанию пользователя**. OpenSpec CLI validation не выполнена: CLI отсутствует в PATH, change не архивирован.
