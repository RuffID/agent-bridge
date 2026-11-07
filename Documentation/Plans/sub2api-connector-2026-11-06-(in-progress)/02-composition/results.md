# Результаты этапа02

Дата: 2026-11-06. [Этап](main.md) · [DI](02.01-connector-selection.md) · [Shared Responses](02.02-shared-responses.md).

## Реализованная граница

Integration выбирает один connector bundle; старый AddAgentBridge/configuration/default и прямые AddCodexLb* сохранены. Собственные Sub2ApiOptions не зависят от CodexLbOptions. Sub2ApiPendingConnector возвращает Unsupported для access/catalog/settings/generation/compact до реализации03. Ошибки/отмена не дают успешных заглушек, не вызывают другой коннектор и не запускают callbacks. Custom ports остаются ответственностью приложения, TryAdd/Options pipeline и scoped lifetimes сохранены.

Повтор selector того же kind идемпотентен. После facade зафиксирован и неявный default; смена запрещена. Повтор facade требует те же object references configuration/factory. Известные прямые CodexLb implementations несовместимы с Sub2Api и проверяются до изменения facade. Произвольная factory/instance приложения не исполняется для угадывания её реализации. Direct AddCodexLb* остаются низкоуровневыми добавляющими регистрациями, а не selector API; после facade приложение отвечает за свои явные Add/Replace.

Общая папка Responses содержит только source-linked internal JSON/SSE механизмы. Это исключает новую публичную transport-поверхность и отдельную обязательную protocol DLL. Новый **AgentBridge.Sub2Api.dll**, напротив, входит в source ProjectReference closure Integration; прежний kit не обновлялся и новой проверенной delivery-поставкой не является. Конкретные gateway/options/catalog/identity, lifecycle/typed errors/continuation остаются у адаптеров. Whole gateway не копировался; shared фреймворк не создавался.

## Baseline и совместимость

Перед правками read-only SHA256/size проверка подтвердила все **127** entries preservation.json и **112** entries kit/delivery.manifest.json; также прочитаны hashes двух frozen DLL. После изменений тест подтвердил обе описи и все **6** hashes baseline-evidence.json (включая frozen DLL). Kit/frozen consumers не перезаписывались и не пересобирались.

Frozen original SHA256: `5cc6345e2f63f7130a4d628b2c2aa3826abe95d733f68e9add0fcde69c79e153`. Frozen surface: `03c009905a668fc9d42515fe807d3cc61b8f0f075fa66c2869c5f036aea25faf`.

- **Source:** текущий legacy transport probe без правок включён Compile/Link в новый test project и собран; прежние Simple/Strict/Usage/SqlServer consumers компилируются existing persistence tests. Методы probes не исполнялись. Это не external DLL consumer против fresh kit.
- **PE/API:** PeSurfaceReader сравнивает baseline и новые compile-check AgentBridge/CodexLb/Integration без загрузки target DLL. Вместо raw signature tokens декодируются имена типов с assembly names, generic формы/constraints, calling convention, static/instance/access/virtual flags, constructors/methods/accessors, parameter names/flags/defaults, public/protected fields/enum values, base types/interfaces и собственные assembly name/version/culture/public key. Все старые записи сохранены; additions — selector extensions и отдельный Sub2Api API. Старые13 CodexLb public types остаются в своей сборке.
- **ABI:** это не полный verifier CLR metadata: custom attributes/nullable, identities всех referenced assemblies и resolution TypeRef/MemberRef/MethodSpec двух неперекомпилированных consumers против нового delivery closure отдельно не проверены. T04/05.02 открыты. Нельзя выводить old→new ABI из успешной компиляции probe или API subset comparison.
- **Runtime:** выполнены isolated Windows DI/transport tests внутри test process с fake handlers. Загрузка старого binary consumer, fresh DLL kit, другие RID, реальная persistence и server/OAuth runtime не проверены.

## Команды и результаты

Все команды из `D:\Media\User\source\repos\agent-bridge`. Перед restore/build прочитаны конкретные проекты и ProjectReference chain (ядро, transport, EFCoreLibrary/maintenance/provider/migrations), global.json, применимые Directory.Build.props/targets, NuGet/lock inputs. Custom Exec/hook не обнаружены; HTTP Directory.Build.props задаёт output только своему test project. Использован существующий локальный cache; сеть для restore не включалась. GeneratePackageOnBuild=false исключает упаковку EFCoreLibrary.

Для каждого из трёх **конкретных** проектов:

```text
tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj
tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj
tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj
```

выполнены команды со своим путём вместо `<project>`:

```powershell
dotnet restore <project> --source C:\Users\Spike\.nuget\packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false
dotnet build <project> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false
```

Все три финальных compile-check: Exit0, **0 warnings / 0 errors**. Первые две попытки нового test chain выявили отсутствующий generic `where T : class` и `using Xunit`; исправлены до выполнения тестов. После последующих изменений affected проекты пересобраны.

```powershell
dotnet test --project tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --no-progress
dotnet test --project tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --no-progress
dotnet test --project tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --filter-class '*IntegrationRegistrationTests' --filter-not-trait 'Dependency=Database' --no-progress
```

| Набор | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Новый: DI20, baseline/API4, linked protocol3 | 27 | 0 | 0 |
| CodexLb: полный existing isolated suite | 414 | 0 | 0 |
| Прежний public facade: IntegrationRegistrationTests | 56 | 0 | 0 |
| Всего отдельных cases | **497** | **0** | **0** |

CodexLb набор проверяет HTTP errors/allowlists/log secrets, JSON/SSE/compact, invalid UTF-8/JSON, canonical unknown/opaque, legacy anchors, callbacks sequential/awaited, partial/EOF, caller/deadline, combined/standalone cleanup failures и app-owned HTTP/handlers. Новый набор проверяет fail-fast selection/repeats/arguments, отсутствие CodexLb binding у Sub2Api, app overrides/PostConfigure, scopes/source presence, actual linked writer/output/framing через HttpClientLibrary. Изолированные tests не запускают host, SQL или процессы.

Статически проверены155 Markdown-ссылок в затронутых документах, XML проектов/решения и наличие всех solution items. UTF-8 прочитан строгим decoder без U+FFFD/четырёх question marks; русский текст просмотрен без mojibake. Новые/изменённые C# файлы остаются меньше20 000 байт (Integration6213→7099; gateway13282→13311; максимальный новый test file18457). Existing endings сохранены, включая CRLF решения и LF транспортных исходников. Main spec и baseline kit не изменены.

## Оставшиеся ограничения

T01–T05 не закрыты. Sub2Api каталог/settings/access/generation/compact и routing semantics относятся к03, provenance/restart/no-replay — к04, external source consumers/full ABI/delivery/RID и live OAuth — к05. context_window не равен доказанному InputContextWindow; полный рабочий input не обещает работоспособную OAuth цепочку. OAuth удаляет max_output_tokens; server retries, reasoning transformations и compact fallback не отключены клиентским отсутствием retry.

CLI OpenSpec/strict validation не запускался: текущий запрос не разрешает проектные scripts/CLI. Это обязательная отдельная проверка05.01; статическая сверка delta/tasks не заменяет её. Main spec не синхронизирована, change не архивирован. Не запускались приложение/hosting/live HTTP/OAuth/БД/Docker/deployment/Git; исходники соседних репозиториев не изменялись. После02 работа остановлена.
