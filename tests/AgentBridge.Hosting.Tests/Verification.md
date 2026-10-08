# Проверка Generic Host integration

Дата: 2026-10-08. Рабочая папка: `D:\Media\User\source\repos\agent-bridge`. SDK: `10.0.401`, target: `net10.0`, runner: существующий Microsoft.Testing.Platform.

## Реализованные проверки

1. Public `AddAgentBridge` в настоящем Generic Host: composition/start/stop без HTTP, вызова read ports, migration, maintenance и cleanup. Без app worker список `IHostedService` пуст; HTTP factory и scoped source не создаются.
2. Настоящий `Host.StartAsync` вызывает зарегистрированный `IStartupValidator`/ValidateOnStart. 17 invalid/missing configuration cases, отдельный Individual без app source и три независимые ошибки options в одном aggregate. До отказа не запускается worker, HTTP factory или storage read; синтетические секреты не выходят в ошибку.
3. Sync scopes используют разные scoped source/settings reader и освобождают sources. Async scopes используют реальные AgentRunner, reader, gateway, UoW и DbContext; экземпляры разных scopes независимы, их disposal ожидается. Stop хоста не освобождает открытые scopes приложения.
4. App-owned BackgroundService запускает реальный `AgentRunner.RunAsync`: actual reader/query/gate через переиспользованные fake base repositories → actual access/settings/catalog → CodexLb/HttpClientLibrary → local handler. Два случая отмены: send и чтение body. Gates показывают, что StopAsync ждёт завершения transport, затем async disposal scope; runner возвращает Canceled.
5. Управляемый отказ actual HTTP read сохраняет исходную IOException, body cleanup в `HttpClientLibrary.CleanupExceptions` и дополнительную ошибку app scope. Ошибка наблюдаема в ExecuteTask, настоящем host logger и app StopAsync. Другой случай проверяет actual AgentRunScope: primary base-read exception и short-scope DisposeAsync exception сохраняются вместе до HTTP/write.
6. Borrowed HttpClient, handler и logger переживают catalog, scope disposal, StopAsync и host DisposeAsync. HTTP pipeline освобождает body. Finally приложения отдельно освобождает borrowed resources и проверяет освобождение всех созданных sources, repositories и bodies.
7. Без app bridge остановка хоста не отменяет активный настоящий HTTP pipeline. Отмену передаёт явный token приложения, а scope освобождает само приложение.

`HostingWorker` и его политика распространения ошибки из StopAsync принадлежат тестовому приложению. Стандартный BackgroundService.StopAsync не гарантирует возврат ошибки ExecuteTask. AgentBridge не регистрирует worker и не связывает операции с host lifetime.

## Статическая проверка

- Проверены новый и существующие test csproj, транзитивные project references, global.json и применимые build-файлы. Новых Exec/custom targets/imports нет. Единственный найденный Directory.Build.props в HttpClientLibrary переназначает output только для его собственного test project. В применимых каталогах нет Directory.Build.targets, NuGet.config и packages.lock.json.
- Host package `Microsoft.Extensions.Hosting 10.0.12` подключён только к новому тестовому проекту. Test packages соответствуют существующим: xunit.v3 4.0.1, Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.0.
- Solution XML корректен, новый project включён ровно один раз. DefaultItemExcludes исключает bin/obj/artifacts. Нет Skip, opt-in, задержек, скрытых retries или fallback.
- Изменённые инструкции, solution и новые source/docs прошли strict UTF-8 проверку без U+FFFD, четырёх вопросительных знаков и признаков mojibake; окончания строк LF. Проверены bounded cleanup и отсутствие оставшегося процесса AgentBridge.Hosting.Tests.

## Точные команды

Явное восстановление нового проекта — exit 0:

```powershell
dotnet restore tests\AgentBridge.Hosting.Tests\AgentBridge.Hosting.Tests.csproj -p:GeneratePackageOnBuild=false
```

Compile-check конкретных проектов — exit 0, по 0 warnings / 0 errors:

```powershell
dotnet build tests\AgentBridge.Hosting.Tests\AgentBridge.Hosting.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\
dotnet build tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\
```

Запуски выполнены после успешной сборки того же compile-check output — все exit 0:

```powershell
dotnet test --project tests\AgentBridge.Hosting.Tests\AgentBridge.Hosting.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test --project tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test --project tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\
dotnet test --project tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\ --filter-not-trait "Dependency=Database"
```

| Набор | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Hosting | 28 | 0 | 0 |
| Core | 488 | 0 | 0 |
| CodexLb | 403 | 0 | 0 |
| Persistence isolated | 352 | 0 | 0 |
| Всего | 1271 | 0 | 0 |

Первый hosting-набор до добавления проверки отсутствия автоматической отмены: 27/27. Первая compile-check сборка имела 26 analyzer warnings; они устранены без suppression. Финальный hosting build и прогон после изменения cleanup: 0 warnings/errors, 28 passed, 0 failed/skipped. Повторные прогоны выполнялись после конкретных изменений; скрытых retries нет.

Обычные Debug outputs для Visual Studio обновлены следующими командами — exit 0, по 0 warnings / 0 errors:

```powershell
dotnet build tests\AgentBridge.Hosting.Tests\AgentBridge.Hosting.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false
dotnet build tests\AgentBridge.Tests\AgentBridge.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false
dotnet build tests\AgentBridge.CodexLb.Tests\AgentBridge.CodexLb.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false
dotnet build tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false
```

## Дефекты и непроверенные границы

Production-дефектов в проверенных сценариях не обнаружено. Production-код и соседние библиотеки не изменялись; Git-команды не выполнялись.

DB-проверки и их инфраструктура не добавлялись/не менялись. Existing base repository doubles только подключены linked Compile, без копий и изменений. Dependency=Database не запускался. NoDatabaseInterceptor отклоняет попытку открыть соединение до I/O.

Runner здесь отменяется на чтении каталога либо отказывает на чтении root, до durable begin. Не проверены полный successful model turn, generation/SSE, запись checkpoint/finalization/recovery в настоящую БД под hosting, SQL atomicity/concurrency, migrations/maintenance/cleanup на БД, live codex-lb HTTP, production hosting и deployment. Эти результаты не заменяют существующие отдельные DB/transport acceptance checks.
