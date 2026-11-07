# Стандартная регистрация AgentBridge

Actual API находится в `AgentBridge.Integration.dll`, namespace `AgentBridge.Integration`:

```csharp
IServiceCollection AddAgentBridge(this IServiceCollection services,
    IConfiguration configuration, Func<IServiceProvider, HttpClient> httpClientFactory);
```

Конфигурация содержит разделы `AgentBridge` (Agent/Retention/Compaction), `Database` и выбранный `CodexLb` (default) либо `Sub2Api`. Можно передать выбранный раздел приложения с такой внутренней структурой. Библиотека не регистрирует IConfiguration и не открывает файлы. Existing [required options13](05-configuration-and-lifecycle.md) и стандартные Configure/PostConfigure/reload сохраняются; отсутствие ключа не исправляется предшествующим Configure. Options проверяются при получении либо явном `IStartupValidator.Validate` до операций; host необязателен.

## Выбор коннектора и граница sub2api

Без нового вызова сохраняется codex-lb. Явный выбор выполняется в том же namespace до facade:

```csharp
services.AddAgentBridgeSub2ApiConnector();
services.AddAgentBridge(configuration, httpClientFactory);
// Альтернатива для отдельной composition: AddAgentBridgeCodexLbConnector().
```

Sub2ApiOptions и Sub2ApiKeySourceMode находятся в AgentBridge.Sub2Api.Configuration. Раздел Sub2Api имеет BaseAddress, Model, ReasoningEffort, KeySource, SharedApiKey, GenerationTimeout, CompactTimeout; все необходимые значения задаются явно, SharedApiKey допускается отсутствующим только при Individual. Фиктивный CodexLb не требуется. Scoped доступ/ordinary каталог/settings работают через HttpClientLibrary; индивидуальный ключ приоритетен, ошибка не разрешает fallback. Явный AgentBridge:Compaction:BudgetPolicy=ServerValidationWithUnknownWindow читает manifest и проверяет объявленные exact ID/effort/API-доступность, сохраняя неизвестное окно и серверную проверку. Это не provenance pool и не guaranteed budget; прежние политики при unknown дают Unsupported. [Бюджетный контракт и v1/v2 snapshot](<../Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/client-adaptation.md>) не изменены работой04.01. Generation выполняет один POST с полным canonical request без previous_response_id/legacy turn-state и читает JSON/SSE по Content-Type, включая SSE для non-stream. Incoming continuation отклоняется до factory; returned response id не создаёт anchor. Canonical гарантия относится к границе AgentBridge; server transformations допустимы без обещания upstream сохранности/identity. Awaited callbacks, partial/terminal, caller/deadline и primary/cleanup сохраняются. **Compact Unsupported до HTTP** по T05.

[Результаты03](<../Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/results.md>) различают implementation и ограничения сервера. Доработка sub2api исключена; version/profile/revision поля не являются Sub2ApiOptions или зависимостью реализации. T01–T05 открыты.

На04.01 добавлены ModelConnectorProvenance и optional IModelContextGateway обоих встроенных коннекторов. Отдельный marker связывает connector/dialog/owner/agent/endpoint/access, не содержит ключа и не отправляется upstream. AgentRunner проверяет каждый opaque источник до Begin и между шагами; standalone ContextCompactor проверяет историю перед маркировкой, direct gateway — marker до HTTP. Одинаковая модель не разрешает чужое/неизвестное opaque-состояние. Для прямого opaque запроса trusted приложение явно переносит ConnectorProvenance из родного отчёта через WithConnectorProvenance; метка всего запроса означает, что приложение проверило все его источники. Это защита от смешивания доверенных данных, не авторизация. Новый opaque provider/input без метки отвергается, обычный текст допустим. Custom gateway без новой capability сохраняет прежний путь.

EF сохраняет один новый JSON wrapper отчёта с canonical envelope и отдельными connector/binding, без reader старых диалогов и миграции. Output и continuation сохраняются отдельно, публичные сигнатуры и физическая схема не менялись. Поддержка старых диалогов обоих коннекторов отменена. [Результаты04](<../Plans/sub2api-connector-2026-11-06-(in-progress)/04-dialog-state/results.md>):04.01 completed изолированно; actual facade/runner/Sub2Api/persistence ports проверены вместе для нескольких turns/tools, сериализованного restart с новым DI, pinned settings/access и отказов/recovery без replay. 1660 isolated cases прошли, включая16 новых04.02. Реальная БД не запускалась;04.02 остаётся verification до provider restart04.02.04.

Автоматический compact default Sub2Api gateway отключён через IModelGateway.AutomaticCompactionEnabled=false. Достижение локального threshold продолжает полный request через отдельный generation guard, без compact dispatch/save или обрезания. Known budget сохраняет прежний локальный отказ, unknown window — явную server validation; HTTP-ошибка не вызывает retry. Ручной/force compact остаётся Unsupported. Это не включение непроверенного compact и не новый конфигурационный флаг. [Условия последующего включения и риск удаления окна при recovery](../Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/03.03-compaction.md) сохраняют T05 открытым.

Повтор selector того же коннектора — no-op до/после facade. Другой выбор, включая смену default после facade, отклоняется до I/O и изменений второго bundle. Известные прямые CodexLb gateway/catalog/access/settings registrations нельзя смешивать с выбором Sub2Api перед facade. Произвольные factory/instance overrides приложения не исполняются для проверки их реализации и сохраняются. Direct AddCodexLb* остаются низкоуровневыми добавляющими регистрациями; не используйте их как selector после facade. Последующие app Add/Replace сохраняют стандартную ответственность приложения за согласованность.

Оба адаптера компилируют единые internal механизмы [Responses](../../adapters/AgentBridge.Responses/AGENTS.md) через Compile/Link. Gateway/options/catalog/identity, bound legacy continuation, lifecycle и typed ошибки codex-lb остаются отдельными. Новая protocol DLL не добавляется; AgentBridge.Sub2Api.dll добавлена в source closure Integration и должна быть учтена новой поставкой05. Старый kit не обновлялся.

Нормы и текущие требования — [agent-runtime](../../openspec/specs/agent-runtime/spec.md); полное подключение sub2api пока описано только [будущим change](../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md). Его требования не объявляются реализованными после одной DI-подготовки.

## Обязательные зависимости приложения

До фасада приложение явно подключает модуль выбранной БД: `AddAgentBridgeSqlServer`, `AddAgentBridgeSqlite` либо `AddAgentBridgePostgreSql` из соответствующего `AgentBridge.Persistence.<Provider>`. Общий EF-адаптер и facade не подтягивают остальные драйверы. Missing/wrong/ambiguous module отклоняется options validation; [состав и API](28-provider-modules-and-shared-delivery.md).

`ILoggerFactory` регистрируется **до** фасада; отсутствие descriptor немедленно отклоняется без изменений контейнера. `AddLogging` либо logging builder приложения предоставляет factory; sinks/Serilog, levels, redaction, файловый путь/rotation/retention принадлежат приложению. Наличие factory не доказывает наличие sink. Console/custom factory не требует file path. Фасад не создаёт собственный logger.

HTTP factory обязательна и вызывается внутри scope только при разрешении default pipeline. Она возвращает **принадлежащий приложению** HttpClient; фасад и actual HttpApiClient его не освобождают. У приложения остаются timeout, handler lifetime, disposal, отсутствие retry/смены аккаунта. Для стандартного IHttpClientFactory возвращаемый client можно регистрировать scoped в **расширении приложения**:

```csharp
public static IServiceCollection AddApplicationAgentHttp(this IServiceCollection services)
{
    services.AddHttpClient("AgentBridgeCodexLb", client =>
        client.Timeout = Timeout.InfiniteTimeSpan);
    services.AddScoped<HttpClient>(provider =>
        provider.GetRequiredService<IHttpClientFactory>().CreateClient("AgentBridgeCodexLb"));
    return services;
}
```

После этого composition root вызывает группу приложения и библиотечный фасад:

```csharp
builder.Services.AddApplicationAgentHttp();
builder.Services.AddAgentBridgeSqlServer(); // namespace AgentBridge.Persistence.SqlServer
builder.Services.AddAgentBridge(builder.Configuration,
    provider => provider.GetRequiredService<HttpClient>());
```

Здесь DI приложения освобождает scoped client, IHttpClientFactory управляет handlers; callback фасада получает заимствованный объект. `AddApplicationAgentHttp` — пример кода приложения, не API AgentBridge. При custom client/factory приложение обеспечивает аналогичное владение. Default pipeline использует existing AddCodexLbResponses и HttpClientLibrary, безопасную диагностику None и предел error body64 КиБ; собственных HTTP клиентов, host или I/O при регистрации нет.

## Ключи и business extension points

Shared без app source использует внутренний null source; пустые пользовательские классы не нужны. SharedApiKey обязателен. Individual требует зарегистрировать `IIndividualModelKeySource` **до AddAgentBridge**; singleton options validator проверяет наличие descriptor, не разрешает scoped источник из root. Источник создаётся только в runtime scope. В Shared supplied individual key имеет приоритет; пустой/неверный ключ, отказ источника и HTTP не разрешают общий fallback. В Individual даже null не разрешает общий ключ. После фасада нельзя включить Individual одним source registration: создайте composition в документированном порядке до BuildServiceProvider.

Ordered `IContextProvider` регистрируются до или после фасада; scoped ContextBuilder перечисляет их в DI-порядке без сортировки. Пустой набор сохраняет обычную историю/window/tail/new input. Custom ContextBuilder также допустим. `AddAgentBridgeTool<THandler,TValidator>` до/после фасада оставляет handler/validator scoped, отдельный async scope на invocation; registry/executor singleton не удерживают business state.

Defaults для public ports, HttpApiClient, counter, inspector, ContextBuilder и сценариев добавляются через TryAdd: заранее зарегистрированный contract сохраняется; последующий явный Add/Replace выбирается обычным правилом DI. Это single-service override; IEnumerable перечисляет все явные registrations. Compatibility port остаётся optional, неподтверждённое opaque switch отклоняется. Configure выполняется до PostConfigure по штатному Options pipeline независимо от порядка подключения фасада. Не заменяйте scoped зависимости singleton, включайте ValidateScopes/ValidateOnBuild.

Повтор **с теми же объектами IConfiguration и HTTP delegate** — no-op, без повторного binding/modules/provider registration. Другие arguments явно отклоняются: новый app/configuration требует нового контейнера. Reload исходной IConfiguration применяется штатно; это не замена регистрационного объекта.

## Состав и lifetimes

| Группа | Lifetime / поведение |
| --- | --- |
| Options | Existing standard Options pipeline/required validators13 |
| EF persistence | Scoped DbContext/base repositories/session/read ports/scenario UoW; existing EFCoreLibrary |
| HTTP/catalog/access/settings/gateway | Scoped actual CodexLb/HttpClientLibrary; borrowed app HttpClient |
| Diagnostics, registry/executor, counter/inspector, TimeProvider | Singleton defaults; app overrides сохраняются |
| ContextBuilder/compactor/runner/settings/cleanup/model guard | Scoped; providers берутся в app scope |
| ContextBudgetGuard | Transient |
| Business handler/validator | Scoped per invocation через registry/executor |

Фасад не строит ServiceProvider, не включает maintenance/миграции/initializer/background services, не запускает cleanup/compact/HTTP/БД/files, не создаёт auth policies/endpoints/scheduler. Provider migrations DLL поставляется отдельно для выбранной БД. Authorization owner/agent, business services и внешние действия принадлежат приложению.

## Проверки

[Этап02](<../Plans/sub2api-connector-2026-11-06-(in-progress)/02-composition/results.md>) проверил 27 новых isolated cases (DI/baseline/API/shared), 414 codex-lb и 56 прежних facade cases; compile-check трёх projects0 warnings/errors. Baseline сохранён, legacy constructors/types/signatures сверены PE/API. Source-linked probe не заменяет external source compilation/full old→new ABI resolution frozen consumers, новую поставку и live OAuth. T01–T05 открыты; strict CLI OpenSpec не выполнялся.

[Public facade tests](../../tests/AgentBridge.Persistence.EfCore.Tests/IntegrationRegistrationTests.cs) используют in-memory configuration и ValidateScopes/ValidateOnBuild без host. Actual DI/EF metadata/HTTP library отделены от fake handler/local stream, отсутствующей БД/native/runtime. [Simple consumer](../../tests/Delivery/Consumer/SimpleRegistration.cs) и [advanced consumer](../../tests/Delivery/Consumer/UsageRegistration.cs) компилируются как source; methods не исполняются. External binary kits/MSSQL/три RID относятся к15/16, runtime/provider/live — к17–19. Results14 (история Git).
