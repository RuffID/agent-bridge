# Собственные DLL и обязательные NuGet-зависимости

## Подключение

AgentBridge, выбранный модуль БД и его migrations assembly, EFCoreLibrary с maintenance и HttpClientLibrary поставляются своими DLL. Сторонние Microsoft/System/Azure и другие зависимости подключаются через PackageReference импортируемого файла. Публикация наших библиотек в NuGet не требуется.

Для SQL Server комплект находится в `artifacts/nuget-delivery/SqlServer`, для SQLite/PostgreSQL — в соседних одноимённых папках. Это сборочные artifacts, которые не входят в исходники. Скопируйте весь выбранный комплект в приложение и импортируйте его **после** остальных PackageReference:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
<!-- Здесь собственные PackageReference приложения. -->
<Import Project="vendor/AgentBridge/AgentBridge.Delivery.props" />
```

```text
AgentBridge/
  lib/                            10 собственных DLL и доступные XML
  AgentBridge.Delivery.props      общий импорт и проверки
  AgentBridge.References.props    только локальные Reference/HintPath
  AgentBridge.Dependencies.props  обязательные exact PackageReference
  delivery.manifest.json          schema3, SHA256, package roots/source graphs
```

SQL Server поддерживает win-x64/linux-x64/linux-arm64; SQLite/PostgreSQL комплекты проверены для win-x64. Наши DLL одинаковы для RID выбранного комплекта; native-файлы, разные варианты сторонних сборок и локализации выбирает NuGet/SDK. Они не хранятся в libs. Некоторые Microsoft/System сборки web-приложения предоставляет shared framework ASP.NET Core: SDK не копирует их повторно в output. .NET 10 runtime устанавливается отдельно.

Текущий импорт требует TargetFramework=net10.0. Он задаёт все поддержанные RuntimeIdentifiers, RestorePackagesWithLockFile и CopyLocalLockFileAssemblies. Central package management требует отдельного варианта поставки и не поддерживается этим импортом. Полные DLL-комплекты остаются альтернативой; их формат описан в [документе28](28-provider-modules-and-shared-delivery.md).

## Зависимости и версии

[Assemble-NuGetDelivery.ps1](../../tests/Delivery/Build/Assemble-NuGetDelivery.ps1) сначала проверяет полный manifest каждого исходного SDK kit: hashes/size/case/paths/provenance и отсутствие лишних файлов. Наши DLL/XML сохраняются один раз, их bytes должны совпадать между RID. Пакетные roots вычисляются по прямым NuGet-зависимостям всех project libraries в исходном deps; учитывать только AgentBridge.dll недостаточно. Транзитивные зависимости восстанавливает NuGet.

SQL Server имеет 12 обязательных PackageReference: Microsoft.Bcl.Memory, Microsoft.Data.SqlClient, Microsoft.EntityFrameworkCore/Relational/SqlServer, Microsoft.Extensions.DependencyInjection.Abstractions, Logging/Logging.Abstractions, Options.ConfigurationExtensions и три Microsoft.ML.Tokenizers пакета. SQLite имеет 13 roots, PostgreSQL — 12. Версии берутся из проверенного source graph, а не выбираются вручную по имени DLL.

Все roots задаются exact диапазоном `[version]`. Если приложение уже указало тот же пакет с minimum version, равной проверенной версии, импорт приводит её к exact без дубликата. Иная версия, удалённый обязательный пакет или повторный PackageReference отклоняются Error-only target до restore/build. Импорт не запускает скрипты, DLL, приложение или SQL.

При первом restore приложение создаёт собственный packages.lock.json, фиксирующий все прямые и транзитивные версии/content hashes. При существующем lock импорт включает RestoreLockedMode. Обычная проверка:

```powershell
dotnet restore .\Application.csproj --locked-mode
```

После намеренного обновления комплекта или зависимостей пересоздайте lock штатным NuGet restore, проверьте изменения и снова выполните locked restore:

```powershell
dotnet restore .\Application.csproj --force-evaluate -p:RestoreLockedMode=false
dotnet restore .\Application.csproj --locked-mode
```

Не редактируйте generated props, manifests или locks вручную. Упаковщик требует новый destination и отдельное разрешение точной команды. Сам он не выполняет restore и не загружает DLL. Schema3 содержит hashes собственных файлов, обязательные package roots и исходный resolved package graph каждого RID; это provenance, а не deps приложения.

## TelegramCodexRelayBot и проверка

Бот импортирует libs/AgentBridge.props из Infrastructure/host/tests. Он сохраняет default win-x64 на Windows и linux-x64 на Linux; linux-arm64 задаётся явно. Повторный Microsoft.EntityFrameworkCore.SqlServer PackageReference из Infrastructure удалён: его добавляет комплект. В libs/AgentBridge осталось 23 файла/1,03 МиБ вместо 140 файлов/81,68 МиБ полной общей поставки. В lib — 10 DLL и 9 доступных XML; базовый EFCoreLibrary не поставлял XML, новых файлов для него не создавалось.

В трёх проектах бота созданы packages.lock.json, включающие разрешение для трёх RID. Dockerfile копирует host/Infrastructure locks до restore и использует --locked-mode; контейнерная сборка не запускалась.

Проверка 2026-10-08: 38 metadata tests, включая 8 новых проверок local/package closure, и 113 изолированных bot tests пройдены, 0 failed/skipped. Сборки бота для трёх RID и пяти внешних DLL/NuGet consumers прошли без предупреждений и ошибок; locked restore всех вариантов успешен. Методы внешних consumers не исполнялись.

В пяти consumer outputs все managed/native/resources исходной SDK closure совпали по SHA256: SQL Server 70/68/67 файлов, SQLite39, PostgreSQL34. В bot outputs совпали 55/54/53 файла; остальные базовые сборки предоставляет shared framework и они отсутствуют в runtime graph приложения. Проверка MSBuild допускает одинаковую app version и отклоняет другую version, удалённый/дублированный пакет и неподдержанный RID.

Это compile/metadata и изолированная проверка. Бот, Docker, БД, SQL, реальные Telegram/codex-lb и Linux runtime не запускались; native loading и реальные provider операции этими проверками не подтверждены.
