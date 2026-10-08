# Проектная проверка поставки

Metadata-тесты используют комплекты из `artifacts/delivery-tests/current/` в этом репозитории. Пользовательские `AGENTBRIDGE_DELIVERY_ROOT`, `AGENTBRIDGE_NUGET_DELIVERY_ROOT` и `AGENTBRIDGE_SHARED_DELIVERY_ROOT` больше не влияют на выбор файлов; удалять их в настройках Windows для запуска этих тестов не требуется.

## Запуск

Из корня репозитория:

```powershell
dotnet test --project tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false --results-directory artifacts/test-results/delivery-tests
```

Для первой сборки самого тестового проекта при отсутствии его assets сначала требуется обычный `dotnet restore` этого `.csproj`. SDK .NET 10 и PowerShell 7 (`pwsh` в PATH) обязательны. Подготовка комплектов сама выполняет NuGet restore runtime-графов; при недостающих пакетах может потребоваться доступ к настроенным NuGet-источникам.

Обычная сборка delivery-тестов перед компиляцией вызывает [Prepare-DeliveryTests.ps1](Build/Prepare-DeliveryTests.ps1):

1. Проверяет SHA256 исходников AgentBridge/EFCoreLibrary/HttpClientLibrary, упаковщиков и build inputs, а также выбранный SDK.
2. При изменении входов последовательно выполняет отдельные restore/build пяти Release-вариантов: SQL Server win-x64/linux-x64/linux-arm64 и SQLite/PostgreSQL win-x64.
3. Создаёт свежие полные SDK-комплекты, DLL/NuGet и shared bundle через существующие упаковщики.
4. После повторной проверки входов заменяет `current` и записывает маркер успешной подготовки. Если входы не менялись, использует готовый комплект; его фактические hashes проверяют metadata-тесты.

```text
artifacts/delivery-tests/
  preparation.lock
  current/
    ready.json
    sdk/<Provider>/<RID>/
    nuget/<Provider>/
    shared/SqlServer/
```

Build output всех проектов каждого варианта изолирован в собственном staging. Готовая поставка не смешивается с предыдущими файлами. Неудачная подготовка оставляет staging для диагностики, удаляет маркер актуальности старого комплекта и останавливает сборку. Подготовка и выполнение тестов блокируют взаимную замену файлов.

## Visual Studio и Microsoft.Testing.Platform

Проект сохраняет xUnit v3 и Microsoft.Testing.Platform из `global.json`. Путь к артефактам записывается в `AssemblyMetadata` во время сборки: MTP, Visual Studio adapter и запуск из другого рабочего каталога читают одну проектную поставку.

При запуске из Test Explorer должна быть включена сборка проектов перед тестами. `DisableFastUpToDateCheck` отключает пропуск MSBuild для этого тестового проекта; повторная подготовка внутри MSBuild использует проверку SHA256. Design-time build и discovery сами по себе не запускают упаковщики. Тесты не создают дочерние процессы.

`--no-build` или отключённая сборка перед тестами проверяют последний успешно подготовленный комплект. Такой запуск не пересобирает изменённые исходники. После незавершённой подготовки assembly fixture завершает запуск ошибкой вместо проверки предыдущей поставки.

Compile-check без выполнения подготовительных скриптов:

```powershell
dotnet build tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:AgentBridgeSkipDeliveryPreparation=true
```

Эта команда проверяет компиляцию тестов, но не актуальность или состав комплектов. По правилам репозитория первый запуск добавленного Exec и подготовительных скриптов требует отдельного разрешения точной команды. Runtime, приложение, Docker, БД и SQL не запускаются; metadata-проверки не доказывают работу провайдеров на реальных платформах.

## Проверка изменений 2026-10-08

Полный запуск через Microsoft.Testing.Platform прошёл 38 тестов без ошибок и пропусков. Повторный запуск при неизменных входах также прошёл 38 тестов, время записи ready.json сохранилось: комплекты повторно не упаковывались.

VSTest 18.10.0 из установленной Visual Studio Community 2026 с xUnit VSTest Adapter 4.0.0 обнаружил и выполнил все 38 тестов без ошибок и пропусков. Проверен движок/adapter; запуск кнопкой Test Explorer в интерфейсе Visual Studio не выполнялся. При отдельном вызове цели с DesignTimeBuild=true MSBuild вернул Skipped: подготовка не запускалась. Финальный MTP-запуск после изменения исходного теста снова прошёл 38 тестов; подготовка обновила комплекты по новому fingerprint. Это isolated metadata evidence, без runtime AgentBridge/БД/HTTP.
