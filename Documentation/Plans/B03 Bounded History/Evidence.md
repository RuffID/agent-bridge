# B03: fresh isolated evidence

Дата: 11.10.2026. Root: `D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011`. Историческое Ledger evidence `assistant03-20261011` не используется для подтверждения этих изменений.

## Проверенные inputs и ограничения

Root/local AGENTS, csharp-project-rules style/build/AGENTS references, backend-uow и DI skills, конкретные csproj/locks/import/targets и source consumers проверены. Тестовый base I/O fake применяет actual EF predicates/SQL size expression/Take до выдачи строк. SQL provider translation использует disconnected context, соединение остаётся Closed. Реальных HTTP/model/SQL/DB/Docker/hosting/browser/dev/watch/publish/deployment/Git не было. После отдельного разрешения выполнены ровно четыре packaging commands; другие project scripts/Prepare-DeliveryTests/Metadata build hooks не исполнялись. GeneratePackageOnBuild=false исключает соседний pack hook; все build intermediates ProjectReference размещены в AgentBridge artifacts, соседние source checkouts не изменялись.

Restore только из `--source C:/Users/Spike/.nuget/packages`, NuGetAudit=false, без сети. Existing source locks и accepted Ledger locks не менялись. Новые independent external consumers сначала сгенерировали собственные locks обычным offline restore (`--use-lock-file -p:RestoreLockedMode=false`), затем все positive RID restores выполнялись в locked mode; negative drift копирует неизменённый lock и меняет только test project PackageReference. Assertions/exclusions/thresholds/schema/migrations/production package versions не ослаблены/не изменены. В репозитории AgentBridge threshold config не обнаружен: покрытие приводится как измерение, без выдуманного threshold acceptance. Ledger thresholds здесь не применялись.

## Тесты и compilation

| Проект | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| AgentBridge.Tests | 542 | 0 | 0 |
| AgentBridge.CodexLb.Tests | 414 | 0 | 0 |
| AgentBridge.Persistence.EfCore.Tests, только Dependency!=Database | 470 | 0 | 0 |
| **Всего** | **1426** | **0** | **0** |

**48 новых B03 cases**: BoundedHistoryQueryTests15 + BoundedHistoryTests33. Покрыты inclusive records/bytes/query limits и limit+1, 1000 scalar candidate rows с bounded выдачей, trailing spaces/NUL/Unicode; count/byte growth между queries; shared operation budget и cancellation/I/O. SQL size/payload translation проверена для всех10 mapped tables, включая complex envelopes/errors/continuation/journal и binary owner.

Actual reader tests:20 малых turns; oversized item/report/tool attempts/compact/recovery pairs/result/root/profile; JSON byte boundary без normalization;5000 recovery records отказываются до payload/JSON; owner/site/agent/profile/expiry/tombstone до canonical; final root/revision/incarnation/runtime/delete/profile/tombstone/expiry races без disclosure. Canonical unchanged/no-save assertions сохраняются.

Actual runner/internal loader tests: начальная oversized history до providers/model, strict policy omission до reader I/O, oversized old compact в Begin с rollback/no-save, explicit new turn после append-only Unknown/function-pair recovery, repeated call_id/no old-turn replay, fencing late writer и Unknown commit в fresh scope. Existing core regressions проверяют whole model payload budget guard, context/executor/compact/partial/cleanup; B03 использует ту же actual orchestration. Это не SQL/provider crash/restart acceptance.

Debug builds трёх конкретных test csproj:0 errors/warnings. Release SDK Build SqlServer win-x64/linux-x64/linux-arm64:0 errors/warnings. SDK10.0.401; final absolute Windows PathMap нормализует PDB/generated paths. Первоначальный forward-slash PathMap не нормализовал CodeView и дал RID hash mismatch; исправлены build inputs, DLL между RID не копировались. Финальные10 local DLL и9 XML одинаковы по SHA-256 всех RID, `sdk-own-hashes.json`; например AgentBridge.dll = `5eb38395285750ae62a4815e9187023192c7ee731fccb8de4b8652cb7a675b8f`.

`binary-api-probe/BinaryApiProbe.csproj` компилирует actual `tests/Delivery/Consumer/BoundedHistoryRegistration.cs` напрямую против трёх SDK DLL outputs, без ProjectReference и без исполнения методов; три compile-check0 warnings/errors. Подтверждены public registration/limits/strict reader/runner symbols. Этот probe не заменяет external full kit/props/locks acceptance.

## Fresh coverage

Pinned cached dotnet-coverage18.9.0 собирает три полных isolated runs; merge принимает ровно `coverage/core.cobertura.xml`, `coverage/codexlb.cobertura.xml`, `coverage/persistence.cobertura.xml`. Historical/targeted reports не включаются.

| Production assembly | Line | Branch |
| --- | ---: | ---: |
| AgentBridge | 94.99% | 91.08% |
| AgentBridge.Persistence.EfCore | 91.97% | 78.96% |
| AgentBridge.Persistence.SqlServer | 91.76% | 84.62% |
| AgentBridge.CodexLb | 96.06% | 87.27% |

Unrounded rates — `coverage/production-rates.json`; merged report — `coverage/merged.cobertura.xml`. Reports собраны после production changes; subsequent docs/sample не меняют production execution inputs.

## Повторяемые команды

Для конкретного `<project>` core/CodexLb/persistence:

```text
dotnet restore <project> --locked-mode --source C:/Users/Spike/.nuget/packages -p:NuGetAudit=false -p:GeneratePackageOnBuild=false -p:UseArtifactsOutput=true -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/build -p:ArtifactsPivots=debug --nologo
dotnet build <project> -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:UseArtifactsOutput=true -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/build -p:ArtifactsPivots=debug --nologo
dotnet test --project <project> -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:UseArtifactsOutput=true -p:ArtifactsPath=D:/Media/User/source/repos/agent-bridge/artifacts/b03-bounded-history-20261011/build -p:ArtifactsPivots=debug --output Detailed
```

Persistence **обязательно** добавляет `--filter-not-trait Dependency=Database`. Hosting.Tests/Delivery.Metadata build hooks не запускать в этом scope. Для collect pinned DLL:

```text
dotnet C:/Users/Spike/.nuget/packages/dotnet-coverage/18.9.0/tools/net8.0/any/dotnet-coverage.dll collect '<точная dotnet test команда выше, persistence с фильтром>' -f cobertura -o <fresh report>
dotnet C:/Users/Spike/.nuget/packages/dotnet-coverage/18.9.0/tools/net8.0/any/dotnet-coverage.dll merge artifacts/b03-bounded-history-20261011/coverage/core.cobertura.xml artifacts/b03-bounded-history-20261011/coverage/codexlb.cobertura.xml artifacts/b03-bounded-history-20261011/coverage/persistence.cobertura.xml -f cobertura -o artifacts/b03-bounded-history-20261011/coverage/merged.cobertura.xml
```

Release restore/build для каждого `<rid>`: `tests/Delivery/Build/AgentBridge.Delivery.csproj`, `DeliveryProvider=SqlServer`, `RuntimeIdentifier=<rid>`, `Configuration=Release`, `GeneratePackageOnBuild=false`, `UseArtifactsOutput=true`, `ArtifactsPath=D:\Media\User\source\repos\agent-bridge\artifacts\b03-bounded-history-20261011\sdkbuild\SqlServer-<rid>`, `ArtifactsPivots=release`; Build `--no-restore`, `PathMap=<тот же абсолютный Windows build root>=/_/delivery-build`. Restore local source/locked/NuGetAudit=false как выше. Final logs `release-<rid>-restore.log`, `release-<rid>-build.log`.

Initial failed attempts сохранены: unsupported report-trx switches давали0 cases/exit5, test help attempt SDK failure; compile-only test typo/style исправлены. Эти runs не acceptance; финальные полные successful runs и fresh reports указаны выше.

## Kit / baseline

Own production/delivery inputs hashes: `agentbridge-source-inputs.sha256`; neighboring read-only references — `ef-source-inputs.sha256`, `http-source-inputs.sha256`; digest `source-fingerprints.json`. Алгоритм: sorted relative paths + lowercase SHA-256 per file, LF, UTF-8 no BOM, SHA-256 всех строк; selection находится в inventory. Это source fingerprints, не Git revision.

`changed-inputs.json` перечисляет43 изменённых source/docs/project inputs с hashes/BOM/newline type: strict UTF-8 decoding успешно, mixed CRLF/LF, U+FFFD и повреждённые последовательности отсутствуют. XML solution/example parsed успешно; `public-api-xml.json` фиксирует actual XML members новых контрактов/регистрации/runner. Эти checks не исполняют DLL.

Все три SDK assets имеют exact EF10.0.12/SqlClient7.0.2. После отдельного прямого разрешения выполнены [четыре точные команды](Packaging-commands.md), без изменения упаковщиков. Complete kit: `D:/Media/User/source/repos/agent-bridge/artifacts/b03-delivery-20261011/SqlServer`; manifest SHA-256 `fd66709eb625acc138b90ed5da1834bf0a988bb10258630ef43ecb4a84dac633`. Schema3/net10.0/Release,10 own DLL/9XML/3props/12 required roots. `packaging-results.json` содержит hashes трёх SDK kits и bundle, source kits114/112/112 files, bundle22 entries.

## External full-kit acceptance

Consumer/probe root вне всех репозиториев: `C:/Users/Spike/AppData/Local/Temp/AgentBridge.B03.20261011`. Complete kit скопирован без изменений в `delivery`; исходники Consumer/Compatibility и selected BoundedHistoryQueryTests/FakeHistoryQueryRepository скопированы для проверки **actual DLL**, без ProjectReference. FrameworkReference не запускает host. Methods compile-only consumer не исполняются. Приёмка охватывает SqlServer.

- Full Consumer: local-cache-only locked restore/build win-x64/linux-x64/linux-arm64, каждый0 warnings/errors. Новый actual registration/reader/runner API sample компилируется из поставленных DLL/props.
- Windows external binary project: **29 passed,0 failed/skipped**. Existing binary compatibility3 (closed SQL context/repository/maintenance DI и fake HTTP pipeline), actual bounded query15 из source tests, focused kit metadata11 (полный manifest/file hashes/size/case/portable References; три source SDK closure/12 exact roots; missing/case/hash negative snapshot controls; три RID/native metadata/deps/SqlClient PE reference; public binary API/XML).
- Native metadata reading не запускает Load/Free или SQL provider. Linux kits по actual resolved SqlClient7 graph не имеют native assets; Windows native имеет PE AMD64 без managed metadata.
- Семь ожидаемых exact/framework/RID/lock отказов: conflicting SqlClient6.1.6; duplicate direct7.0.2; net9.0; lock-disabled; missing RID; unsupported linux-riscv64; locked Serilog4.3→4.4 drift с **неизменённым copied lock**. Все дали expected explicit target либо NU1004, не fallback/cache-miss failure. `negative-guards.json` содержит точные команды/exit codes; `negative-*.log` — outputs.

Repro inputs/сгенерированные locks сохранены в `binary-consumer-inputs`, hashes — `binary-consumer-input-hashes.json`. Logs: `binary-Consumer-initial-restore.log`, `binary-Compatibility-initial-restore.log`, `binary-consumer-<rid>-{restore,build}.log`, `binary-tests-{restore,build}.log`, `binary-tests.log`. External project imports `-p:AgentBridgeDeliveryRoot=<probe>/delivery` после direct PackageReferences; initial offline lock generation только для новых consumer projects. Positive locked/build commands используют явный RID, `GeneratePackageOnBuild=false`; test выполняется Windows Release `dotnet test --project <probe>/Compatibility/AgentBridge.BinaryCompatibility.Tests.csproj --no-build --no-restore --output Detailed` с теми же properties. Содержимое ready/preparation gate старой full matrix не подменялось; packaging scripts не копировались в harness.

Разрешённая B03 library/isolation/delivery граница **Completed**. Остаток AgentBridge B03 отсутствует; отдельная следующая работа — проверка/принятие kit в Ledger и весь mandatory03. Coverage source report1426 остаётся свежим: packaging/harness/docs не меняют production source. Binary29 — отдельное evidence, не дополнительные уникальные source cases.

Ledger baseline `libs/AgentBridge.SqlServer/delivery.manifest.json` повторно проверен: `5fd8ddabe234857a68a79d49a74079e384920e7a122af7536dc54b7b97d9206a`; он не заменён. Production config не читался. Модель Ledger/effort/approximate profile не изменялись.
