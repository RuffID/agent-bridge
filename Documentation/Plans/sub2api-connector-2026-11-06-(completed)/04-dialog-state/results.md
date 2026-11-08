# Актуальный результат этапа04 — PostgreSQL restart и recovery

Дата: 2026-11-08. Этап04,04.02 и04.02.04 — **completed** в согласованном объёме первой версии без нового compact. [Этап](main.md) · [04.02.04](04.02.04-persistence-restart.md) · [Главный план](../main.md). Этап05 не начат; T01/T02/T04/T05 не закрываются этой проверкой.

## Выполненный объём

По отдельному поручению добавлен Integration/Sub2Api:5 PostgreSQL cases в отдельном Sub2ApiPersistenceRestartTests, facade fixture, технический session decorator для потери commit acknowledgement и локальный AGENTS. В родительском Integration уже20 собственных C# файлов; самостоятельная группа сразу вынесена в подпапку, без переноса прежних тестов. Production-код и схема не менялись; новые C# файлы меньше20000 байт.

Использованы actual public AddAgentBridgeSub2ApiConnector/AddAgentBridge, AgentRunner.RunAsync с registered RunOptions, Sub2Api/HttpClientLibrary и production persistence ports/UoW/base repositories EFCoreLibrary. Existing DatabaseIntegrationFixture создаёт postgres:18 с отдельным loopback-портом, пользователем и уникальными БД; fsync/full_page_writes/synchronous_commit включены. Инициализация схемы выполняется настоящим maintenance API. Клиентские утилиты PostgreSQL18 найдены в реестре и проверены штатным bounded runner; backup/restore в этих случаях не выполнялись. Только HTTP handler и инструмент приложения подставные; рабочая конфигурация и реальные credentials не читаются.

Проверено:

- JSON и SSE: committed Started виден отдельному DbContext до handler, полный turn/payload/provenance восстанавливается после disposal facade root и создания нового DI root. Следующая generation содержит opaque reasoning с неизвестным полем, первый вопрос/ответ, прежнюю call/output пару с executionId и новый вопрос. Новый handler не вызывается, automatic compact/continuation отсутствуют.
- Потеря acknowledgement после настоящего commit Started и Succeeded. Внешний outcome остаётся неизвестным вызывающей стороне; primary IOException и реальный отказ rollback завершённой NpgsqlTransaction сохраняются вместе. Новый root видит Active и отклоняет run без HTTP. Живой lease запрещает fence; expiry equality на управляемом server TimeProvider разрешает fence с новым epoch. Renew прежнего lease отклоняется. Succeeded использует сохранённый output; Started превращается в effective Unknown и требует явного AcknowledgedUnknown, без ложного NotStarted и replay.
- Отмена уже начатого handler: Unknown/RecoveryRequired сохраняются в БД и блокируют новый run после restart. Explicit recovery записывается append-only; третий DI root читает его по RecoveryId и продолжает только новый turn без handler replay. После fence/recovery/нового turn исходные SQL rows DialogTurns/CanonicalItems/ModelSteps прежнего turn совпадают побайтно в наблюдаемой JSON-проекции строк.

Первый запуск обнаружил ошибку подготовки: opt-in RunBoundedAsync требует отдельной регистрации IBoundedDialogReader. Fixture исправлен на public RunAsync с registered RunOptions — тот же путь использует реальная консоль; production bounded policy не добавлялась. Следующий запуск подтвердил2 happy-path cases; assertions отказов уточнены по established UnitOfWorkScope contract: после actual commit сохраняются обе причины AggregateException. Создание scoped gateway может получить app-owned HttpClient без I/O; отсутствие отправки проверяется по пустому HTTP journal, а не по вызову фабрики. Финальный полный прогон5 cases прошёл. Ожидания безопасности, сохранности history и no-replay не ослаблялись.

Также исправлено устаревшее isolated pinned-run ожидание: вместо общего /v1/ проверяются точный /backend-api/codex/models и два /v1/responses.11 новых DialogCanonicalJson cases и регрессия manifest-маршрута из предыдущей работы теперь выполнены: актуальные isolated результаты ниже. Исторические записи предыдущих прогонов сохранены после разделителя.

## Команды и фактический результат

```powershell
dotnet build .\tests\AgentBridge.Persistence.EfCore.Tests\AgentBridge.Persistence.EfCore.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false --nologo -v quiet
dotnet .\tests\AgentBridge.Persistence.EfCore.Tests\artifacts\compile-check\Debug\net10.0\AgentBridge.Persistence.EfCore.Tests.dll -class 'AgentBridge.Persistence.EfCore.Tests.Integration.Sub2Api.Sub2ApiPersistenceRestartTests' -trait 'Dependency=Database' -noLogo -longRunning 30
dotnet .\tests\AgentBridge.Persistence.EfCore.Tests\artifacts\compile-check\Debug\net10.0\AgentBridge.Persistence.EfCore.Tests.dll -trait- 'Dependency=Database' -noLogo
docker ps -a --format '{{.ID}} {{.Names}} {{.Image}}'
```

Build:0 warnings/errors. Последний PostgreSQL прогон:5 passed,0 failed/errors/skipped/not-run,9,941 с с подготовкой/cleanup. Последний isolated persistence:510 passed,0 failed/errors/skipped/not-run,3,534 с. Время не включает написание тестов и предшествующие попытки подготовки. После завершения docker ps -a пуст; каталог agentbridge-integration в temp не содержит собственных тестовых подкаталогов. Ручного удаления ресурсов и Docker prune не было.

Актуальные результаты из этой сессии без суммирования повторов:

| Набор | Passed | Failed | Errors | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Core, isolated | 569 | 0 | 0 | 0 |
| Sub2Api, isolated | 178 | 0 | 0 | 0 |
| CodexLb, isolated | 422 | 0 | 0 | 0 |
| Persistence, isolated | 510 | 0 | 0 | 0 |
| Console, isolated | 63 | 0 | 0 | 0 |
| PostgreSQL, только новый класс04.02.04 | 5 | 0 | 0 | 0 |

1742 isolated cases и отдельно5 PostgreSQL cases. Core/Sub2Api/CodexLb/Console выполнены перед добавлением этой группы; production-код после них не менялся. Первый совместный CodexLb запуск был прерван без результата; отдельный диагностический повтор прошёл422 cases и только он учтён. Изначальный isolated persistence failure из-за URL устранён и510 cases повторно прошли. API/frozen kit и legacy consumers не пересобирались.

## Существенные границы

Это reconnect приложения с новым DI/DbContext над настоящей PostgreSQL, без рестарта PostgreSQL/ОС. Прерывание и потеря acknowledgement вводятся управляемо после фактических записей; физический обрыв сети или kill процесса не выполнялись. Unknown не выдаётся за подтверждённый внешний исход. SQLite/MSSQL и весь исторический maintenance-набор не запускались без нового риска. Live OAuth/sub2api, DLL-поставка/consumer ABI, другие RID и strict OpenSpec CLI остаются задачами05; этот этап не начат. Compact и основание T05 не менялись.

Ручные изменения выполнены через apply_patch. Соседние репозитории, baseline kit/frozen DLL, исторические01–02/evidence, completed03.03.01 и main OpenSpec spec не менялись. Git, OpenSpec CLI, project scripts, host, live HTTP/OAuth и deployment не выполнялись. Разрешённые SQL/миграции/Docker ограничены собственными ресурсами existing integration fixture.

Ниже сохранены предшествующие результаты04 на дату их выполнения; их статусы не заменяют актуальную запись выше.

---

# Актуальный результат этапа04 — изолированные проверки

Дата: 2026-11-07. Этап04 и04.02 — **verification**,04.01 — **completed** изолированно. Подэтапы04.02.01–03 completed изолированно;04.02.04 not-started. [Этап](main.md) · [Декомпозиция04.02](04.02-restart-and-recovery.md) · [Главный план](../main.md).

## Изменения и проверенное поведение

По явному поручению выполнены четыре isolated suites. Исправлены три дефекта тестовой подготовки после04.01: direct Sub2ApiCatalogFixture получил требуемый IContextContentInspector; active compact bounded loader fixture создаётся текущим ModelResponseRecord.FromModelResponse; SSE controls helper маркирует вручную подготовленный synthetic opaque input. Assertions исходных guards и production-код не ослаблялись. Первоначально persistence имел1 failure, CodexLb диагностический прогон —13 failures; после исправления затронутые полные наборы прошли. Первый CodexLb run был прерван после отсутствия завершения; он не засчитывается как successful run.

Добавлены16 cases04.02 в отдельной папке tests/AgentBridge.Persistence.EfCore.Tests/Sub2Api с локальным AGENTS. Public AddAgentBridgeSub2ApiConnector/AddAgentBridge создают actual gateway/access/catalog/settings, ContextBuilder/AgentRunner/ToolExecutor и existing persistence reader/lifecycle/UoW. App-owned HttpClient имеет только локальный handler. Doubles остаются на границах app provider/key/validator/business handler и base repositories/transaction; host и соединение с БД отсутствуют.

Проверены несколько turns и инструменты в JSON/SSE, Started checkpoint до handler, полный canonical opaque/tool pair после tools, отсутствие continuation/marker в wire, automatic compact off выше threshold и separate budget assessment. Reload ключа/model/effort/endpoint/config и независимого committed settings record внутри handler не меняет текущий snapshot. Такая подмена committed settings моделирует внешнюю запись, не доказывает SQL concurrency settings writer.

Restart сериализует все committed DTO в JSON, создаёт новые списки/объекты и DI root; основной continuation case dispose первого контейнера происходит до следующего run. Reader восстанавливает точный новый wrapper/provenance; opaque/tool output проходят в следующую generation без повторного business action. Чужой ключ после restart блокируется до Begin/generation. Это изолированный serialized restart, не настоящий persistence reconnect.

Отдельно прошли HTTP error, incomplete terminal SSE, SSE EOF с partial report и caller cancellation после выполненного tool; запрет replay существующего turn; expiry/delete после fence во время HTTP; stale lease/fence до поздней записи; unknown applied start/outcome commit acknowledgement; Unknown handler outcome → serialized restart → RecoveryRequired → explicit recovery → новый turn без replay; cleanup failure после commit. Active deletion сначала подтверждает existing delete_active_run. История и уже сохранённые outputs не заменяются новой generation; HTTP response content освобождается.

## Выполненные команды

После проверки исходников/build chain запускались прямые test DLL из compile-check. Native xUnit v3 In-Process Runner принимает `-trait-`, а не MTP `--filter-not-trait`; первоначальные команды с последним аргументом остановились на unknown option до выполнения тестов и не учитываются.

```powershell
dotnet .\tests\AgentBridge.Tests\artifacts\compile-check\Debug\net10.0\AgentBridge.Tests.dll -trait- "Dependency=Database" -noLogo
dotnet .\tests\AgentBridge.Sub2Api.Tests\artifacts\compile-check\Debug\net10.0\AgentBridge.Sub2Api.Tests.dll -trait- "Dependency=Database" -noLogo
dotnet .\tests\AgentBridge.CodexLb.Tests\artifacts\compile-check\Debug\net10.0\AgentBridge.CodexLb.Tests.dll -trait- "Dependency=Database" -noLogo
dotnet .\tests\AgentBridge.Persistence.EfCore.Tests\artifacts\compile-check\Debug\net10.0\AgentBridge.Persistence.EfCore.Tests.dll -trait- "Dependency=Database" -noLogo
```

| Набор | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core | 569 | 0 | 0 |
| Sub2Api | 170 | 0 | 0 |
| CodexLb | 422 | 0 | 0 |
| Persistence без Dependency=Database, включая16 новых04.02 | 499 | 0 | 0 |
| Всего, без суммирования повторных прогонов | 1660 | 0 | 0 |

Фильтр исключает Database cases из запуска;0 skipped относится к выбранным cases, не означает запуск интеграций. До добавления04.02 persistence имел483 successful cases. Адресный namespace04.02 также запускался при разработке; его counts входят в499 и отдельно не складываются.

Compile-check Sub2Api.Tests, CodexLb.Tests и Persistence.EfCore.Tests после правок завершился с0 warnings/errors:

```powershell
dotnet build <test-project.csproj> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false
```

## Ограничения и следующий участок

04.01 завершён в изолированном объёме. Реальный persistence restart/recovery выделен в04.02.04 и не выполнялся, поэтому04.02 и этап04 остаются verification. Текущий запуск разрешён только без БД/Docker/host/сети; эта запись не отменяет ограничения и не согласует новую приёмку. Test fake transaction не подтверждает SQL isolation, настоящий unknown commit или provider restart.

Production-код в этом продолжении не изменён. Соседние репозитории, baseline kit, frozen consumer DLL, исторические01–02, evidence03, completed03.03.01 и main OpenSpec spec не менялись. Git, CLI OpenSpec, проектные scripts, host/live HTTP/OAuth, БД/SQL, Docker и deployment не выполнялись. Этап05 и отложенный compact не начинались; основание T05 не изменилось. Новая поставка/ABI и live-приёмка остаются отдельными.

Статически проверены32 изменённых/новых файла и230 относительных Markdown-ссылок: отсутствующих targets, ошибок strict UTF-8, U+FFFD, четырёх question marks и mixed EOL нет. XML solution корректен, все четыре новых документа04.02 включены в solution. SHA25616 защищённых файлов исторических01–02/evidence03/completed03.03.01/main spec совпали с предыдущим snapshot. CRLF solution сохранены. Новые C# файлы меньше10000 байт; большие BoundedHistoryTests и ResponsesJsonTests получили только необходимые изменения fixture/helper, без новой самостоятельной ответственности. В root persistence tests44 C# файла; новые связанные сценарии сразу вынесены в отдельную Sub2Api папку, массовые переносы прежних тестов не выполнялись.

Следующая запись ниже сохранена как предшествующий результат04.01 до разрешения и выполнения тестов; её статусы и counts не являются текущими.

---

# Результаты этапа04

Дата: 2026-11-07. Этап04 — **in-progress**, 04.01 — **verification**, 04.02 — **not-started**. [Этап](main.md) · [04.01](04.01-connector-provenance.md) · [Главный план](../main.md).

## Согласованный объём

Поддержка ранее сохранённых диалогов обоих коннекторов отменена. Используется один новый формат отчётов, без reader старого JSON, ветвления v1/v2 и backfill. Прежние публичные сигнатуры и физическая схема БД сохраняются. Отдельный budget snapshot v1/v2 не меняется. Sub2Api compact остаётся Unsupported; T05 и отрицательное основание03.03.02 не изменились.

## Реализация04.01

ModelResponse и ModelRequest получили отдельное immutable ConnectorProvenance. IModelContextGateway обоих встроенных коннекторов создаёт локальный binding connector/dialog/owner/agent/endpoint/key без TurnId и без сохранения ключа. Это доверенные локальные metadata от случайного смешивания, не авторизация и не upstream identity.

ContextModelGuard проверяет каждый opaque источник отдельно: active compact даже без continuation, каждый непокрытый model output и residual input/tools с сохранением occurrences. Одинаковый model ID и model compatibility приложения не обходят guard. AgentRunner проверяет историю и новые provider/input до Begin, историю между шагами и маркирует только проверенный подготовленный запрос. Неизвестный новый opaque input отклоняется, обычный текст допускается. Standalone ContextCompactor тоже проверяет источники до маркировки. Custom gateway без новой capability сохраняет прежний путь.

Direct gateways отклоняют opaque запрос без родной метки до HTTP; Sub2Api делает это до factory и callbacks. Полученные lifecycle reports и сохранённый runner partial output получают provenance. Sub2Api continuation остаётся null; CodexLb continuation сохраняет свой отдельный контракт. CodexLb compact использует binding generation endpoint для следующей generation. Маркер не добавляется в wire body, canonical envelope/output или логи. Sub2Api использует существующий safe outcome log, runner — существующий итог операции.

ModelResponseRecord пишет единственный wrapper в EnvelopeJson: envelope — исходный JSON как строка либо null, connector/binding — отдельная пара либо оба null. Исходное содержимое envelope, включая whitespace/escaping, восстанавливается отдельно. OutputJson и ContinuationJson сохраняют прежние роли. Старый raw EnvelopeJson/null и неверная форма отклоняются явно. ResponseFormatVersion=1 остаётся физическим discriminator существующей схемы, не выбирает версии reader. Миграции и SQL не менялись.

## Проверка компиляции

После статической проверки csproj и отсутствия применимых Directory.Build.props/targets и NuGet.config в цепочке каталогов выполнена команда ниже для каждого из девяти проектов таблицы. Restore, pack и сборка соседних ProjectReference не запускались.

```powershell
dotnet build <project.csproj> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false
```

| project.csproj | Итог последнего compile-check |
| --- | --- |
| AgentBridge/agent-bridge.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.Sub2Api/AgentBridge.Sub2Api.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj | 0 warnings, 0 errors |
| adapters/AgentBridge.Integration/AgentBridge.Integration.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.Tests/AgentBridge.Tests.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj | 0 warnings, 0 errors |
| tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj | 0 warnings, 0 errors |

Первый compile-check persistence tests выявил nullable warning в новом assertion; после исправления повторная компиляция прошла без предупреждений. Тесты не запускались: явного поручения на запуск не было. Предыдущие passed counts этапа03 не подтверждают изменения04.01.

## Подготовленные тесты и ограничения

Добавлены сценарии compact без continuation, same-model foreign/unknown state, родное окно с чужим tail, duplicate residual occurrences, изменения binding и новый TurnId, plaintext/unknown input. Actual Sub2Api gateway/runner/standalone compactor проверяются на отказ до factory/Begin/save и передачу родного synthetic opaque input в JSON/SSE без marker в wire. Actual CodexLb gateway проверяется для generation/compact. Persistence cases покрывают все lifecycle, отдельное provenance, точное содержимое envelope и отказ старого/повреждённого wrapper. Существующие protocol/compaction fixtures адаптированы к явному происхождению.

Эти сценарии только добавлены и скомпилированы, их выполнение не подтверждено. Реальный persistence restart, несколько turns с business tools после пересоздания DI и recovery относятся к04.02 и не начинались. Нет новой поставки, проверки frozen consumer против fresh closure или live OAuth/deployment. Sub2Api/HttpClientLibrary/EFCoreLibrary, baseline kit, frozen DLL, исторические01–02, evidence03 и main spec не редактировались. Git, проектные scripts, OpenSpec CLI, host, live HTTP/OAuth, БД/SQL, Docker и deployment не выполнялись.

Большой AgentRunner получил только integration calls; самостоятельная новая координация вынесена в AgentConnectorContext. Большой ResponsesJsonTests получил только настройку marker в существующих helpers; новые самостоятельные проверки находятся в CodexLbProvenanceTests. Остальные новые/изменённые C# файлы меньше20000 байт.

Статическая проверка охватила50 файлов и261 относительную Markdown-ссылку: отсутствующих targets, ошибок strict UTF-8, U+FFFD, четырёх question marks и проверенных mojibake-последовательностей нет. XML solution корректен, новый results04 включён в навигацию и solution. Hashes16 защищённых исторических файлов/evidence/завершённого03.03.01/main spec совпали с прежним snapshot. Исходные LF и CRLF AgentRunner/solution сохранены. Frozen kit не открывался и не пересобирался; совпадение его hashes в этом проходе не проверялось.
