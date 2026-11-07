# Результаты этапа03

Дата: 2026-11-07. Статус: **blocked**, частичная реализация03.01. [Этап](main.md) · [Блокеры и расширение контракта](contract-blockers.md).

## Реализовано

Существующий Sub2Api bundle получил отдельные internal access resolver, ordinary ModelCatalog и ModelSettingsReader вместо четырёхпортового PendingConnector. Facade передаёт прежнюю app-owned HTTP factory в тот же composition path. Не добавлены параллельный способ подключения, public transport API, протокольный framework, скрипты или подготовка artifacts для обычных тестов.

Individual source scoped; индивидуальный ключ имеет приоритет, только null в Shared допускает общий. Ошибка/empty/непригодный ключ не запускают fallback; ReadWithAccessAsync использует pinned доступ без повторного source I/O. Primitive selection сохраняется до ожидания источника. Safe Options pipeline не публикует значения.

Каталог GET server-prefix + /v1/models через actual HttpClientLibrary использует per-request Bearer и GenerationTimeout на send/body. Принимаются root/prefix и один конечный /v1; неоднозначные endpoint/encoded route/двойной /v1 отклоняются. No retry/cache/смены модели/ключа. App HttpClient/handlers не освобождаются адаптером; response/content/streams освобождает actual library. 401/403 дают Unauthorized/Forbidden, прочие HTTP-отказы Rejected; caller/локальный Timeout/primary+cleanup различаются. Диагностика использует existing ILogger pipeline, без raw bodies/headers/секретов, error capture64 КиБ.

Ordinary каталог подтверждает только exact model IDs. Неизвестные metadata, включая похожий input_context_window, не подменяют согласованный контракт. Budget/output limit=null, efforts/modalities пусты, flags=false/HasMetadata=false. Malformed envelope/duplicate ID/properties, invalid JSON/UTF-8 отклоняются. JsonElement требует проверки UTF-8 также неизвестных полей. Settings сохраняет existing ModelSelectionValidator и отказывает Unsupported при недоказанном бюджете T01.

## Заблокировано

T01 не закрыт: в текущем публичном контракте нет доказанного входного бюджета и provenance возможностей для выбираемого generation pool. T05 не закрыт: server retries/opaque normalization, compact mapping/fallback и model echo не дают требуемых гарантий. Sub2ApiModelGateway явно Unsupported до HTTP/factory/callback; JSON/SSE/compact транспорт **не реализован**. Direct gateway/custom settings не обходят guard. Canonical input/legacy continuation не меняются; обычная генерация не заменяет compact.

[Конкретные источники и необходимое расширение](contract-blockers.md). T02 live/tool chain, T03 provenance04, T04 новая поставка/ABI также остаются открытыми. ContextBuilder/ContextCompactor/IDialogContextWriter, история, persistence, restart и codex-lb не изменялись. Этап04 не начинался.

## Проверки и команды

Из `D:\Media\User\source\repos\agent-bridge` перед сборкой прочитаны три конкретных test csproj и все транзитивные ProjectReference20 проектов, ancestor Directory.Build.props/targets/NuGet/lock inputs, global.json. Custom Exec/hooks не обнаружены; HTTP Directory.Build.props влияет только на его собственные tests. Assets/пакеты уже доступны: restore не выполнялся, сеть не использовалась. GeneratePackageOnBuild=false исключает упаковку EFCoreLibrary.

Для каждого конкретного проекта:

```text
tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj
tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj
tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj
```

```powershell
dotnet build <project> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false -v:q
```

Все финальные compile-check: Exit0,0 warnings/errors. Сборка Sub2Api test project также компилирует затронутые Sub2Api/Integration проекты. Первая сборка имела18 xUnit1051 warnings: пропущенные test cancellation tokens исправлены, suppression нет.

Дополнительно той же build-командой отдельно собраны `adapters/AgentBridge.Sub2Api/AgentBridge.Sub2Api.csproj` и `adapters/AgentBridge.Integration/AgentBridge.Integration.csproj`:0 warnings/errors. После финальной проверки стиля Sub2Api test project пересобран, полный98-case набор повторён без падений/пропусков.

```powershell
dotnet test --project tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --no-progress
dotnet test --project tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --no-progress
dotnet test --project tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --filter-class '*IntegrationRegistrationTests' --filter-not-trait 'Dependency=Database' --no-progress
```

| Финальный набор | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Sub2Api: existing27 + новые71 | 98 | 0 | 0 |
| CodexLb: полный isolated suite | 414 | 0 | 0 |
| Прежний facade: IntegrationRegistrationTests | 56 | 0 | 0 |
| Всего | **568** | **0** | **0** |

Новые cases проверяют доступ, scope/pinning/selection, неизвестные capabilities/budget, exact alias/case, equality/overflow на synthetic catalog, последовательные запросы без cache, root/prefix URI, 401/403/429/5xx/оба error envelopes/oversize/malformed, invalid UTF-8 в известном и неизвестном поле, caller до headers и при success/error body, deadline на тех же стадиях, cleanup primary/secondary, resource ownership и safe logger channels. Gateway tests подтверждают только безопасный отказ, неизменность canonical input и отсутствие HTTP/callback. Shared tests не стали transport acceptance.

CodexLb regression сохраняет JSON/SSE/compact/tools, canonical/legacy continuation, output ordering/terminal/partial/EOF, awaited callbacks/failures, UTF-8/JSON, caller/deadline и cleanup/ownership. Это доказательство прежнего коннектора, не реализации sub2api.

### Неуспешные проверки до исправлений

Первый test run:83 cases,80 passed,3 failed,0 skipped. Ownership probe без Bearer выявил null dereference только в fake handler. Два success-body timing cases не достигали read gate: пустой MemoryStream объявлял Content-Length=0; один gate ждал10 секунд и завершился timeout. Исправлены fixture handler и non-seekable gated stream; pending task всегда отменяется и await в finally. После исправления83/83 passed.

Дополнительная регрессия `--filter-method '*InvalidUtf8OrJsonIsRejected'`:3 cases,2 passed,1 failed. Невалидный UTF-8 неизвестного поля принимался production reader из-за ленивого JsonElement decoding. Исправлена проверка полного JSON text перед проекцией. Адресный повтор3/3 passed; затем расширенный финальный набор98/98. Указанные неудачные прогоны не скрыты итоговым green. Зависаний вне описанного10-секундного fixture gate и других нестабильных прогонов не наблюдалось.

## Совместимость и границы доказательств

- Source: прежний source-linked transport probe и facade consumers компилируются без изменения их исходников; это не rebuild внешнего consumer против fresh kit.
- PE/API: existing tests сравнивают текущую поверхность с embedded текстовым эталоном. Старые CodexLb public types/assembly ownership сохранены. Исходники CodexLb/shared protocol не изменены.
- Old→new ABI: resolution frozen неперекомпилированных DLL против новой поставки не проверялся. Baseline kit/frozen consumers не использовались, не пересобирались и не перезаписывались; их сохранность в этом запуске отдельно не заявляется.
- Runtime: только Windows isolated DI/actual HttpClientLibrary с fake handlers/local streams. Нет live HTTP/OAuth, provider DB, приложения/host, Docker, deployment, новых DLL kits или других RID. Ordinary tests не зависят от AppData/kit/готового artifacts-комплекта.

Документы/AGENTS/навигаторы/solution items/OpenSpec tasks обновлены, исторические01–02 и main spec сохранены. Статически проверены137 относительных ссылок без broken targets, наличие всех6 stage03 Markdown solution items, XML решения и41 UTF-8 файлов без U+FFFD/четырёх question marks. Сохранены LF исходников/Markdown и CRLF решения; русский текст просмотрен без mojibake. Изменённый ConnectorSelectionTests18114 байт, Integration7118; все новые C# файлы меньше20000 байт (максимум CatalogTests12442). CLI OpenSpec/strict validation и Git не выполнялись. Stage03 остаётся blocked; переход к04 не выполнен.
