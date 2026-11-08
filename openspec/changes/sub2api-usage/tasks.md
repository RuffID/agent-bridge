# Реализация и проверка

- [x] Проверить правила, независимые контракты, selector/facade и текущие server usage/auth/statistics.
- [x] Подготовить OpenSpec change до изменения поведения.
- [x] Реализовать optional DI и usage gateway без зависимости от CodexLb.
- [x] Добавить и выполнить изолированные проверки режимов, optional, отказов, отмены/deadline, URL, DI и Unsupported.
- [x] Выполнить локальный restore и compile-check конкретных проектов.
- [x] Обновить актуальные требования, контекст и пример подключения.
- [ ] Выполнить OpenSpec CLI validation после отдельного разрешения.
- [x] Подготовить DLL штатным упаковщиком после согласования точной команды.
- [ ] Проверить реальный GET /v1/usage после отдельного разрешения.

## Изолированные результаты 2026-11-08

Проверены csproj и transitive build inputs; executable hooks не используются, GeneratePackageOnBuild=false. Restore использовал только C:/Users/Spike/.nuget/packages. ArtifactsPath и BaseOutputPath перенаправлены в agent-bridge/artifacts/sub2api-usage для всех проектов, включая read-only внешние источники.

```powershell
dotnet restore tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false --artifacts-path D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage/bin/
dotnet build tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false --artifacts-path D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage/bin/
dotnet test --project tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage/bin/ --no-progress
```

Финальный build тестового проекта и production closure: 0 warnings/errors. MTP: 240 passed, 0 failed, 0 skipped (62 новых usage cases и 178 existing Sub2Api cases). Включены оба режима, optional/null/unknown fields, invalid UTF-8/JSON/error envelope, HTTP/transport failures, send/success-body/error-body caller/deadline, приоритет caller, timeout borrowed HttpClient, cleanup failures, URL normalization, lazy optional public DI, app override и Unsupported без factory. Пример Consumer/Sub2ApiAuxiliaryUsage.cs source-linked и скомпилирован; его методы не исполнялись.

Нормативная delta вручную сверяется с текущей spec; это не CLI validation. Проверены XML проекта/solution, UTF-8, отсутствие U+FFFD/четырёх вопросительных знаков и размещение новых исходников. Завершённые планы и Telegram-бот не изменялись. Git, OpenSpec CLI, host, live HTTP/OAuth, БД, migrations, Docker и deployment не выполнялись. Change не архивирован; внешний consumer по provider/RID матрице и live usage остаются непроверенными. Ручная подмена DLL не выполнялась.

## Штатная поставка после отдельного разрешения 2026-11-08

Выполнена согласованная команда:

```powershell
pwsh -NoLogo -NoProfile -File "D:\Media\User\source\repos\agent-bridge\tests\Delivery\Build\Prepare-DeliveryTests.ps1"
```

Exit code 0. Подготовлены пять Release SDK kits: SqlServer/win-x64, linux-x64, linux-arm64; Sqlite/win-x64; PostgreSql/win-x64. Созданы три DLL/NuGet bundles (по 11 собственных DLL) и shared SqlServer bundle. Активный root: `artifacts/delivery-tests/current`; ready.json: SDK 10.0.403, Release, fingerprint `ce51ce8b051da901b7cd10702e199ff34ec0d6cd19327aae15df3031146b4418`. В XML новой Integration DLL присутствует AddAgentBridgeSub2ApiAuxiliary.

Metadata-проект восстановлен из локального C:/Users/Spike/.nuget/packages, собран с GeneratePackageOnBuild=false и отдельными ArtifactsPath/BaseOutputPath в `artifacts/sub2api-usage-metadata`, 0 warnings/errors. `dotnet test --project tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage-metadata -p:BaseOutputPath=D:/Media/User/source/repos/agent-bridge/artifacts/sub2api-usage-metadata/bin/ --no-progress`: 42 passed, 0 failed, 0 skipped. Проверены manifests/hashes/closure/PE/ELF/XML и переносимые References; target DLL/native code не исполнялись. Внешняя бинарная компиляция нового usage-примера и live usage этой проверкой не подтверждены.
