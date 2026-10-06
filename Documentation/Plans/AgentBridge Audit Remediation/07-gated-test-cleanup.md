# 07 — Завершение gated test work при раннем выходе

[Навигатор](README.md). Статус: **принят в A/B-границе**. Зависимость: 00. Находка: **ABQA-010, подтверждённый пробел проверки, S4**.

## Цель и область

Гарантировать завершение и наблюдение начатых test tasks при assertion failure и timeout, сохраняя смысл исходного concurrency теста. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>), evidence14. Это надёжность тестов, а не доказанный production hang/leak.

Две отдельные области:

- [UnitOfWorkScopeTests.SharedGateRejectsReadAndWriteWhileFirstOperationIsPending](../../../tests/AgentBridge.Persistence.EfCore.Tests/UnitOfWorkScopeTests.cs) в AgentBridge.
- [CoordinatorTests.Fatal_failure_poison_precedes_release_to_waiter](../../../../work/EFCoreLibrary/tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs) в EFCoreLibrary, по отдельному поручению.

## Работы

1. Перенести release/cancel и await всех начатых задач в гарантированный finally. Использовать идемпотентное завершение сигналов; не оставлять задачу, которая ждёт gate без cancellation.
2. Сохранить действующие assertions порядка и blocking/poison. Cleanup не должен превращать assertion failure в pass или навсегда задерживать его наблюдение.
3. Проверить обе задачи/entrant в EF coordinator case, а не только первую. Bounded ожидания должны завершаться также при ранней ошибке.
4. Создать локальный различающий сценарий раннего выхода вокруг того же test lifecycle без намеренно падающего постоянного xUnit case. Проверить наблюдение исходной ошибки и завершённость tasks после cleanup; не добавлять runtime API ради теста.
5. Новые gated fixtures этапов01–06 сразу выполнять с finally/await, не откладывая их корректность до этого этапа. Не переписывать весь test suite механически.

## Проверки

B: обычный путь двух исходных тестов, ранняя assertion-подобная ошибка, timeout/cancellation. Все tasks завершены и observed; исходная failure сохраняется. Подставные gates/ports не создают БД/процессов.

Compile-check затронутых двух test проектов отдельно; для AgentBridge persistence — `Dependency!=Database`, для цепочек EF — `GeneratePackageOnBuild=false`. Не суммировать повторные runs.

## Критерии завершения

Оба исходных места исправлены и адресно проверены. Если разрешена только одна библиотека, ABQA-010 закрывается лишь частично. Нет unobserved/pending work после раннего выхода; прежний смысл assertions сохранён.

## Результаты

### Область и baseline

2026-10-06, Asia/Novosibirsk. Реализовано отдельное поручение07: только lifecycle двух указанных тестов, локальные regression helpers и этот отчёт. Прочитаны задание07, результаты00/01/02, README/Decisions плана, ABQA-010/итог15 аудита, актуальные требования main OpenSpec, root/tests и Documentation/Plans AGENTS. Применены `csharp-project-rules` с style/build-validation и `backend-uow-repositories` с reference. Production API, зависимости, AGENTS, historical reports и принятые решения не менялись.

Actual вход: AB HEAD `9224f1e8ec74f1f4e0fefc06988f1ec88b150c08`, изменён только чужой `Documentation/Plans/AgentBridge Audit Remediation/Coordination.md`; EF HEAD `bdb0e36bc1d52fb4b5a028559cd65373b4bad704`, чисто. Обязательный00 принят commit `bfd09fda45d5d36f97feec2ee0f3af3a18912acd`; исправления01/02 и их результаты сохранены. EF existing fatal case уже имеет finally/release/await после01 — его намеренно не удаляли для получения failing evidence.

Exact manifest:

| Repo | Файл | Изменение |
| --- | --- | --- |
| AgentBridge | `tests/AgentBridge.Persistence.EfCore.Tests/UnitOfWorkScopeTests.cs` | Shared lifecycle исходного case, finally/release/bounded observation, три early-exit controls |
| AgentBridge | `Documentation/Plans/AgentBridge Audit Remediation/07-gated-test-cleanup.md` | Статус и результаты07; исходное задание сохранено |
| EFCoreLibrary | `tests/EFCoreLibrary.Maintenance.Tests/CoordinatorTests.cs` | Shared lifecycle исходного fatal case, bounded observation обоих tasks, шесть early-exit controls |

Coordination принадлежит координатору, исключён. EF fixture, production, csproj, migrations/generated, HTTP/LB source не изменены. Новых чатов/субагентов/веток/worktrees нет.

### Изменение и before/after

Оригинальные cases и регрессии вызывают один локальный helper своего lifecycle. AB сохраняет pending first, write rejection, read rejection, ReadCalls0, successful first и доступное чтение после освобождения gate. EF сохраняет entered handshake, pending waiter, fatal first, GatePoisoned waiter и неизменное число inspections. Helper не заменяет actual gate подставным готовым результатом.

AB впервые освобождает completion и наблюдает first в finally. EF сохраняет принятый finally01 и запускает наблюдение owner/waiter вместе через WhenAll: ошибка ожидания одного не пропускает начало наблюдения другого. Сигналы завершаются идемпотентно через TrySetResult; локальное ожидание completion/fail получает watchdog cancellation5s, cleanup имеет bound10s. EF waiter использует existing maintenance budget3s. Отказ bounded cleanup при уже имеющейся ошибке теста сохраняет обе причины в test-only AggregateException; при successful cleanup наружу выходит тот же primary object со stack. Ветка дополнительного cleanup timeout проверена статически; искусственная навсегда зависшая production task не создавалась.

Регрессии прерывают тот же путь до release: actual `Assert.Fail`, actual `WaitAsync(30ms)` timeout и отмена observer wait с исходным token. Primary сохраняется через ExceptionDispatchInfo; после helper проверяются identity/type/token и terminal состояние исходных tasks. AB first должен уже быть IsCompletedSuccessfully/Success; EF owner и entrant должны уже быть IsFaulted с конкретными fatal/GatePoisoned codes. Поэтому окончание только WaitAsync wrapper не считается завершением source task. Отмена observer не объявляется отменой maintenance/UoW operation: first освобождается нормальным release. Оба early сценария — постоянные passing xUnit tests ожидаемой ошибки, намеренно failing final case отсутствует.

У regression harness есть отдельный finally/safety release и bounded observation. Он исполняется **после** assertions terminal state и не способен сделать их успешными задним числом; before-run не оставляет pending/unobserved work. Before AB использует extraction исходного lifecycle без нового helper-finally. Все три controls падают непосредственно на IsCompletedSuccessfully=False до safety cleanup; исходная ошибка уже совпала по identity/type/token. Normal case проходит. Before EF8/8 честно подтверждает действенность finally01 и закрывает прежний пробел early-exit evidence; нового EF production hang/leak не заявлено.

| Run / TRX относительно cwd repo | Exit | Passed / failed / skipped |
| --- | --- | --- |
| AB `artifacts/stage07/ab-before.trx` | 1 | 5 / 3 / 0 |
| EF `artifacts/stage07/ef-before.trx` | 0 | 8 / 0 / 0 |
| AB `artifacts/stage07/ab-after.trx` | 0 | 17 / 0 / 0 |
| EF `artifacts/stage07/ef-after.trx` | 0 | 8 / 0 / 0 |
| AB `artifacts/stage07/ab-final.trx` | 0 | 17 / 0 / 0 |
| EF `artifacts/stage07/ef-final.trx` | 0 | 8 / 0 / 0 |

AB before filter SharedGate также выбрал четыре existing DatabaseMaintenanceTests.PartialFailurePoisonsSharedGate controls: они прошли; пятый pass — исходный UoW normal case. Final AB17 — весь небольшой UnitOfWorkScopeTests: original14 плюс early3. EF8 — original fatal2 плюс early6 (migration/cleanup × assertion/timeout/cancellation). After и final пересекаются и не суммируются. Final выполнен после последних изменений сохранения primary при secondary bounded-cleanup error. Это fresh evidence07, не historical TRX01–06.

### Preflight и точные команды

`dotnet --version`:10.0.401, Exit0. Проверены оба конкретных test csproj и рекурсивный ProjectReference graph (14 проектов), ancestor Directory.Build.props/targets/Directory.Packages.props/NuGet.config/global.json/lock names до корня диска, DefaultItemExcludes, source imports/Exec/custom targets и existing assets/TFM/package sources. Source executable hooks в graph не найдены. Единственный ancestor source props — HTTP Directory.Build.props с output/intermediate только его собственного test project. Delivery consumer import вне graph. EF root packaging=true подавлен GeneratePackageOnBuild=false. Assets net10.0 существуют, HTTP имеет также net8.0; sources existing nuget.org/SDK local source. Это не аудит installed SDK/package-generated hooks. Restore/network/install/pack/publish не выполнялись.

Все шесть AB Integration classes имеют Dependency=Database; адресные AB runs дополнительно исключают их `Dependency!=Database`. Прочитанные выбранные методы используют doubles/tasks, без БД, SQLite in-memory, native/process/HTTP/hosting. Компиляция transitive providers/HTTP/migrations не исполняет эти интеграции.

Ниже AB cwd = `D:/Media/User/source/repos/agent-bridge`; EF cwd = `D:/Media/User/source/repos/work/EFCoreLibrary`. BaseOutputPath относительный, output отдельный в каждом project directory. Конкретные builds выполнялись последовательно, без solution/Rebuild; independent test runs двух repo — параллельно. Все builds Exit0. Первый AB compile показал xUnit2020 на Assert.True(false); до before-test исправлено на Assert.Fail и выполнен свежий build. Все builds, соответствующие TRX ниже, warnings0/errors0. Failed build/stale --no-build отсутствуют.

AB before:

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ -flp:logfile=artifacts/stage07-ab-before-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ --filter 'Dependency!=Database&FullyQualifiedName~SharedGate' --logger 'trx;LogFileName=ab-before.trx' --results-directory artifacts/stage07 --diag artifacts/stage07-ab-before-test.log
```

EF before:

```powershell
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ -flp:logfile=artifacts/stage07-ef-before-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ --filter 'FullyQualifiedName~Fatal_failure_' --logger 'trx;LogFileName=ef-before.trx' --results-directory artifacts/stage07 --diag artifacts/stage07-ef-before-test.log
```

AB after/final (каждый test после соответствующего successful build):

```powershell
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ -flp:logfile=artifacts/stage07-ab-after-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ --filter 'Dependency!=Database&FullyQualifiedName~UnitOfWorkScopeTests' --logger 'trx;LogFileName=ab-after.trx' --results-directory artifacts/stage07 --diag artifacts/stage07-ab-after-test.log
dotnet build tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ -flp:logfile=artifacts/stage07-ab-final-build.log
dotnet test tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ --filter 'Dependency!=Database&FullyQualifiedName~UnitOfWorkScopeTests' --logger 'trx;LogFileName=ab-final.trx' --results-directory artifacts/stage07 --diag artifacts/stage07-ab-final-test.log
```

EF after/final:

```powershell
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ -flp:logfile=artifacts/stage07-ef-after-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ --filter 'FullyQualifiedName~Fatal_failure_' --logger 'trx;LogFileName=ef-after.trx' --results-directory artifacts/stage07 --diag artifacts/stage07-ef-after-test.log
dotnet build tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ -flp:logfile=artifacts/stage07-ef-final-build.log
dotnet test tests/EFCoreLibrary.Maintenance.Tests/EFCoreLibrary.Maintenance.Tests.csproj -c Debug --no-build --no-restore -p:GeneratePackageOnBuild=false -p:BaseOutputPath=artifacts/compile-check/stage07/ --filter 'FullyQualifiedName~Fatal_failure_' --logger 'trx;LogFileName=ef-final.trx' --results-directory artifacts/stage07 --diag artifacts/stage07-ef-final-test.log
```

Final DLL SHA256 (Get-FileHash, Exit0):

| Repo / DLL относительно cwd | SHA256 |
| --- | --- |
| AB `tests/AgentBridge.Persistence.EfCore.Tests/artifacts/compile-check/stage07/Debug/net10.0/AgentBridge.Persistence.EfCore.Tests.dll` | `3CCCDBA4503950163E0C72AAA99F17C965F4038E3B3B5D15CD44994B40C28851` |
| EF `tests/EFCoreLibrary.Maintenance.Tests/artifacts/compile-check/stage07/Debug/net10.0/EFCoreLibrary.Maintenance.Tests.dll` | `3C631184E14825BA746BBD6AD6AFA65F0FB1135FF85A89446FC4055EAF815BAF` |

TRX/logs/DLL хранятся только в игнорируемых artifacts. Before/after используют обновляемый stage07 output; отдельные logs/TRX сохранены, final hashes не являются before binary identity.

### Передача и ограничения

Actual: UnitOfWorkScope/PersistenceOperationGate/ExpiredDialogReader и DatabaseMaintenance/SingleInitializerGate/MaintenanceBudget. Doubles: FakeUnitOfWorkSession/transaction/base repositories и MaintenanceFixture provider/migration/pin; gates — локальные TaskCompletionSource. Normal, early assertion, timeout observer и cancellation observer завершают и await начатые задачи, исходные concurrency assertions сохранены. ABQA-010 закрыт в выбранной локальной A/B-границе обоих исходных мест; на момент первичной передачи независимая приёмка координатором ожидалась. Это не production hang/leak, provider isolation, actual timeout ресурсов или общее завершение аудита. C/D проверок этап07 не требует и не заменяет этапы17–19.

Не запускались остальные suites, БД/SQL/migrations tooling, приложение/hosting/Docker, live HTTP/codex-lb, native/process business operations, scripts/CLI/install/deployment. Других вопросов или блокеров B нет.

Ручные изменения через apply_patch; исходные UTF-8 без BOM/LF сохранены. Финальный контроль трёх manifest files: strict UTF-8, без U+FFFD/четырёх question marks/проверенных mojibake markers; diff --check обеих repo Exit0. Исходное задание07 до Results совпадает с HEAD кроме статуса. Final statuses: AB — только два собственных manifest files плюс чужой Coordination; EF — только CoordinatorTests; HTTP clean; LB сохраняет чужую untracked .vs/. Add/commit не выполнялись. Готово к review; остановка до явной приёмки и поручения на отдельные commits по repo.

**Приёмка07, 2026-10-06:** координатор от имени пользователя принял A/B после независимой проверки полных source diffs, original lifecycle/early cases/primary preservation, fresh builds/точных команд, final TRX AB17/17 и EF8/8, before AB5/8 и EF8/8, UTF-8 трёх manifest files и diff checks. ABQA-010 закрыт локально в обеих исходных областях; production hang/provider/runtime не заявлены. Повторный рекурсивный обход двух test csproj подтвердил **14 уникальных проектов**; это число уже указано в актуальном preflight и handoff. Поручены отдельные `test:` commits: EF — только CoordinatorTests; AB — только UnitOfWorkScopeTests и этот отчёт. Coordination исключён. После final evidence code/tests не менялись; из-за документальной записи тесты повторно не запускались.
