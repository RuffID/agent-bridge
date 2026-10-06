# Стандартная регистрация AgentBridge

Actual API находится в `AgentBridge.Integration.dll`, namespace `AgentBridge.Integration`:

```csharp
IServiceCollection AddAgentBridge(this IServiceCollection services,
    IConfiguration configuration, Func<IServiceProvider, HttpClient> httpClientFactory);
```

Конфигурация содержит разделы `AgentBridge` (Agent/Retention/Compaction), `CodexLb`, `Database`. Можно передать выбранный раздел приложения с такой внутренней структурой. Библиотека не регистрирует IConfiguration и не открывает файлы. Existing [required options13](05-configuration-and-lifecycle.md) и стандартные Configure/PostConfigure/reload сохраняются; отсутствие ключа не исправляется предшествующим Configure. Options проверяются при получении либо явном `IStartupValidator.Validate` до операций; host необязателен.

## Обязательные зависимости приложения

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

[Public facade tests](../../tests/AgentBridge.Persistence.EfCore.Tests/IntegrationRegistrationTests.cs) используют in-memory configuration и ValidateScopes/ValidateOnBuild без host. Actual DI/EF metadata/HTTP library отделены от fake handler/local stream, отсутствующей БД/native/runtime. [Simple consumer](../../tests/Delivery/Consumer/SimpleRegistration.cs) и [advanced consumer](../../tests/Delivery/Consumer/UsageRegistration.cs) компилируются как source; methods не исполняются. External binary kits/MSSQL/три RID относятся к15/16, runtime/provider/live — к17–19. [Results14](<../Plans/AgentBridge Audit Remediation/14-simplified-registration.md#результаты>).
