# Исходный контракт AgentBridge до подключения sub2api

Дата фиксации: 2026-11-06. [Подэтап01.02](01.02-legacy-contract.md) · [Методика и результаты проверки](compatibility-checks.md) · [Hashes исходников](source-evidence.json).

Контракт ниже относится к текущим исходникам и совпавшей с ними поставке. Сохранение этих точек входа — требование будущего рефакторинга; обновлённых DLL ещё нет. Compile-only consumer не исполняет регистрации, конструкторы, HTTP, tools или БД.

## Сохраняемые assembly identities

Выбран полный SDK kit SQL Server / win-x64 / net10.0 / Release из `artifacts/delivery-tests/current/sdk/SqlServer/win-x64`. Сопоставлены источник fingerprint, manifest hashes/size всех112 файлов и PE AssemblyDefinition. PE читались через System.Reflection.Metadata / PEReader; целевые assemblies не загружались для исполнения.

| Assembly | AssemblyVersion | Culture / public key | Public top-level types | SHA256 DLL |
| --- | --- | --- | --- | --- |
| AgentBridge | 1.0.0.0 | neutral / unsigned | 169 | См. baseline-evidence.json |
| AgentBridge.CodexLb | 1.0.0.0 | neutral / unsigned | 13 | 3d66eb87f71a9e8348e2488fad379ac8e2e2014ca51b303e5d0d244696b46e88 |
| AgentBridge.Integration | 1.0.0.0 | neutral / unsigned | 1 | См. baseline-evidence.json |
| AgentBridge.Persistence.EfCore | 1.0.0.0 | neutral / unsigned | 57 | См. baseline-evidence.json |
| AgentBridge.Persistence.SqlServer | 1.0.0.0 | neutral / unsigned | 2 | См. baseline-evidence.json |
| AgentBridge.Persistence.Migrations.SqlServer | 1.0.0.0 | neutral / unsigned | 3 | См. baseline-evidence.json |

Основание: сохранённые `kit/delivery.manifest.json`, `pe-metadata.json`, `preservation.json`; переносимая выжимка — [baseline-evidence.json](baseline-evidence.json). Counts — опись этого набора, не достаточный критерий ABI. AssemblyName ядра задан явно в [agent-bridge.csproj](../../../../AgentBridge/agent-bridge.csproj), имена адаптеров следуют csproj; TargetFramework всех трёх основных проектов net10.0. Отсутствие явного Version в csproj не заменяет фактическое PE значение.

Integration содержит AssemblyReference на AgentBridge.CodexLb и общий persistence adapter; исходник [Integration.csproj](../../../../adapters/AgentBridge.Integration/AgentBridge.Integration.csproj) подтверждает ProjectReference. HttpClientLibrary входит в CodexLb dependencies. Поэтому separate Sub2Api csproj не доказывает возможность удалить CodexLb.dll из facade closure. Сохраняются DLL/XML ядра, legacy adapter, facade, выбранного provider/migrations и dependencies выбранного комплекта. Другие provider/RID не считаются проверенными этой фиксацией.

Внешние локальные зависимости также сохранены целиком в kit. Фактические Version/FileVersion и AssemblyVersion различаются:

| Dependency | Текущий csproj Version | AssemblyVersion в сохранённом kit |
| --- | --- | --- |
| HttpClientLibrary | 0.0.0.6, FileVersion=0.0.0.6 | 1.0.0.0 (явно задан в csproj) |
| EFCoreLibrary | 0.0.9 | 0.0.9.0 |
| EFCoreLibrary.Maintenance | По manifest поставки | 1.0.0.0 |
| EFCoreLibrary.Maintenance.SqlServer | По manifest поставки | 1.0.1.0 |

Основание — сохранённый delivery.manifest.json и прочитанные [HttpClientLibrary.csproj](../../../../../work/HttpClientLibrary/HttpClientLibrary.csproj), [EFCoreLibrary.csproj](../../../../../work/EFCoreLibrary/EFCoreLibrary.csproj). В root/Documentation AGENTS ещё названы HttpClientLibrary0.0.0.5 и EFCoreLibrary0.0.5; эти сведения отстают от фактических исходников/поставки. Здесь baseline зафиксирован по владельцам, инструкции и соседние проекты не переписывались. Package version нельзя использовать вместо assembly identity.

## Facade и прямые регистрации

Все методы ниже public static, возвращают `Microsoft.Extensions.DependencyInjection.IServiceCollection`; первый аргумент — `this IServiceCollection services`. Сохраняются namespace, имя declaring type, overload, параметры, порядок, return type и static/instance характер.

| Declaring type | Сигнатура после services | Источник |
| --- | --- | --- |
| AgentBridge.Integration.AgentBridgeIntegrationExtensions | AddAgentBridge(IConfiguration configuration, Func<IServiceProvider, HttpClient> httpClientFactory) | [Facade](../../../../adapters/AgentBridge.Integration/AgentBridgeIntegrationExtensions.cs) |
| AgentBridge.CodexLb.Configuration.CodexLbConfigurationExtensions | AddCodexLbConfiguration(IConfiguration configuration) | [Configuration extensions](../../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbConfigurationExtensions.cs) |
| тот же type | AddCodexLbConfiguration(Action<CodexLbOptions> configure) | тот же исходник |
| AgentBridge.CodexLb.Configuration.CodexLbModelCatalogExtensions | AddCodexLbModelCatalog(Func<IServiceProvider, HttpClient> httpClientFactory, HttpClientLoggingOptions? loggingOptions = null) | [Catalog extensions](../../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbModelCatalogExtensions.cs) |
| AgentBridge.CodexLb.Configuration.CodexLbResponsesExtensions | AddCodexLbResponses(Func<IServiceProvider, HttpClient> httpClientFactory, HttpClientLoggingOptions? loggingOptions = null) | [Responses extensions](../../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbResponsesExtensions.cs) |
| AgentBridge.CodexLb.Configuration.CodexLbAuxiliaryExtensions | AddCodexLbAuxiliary(IConfiguration configuration) | [Auxiliary extensions](../../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbAuxiliaryExtensions.cs) |

Здесь IConfiguration — Microsoft.Extensions.Configuration; options — Microsoft.Extensions.Options; HttpApiClient/HttpClientLoggingOptions — HttpClientLibrary.Clients/Models; HttpClient — System.Net.Http. Defaults optional параметров важны для повторной source compilation, даже если в ранее скомпилированном IL аргументы уже подставлены. Extension-метод в другом namespace/assembly с тем же именем не сохраняет старую ABI ссылку на declaring type.

## Все 13 public types AgentBridge.CodexLb

| Namespace и type | Public construction / члены |
| --- | --- |
| Configuration.CodexLbConfigurationExtensions | Две регистрации из таблицы выше; static class |
| Configuration.CodexLbModelCatalogExtensions | Одна регистрация; static class |
| Configuration.CodexLbResponsesExtensions | Одна регистрация; static class |
| Configuration.CodexLbAuxiliaryExtensions | Одна регистрация; static class |
| Configuration.CodexLbOptions | Публичный parameterless ctor. get/set: string? BaseAddress, string? Model, string ReasoningEffort, ModelKeySourceMode? KeySource, string? SharedApiKey, TimeSpan GenerationTimeout, TimeSpan CompactTimeout |
| Configuration.CodexLbAuxiliaryOptions | Публичный parameterless ctor. get/set TimeSpan: MetadataTimeout, FileCreateTimeout, FileUploadTimeout, FileFinalizeTimeout, ImageTimeout |
| Configuration.ModelKeySourceMode | enum underlying Int32; Shared=0, Individual=1 |
| Models.CodexLbModelAccessResolver | ctor(IIndividualModelKeySource keySource, IOptionsSnapshot<CodexLbOptions> options); implements IModelAccessResolver |
| Models.CodexLbModelCatalog | ctor(HttpApiClient http, IOptionsSnapshot<CodexLbOptions> options); implements IModelCatalog |
| Models.CodexLbModelSettingsReader | ctor(IModelAccessResolver accessResolver, IModelCatalog catalog, IOptionsSnapshot<CodexLbOptions> options, IOptionsSnapshot<ContextCompactionOptions> contextOptions); implements IModelSettingsReader |
| Responses.CodexLbModelGateway | ctor(HttpApiClient http, IOptionsSnapshot<CodexLbOptions> options); implements IModelGateway |
| Responses.CodexLbServiceError | Наследует ServiceError; public int? HttpStatus, string? ApiType, Code, Param — только get. Конструктор **internal**, публичного ctor нет; нельзя требовать его появления или переезда type |
| Auxiliary.CodexLbAuxiliaryGateway | ctor(HttpApiClient http, IOptionsSnapshot<CodexLbOptions> options, IOptionsSnapshot<CodexLbAuxiliaryOptions> auxiliary); implements IModelAuxiliaryGateway |

Полный namespace в первом столбце начинается с `AgentBridge.CodexLb.`. Sources: [Models](../../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelAccessResolver.cs), [catalog](../../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelCatalog.cs), [settings](../../../../adapters/AgentBridge.CodexLb/Models/CodexLbModelSettingsReader.cs), [gateway](../../../../adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs), [error](../../../../adapters/AgentBridge.CodexLb/Responses/CodexLbServiceError.cs), [auxiliary](../../../../adapters/AgentBridge.CodexLb/Auxiliary/CodexLbAuxiliaryGateway.cs), [options](../../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbOptions.cs), [auxiliary options](../../../../adapters/AgentBridge.CodexLb/Configuration/CodexLbAuxiliaryOptions.cs), [enum](../../../../adapters/AgentBridge.CodexLb/Configuration/ModelKeySourceMode.cs).

### Public instance signatures

Все методы возвращают Task<ServiceResult<T>>, кроме сохранённых get/set. Названия параметров и optional defaults зафиксированы также XML/PE и compile-only probe.

| Type | Метод | T |
| --- | --- | --- |
| CodexLbModelAccessResolver | ResolveAsync(DialogOwnerId ownerId, CancellationToken ct = default) | ModelAccess |
| CodexLbModelCatalog | ReadAsync(ModelAccess access, CancellationToken ct = default) | ModelCatalogSnapshot |
| CodexLbModelSettingsReader | ReadAsync(DialogOwnerId ownerId, string? model = null, string? effort = null, CancellationToken ct = default) | ModelSettingsSnapshot |
| тот же | ReadWithAccessAsync(DialogOwnerId ownerId, ModelAccess access, string? model = null, string? effort = null, CancellationToken ct = default) | ModelSettingsSnapshot |
| CodexLbModelGateway | GenerateAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, Func<ModelStreamUpdate, CancellationToken, ValueTask>? onUpdate = null, CancellationToken cancellationToken = default) | ModelResponse |
| тот же | CompactAsync(ApplicationCallContext call, ModelRequest request, ModelAccess access, CancellationToken cancellationToken = default) | ModelResponse |
| CodexLbAuxiliaryGateway | ReadUsageAsync(ModelAccess access, CancellationToken cancellationToken = default) | ModelAuxiliaryResult |
| тот же | UploadFileAsync(ModelFileUpload file, ModelAccess access, CancellationToken cancellationToken = default) | string |
| тот же | GenerateImageAsync(ModelImageRequest request, ModelAccess access, CancellationToken cancellationToken = default) | ModelAuxiliaryResult |
| тот же | EditImageAsync(ModelImageEditRequest request, ModelAccess access, CancellationToken cancellationToken = default) | ModelAuxiliaryResult |

Порты, модели, ServiceResult/ServiceError, dialog owner/call context остаются в AgentBridge.dll: [IModelGateway](../../../../AgentBridge/Application/Ports/IModelGateway.cs), [IModelCatalog](../../../../AgentBridge/Application/Ports/IModelCatalog.cs), [IModelAccessResolver](../../../../AgentBridge/Application/Ports/IModelAccessResolver.cs), [IModelSettingsReader](../../../../AgentBridge/Application/Ports/IModelSettingsReader.cs), [IIndividualModelKeySource](../../../../AgentBridge/Application/Ports/IIndividualModelKeySource.cs), [IModelAuxiliaryGateway](../../../../AgentBridge/Application/Ports/IModelAuxiliaryGateway.cs). Нельзя переносить/переименовывать их как часть transport extraction или добавлять обязательный connector-аргумент в существующие ctor/methods. Полная PE и XML поверхность всех169 top-level core types сохранена вместе с DLL; таблица не сокращает её до перечисленных примеров.

## Конфигурация и ключи

Основание — CodexLbConfigurationExtensions, CodexLbOptions, CodexLbModelAccessResolver, CodexLbModelSettingsReader, ModelSelectionValidator и facade, ссылки выше.

- Старый facade читает переданный root/подраздел, содержащий соседние `AgentBridge`, `CodexLb`, `Database`; AddCodexLbConfiguration(IConfiguration) получает сам CodexLb section. Никакой новой секции/выбора для прежнего consumer не требуется. Default остаётся codex-lb.
- Обязательны явные BaseAddress, Model, ReasoningEffort, KeySource, GenerationTimeout, CompactTimeout. Рабочих transport defaults нет. Допустим HTTP(S) абсолютный адрес без userinfo/query/fragment; literal /v1 append сохраняется. Effort не проверяется локальным статическим списком.
- Shared требует непустой SharedApiKey; Individual допускает отсутствие общего ключа. Present shared key в любом режиме должен быть непустым и без whitespace/control. Сроки положительные, не выше диапазона таймера .NET.
- Индивидуальный источник вызывается и в Shared. Только null допускает shared fallback, только при Shared. Empty/whitespace/ошибка source/HTTP401 не разрешают переход на общий ключ. Источник получает DialogOwnerId и CancellationToken; ключ не попадает в safe snapshots/БД.
- Facade требует IIndividualModelKeySource, зарегистрированный **до** AddAgentBridge, если KeySource=Individual. Для Shared без app source регистрируется scoped null source. Direct AddCodexLb* требуют source приложения самостоятельно, fallback source не создают.
- Reader null model/effort берёт из options, explicit override проверяет точно, input budget/effort — по ModelSelectionValidator. ReadWithAccessAsync использует уже зафиксированный ModelAccess без повторного source I/O. Scoped options и pinned run access нельзя заменить singleton snapshots.
- Optional `CodexLb:Auxiliary` регистрируется отдельно: вызывающий передаёт сам subsection, задаёт все пять сроков. Facade не включает auxiliary автоматически. Usage/files/images сохраняются для codex-lb; для sub2api их поддержка не установлена.

## Overrides, lifetime и повторная регистрация

Основание — facade и direct extensions; [core Configuration](../../../../AgentBridge/Configuration/AGENTS.md), [tools registration](../../../../AgentBridge/Configuration/AgentBridgeToolsExtensions.cs), [tokenization](../../../../AgentBridge/Configuration/AgentBridgeTokenizationExtensions.cs).

| Путь | Действующее поведение, которое нужно сохранить |
| --- | --- |
| Facade | Проверяет наличие app ILoggerFactory. Переносит descriptors modules через TryAdd; IValidateOptions<> через TryAddEnumerable, EF IDbContextOptionsConfiguration<> добавляет в options pipeline. Предварительные custom gateway/catalog/resolver/settings/storage/context registrations сохраняются. ICollection<IContextProvider> остаётся в порядке регистрации. |
| Direct AddCodexLbModelCatalog | AddScoped для HttpApiClient, IModelAccessResolver, IModelCatalog, IModelSettingsReader; обычный Add, **не TryAdd**. Повтор добавляет descriptors; обычный GetRequiredService берёт последний, IEnumerable видит все. Нельзя обещать ему facade idempotency. |
| Direct Responses | Каждый вызов повторно вызывает catalog registration и AddScoped<IModelGateway,CodexLbModelGateway>. Auxiliary аналогично AddScoped<IModelAuxiliaryGateway,...> и добавляет options delegates. Configuration overloads дополняют Configure/validators pipeline. |
| Facade повтор | Сравнивает config и factory через ReferenceEquals. Те же **объекты** — no-op; другие references — InvalidOperationException. Равные значения/эквивалентный новый delegate недостаточны. Маркер singleton, без ServiceProvider и I/O. |
| Core defaults | ContextBuilder, ContextCompactor, AgentRunner, AgentSettingsService, ContextModelGuard, ExpiredDialogCleanup — scoped TryAdd. ContextBudgetGuard — transient. TimeProvider, token counter/content inspector, registry/executor — singleton defaults. Tool handler/validator создаются scoped для invocation; не переносить transport state в эти singleton services. |
| HTTP | Factory вызывается при scoped resolution HttpApiClient, не при AddAgentBridge. App владеет HttpClient/handlers, lifetime, timeout, disposal; HttpClientLibrary — request/response/stream cleanup. При создании client не выполнять I/O; не добавлять hidden retry/account switching/default mutable Authorization. Предел error capture=65536 bytes, logging error mode копируется при direct registration. |
| Logger / options | ILoggerFactory/provider/sinks задаёт app; facade не создаёт logging host. Options BindSafely/validation и startup validation сохраняются; IConfiguration не становится зависимостью runtime сервисов. Не строить временный ServiceProvider для нового выбора. |

Извлечение reader/writer в новый shared assembly допустимо позже только с сохранением legacy public types, signatures, type ownership и dependencies (либо явно проверенным type forwarding). Wrapper предпочтительнее необоснованного переноса; данный этап ничего не перемещает. Неизменная source signature сама по себе не подтверждает ABI или сохранение DI semantics.
