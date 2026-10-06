# 05 — Хронология валидирующего Restore

[Навигатор](README.md). Статус: **принят в A/B-границе**. Зависимость: 00. Находка: **ABQA-005, S3, подтверждена статически**.

## Цель и область

Отклонять внешний snapshot, LastChangedAtUtc которого не может соответствовать принятой mutation при заданной revision/истории. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), [Dialog](../../../Domain/Dialogs/Dialog.cs) и [OpenSpec](../../../openspec/specs/agent-runtime/spec.md), валидирующее восстановление и локальные инварианты.

Работы в Domain и [DialogRestorationTests](../../../tests/AgentBridge.Tests/DialogRestorationTests.cs). Не вводить EF-зависимость в Domain, не лечить строки БД, не менять schema/migrations или обязательность timestamp полей.

## Работы

1. Добавить точный контрпример аудита: revision1, turns пусты, context(version1,prefix0,created=t0+1min), LastChanged=t0+2min при корректных UTC/owner/expiry. Restore должен отклонять состояние до выдачи агрегата.
2. Сформулировать проверку по реально известным mutation/revision границам. Не требовать LastChanged равным последнему child timestamp во всех состояниях: append имеет отдельную revision без отдельного persisted timestamp.
3. Точечно усилить фабрику; сохранить допустимые round-trip Create/Begin/Append/Complete/compact и вариант с дополнительной append revision, где LastChanged позже child date.
4. Проверить отсутствие изменения исходных snapshot collections при отказе. Сохранить lifetime/owner/fixed expiry и независимую settings version.
5. Сверить передачу времён через DialogStateLoader и context UoW. Их просмотренные штатные writes не порождали исходный контрпример; не объявлять их дефектом без нового доказательства.

## Проверки

- B: невалидный context-only snapshot и допустимый snapshot с extra append revision; existing corruption/round-trip cases.
- Границы UTC, created/expiry, revision overflow/несогласованность, prefix/status в затронутой фабрике.
- Через fake persistence loader неверное восстановление не разрешает дальнейшую запись; это не доказательство corrupt реальной БД.
- Compile-check ядра и затронутого test проекта; реальный persistence round-trip отдельно17.

## Критерии завершения

Контрпример отклонён, допустимые дополнительные mutations не запрещены. Новый инвариант защищён Domain без обходных setters/reflection/friend assembly. Физическое повреждение БД и штатное происхождение контрпримера не заявляются.

## Результаты

### Область и baseline

2026-10-06, Asia/Novosibirsk. Выполнено отдельное поручение05 в A/B-границе. Actual входной HEAD AgentBridge `6b8a01b2f64ffda382ca9a3f1fc437e28ca83252`; уже изменён чужой `Coordination.md`, index пуст. Принятые результаты00–04, README/Decisions, текущий нормативный Restore/short-write контракт main OpenSpec, ABQA-005 и итог15 аудита сверены. Применены root/Domain/tests/Documentation/Plans и EF adapter/UnitOfWork AGENTS, `csharp-project-rules` (style/domain-modeling/build-validation/agents-maintenance), `backend-uow-repositories` (backend-uow). Старые отчёты и принятые решения не переписаны; исходное задание05 сохранено, кроме статуса.

Exact manifest только в `D:/Media/User/source/repos/agent-bridge`:

| Файл | Изменение |
| --- | --- |
| `Domain/Dialogs/Dialog.cs` | Максимальное известное время Begin/terminal/context и проверка LastChanged по доступным revision/mutation границам |
| `Domain/AGENTS.md` | Устойчивый инвариант Restore; append без отдельного timestamp, settings independent version |
| `tests/AgentBridge.Tests/DialogRestorationTests.cs` | Различающий audit case, Begin/terminal controls, стадийные round-trip, UTC/date/revision bounds и overflow |
| `tests/AgentBridge.Persistence.EfCore.Tests/DialogWritePortsTests.cs` | Context-only fake-loader regression и штатный positive control |
| `Documentation/Plans/AgentBridge Audit Remediation/05-restore-chronology.md` | Статус и этот отчёт |

В соседних EFCoreLibrary/HttpClientLibrary/codex-lb собственного manifest нет. Их исходники read-only; transitive build output только в игнорируемых artifacts. EF/HTTP status чистый; LB сохраняет чужой untracked `.vs/`. Coordination исключён и может независимо обновляться координатором. Public API, schema/migrations, nullable timestamp requirements, настройки и штатные UoW writes не изменены. Новых веток/worktree/чатов/субагентов нет; Git mutations не выполнялись.

### Известные временные границы и изменение

Каждый Begin и terminal completion оставляет отдельный timestamp, каждый принятый context также оставляет timestamp. Их число даёт `minimumRevision`; максимум их дат вместе с CreatedAtUtc даёт `latestKnownChange`. При revision=minimumRevision скрытых append нет, поэтому LastChanged обязан равняться этому максимуму. Дополнительные revisions могут принадлежать append, однако append требует InProgress turn: если все turns уже terminal, после любого append есть timestamp completion, не более ранний. Поэтому и в этом случае LastChanged равен известному максимуму. При дополнительных revisions и наличии InProgress более поздний LastChanged допустим. Равенство времени разных mutations разрешено.

Это проверка необходимых границ по доступным данным, а не реконструкция всех append или доказательство происхождения snapshot. Settings.Version независимо от истории и не объясняет root mutation. Фабрика не исправляет входные данные и не выдаёт агрегат при отказе; проверки используют публичный Restore без reflection/setters/friend assembly.

Прочитаны `DialogStateLoader.LoadAsync`, `DialogContextUnitOfWork.SaveCoreAsync` и существующая передача дат через Domain/Apply root. Loader передаёт root.Revision/LastChanged и child timestamps без нормализации. Штатный context write получает обе даты из одной принятой Domain mutation. Positive fake-loader control подтверждает это на исполненном алгоритме. Нового evidence штатного происхождения corrupt state нет; loader/UoW не объявлены дефектными и не изменены.

### Before/after и controls

Тесты добавлены до production fix. На исходном Restore audit snapshot (revision1, turns пусты, context v1/prefix0/t0+1min, LastChanged t0+2min) и ещё три controls Begin/terminal/terminal-with-append с LastChanged на один tick позже известной последней mutation прошли фабрику: assertions `No exception was thrown`. Fake context writer после корректного compact получил вручную повреждённый persistence DTO root.LastChanged=t0+2min; следующий compact t0+3min был разрешён вместо ожидаемого отказа. Это синтетический внешний snapshot/committed fake row, не реальная БД.

| Run / `artifacts/stage05/` | Exit | Passed / failed / skipped | TRX rows |
| --- | --- | --- | --- |
| `core-before.trx` | 1 | 24 / 4 / 0 | 28 |
| `persistence-before.trx` | 1 | 1 / 1 / 0 | 2 |
| `core-after.trx` | 0 | 68 / 0 / 0 | 68 |
| `persistence-after.trx` | 0 | 24 / 0 / 0 | 24 |

Core after включает весь DialogRestorationTests (37), DialogTests (30) и existing `AgentSettingsTests.SelectionUsesValueIdentityAndIndependentVersion` (1). Before не включал девять новых date/revision bounds, добавленных после production fix; они не заявляются before reproduction. Round-trip покрывает Create, Begin, compact prefix0, дополнительный Append после compact (revision3, child latest t0+2min, LastChanged t0+3min), completion, compact terminal prefix и повторный compact, включая одинаковые времена. Проверены owner/fixed expiry, свежий lifetime и stale прежний snapshot. Corruption cases сохраняют входные turn/context lists. Long.MaxValue revision с active turn восстанавливается; следующая append вызывает OverflowException до изменения revision/LastChanged/детей.

Persistence after — весь DialogWritePortsTests: corrupt-loader case и correct context-only control, existing history corruption, guards, payload/fixed lifetime, terminal prefix и repeat compact, staging failure/committed data, CAS interleavings и delete/recreate. Corrupt case требует exact events `begin, rollback, dispose, clear`, отсутствия save/staged writes и неизменных committed root/context. Positive case сохраняет context version2 и root revision2 с одной новой датой. Пересекающиеся before/after counts не суммируются.

### Preflight, точные команды и артефакты

SDK `dotnet --version`: `10.0.401`, Exit0. Проверены concrete core/оба test csproj и полная source ProjectReference closure persistence tests (12 проектов), ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json/lock names до D:/, existing net10.0 assets, generated NuGet import lists и конфигурации из assets. Source custom Exec/Target/Import hooks не найдены; HTTP Directory.Build.props меняет output только собственного HTTP test project, не входящего в closure. Package hooks/installed SDK полностью не аудитировались. NuGet sources — existing nuget.org/offline; Telegram source disabled. DefaultItemExcludes присутствует, EF root GeneratePackageOnBuild=true подавлен false. Все шесть integration classes имеют Dependency=Database и исключены exact filter. Restore/network/install/pack/publish не выполнялись.

Все команды из cwd `D:/Media/User/source/repos/agent-bridge`, Debug, конкретные проекты, Build без Rebuild/solution. Все пять build команд Exit0/warnings0/errors0. Каждому test --no-build предшествует successful fresh build того же проекта/config/output; failed build/stale DLL evidence нет.

До production fix:

```powershell
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage05/ -flp:logfile=artifacts/stage05-core-before-build.log
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage05/ --filter 'FullyQualifiedName~DialogRestorationTests' --logger 'trx;LogFileName=core-before.trx' --results-directory artifacts/stage05
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage05/ -flp:logfile=artifacts/stage05-persistence-before-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage05/ --filter 'Dependency!=Database&FullyQualifiedName~DialogWritePortsTests.ContextOnlyChronologyIsValidatedBeforeWrite' --logger 'trx;LogFileName=persistence-before.trx' --results-directory artifacts/stage05
```

После fix и последних C# tests:

```powershell
dotnet build agent-bridge.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage05/ -flp:logfile=artifacts/stage05-core-after-build.log
dotnet build tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=artifacts/compile-check/stage05/ -flp:logfile=artifacts/stage05-domain-after-build.log
dotnet test tests/AgentBridge.Tests/AgentBridge.Tests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=artifacts/compile-check/stage05/ --filter 'FullyQualifiedName~DialogRestorationTests|FullyQualifiedName~DialogTests|FullyQualifiedName~AgentSettingsTests.SelectionUsesValueIdentityAndIndependentVersion' --logger 'trx;LogFileName=core-after.trx' --results-directory artifacts/stage05
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage05/ -flp:logfile=artifacts/stage05-persistence-after-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage05/ --filter 'Dependency!=Database&FullyQualifiedName~DialogWritePortsTests' --logger 'trx;LogFileName=persistence-after.trx' --results-directory artifacts/stage05
```

Relative BaseOutputPath создаёт отдельный output в каждом project directory. Before binaries затем обновлены after builds; отдельные logs/TRX сохранены, final hashes не выдаются за before identity. Outputs/logs/TRX только в игнорируемых artifacts. Final Get-FileHash SHA256:

| Файл относительно cwd | SHA256 |
| --- | --- |
| `artifacts/compile-check/stage05/Debug/net10.0/AgentBridge.dll` | `CFD738B76BCFC8A33FB5388D09320D83CF1BFA0B0CD7C1B8C1E133151EDFBF7E` |
| `tests/AgentBridge.Tests/artifacts/compile-check/stage05/Debug/net10.0/AgentBridge.Tests.dll` | `C9A07A845551A742C8E4038DC063492A3400523CACE60129FF33D042E6677881` |
| `tests/AgentBridge.Persistence.EfCore.Tests/artifacts/compile-check/stage05/Debug/net10.0/AgentBridge.Persistence.EfCore.Tests.dll` | `C890BCFE98AAD70D357868DB61DEF1BDE9A897684CFDAD677975E951FE85C47D` |

### Граница передачи

Actual: публичный Domain API и Restore, production loader/guard/context UoW/scope/repository adapters. Doubles: committed/staged base repositories и session/transaction; settings control использует existing fake ports. Это B evidence отказа до записи, не actual SQL atomicity/corrupt DB/provider enforcement/restart. Реальный persistence round-trip остаётся17; live/restart —19. SQLite in-memory/реальное I/O, приложение/hosting/HTTP/SQL/БД/Docker/native/business processes/deployment/project scripts/CLI не запускались. Полные core/persistence/maintenance/transport suites не запускались;05 проверен адресно, общая регрессия16 отдельно.

Ручные изменения через apply_patch; исходные UTF-8 без BOM/LF сохранены. Контроль strict UTF-8, U+FFFD/четырёх question marks/mojibake, исходного задания и git diff --check выполнен без находок. Первые discovery-команды ошиблись в предполагаемых именах Domain/Dialog.cs и settings test, а Windows rg не раскрыл positional wildcard; actual paths найдены повторным rg --files. Это ошибки чтения/поиска, не build/test failures. Git сообщает LF→CRLF при будущем касании; actual пять файлов остаются LF.

Локальных блокеров A/B и новых вопросов нет. ABQA-005 исправлен локально и готов к review в указанной границе. До явной приёмки/поручения координатора add/commit не выполняются; Coordination и чужие файлы исключены.

**Приёмка05, 2026-10-06:** координатор от имени пользователя принял этап в A/B после независимой проверки полного source/test diff и отчёта, необходимых chronology/minimumRevision/active append границ, сохранности исходного задания, UTF-8/LF пяти файлов, diff --check, actual before TRX24/28 и1/2, after68/68 и24/24 с rows/counters, свежих сборок и exact isolated filter. Поручен отдельный локальный fix-коммит только пяти файлов manifest; Coordination исключён. Code/tests после after не менялись. Реальные C17/БД/restart/D19 остаются открытыми; push не разрешён.
