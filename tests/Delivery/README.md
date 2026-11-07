# Отдельные проверки поставки и исторического API

В обычном решении подключены пять тестовых проектов ядра, транспорта, композиции, хранения и hosting. API-тесты Sub2Api читают текстовый эталон из embedded resource и не требуют сохранённых DLL или каталога профиля.

Metadata, LegacyBaseline и внешний Compatibility представлены в solution как документы для отдельного запуска. Сборка решения и самих тестовых проектов не запускает упаковщики или PowerShell.

## Проверка текущей поставки

Metadata-тесты проверяют реальные файлы комплектов, поэтому используют `artifacts/delivery-tests/current/`. Это generated output проверки поставки, а не источник expected для обычных unit-тестов. Пользовательские AGENTBRIDGE_*_DELIVERY_ROOT не читаются.

Сначала явно подготовить комплекты разрешённой командой из корня репозитория:

```powershell
pwsh -NoLogo -NoProfile -File "tests/Delivery/Build/Prepare-DeliveryTests.ps1"
```

Нужны .NET 10 и PowerShell 7. Скрипт выполняет restore/build пяти Release-вариантов: SQL Server win-x64/linux-x64/linux-arm64 и SQLite/PostgreSQL win-x64; создаёт DLL/NuGet и shared bundles. Перед заменой current проверяет SHA256 входов, использует отдельный staging и блокировку. При неизменных входах использует ранее подготовленный комплект. Отказ не активирует предыдущую поставку; staging остаётся для диагностики. При недостающих пакетах может потребоваться доступ к настроенным NuGet-источникам.

После подготовки отдельно восстановить/собрать и запустить конкретный проект:

```powershell
dotnet restore tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false
dotnet build tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false
dotnet test --project tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --no-progress
```

Путь к локальному источнику пакетов заменить своим. Без готового ready.json tests завершаются понятной ошибкой; файлы не создаются и не пропускаются молча. Новый source build сам по себе не обновляет kit: перед проверкой поставки изменённых исходников повторить явную подготовку. Пути комплекта записываются в AssemblyMetadata и не зависят от cwd или BaseOutputPath.

Обычный compile-check Metadata выполняет только компиляцию. AgentBridgeSkipDeliveryPreparation больше не нужен, Exec/BeforeCompile и DisableFastUpToDateCheck удалены. Проект можно открыть отдельно в Visual Studio; подготовка комплекта остаётся явной. Runtime, native loading, HTTP, БД и Docker metadata-тестами не запускаются.

## Исторический baseline этапа01

[LegacyBaseline](LegacyBaseline/AgentBridge.LegacyBaseline.Tests.csproj) отдельно проверяет сохранность старых DLL, frozen consumers и происхождение текстового API-эталона. Это acceptance evidence, а не обязательные локальные файлы каждого разработчика.

Путь выбирается явно и записывается в metadata тестовой сборки:

```powershell
dotnet restore tests/Delivery/LegacyBaseline/AgentBridge.LegacyBaseline.Tests.csproj --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false
dotnet build tests/Delivery/LegacyBaseline/AgentBridge.LegacyBaseline.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false -p:AgentBridgeLegacyBaselineRoot="C:/path/to/preserved-baseline"
dotnet test --project tests/Delivery/LegacyBaseline/AgentBridge.LegacyBaseline.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false -p:AgentBridgeLegacyBaselineRoot="C:/path/to/preserved-baseline" --no-progress
```

Сначала проверяются шесть pinned hashes evidence этапа01, затем описи127/112, включая frozen DLL; только после этого текстовый API сравнивается со старыми PE. Отсутствующий/повреждённый baseline — ошибка явного audit-запуска. Нет default профиля, поиска другого kit, автоматической генерации или Skip. Старый комплект и frozen consumers никогда не перезаписываются.

[legacy-api-baseline.json](../AgentBridge.Sub2Api.Tests/Fixtures/legacy-api-baseline.json) хранит 2657 семантических записей трёх старых сборок и SHA256 их исходных DLL. Он встроен в оба проекта. Обновлять expected по новым исходникам запрещено: изменения API должны оцениваться как изменения совместимости. Source compilation/API subset comparison и проверка старого комплекта не заменяют old→new CLR ABI resolution, runtime или live OAuth.

## Внешний DLL-потребитель

`Compatibility/` проверяется отдельно после копирования исходников за пределы репозиториев с импортом целого выбранного binary kit. Правила — [Compatibility/AGENTS.md](Compatibility/AGENTS.md). Это отдельный этап проверки поставки, а не source ProjectReference unit-тест.

## Разрешения

Скрипты подготовки требуют разрешения точной команды по AGENTS.md. Обычная сборка тестов их не вызывает. Generated комплекты хранятся только в ignored artifacts; защищённый исторический baseline выбирается отдельно и доступен только для чтения.

## Проверка изменений 2026-10-08

Полный запуск через Microsoft.Testing.Platform прошёл 38 тестов без ошибок и пропусков. Повторный запуск при неизменных входах также прошёл 38 тестов, время записи ready.json сохранилось: комплекты повторно не упаковывались.

VSTest 18.10.0 из установленной Visual Studio Community 2026 с xUnit VSTest Adapter 4.0.0 обнаружил и выполнил все 38 тестов без ошибок и пропусков. Проверен движок/adapter; запуск кнопкой Test Explorer в интерфейсе Visual Studio не выполнялся. При отдельном вызове цели с DesignTimeBuild=true MSBuild вернул Skipped: подготовка не запускалась. Финальный MTP-запуск после изменения исходного теста снова прошёл 38 тестов; подготовка обновила комплекты по новому fingerprint. Это isolated metadata evidence, без runtime AgentBridge/БД/HTTP.
