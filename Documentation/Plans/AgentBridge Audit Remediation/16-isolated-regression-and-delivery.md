# 16 — Изолированная регрессия и compile-only DLL

[Навигатор](README.md). Статус: **принят в B-границе**. Зависимости: принятые01–15 в выбранном объёме; непроверенные части имеют явный блокер, а не фиктивный pass.

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

### Область, исходное состояние и manifest

2026-10-06, Asia/Novosibirsk. Выполнено исходное задание этапа16, сохранённое выше. Fresh commands исполнялись 15:09:40–15:11:43 UTC+07:00; затем выполнены read-only сверки evidence. Before: принятые исправления01–15 и kits15, но общей свежей регрессии на текущем HEAD не было. After: три production compile-check, три полных isolated suites, адресный EF maintenance, оба HTTP TFM, пять новых kits/внешних consumers и metadata/negative controls. Различающего регресса не обнаружено; production/tests/Delivery source и public API не менялись, новые тесты не добавлялись.

Единственный tracked writable файл:

```text
Documentation/Plans/AgentBridge Audit Remediation/16-isolated-regression-and-delivery.md
```

AgentBridge HEAD до/после `a874c346c288c6b5b6343a59dea4a1220bd785f8`; принятый delivery15 commit `b0c81321b1a9cfe0b4c640a1413419088daae88d` уже входит в baseline. Соседи read-only: EFCoreLibrary `5962deceb6ea01306cbbda400040db88ea8e5df9`, HttpClientLibrary `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`, codex-lb `f2b8e042c4ce012ae703bc939413bf9961b00032`. EF/HTTP чистые; foreign codex-lb `.vs/` сохранён. Чужой `Coordination.md` исключён из manifest и не редактировался. Index не менялся; add/commit/push отсутствуют. Соседние manifests пусты.

Generated evidence находится только в ignored `artifacts/stage16/` и `artifacts/delivery/stage16/`. Два test-only orchestration/verification helper созданы вне всех repo в `C:/Users/Spike/Documents/Codex/Stage16-20261006/`; их точные копии/SHA сохранены в evidence. Это не новые production scripts или build hooks. Все пять external consumers находятся в подпапках `consumers/<Provider>-<RID>/` того же внешнего каталога; методы примеров не исполнялись.

### Правила, preflight и разрешение

Прочитаны текущие root/tests/Delivery/Documentation/Plans и adapter/library AGENTS, README/Decisions, Results обязательных предшественников, Findings/итог15 и относящиеся clauses main OpenSpec. Q-003–005 не переоткрывались. Применён `csharp-project-rules` с style/build-validation; UoW/DI границы сверены по `backend-uow-repositories`/reference и `aspnetcore-project-rules`. EF tooling/schema/migrations не изменялись и не запускались.

Перед сборками проверены конкретные csproj и ProjectReference graph, source imports/targets/Exec, ancestor build/NuGet/global/lock names, output exclusions и traits. Source snapshot содержит22 csproj (13 AgentBridge,7 EF,2 HTTP),474 source/rules/config inputs с SHA256. Единственный найденный repository Directory.Build.props — HTTP test output/intermediate override; command-line BaseOutputPath задаёт отдельный stage16 output. Source Delivery targets выполняют только Error validation, helper вызывается явно. Packaging EF подавлен `GeneratePackageOnBuild=false` во всех цепочках. Использовались конкретные Build, без solution/Rebuild/pack/publish.

Restore был явным и offline: `--source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false`. Network sources/runtime downloads не добавлялись. Delivery framework-dependent graph ограничен `TargetFramework=net10.0`, `SelfContained=false`, `EnableRuntimePackDownload=false`, `EnableAppHostPackDownload=false`, как в принятом helper15; package runtime/native/resources resolution сохранён. Assets/dgspec каждого variant закреплены до его SDK Build в `inputs/<Provider>-<RID>/`. Дополнительный post-run inventory `final-project-inputs.json` содержит87 final assets/dgspec/generated imports, `package-build-inputs.json` —43 current package props/targets с содержимым/SHA; последние являются final inventory, не заменяют pinned variant inputs или per-run deps.

Установленный SDK actual `10.0.401`; доступны Microsoft.NETCore.App `8.0.31`/`10.0.12`. Тесты выполнены на Windows, TFM указан каждым TRX/build receipt. Current package graph сохранён в assets/deps: EF10.0.11/Npgsql10.0.3/SqlClient6.1.6/SQLitePCLRaw2.1.12/tokenizers+data2.0.0, EFCoreLibrary0.0.5/HTTP FileVersion0.0.0.5; Extensions10.0.11 и Configuration/Binder/Options.ConfigurationExtensions10.0.3. Core/Codex/persistence/EF/Metadata runner: Test.Sdk18.0.1/xUnit2.9.3/VS3.1.5; HTTP: Test.Sdk17.14.1/xUnit.v3 версии3.0.1/VS3.1.1; Roslyn metadata5.0.0. Полные версии, включая отличающиеся isolated dependency graphs, берутся из per-run output deps и inputs, а не из одной общей версии таблицы.

Основание запуска — переданное прямое разрешение пользователя всех test commands/scripts, включая isolated suites, delivery helpers/targets и compile-only consumers. Повторное разрешение не запрашивалось. Это не permission реальных БД/SQL/HTTP/hosting/deployment. Реальные persistence cases имеют `Dependency=Database`; полный isolated набор использовал `Dependency!=Database`. В fresh TRX нет классов `.Integration.`. EF process/native tests подставляют factory/handles/sessions; `StartCore` тестового handle не запускает процесс. HTTP использует stub handlers/local streams.

### Exact commands и связь evidence

Полный actual receipt: [commands.jsonl](../../../artifacts/stage16/commands.jsonl) —53 records с executable/argv либо helper parameters, cwd, start/end и exit; dotnet records содержат log SHA, helper records — generator SHA. [commands-expanded.ps1](../../../artifacts/stage16/commands-expanded.ps1) содержит все53 полностью раскрытые команды без placeholders; файл генерируется из receipt и не запускался. `Run-Stage16.ps1` сохраняет порядок, labels и fail-fast; `Verify-Stage16.ps1` независимо сверяет результаты и copies. Их actual launch commands, cwd AgentBridge:

```powershell
& 'C:/Users/Spike/Documents/Codex/Stage16-20261006/Run-Stage16.ps1' -Phase Snapshot
& 'C:/Users/Spike/Documents/Codex/Stage16-20261006/Run-Stage16.ps1' -Phase Suites
& 'C:/Users/Spike/Documents/Codex/Stage16-20261006/Run-Stage16.ps1' -Phase Kits
& 'C:/Users/Spike/Documents/Codex/Stage16-20261006/Run-Stage16.ps1' -Phase Consumers
& 'C:/Users/Spike/Documents/Codex/Stage16-20261006/Run-Stage16.ps1' -Phase Metadata
& 'C:/Users/Spike/Documents/Codex/Stage16-20261006/Verify-Stage16.ps1'
```

Каждая phase/verification завершилась Exit0. Receipt содержит20 successful restores,20 successful конкретных Build (warnings0/errors0),7 successful test commands,5 successful kit helper calls и1 ожидаемый negative Build Exit1. Helper success означает отсутствие исключения в PowerShell fail-fast script, а не отдельный native process exit. Builds/tests выполнялись последовательно; no-build после failed build не использовался. Negative RID control был после пяти successful consumers и до отдельного fresh Metadata build; его output не использовался для тестов.

Точные test commands (absolute project paths, cwd AgentBridge), все Exit0:

```powershell
dotnet test D:/Media/User/source/repos/agent-bridge/tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug -f net10.0 --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage16/ --logger 'trx;LogFileName=core-suite.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage16/trx
dotnet test D:/Media/User/source/repos/agent-bridge/tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj -c Debug -f net10.0 --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage16/ --logger 'trx;LogFileName=codex-suite.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage16/trx
dotnet test D:/Media/User/source/repos/agent-bridge/tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug -f net10.0 --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage16/ --logger 'trx;LogFileName=persistence-suite.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage16/trx --filter 'Dependency!=Database'
dotnet test D:/Media/User/source/repos/work/EFCoreLibrary/tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug -f net10.0 --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage16/ --logger 'trx;LogFileName=ef-maintenance.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage16/trx --filter 'FullyQualifiedName~CoordinatorTests|FullyQualifiedName~ProcessOwnershipTests|FullyQualifiedName~NativeBackupTests|FullyQualifiedName~ProviderTests'
dotnet test D:/Media/User/source/repos/work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug -f net8.0 --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage16/ --logger 'trx;LogFileName=http-net8.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage16/trx
dotnet test D:/Media/User/source/repos/work/HttpClientLibrary/HttpClientLibrary.Tests/HttpClientLibrary.Tests.csproj -c Debug -f net10.0 --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage16/ --logger 'trx;LogFileName=http-net10.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage16/trx
$env:AGENTBRIDGE_DELIVERY_ROOT='D:/Media/User/source/repos/agent-bridge/artifacts/delivery/stage16'
dotnet test tests/Delivery/Metadata/AgentBridge.Delivery.Metadata.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage16/ --logger 'trx;LogFileName=metadata.trx' --results-directory D:/Media/User/source/repos/agent-bridge/artifacts/stage16/trx
```

Каждая запись `<label>-test` связана в [verification.json](../../../artifacts/stage16/verification.json) с ровно одним successful `<label>-build`, завершившимся раньше test; `<label>-before-test-output.json` и `after-test-output.json` содержат SHA/size каждого DLL/XML/deps/runtimeconfig и остальных output files. Все семь before/after списков совпадают. Source snapshot до запуска и повторная сверка474 inputs совпадают; отдельные `core/codex/persistence-output.json` закрепляют production compile-check. Неизменность source + build timestamps + actual output/test DLL SHA связывает текущий source с run; это не доказательство каждого исторического TRX.

### Fresh TRX и проверенная область

| TRX в artifacts/stage16/trx | Passed | Failed | Skipped | Scope |
| --- | --- | --- | --- | --- |
| core-suite.trx | 502 | 0 | 0 | Полный core/Application, offline BPE, doubles |
| codex-suite.trx | 403 | 0 | 0 | Полный transport/JSON/SSE/compact, fake HTTP |
| persistence-suite.trx | 289 | 0 | 0 | Dependency!=Database, model/DI/mapping/UoW doubles |
| ef-maintenance.trx | 159 | 0 | 0 | Coordinator83, ProcessOwnership14, NativeBackup17, Provider40, SqliteProvider5 |
| http-net8.trx | 58 | 0 | 0 | Полный library suite net8.0, stub handlers/streams |
| http-net10.trx | 58 | 0 | 0 | Тот же suite net10.0; отдельная TFM совместимость |
| metadata.trx | 27 | 0 | 0 | Пять kits, PE/ELF/XML/closure/manifests/negative controls |

TRX counters и фактические result rows совпадают; outcomes не суммируются с predecessor suites, двумя HTTP TFM или повторяющимися kit cases. Core подтверждает существующие ContextBuilder/Compactor/full budget, ToolExecutor/AgentRunner attempt identity/no-replay, pinned settings и bounded cleanup; transport — known nested validation до fake HTTP, canonical continuation, cancellation и primary/cleanup; persistence — существующие UoW/CAS/facade/config/metadata границы. Реальная транзакционность и crash recovery doubles не доказываются. EF substring `~ProviderTests` также выбирает SqliteProviderTests5: это фактическая адресная область, не полный maintenance набор.

| Run | Test DLL SHA256 | TRX SHA256 |
| --- | --- | --- |
| core-suite | 7e221e3d20da66c95ae7e31139578b4a813e45c77bb9a6d8f08d73c56da8b3cb | 7df94f0092614c3adcd2d0343ff351a1942e6124be98649b6602f0055816ea4a |
| codex-suite | 2f82de6d62a91cc2e10b7394b7f11b947207b2b9d84520ba4b93bf90ed1338c7 | 8baa90d9bbc542779324779eb85bd12bea0ff20e94adbcf27ac25d27b3bf72c5 |
| persistence-suite | 0c40ab6acdc847875c63f387b01bd97ebe1345116d435208eab462886034e20f | 552b4794d4be8c35029cc869a3e19541d9043a728e39589f1bf923f6cfd60413 |
| ef-maintenance | 1200b2796923a7186fbbe4058b1e8564578374604a78879131578c5b63d1b6ea | 596bdc378626e388789cdf5b37ccfd41686e736098ab739ef2c1ec70edd47985 |
| http-net8 | 54ddcf000f308d53ae5ad3faacdb0d82e7804098691d5e051a3255434c538ecd | 497651834c33b6d1ccd00acf145ad44b0ece909eb73677c5789e734ce7caf92e |
| http-net10 | de43f50f5daea062956c648de8268006e6ea1334d49007405b3c5211df96b406 | b3ed74e41c05fb86142632b2d94db9c441b082c57eac5e497ea8b8e24e004c8c |
| metadata | 3adc1ba4d3ba484dbc138ecf4f0d6abe7d201336963c0ac239c6b8c3fb5d7e61 | 4216d1ba47b2454f2038f60c77f83c5932cdabb4e0d1093d13fa1f1bb9b74c38 |

### Пять новых комплектов и внешние consumers

Каждый variant последовательно прошёл свой offline restore → fresh SDK Build в `artifacts/stage16/sdk/<Provider>-<RID>` → неизменённый принятый `Assemble-Delivery.ps1` → новый destination `artifacts/delivery/stage16/<Provider>/<RID>`. Старые stage15 artifacts не переиспользовались и не удалялись. Manifest фиксирует текущий HEAD/EF/HTTP/SDK, assets/generator SHA, origin/asset/size/assembly/file versions/culture и SHA каждого файла. Package assets сверены helper с cache originals, project assets — с fresh SDK output; XML/generated files вручную не исправлялись. Common statically referenced provider dependencies не удалялись ради уменьшения kits.

| Kit | Managed DLL | XML | Satellites | Native | Manifest entries | Manifest SHA256 |
| --- | --- | --- | --- | --- | --- | --- |
| SqlServer/win-x64 | 64 | 57 | 13 | 3 | 140 | 78fba70ca00fa5a85a31da7c6f11e5d051483b4295f5a6c715116a39da279f34 |
| SqlServer/linux-x64 | 63 | 57 | 13 | 2 | 138 | 16dbe5c17de729e9ea2f2207c8b4cd8f197fe1a56e7ed43401d828fa2b45d29e |
| SqlServer/linux-arm64 | 63 | 57 | 13 | 1 | 137 | 626d4d422504ced9e38e9f311405e9a49cf359c7e913ead010fd36dfcc6088b8 |
| Sqlite/win-x64 | 64 | 57 | 13 | 3 | 140 | 4b39cc40fee2f94ba33dda600866050ec43583948579db62db166c983e28c6ba |
| PostgreSql/win-x64 | 64 | 57 | 13 | 3 | 140 | 4acc5e38f61931be10df80e77af71a37f18f37f8abbff6d2d9b1a7719387f1d8 |

Windows native: `e_sqlite3.dll`, `Microsoft.Data.SqlClient.SNI.dll`, `msalruntime.dll` — PE AMD64. Linux x64: `libe_sqlite3.so`, `libmsalruntime.so` — ELF64 little-endian x86-64. Linux ARM64: `libe_sqlite3.so` — ELF64 little-endian AArch64; отсутствующий MSAL ARM64 native не выдумывается, SDK его не выбирает. Во всех kits сохранены13 SqlClient culture satellites в `resources/<culture>/`, consumer получает `<culture>/` без flattening. SqlClient SDK-selected managed Windows/unix assets не смешиваются.

Пять consumers собраны вне repo с copy полного kit и всех текущих `.cs/.csproj` из `tests/Delivery/Consumer`. Ни ProjectReference, ни PackageReference, ни repo source paths в references не используются. Compiled short facade/IConfiguration/app logger, advanced tools/business scopes, legacy PostgreSQL и actual MSSQL server examples; app authorization/hosting/HTTP/DB методы не исполнялись. `verification.json` содержит SHA каждого consumer DLL и полных source/kit/output snapshots; все copied manifest/managed/XML/native/resources совпали с kit/SDK, consumer sources совпали с originals.

Metadata27 проверяет managed dependency closure/versions, выбранную migrations assembly, отсутствие tooling DLL, embedded tokenizer data, русский XML summary/interface/inheritdoc linkage, одинаковые MSSQL public signatures/XML трёх RID, exact case/portable paths, manifest size/SHA/SDK closure/cultures, PE managed против native PE/ELF и архитектуры. Negative controls отвергают missing/case/hash mismatch, escape/absolute paths, wrong architecture, malformed ELF и mixed OS/managed native. Actual consumer negative `wrong-rid-negative` импортирует SqlServer/linux-x64 kit в win-x64 Build: Exit1 с единственной ошибкой `Application RuntimeIdentifier must match AgentBridgeDeliveryRid (linux-x64)`; это ожидаемый отказ, не regression.

### Evidence hashes, ограничения и передача

| Evidence в artifacts/stage16 | SHA256 |
| --- | --- |
| source-snapshot.json,474 inputs | a9ffca225e65339ab73feb7f727061e504f5f3cd98924d8d68cdab182aefd203 |
| Run-Stage16.ps1 | e38308d99ed614539021f7b0cb945c48272b209210cc8e5ea386e249d69830d7 |
| Verify-Stage16.ps1 | 1721362f06f398c3f07162a77800283addce32dfd6727d1edb3ac37330d4ef9c |
| commands.jsonl | d7f7f7adb4288024c2abdbc8dba8a7321863341138dbfabb03bd27595f055a9f |
| commands-expanded.ps1 | 4191bb3f7a8b2d594657d56b89189dd7be866bd382d0594644579e7032d75f6a |
| verification.json | 0e453c9390110c8fa48d54e64d409f71a241c24478db202311f684caecf7fa92 |
| final-project-inputs.json | a3b9660d3fae74ab60a370a73c89366877066f6ae6d5ee3bf67947d6c5b6fe74 |
| package-build-inputs.json | 6ba6657d98d8865d330cb688c2647bf106efaf81923b4e16b450c4d6ac1eab28 |

Delivery generator SHA256 `be36909cd70e77311e202f8cd004d6bdc5beb230dedd55416d68632e95046270`, совпадает во всех пяти manifests. Все per-file source/binary/deps/assets/copy SHA доступны в связанных receipts/snapshots; mismatch0. Final doc проверен как UTF-8/LF без BOM, U+FFFD, четырёх вопросительных знаков и проверенных mojibake markers; scoped diff --check успешен. Исторические отчёты/Decisions/общий навигатор/Coordination не переписаны.

Выполнен только B scope16. Provider enforcement/CAS/rollback/backup/restore/native17, actual Windows/Linux x64/ARM64 runtime18, live catalog/JSON/SSE/compact/business authorization и process crash/recovery19 **не запускались**: согласованные реальные ресурсы/TLS/credentials/budget/cleanup отсутствуют в этом поручении. Cross-build на Windows не Linux runtime; fake UoW не SQL atomicity, fake handlers не deployed codex-lb. SQL Server Engine ARM64, AOT/trimming/single-file, Release и SQLite/PostgreSQL Linux matrix не проверены. Отсутствующий test не объявляется дефектом; общий счётчик confidence/исторических outcomes не вычислялся. CLI/spec не менялись и повторная OpenSpec validation не выполнялась.

Работа готова к независимой приёмке координатором. Writes остановлены; фактический commit возможен только после явной приёмки и отдельного поручения адресного add/commit этого единственного файла. Runtime17–19 и общую приёмку20 этот результат не закрывает.

**Приёмка16, 2026-10-06:** координатор от имени пользователя принял B-границу после независимого review Results/orchestration,53 actual commands/fresh build timestamps,7 TRX rows/counters,474 source SHA,7 unchanged before/after outputs,5 kits/external consumer copies/resources/generator и scoped diff/UTF-8. Production source не менялся; C/D17–19 остаются открытыми. Поручен локальный commit только этого файла; Coordination/ignored artifacts/foreign исключены. Предыдущие commands/evidence сохранены, повтор тестов без новой причины не выполнялся.
