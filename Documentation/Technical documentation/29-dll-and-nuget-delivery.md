# Собственные DLL и обязательные NuGet-зависимости

Поставка catalog/lifecycle/recovery для 01A от 11.10.2026: `artifacts/assistant01a-delivery-20261011-coherent/SqlServer`, manifest SHA256 `5fd8ddabe234857a68a79d49a74079e384920e7a122af7536dc54b7b97d9206a`. Это отдельный целый schema3 комплект с exact EF 10.0.12 / SqlClient 7.0.2, прошедший external locked restore/compile для трёх SQL Server RID. [Фактический API](30-dialog-catalog-and-recovery.md), [полное evidence](<../Plans/Assistant Dialog Capabilities/Evidence.md>) и ограничения приёмки; в Ledger этот комплект не принят автоматически, его 01A остаётся In Progress. Следующие разделы описывают общий механизм поставки.

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

## Комплект для AquaByte Ledger, 2026-10-10

Отдельная новая поставка: `artifacts/ledger-delivery-20261010-ef10.0.12-sql7.0.2/SqlServer`. Старая `artifacts/nuget-delivery/SqlServer` не заменялась. Рядом находятся `sdk/SqlServer/<RID>` с полной runtime closure и `evidence` с consumer assets/deps/locks. Подробный отчёт и hashes — `README.md` и `SHA256SUMS.txt` в корне новой поставки.

Целевой комплект: net10.0, EF Core/Relational/SqlServer 10.0.12, Microsoft.Data.SqlClient 7.0.2, обязательные Microsoft.Extensions и Bcl.Memory 10.0.12, Tokenizers/data 2.0.0. Все 12 direct roots сформированы упаковщиком из нового SDK graph и сохранены как exact constraints. IdentityModel/Jwt разрешены в 8.16.0. SQL Server maintenance пересобран с assembly reference SqlClient 7.0.0.0; base repository/UoW API 0.0.5 и HTTP API/FileVersion 0.0.0.5 сохранены. HTTP net8.0 сохраняет Logging.Abstractions 10.0.2 и проходит собственные проверки.

SqlClient 7 не включает прежний NativeInterop/libmsalruntime.so в эту closure. Windows получает SNI.runtime 6.0.2 и один native SNI asset; оба Linux RID не имеют native assets в SDK-selected SQL Server graph. Metadata checks проверяют полный фактический manifest/deps, а ELF negative controls используют явно синтетические headers обеих архитектур.

Проверены 166 EF maintenance, 58 HTTP на каждом TFM, 359 isolated persistence, 414 CodexLb и 42 delivery metadata теста; внешний binary consumer прошёл ещё 3 Windows-теста strict DI/закрытого SQL provider/repository/UoW и HTTP stub. Consumer копируется из `tests/Delivery/Compatibility` вне всех repo, без ProjectReference. Его combined graph включает EF Design/Tools/InMemory 10.0.12 и неизменные Serilog-пакеты P00; все три RID проходят build и locked restore. По собственному consumer deps проверены 268/267/267 runtime/native/resource assets и их SHA256 относительно package originals либо локальных DLL. Отдельный direct SqlClient 6.1.6 отклоняется exact validation до восстановления пакетов.

Это подготовленная контрактная поставка для последующего принятия P00. AquaByte Ledger не изменялся; его combined restore/build и бизнес-регрессия не выполнялись. Native loading, настоящие SQL connection/transactions/backup/restore/migrations, Linux runtime и реальные интеграции остаются непроверенными. L1 в Ledger не отмечен закрытым.
