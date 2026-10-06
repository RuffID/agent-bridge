# 16 — Изолированная регрессия и compile-only DLL

[Навигатор](README.md). Статус: **не начат**. Зависимости: принятые01–15 в выбранном объёме; непроверенные части имеют явный блокер, а не фиктивный pass.

## Цель и область

Проверить совместимость совокупности исправлений и подготовить свежие DLL-комплекты для дальнейшего runtime evidence. Источники — [пробелы аудита14](<../AgentBridge Quality Audit/14-test-evidence-and-gaps.md>), [правила тестов](../../../tests/AGENTS.md) и [Delivery](../../../tests/Delivery/AGENTS.md).

## Работы

1. Проверить конкретные `.csproj`, imports/Exec hooks, lock/config files, output paths и traits; составить команды по действующим правилам. Restore/network, packaging или project scripts не включать незаметно.
2. Собрать свежие затронутые production/test проекты, включая actual ProjectReference зависимости. Сохранить `GeneratePackageOnBuild=false` в EF-цепочках. Не запускать solution/Rebuild и не править generated output.
3. Запустить три изолированных набора ядра, CodexLb и persistence на успешно собранных текущих binaries. Для persistence использовать `Dependency!=Database`, исключив все реальные интеграции по проверенным traits.
4. Выполнить адресные maintenance и HTTP library tests, затронутые исправлениями. Проверить transport/cancellation, UoW/CAS, compact/full budget, attempt identity/no-replay, settings snapshot и bounded cleanup в существующих изолированных границах.
5. По правилам tests/Delivery подготовить свежие MSSQL kits для win-x64/linux-x64/linux-arm64 из принятых версий зависимостей и facade14. Existing SQLite/PostgreSQL kits проверить адресно на затронутой платформе, не обещая им незапущенную RID-матрицу. Запуск build/helper scripts требует точного разрешения. Output/manifest хранить только в игнорируемых artifacts.
6. Собрать внешнего binary consumer и примеры руководства25 всеми тремя основными kits без ProjectReference/PackageReference. Проверить actual короткое IConfiguration API и отдельный app logger. Legacy PostgreSQL и актуализированный MSSQL server sample проверять в собственной compile-only границе; hosting/authorization методы не исполнять.
7. Проверить manifests, closure, hashes, XML, PE/metadata и отсутствие dependency conflicts на compile уровне. Зафиксировать конфигурацию/RID, версии/хеши источников и binaries, SDK/packages и связь каждого TRX с build.

## Проверки

B без application/hosting/HTTP/БД/native loading: сборки, изолированные suites, metadata и compile-only consumer. Failed build останавливает тесты этого binary; старый `--no-build` результат не принимается. Не суммировать пересекающиеся suites, два TFM и повторные delivery cases.

Новые массовые тесты без различающего поведения не добавлять. Расширять проверку лишь для реальных изменений или оставшихся рисков. Runtime/native readiness относится к18.

## Критерии завершения

Есть успешные свежие сборки, TRX с фактическими passed/failed/skipped и явным scope, manifests трёх MSSQL kits и compilation каждого consumer. Публичные DLL/XML/examples согласованы; existing providers имеют адресное regression evidence. Неизолированные17–19 не выдаются за пройденные.

## Результаты

Сборки, тесты и создание комплектов ещё не выполнялись.
