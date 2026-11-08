# Результаты05.01–05.02

Дата:2026-11-08. Статус: **05.01 completed,05.02 completed** в локальных границах. Этап05 — **verification**;05.03 **not-started**. [Навигатор](main.md), [сценарии delta](scenario-coverage.md).

## Что подтверждено

Прежнее source-подключение без бизнес-правок и новый выбор Sub2Api собираются из DLL вне репозиториев. Semantic API прежних трёх сборок сохранён. Все ссылки двух неперекомпилированных frozen DLL разрешены на новой SQL Server closure. Windows binary tests подтверждают actual DI, package graph, оба BPE словаря и Sub2Api JSON/SSE с полным контекстом через локальный handler. История старого JSON не поддерживается согласно принятому решению; сохранение API/ABI не означает совместимости данных.

Изменения production-кода на05.01–02 не потребовались. Добавлены compile-only Sub2ApiRegistration, отдельный metadata ABI verifier с synthetic controls и внешний binary Sub2Api test. В verifier выделены CLI signatures, caller context и generic constraints. Исправления подготовки verifier (native DLL в compiler references, caller/MethodImpl context, accessors и nested generic shape) не являлись дефектами ABI AgentBridge. Предупреждение xUnit о CancellationToken исправлено в новом тесте; финальные сборки без warnings.

## 05.01: изолированная регрессия

Перед запуском сверены concrete csproj, imports/Directory.Build/NuGet/locks и fixtures. Executable build hooks не обнаружены. Сборки конкретных проектов Debug выполнялись с:

```text
dotnet build <csproj> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false --nologo -v quiet
```

Проверены ядро, CodexLb, Sub2Api, Persistence.EfCore, Integration и пять соответствующих test projects —10 compile-check,0 warnings/errors. Нативный xUnit runner:

```text
dotnet <test-project>/artifacts/compile-check/Debug/net10.0/<test-assembly.dll> -trait- 'Dependency=Database' -noLogo -longRunning 30
```

| Набор | Passed | Failed / Errors / Skipped / Not run |
| --- | ---: | --- |
| Core |569|0 /0 /0 /0|
| Sub2Api |178|0 /0 /0 /0|
| CodexLb |422|0 /0 /0 /0|
| Persistence isolated |510|0 /0 /0 /0|
| Hosting |28|0 /0 /0 /0|
| Всего |1707|0 /0 /0 /0|

Hosting использует ранее разрешённый тестовый Generic Host без production-приложения/HTTP/БД. Database-trait исключён. Консольный клиент и PostgreSQL tests на этом этапе не запускались; их результаты04 не суммируются с этим прогоном.

Разрешённые OpenSpec команды выполнены с `OPENSPEC_TELEMETRY=0` и `DO_NOT_TRACK=1`:

```text
openspec status --change add-sub2api-connector --json
openspec instructions apply --change add-sub2api-connector --json
openspec validate add-sub2api-connector --strict --no-interactive
openspec validate --specs --strict --no-interactive
```

Обе strict validation завершились Exit0. Первоначальные length warnings устранены структурно: подробности десяти main и шести delta requirements перенесены в testable scenarios. Все исходные нормативные предложения16 длинных блоков проверены на сохранность. Изменения поведения delta в main не переносились, sync/archive не выполнялись. [Таблица](scenario-coverage.md) сопоставляет каждый из35 сценариев delta с actual tests или source evidence, включая границы непроверенного server поведения.

## 05.02: свежая поставка

По отдельному разрешению выполнена и повторена после окончательных test-source правок команда из корня agent-bridge:

```powershell
$env:RestoreSources='C:\Users\Spike\.nuget\packages'
pwsh -NoLogo -NoProfile -File 'tests/Delivery/Build/Prepare-DeliveryTests.ps1'
```

Пять Release builds:0 warnings/errors. Только local package source. Вложенные Assemble-Delivery, Assemble-NuGetDelivery и Assemble-SharedDelivery создали fresh output внутри `artifacts/delivery-tests`. Fingerprint готовой поставки:

```text
1a92b497cfe8941b4d4a0fd4cb39134e5bf1e33128baeb9dfb81b8b337f72ef7
```

SDK10.0.403, Release. Путь: `D:/Media/User/source/repos/agent-bridge/artifacts/delivery-tests/current`. Это generated evidence, не закоммиченные binaries.

| Комплект относительно current | Manifest entries | SHA256 delivery.manifest.json |
| --- | ---: | --- |
| sdk/SqlServer/win-x64 |114|01db29880a1c43bc01f836d1ef33db23ea5375547bbf75c04185401ed9197a26|
| sdk/SqlServer/linux-x64 |112|331acf78d5c3b2e9462ca32c5735c3e4c5c78494671d74fb26d1b3a4a02c29b6|
| sdk/SqlServer/linux-arm64 |112|00c918b1a73078dcfda02081ef3bd7102a74ee03554fc55052a2a5960cf3b2aa|
| sdk/Sqlite/win-x64 |74|0f9b582ad637c7230f51ab9afcd290816d7619a2c6594d67bc2f55fa1da7351c|
| sdk/PostgreSql/win-x64 |68|c6ca5335899c23e5800a39956730ae6dbb101758add093d6d3ef85d6e4fa5adb|
| nuget/SqlServer |24|bbe1a06c1920a6ea1994bf657c00a76a3bbe0f78286c8d303e24d4db3b56b4a0|
| nuget/Sqlite |24|7c39d97f9797a06887a2f6f1f5199d26bd8c294814339d6ef08d795116609f91|
| nuget/PostgreSql |24|af696fe7936c86010ec3248ef7e20626b420334bfe4de77460f768eb63272b4e|

Каждый schema3 bundle содержит11 собственных DLL, включая Sub2Api и сохранённый CodexLb, XML и props. SQL Server/SQLite/PostgreSQL имеют13/13/12 exact package roots. SQL Server shared bundle содержит128 файлов, три schema2 manifests в `shared/SqlServer/platform/<RID>`; Metadata проверяет полное соответствие исходным SDK kits, dedup и отсутствие лишних файлов.

После окончательной подготовки:
- Metadata: **42 passed**,0 failures/errors/skips.
- ABI: **23 passed**,0 failures/errors/skips.
- External Windows binary: **7 passed**,0 failures/errors/skips.

Metadata/Abi собраны отдельно с0 warnings/errors; в solution представлены File items. ABI build properties:

```text
-p:AgentBridgeLegacyBaselineRoot=C:/Users/Spike/AppData/Local/AgentBridge/CompatibilityBaselines/sub2api-2026-11-06
-p:AgentBridgeCurrentDeliveryRoot=D:/Media/User/source/repos/agent-bridge/artifacts/delivery-tests/current
```

После local restore и compile-check запускались `dotnet <compiled-test.dll> -noLogo -longRunning 30`. Никакой упаковки при обычной сборке тестов нет.

## Старая бинарная поверхность

Сначала ABI fixture сверяет6 pinned evidence hashes, сохранённые127 файлов и112 entries baseline kit; затем все hashes новой closure. Historical kit и frozen DLL только читались.

| Frozen consumer | TypeRef | MemberRef | MethodSpec | Новая closure |
| --- | ---: | ---: | ---: | --- |
| original |165|323|48|SQL Server win-x64/linux-x64/linux-arm64|
| surface |181|346|48|SQL Server win-x64/linux-x64/linux-arm64|

Шесть frozen cases, пять semantic surface cases и12 synthetic verifier controls (два положительных и десять намеренно несовместимых вариантов) —23 passed. Проверяются assembly identity/version/token, видимые типы/члены, static/instance/return/параметры/modifiers/generic arguments/constraints, forwarding и caller context; неизвестные формы metadata отклоняются.

Старые DLL напрямую ссылаются на AgentBridge.Persistence.SqlServer. Проверка их против SQLite/PostgreSQL kit была бы неверной матрицей: SQL Server модуль там намеренно отсутствует. Для этих провайдеров проверена semantic поверхность AgentBridge/CodexLb/Integration и сборка неизменённого source consumer. Не заявляется исполнение всех методов frozen DLL или полный CLR runtime аудит произвольного приложения.

## Внешние consumers и package graph

Новый каталог вне репозиториев:

```text
C:/Users/Spike/AppData/Local/AgentBridge/Acceptance/sub2api-2026-11-08-0502
```

Ancestors проверены на Directory.Build/Directory.Packages/NuGet/locks; дополнительных hooks не обнаружено. Скопированы kits и только .cs/.csproj. ProjectReference отсутствуют. Baseline source не менялся. Три consumer (original/surface/current с Sub2ApiRegistration) собраны на пяти SDK variants: **15 compile-check**. Каждый variant использовал отдельные intermediate/output paths. По manifest проверены все output managed/XML/native/resources:111/109/109/71/65 файлов для каждого consumer.

Текущий consumer дополнительно собран с DLL/NuGet bundle на пяти variants: **5 compile-check**, первый restore и повторный `--locked-mode` прошли. Все required roots в locks —Direct с exact requested/resolved version; SDK runtime assets совпали в output по SHA256:64/62/62/38/33.

External Compatibility с EF Design/Tools/InMemory, Serilog и FrameworkReference Microsoft.AspNetCore.App: **3 compile-check** и locked restore для SQL Server RID. InMemory database не используется. В output совпали48/47/47 SDK managed/native/resource файлов; отсутствующие16/15/15 не входят в application runtime graph, на Windows их наличие проверено в shared framework10.0.14. Linux shared runtime не запускался. До Windows тестов сверены hashes собственных DLL.

Команды внешней проверки для каждого concrete project/variant:

```text
dotnet restore <csproj> --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false -p:RuntimeIdentifier=<rid> -p:AgentBridgeDeliveryRoot=<copied-kit> -p:ArtifactsPath=<separate-variant-output> --verbosity quiet
dotnet restore <nuget-csproj> --locked-mode <те же параметры>
dotnet build <csproj> -c Debug --no-restore -p:BaseOutputPath=<separate-variant-output>/compile-check/ -p:GeneratePackageOnBuild=false -p:RuntimeIdentifier=<rid> -p:AgentBridgeDeliveryRoot=<copied-kit> -p:ArtifactsPath=<separate-variant-output> --nologo -v quiet
```

Все23 внешние сборки завершились с0 warnings/errors. Seven Windows cases используют закрытый SqlConnection/change tracker, strict DI, локальный HTTP handler и offline BPE. Новая ветка не требует CodexLb configuration, запрашивает manifest без client_version и отправляет JSON/SSE tool pair с provenance без continuation; compact возвращает Unsupported без нового HTTP. Это не бизнес-выполнение инструмента и не DB acceptance.

Два ожидаемых отрицательных контроля подтвердили Error-only guards: direct SqlClient6.1.6 отклонён против exact7.1.1 до restore; win-arm64 отклонён как неподдерживаемый RID до build. Источник отказа проверен по соответствующему сообщению.

Промежуточная повторная подготовка после test-source изменений не изменила bytes kit entries; это сверено до обновления внешней копии manifests/ready. External output соответствует окончательной поставке. Дополнительное machine-local evidence: `source-compile-results.json`, `output-closure-results.json`, consumer assets/deps и packages.lock.json в указанном каталоге.

## Оставшаяся проверка

05.03 не начинался. Нужны отдельно согласованные deployment/BaseAddress, model/effort, ключ sub2api с OAuth upstream, тестовый диалог/инструмент и лимиты расходов. Затем проверяются actual manifest, несколько turns/tools, JSON/SSE, полный контекст и продолжение после нового DI без retry/replay. T02 остаётся открытым; T01 не требует выдуманного бюджета, T05 относится только к отложенному compact.

На05.01–02 не выполнялись Git, production host, live HTTP/OAuth, SQL/БД, Docker, deployment, delta→main sync или archive. Linux execution/native operations, AOT/trimming/single-file и принятие поставки конкретным рабочим приложением не подтверждены. Baseline/evidence01–04 не переписывались.
