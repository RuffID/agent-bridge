# Результаты этапа03

Дата: 2026-11-07. Статус: **blocked**, частичная реализация03.01. [Этап](main.md) · [Блокеры и расширение контракта](contract-blockers.md).

## Актуальное решение: sub2api не дорабатывается

Пользователь исключил доработку sub2api: адаптируется только AgentBridge к существующему серверу. Подготовленное ниже серверное предложение **отклонено** и сохранено как запись отвергнутого варианта. Его согласование, новые headers/pool revision/metadata и single-attempt enforcement не являются следующими шагами или условиями плана. Требование отсутствия клиентских retries ошибочно расширялось до общего server single-attempt; это расширение устранено из активного OpenSpec delta и инструкций.

В [текущем пути адаптации](contract-blockers.md) сохранены факты о текущем API и отдельно указаны клиентские границы. Existing ServerValidation допускает неизвестную стоимость opaque, но `ModelSelectionValidator` всё ещё требует положительный InputContextWindow; `ContextBudgetGuard` и сохраняемый `TurnModelSettings` используют известное окно. Поэтому переключить policy или подставить локальный порог как model budget нельзя. Existing passthrough также не является доказательством полной canonical preservation: shared prelude и compatibility normalizers сохраняются. Эти вопросы относятся к клиентской адаптации и оценке конкретного existing сценария, а не к запросу переделать sub2api.

Синхронизированы активные main/open-questions/подэтапы, навигаторы, current integration guide, локальный AGENTS и OpenSpec change. Предыдущие результаты ниже описывают предшествующий разбор, а не действующий план серверных работ. Нового C# транспорта в этой корректировке нет; budget/gateway guards сохранены, T01/T05 открыты,04 не начат. Код, main spec и исторические01–02 не менялись; сборки/тесты/live/CLI/Git не запускались. От пользователя не требуется согласовывать серверную доработку: этот вариант исключён.

Проверка корректировки:19 документов/solution,188 относительных ссылок без missing targets,3 JSON-примера, XML/UTF-8/EOL корректны. SHA25617 historical01/02/04/main-spec файлов и size/SHA25646 sub2api sources не изменились. Эта проверка статическая; новых результатов выполнения адаптера нет.

## Предшествующий разбор03: отвергнутый путь серверного расширения

Повторно проверены actual source paths sub2api0.2.19 и доступные versioned публичные Model/routes/release материалы. Все28 sub2api hashes evidence01 совпали; [новая опись46 sources](contract-source-evidence.json) включает дополнительные владеющие файлы. Проверка публичных материалов ограничена описанными источниками, не подтверждает deployment и не доказывает отсутствие частной спецификации.

Достаточного существующего контракта не установлено. Уточнены причины: capabilities intersection не привязан публичной revision к фактическому generation pool; ordinary/passthrough применяют account mapping по-разному; compact способен заменить модель ещё до первого HTTP; encrypted items могут удаляться при invalid-lineage и cross-mode failover. MaxAccountSwitches0 оставляет default3, pool retry count0 не исключает OAuth deadline retry. Internal raw-model observer существует, но public Responses/compact не дают его как обязательную processing metadata; admin usage не является допустимым источником клиента.

Подготовлено [адресное серверное предложение](server-contract-proposal.md): точные владельцы и функции, versioned opt-in headers/response fields, полный pool snapshot и field-level provenance, семантика гарантированного input budget, общий single-attempt enforcement, requested/sent/reported model, явные отказы и regression tests ordinary/passthrough/non-stream/compact. Предложение не согласовано и не реализовано в sub2api. Безопасные guard/settings сохранены; новых C# transport/settings paths нет. T01/T05 не закрыты. Новый блок результатов относится только к этому продолжению; прежние фактические прогоны ниже сохранены.

В этом продолжении изменены только документы, локальная инструкция адаптера и solution items. Исправлено устаревшее текущее руководство26: доступ/ordinary ID catalog уже работают, Unsupported относится к settings с неизвестным бюджетом и generation/compact. Исторические отчёты01–02 не переписывались. Compile-check/.NET и Go tests повторно не запускались: исполняемый код/build chain не менялись; прежние568 passed не являются новым прогоном или приёмкой sub2api. CLI OpenSpec, Git, проектные скрипты, live gateway/OAuth, host, БД, Docker и deployment не выполнялись. Этап04 не начат; новая поставка/baseline/frozen DLL не затрагивались.

Статическая проверка документов этого продолжения и сохранности исходников описана в конце файла. Она не заменяет strict CLI validation, compile-check или выполнение тестов.

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

## Статическая проверка продолжения03

Проверены19 новых/изменённых документов и solution:187 относительных ссылок без отсутствующих targets, XML решения и включение всех8 stage03 файлов. JSON evidence и3 синтетических JSON-примера предложения разбираются; это проверка синтаксиса, не server fixtures или transport acceptance. UTF-8 прочитан строгим decoder без U+FFFD/четырёх question marks; русский текст просмотрен без mojibake. Сохранены LF Markdown/JSON и CRLF solution, без добавления BOM.

Повторная SHA256/size сверка46 файлов sub2api совпала с новой описью. SHA25617 файлов исторических01–02, незапущенного04 и main spec до/после продолжения совпадают. Это проверка конкретных файлов, не Git revision, deployment, runtime или полная опись соседнего репозитория. Сохранность frozen kit/consumer DLL в этом продолжении отдельно не проверялась; записи/пересборки этих файлов не выполнялись. Статусы03.01–03.03/этап03 согласованы как blocked,04 остаётся not-started; все T01–T05 открыты. Новое предложение требует отдельного согласования серверных работ либо эквивалентных versioned доказательств, после чего остаются обязательные adapter/live проверки.
