## Реализация

- [x] Адаптировать регистрацию и все текущие вызовы к EFCoreLibrary 0.0.9.
- [x] Синхронизировать требования и примеры подключения.
- [x] Собрать production и тестовые проекты с обновлёнными зависимостями.
- [x] Выполнить изолированные тесты и зафиксировать ограничения проверки поставки и БД.

## Проверки 2026-11-06

- Restore основных пяти тестовых проектов: конкретные csproj, локальный источник `C:\Users\Spike\.nuget\packages`, `-p:NuGetAudit=false -p:GeneratePackageOnBuild=false`.
- Debug build пяти тестовых проектов и их production closure: `--no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\`, 0 ошибок/предупреждений. Metadata сначала собран с `-p:AgentBridgeSkipDeliveryPreparation=true`, затем отдельно разрешена и выполнена обычная сборка с Prepare-DeliveryTests.ps1.
- Запуск через `dotnet test --project <csproj> -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=.\artifacts\compile-check\ --no-progress`: Core 542, CodexLb 414, Hosting 28, Persistence 470, Metadata 42; все успешно, без пропусков. Persistence дополнительно использует `--filter-not-trait 'Dependency=Database'`.
- Подготовлены свежие Release SDK-комплекты: SQL Server win-x64/linux-x64/linux-arm64, SQLite/PostgreSQL win-x64; NuGet и shared bundles. Внешний compile-only Consumer из `%TEMP%\agent-bridge-dependency-check-83b39d85a74646a0912d22207d9e366c` собран со всеми пятью комплектами без ProjectReference, без ошибок/предупреждений.
- Внешний Compatibility consumer с DLL/NuGet SqlServer, RuntimeIdentifier=win-x64: локальный restore, Debug build и 3 теста успешно. Итого 1499 успешных тестов, 0 ошибок/пропусков.
- Три metadata-теста сначала воспроизвели устаревшее ожидание SqlClient 7.0.2; ожидания обновлены по actual csproj/NuGet nuspec на 7.1.1, EF/Microsoft.Extensions 10.0.14 и SNI.runtime 7.1.0 без ослабления проверок.
- `openspec validate update-maintenance-registration-api --strict`: успешно. `openspec validate --specs --strict`: отказ из-за десяти предупреждений о длине прежних требований (индексы 26, 33, 34, 38, 93, 94, 156, 157, 159, 160), за пределами изменённого требования. Change не архивирован.
- Реальные HTTP/БД/SQL, Docker/Testcontainers, backup/restore, production hosting и Linux runtime не запускались. Metadata/cross-build не подтверждают runtime на Linux или серверные операции.
