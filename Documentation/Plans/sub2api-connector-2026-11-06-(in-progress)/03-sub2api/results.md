# Результаты этапа03

Дата: 2026-11-07. Статус: **blocked compact T05**; безопасный режим без автоматического compact реализован и проверен;03.01/03.02 verification. [Этап](main.md) · [Ограничения](contract-blockers.md) · [Клиентская адаптация](client-adaptation.md).

## Актуальный результат03.03: полный контекст без автоматического compact

Продолжение03.03.02,2026-11-07: **статический анализ завершён, положительного основания совместимости нет; gate blocked T05**. [Подробный вывод](03.03.02-window-compatibility.md) проверяет routing/capability/mapping/forwarding/recovery. HTTP ingress остаётся http_sse; compact capability фильтрует отдельный запрос, generation выбирает аккаунт заново, sticky не является неизменной привязкой. Даже при одном аккаунте и одинаковой модели ordinary HTTP400 invalid_encrypted_content удаляет весь encrypted compaction/compaction_summary и повторяет input без него. Последующий пригодный terminal ответ проходит обычную обработку; после замены turns1–10 окном это допускает успех только по turn11. Нарушается существующее требование пригодности окна как замены покрытого terminal prefix. В passthrough именно такого recovery нет, но исключительность этой ветки для всего deployment/pool не установлена; server failover может привести в ordinary. Cross-mode sanitizer сам удаляет reasoning, не compaction. Mapping и requested echo не доказывают ни совместимость, ни несовместимость; independently verified identity не добавлена как отдельное условие.

Минимальная необходимая клиентская мера **уже выполнена в03.03.01**: не создавать/активировать неподтверждённое окно, полный PreparedRequest через отдельный generation guard, explicit/force Unsupported до factory/HTTP. Нового клиентского решения нет.51 effective source entry прежней описи совпал с локальным source0.2.20; добавлена отдельная windowCompatibilityReview с источниками вывода и hash дополнительно прочитанного HTTP transport resolver. Прежний массив files, compactReview и evidence01–02 сохранены. HTTP/sanitizer Go regressions прочитаны, не исполнены: HTTP fixture использует API-key account и usage-only synthetic success, а не actual compact/OAuth приёмку AgentBridge. Условное поведение source установлено, несовместимость deployment и недостижимость encrypted error не установлены. Обновлены только актуальные документы и OpenSpec tasks/design; main spec, исторические отчёты, baseline kit/frozen DLL не изменялись. Новых сборок/тестов/CLI OpenSpec/live HTTP/БД/Docker/deployment не было.03.03.03–03.03.06 и04 не начаты; работа остановлена после03.03.02.

Статическая проверка продолжения03.03.02:11 изменённых документов,149 относительных ссылок без missing targets; JSON корректен, strict UTF-8 без U+FFFD/четырёх question marks/проверенных mojibake-последовательностей, LF и отсутствие BOM сохранены. SHA256 всех13 исторических файлов01–02, main spec и solution совпали с началом анализа; исторический хвост results03 и прежние files/compactReview JSON не изменены. Дополнительный hash HTTP transport source совпал.

Уточнение плана2026-11-07: этот реализованный режим выделен в [03.03.01](03.03.01-full-context-mode.md); [03.03.02](03.03.02-window-compatibility.md) остаётся blocked T05. Request, response, активация и регрессия вынесены в03.03.03–03.03.06, not-started; [полная декомпозиция](03.03-compaction.md). Изменена только документация/solution items, новые compile-check/tests не запускались. Статически проверены37 Markdown с313 относительными ссылками без missing targets; все34 Markdown плана включены в119 File/Project items корректного XML решения без дубликатов. UTF-8 и прежние окончания строк сохранены, включая CRLF solution; U+FFFD/четыре question marks/проверенные mojibake-последовательности не найдены. Hashes13 исторических файлов01–02 совпали; results03 без этого уточнения и solution без шести новых items совпадают с исходными hashes. Нижеследующие результаты относятся к прежней реализации и не являются приёмкой будущего compact. Исторические разделы сохранены.

Принят безопасный режим: автоматический compact sub2api выключен, полный рабочий контекст проходит отдельный generation guard. Порог сжатия не является пределом входного окна. При unknown window после guard решение о размере остаётся серверу; known window по-прежнему проверяется локально. Ошибка не запускает обрезание, retry, смену выбора или повтор инструмента. Ручной/force compact остаётся Unsupported до factory/HTTP. Best-effort compact и текстовое резюме не включены.

### Реализовано

- IModelGateway.AutomaticCompactionEnabled получил default true, сохраняя прежние реализации. Sub2Api gateway возвращает false. Это локальное решение о dispatch, без нового конфигурационного флага/server API или объявления capability.
- ContextCompactor сохраняет прежние validation/builder/counter checks. При отключённом automatic возвращает новый ContextCompactionStatus.Disabled с полным PreparedRequest, прежним token/window и нулём passes, без gateway compact/save. Новое значение enum добавлено после прежних. Separate full guard AgentRunner не менялся; ошибки выполненного compact не перехватываются ради generation.
- Диагностика ручного compact объясняет неподтверждённое использование окна следующей generation и возможность удаления непрочитанного compact. Mapping больше не выдаётся за доказательство несовместимости. Canonical items, reasoning, инструкции/effort/ordering/tool pairs, прежнее окно/terminal prefix/хвост и история сохраняются.

### Конкретное основание ограничения

[Разбор03.03](03.03-compaction.md) показывает: после compact, покрывающего turns1–10, следующий input содержит окно + turn11. При invalid_encrypted_content HTTP recovery удаляет весь compaction/compaction_summary с encrypted_content; успешный ответ может быть получен без покрытого prefix. Source regression `TestOpenAIGatewayService_Forward_HTTPRetryRecoveryDropsCompaction` описывает именно этот HTTP-путь. Go tests прочитаны, не выполнены. Это доказательство поведения recovery при ошибке, не доказанная несовместимость конкретных моделей/pool. Independently verified actual identity не требуется сама по себе, requested echo не является подтверждением совместимости.

### Фактические проверки

Перед сборками перечитан graph трёх test csproj:13 проектов и3 дополнительных build inputs (global.json двух repo и HttpClientLibrary Directory.Build.props). Ancestor props/targets/NuGet.config/locks проверены; custom Exec/targets/import hooks и locks не найдены. Сеть/restore не требовались. Все сборки с BuildProjectReferences=false и GeneratePackageOnBuild=false: read-only соседние библиотеки не пересобирались, использованы их существующие compile-check outputs.

Собраны семь конкретных проектов: AgentBridge/agent-bridge.csproj, adapters/AgentBridge.Sub2Api/AgentBridge.Sub2Api.csproj, adapters/AgentBridge.CodexLb/AgentBridge.CodexLb.csproj, adapters/AgentBridge.Integration/AgentBridge.Integration.csproj и три test projects ниже. Sub2Api tests повторно собраны после добавления проверки прежнего окна/хвоста. Все сборки Exit0,0 warnings/errors; использован Build без Rebuild.

```powershell
dotnet build <конкретный.csproj> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false -v:q
dotnet test --project <конкретный.test.csproj> --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false --no-progress
```

| Финальный набор текущего продолжения | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| tests/AgentBridge.Tests/AgentBridge.Tests.csproj | 557 | 0 | 0 |
| tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj | 161 | 0 | 0 |
| tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj | 414 | 0 | 0 |
| Всего | **1132** | **0** | **0** |

Новые10 cases используют actual runner/compactor/Sub2Api/HttpClientLibrary, fake handler и in-memory storage: выше threshold уходит полный canonical input с encrypted reasoning/closed tool pair/instructions/effort; known/unknown budgets, JSON/non-stream и streaming callbacks, server rejection без retry, known overflow до factory, Disabled против force Unsupported, сохранённое окно/prefix/непокрытый хвост. Core/CodexLb regression сохраняет default automatic compact, Completed-only/save-before-activation и прежние error/continuation paths. Предварительные9 targeted cases и160 полный Sub2Api прогон перекрываются финальным161; не складываются с ним или с прежними565/1151 как уникальные tests.

### Evidence и ограничения

Исторические source01/02 и baseline не переписывались. При текущей сверке sub2api уже local0.2.20:38 из48 прежних source03 совпали,10 изменились; source01 имеет8 отличий из28 sub2api entries. [contract-source-evidence.json](contract-source-evidence.json) сохраняет прежний массив48 и отдельно current compactReview с новыми hashes/различиями и тремя дополнительными source files. Current recovery/sanitizer/mapping перечитаны; ключевой сценарий удаления compact сохранился. Версия локального source не объявляется версией deployment, исторический PE/baseline не выдаётся за новую поставку.

Статическая сверка:181 относительная ссылка изменённых документов/инструкций без missing targets; все28 Markdown плана присутствуют в existing solution items, XML решения113 entries корректен, SHA256 решения совпал с исходным (CRLF сохранён). Новых документов плана не добавлялось; новый test входит через existing compile glob. Strict UTF-8/отсутствие U+FFFD, четырёх question marks, проверенных mojibake-последовательностей и mixed EOL подтверждены для30 изменённых/новых файлов; русский текст просмотрен. Пять новых/изменённых C# файлов меньше20000 байт, максимум18630; новые самостоятельные tests выделены отдельно, большой AgentRunnerTests не переписывался.13 hashes исторических файлов01–02 совпали. Effective current inventory51 source files (прежние48 с overlay10 изменений и3 additions) совпал с локальным sub2api. Навигаторы/локальные AGENTS/OpenSpec delta/design/tasks синхронизированы; strict CLI validation не выполнялась.

T01–T05 открыты,03.03/03 blocked,03.01/03.02 verification. Полный compact transport/его активация, совместимость целевого маршрута/pool и live OAuth не подтверждены. Для включения нужно установить пригодность existing compact для следующей generation, включая recovery; одного model echo/успешной fixture недостаточно. Не вводились provenance/persistence/restart04, новый server API или изменение sub2api. Main spec не синхронизирована, change не архивирован. Git/CLI OpenSpec/проектные scripts/приложение/host/live HTTP/OAuth/БД/Docker/deployment не запускались. Обычные tests не используют AppData, external kit или frozen consumer; baseline kit/DLL не перезаписывались. Работа остановлена после разрешённой части03.

## Предшествующее продолжение: generation по принятому плану Б

Ниже сохранены результаты предыдущего продолжения. Его совпадение48 hashes и прежнее объяснение compact относятся к тому снимку; актуальные source drift/режим отключения описаны выше.

Принята canonical гарантия на входе sub2api и в истории AgentBridge с допуском existing reasoning normalization/recovery внутри сервера. Успешный ответ не доказывает неизменную upstream передачу или actual model identity. Решение о клиентской границе больше не открыто; sub2api не дорабатывается. Бюджетный ServerValidationWithUnknownWindow и v1/v2 snapshots сохранены без дополнительных изменений.

### Реализовано03.02

- Existing Sub2ApiModelGateway выполняет один POST /v1/responses через scoped HttpClientLibrary. Отложенное разрешение HttpApiClient сохраняет preflight до factory: incoming continuation, invalid key/endpoint и unsupported controls не отправляются.
- Shared writer передаёт полный prepared context, включая принятое compact-окно, хвост/current input, instructions/effort/tools/controls, encrypted reasoning и полные call/output пары. Нет previous_response_id, codex-lb turn-state, клиентского retry, смены ключа/модели/коннектора или выполнения бизнес-инструментов. Returned response id не создаёт continuation.
- Один ответ читается как JSON/SSE по Content-Type, включая non-stream SSE fallback. ResponseSseAccumulator выделен в linked shared sources; codex-lb wrapper сохраняет прежние factories continuation/errors, sub2api использует null continuation. Canonical output/envelope сохраняются полностью; requested model echo не становится verified identity.
- Callbacks последовательны и awaited, включая полные items при JSON вместо SSE. Partial text/arguments/opaque и ordering переживают EOF/отказ/отмену; Completed требует terminal confirmation, HTTP200/[DONE] недостаточны. Общий deadline охватывает headers/body/callbacks; caller имеет приоритет. Primary callback/read/cleanup и secondary cleanup не скрываются. Borrowed HttpClient/handlers остаются у приложения.
- Safe HTTP pipeline дополнен Sub2ApiGenerationFinished: TurnId/lifecycle/закрытый error type без key/payload/raw error. Completed фиксируется после terminal и cleanup; incomplete/failed/exception не объявляются успешной генерацией.

Compact остаётся Unsupported до factory/HTTP, без подмены генерацией: existing initial compact/global mapping может изменить модель, response.model бывает requested echo, совместимость не подтверждена. Existing Completed-only/save-before-activation/history path не менялся. Provenance, persistence/restart04, main spec, baseline kit/frozen consumer DLL не изменялись.

### Фактические проверки плана Б

Перед сборками прочитан graph двух test csproj:12 проектов и15 build inputs (включая ancestor props/global.json; NuGet.config/locks в этом graph не найдены). Custom Exec/targets/import hooks не обнаружены; HttpClientLibrary Directory.Build.props действует только на его собственные tests. Restore не требовался, сеть не использовалась. BuildProjectReferences=false исключил пересборку read-only соседних библиотек; использованы existing compile-check dependencies. Это не новая поставка.

Пять конкретных compile-check: Sub2Api, CodexLb, Integration и их два test projects; все финальные0 warnings/errors. Первоначальная сборка нового test имела один CS8602; nullable assertion исправлен без suppression. После добавления регрессий full Sub2Api прогон вырос143→149→151, все прогоны без failed/skipped. Первые143 уже включали generation; финальный151 подтверждает также полную сохранность JSON output при отмене из callback, invalid UTF-8 unknown поля, unexpected read/cleanup identity, deadline до headers и partial tool arguments/ordering.

```powershell
dotnet build <конкретный.csproj> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false -v:q
dotnet test --project tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false --no-progress
dotnet test --project tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false --no-progress
```

| Финальный набор этого продолжения | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Sub2Api, весь isolated набор | 151 | 0 | 0 |
| CodexLb, весь isolated набор | 414 | 0 | 0 |
| Всего | **565** | **0** | **0** |

Sub2Api включает public facade dispatch, полный повторный request с tool pair, JSON/SSE/non-stream fallback, terminal/partial/ordering/arguments, sequential awaited callbacks и callback failure identity, HTTP status/envelopes без retry, caller/deadline, UTF-8/JSON, cleanup/ownership и safe logs. CodexLb regression подтверждает сохранение прежнего JSON/SSE/compact/continuation и публичной поверхности после извлечения accumulator. Общий runtime565 не складывается с прежними1151 как число уникальных тестов. Core/persistence/budget tests в этом продолжении не повторялись: их код не менялся.

T01/T02/T03/T04/T05 открыты: доказанный budget, live OAuth/tool chain, provenance04, новая поставка/ABI и compact compatibility не заменяются fake fixtures. Этап03 не completed,04 не начат. AppData, готовый kit и внешние artifacts не требуются обычному набору. CLI OpenSpec, Git, проектные скрипты, приложение/host, live HTTP/OAuth, БД, Docker и deployment не выполнялись.

Статическая сверка:19 документов,185 относительных ссылок без missing targets (включая ссылки изменённых инструкций), XML решения и JSON evidence корректны, все9 файлов03 присутствуют в solution. SHA256/size всех48 источников sub2api совпали. Проверены40 документов/инструкций/исходников/solution строгим UTF-8 decoder: повреждений, U+FFFD/четырёх question marks, проверенных mojibake-последовательностей и mixed EOL нет; русский текст просмотрен. LF исходников/Markdown/JSON и CRLF решения сохранены. Все11 изменённых/новых C# файлов меньше20000 байт, максимум ConnectorSelectionTests18569. Новый generation transport отделён от gateway, SSE accumulator общий; массового переноса исходников нет. Навигаторы/main/open-questions/подэтапы/локальные AGENTS/OpenSpec синхронизированы, C01 больше не открытый вопрос, T05 блокирует только неподтверждённый compact. Main spec и исторические01–02 не переписывались.

## Предшествующее продолжение03: неизвестный бюджет

Следующие записи описывают состояние до принятия плана Б. Указанный в них запрет generation по upstream canonical гарантии больше не является актуальным блокером; исторические проверки сохранены.

sub2api не дорабатывается. Пользователь отдельно разрешил явный режим неизвестного входного окна с изменением validator/guard и budget snapshot, сохранив чтение старого формата. Реализован **ServerValidationWithUnknownWindow**. Прежние strict/ServerValidation/default codex-lb не меняются; guessed budget/95%/threshold вместо окна не используются.

Existing bundle/HttpClientLibrary получил manifest projection только для нового режима: GET /v1/models?client_version=<настоящая assembly version AgentBridge>, exact slug/effort/API-доступность как объявления сервера, без утверждения provenance pool. Ordinary каталог сохранён. Settings фиксирует policy; guard сохраняет null окно и RequiresServerValidation даже для known text estimate. Threshold остаётся локальным, гарантированный reserve при unknown не обещается. Counter failure/exact selection/caller checks сохранены.

TurnModelSettings/ContextBudgetAssessment получили nullable DeclaredInputContextWindow с сохранением старых int getters/constructors/signatures. Legacy getter при unknown явно отказывает; System.Text.Json выводит InputContextWindow=null безопасно. TurnSettingsMapping сохраняет строгий known snapshot v1 и читает literal v1; явные server policies используют v2 с null окном и обязательным BudgetPolicy. Схема/таблицы/миграции не менялись. Старый reader v1 не читает v2 — этому режиму нужны обновлённые DLL. Provenance/restart04 не добавлены.

Existing status/compactor поддерживают новый бюджетный режим. Проверен core сценарий Completed-only/save-before-activation, terminal prefix + хвост, неизменная история и Unsupported без generation fallback. Это in-memory gateway/writer, **не реализация Sub2Api compact**.

Разбор [existing forwarding paths](client-adaptation.md) установил конкретное противоречие: shared normalizer удаляет непустой reasoning.content; ordinary invalid-encrypted recovery и cross-mode failover могут удалить encrypted reasoning и вернуть успех. Transport normalization OAuth stream/store/SSE→JSON и обратимые tool/ID формы не объявляются потерями сами по себе. Вызовы text truncation в текущем source являются no-op; прежняя оценка уточнена. Response.model остаётся объявлением/возможным requested echo. Compact может сменить модель до первого send.

**Generation/compact остаются Unsupported до factory/HTTP/callback**, включая новый budget mode. Actual JSON/SSE transport не добавлен; нет dormant реализации или guessed enable flag. Минимальное оставшееся изменение клиентского требования выделено как [C01](../open-questions.md): разрешить перечисленные server transformations, гарантируя canonical только до входа sub2api и в истории AgentBridge. Это не согласовано решением про бюджет. T01–T05 открыты; T01 больше не блокирует settings в явном режиме, T05 блокирует transport. После03 работа остановлена,04 не начат.

### Фактические проверки этого продолжения

Перед сборками прочитаны4 test csproj и весь ProjectReference graph21 проекта, ancestor build props/targets/NuGet/lock inputs и global.json; executable hooks/Exec не обнаружены. Restore не требовался. BuildProjectReferences=false исключил пересборку read-only соседних библиотек; использованы их существующие compile-check DLL, HttpClientLibrary file version0.0.0.6. Core/Sub2Api/общий EF/Integration собраны адресно перед тестами. Это не новая DLL-поставка.

```powershell
dotnet build <конкретный.csproj> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false -v:q
dotnet test --project <конкретный.test.csproj> --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:BuildProjectReferences=false -p:GeneratePackageOnBuild=false --no-progress
```

Конкретные production csproj: AgentBridge/agent-bridge.csproj, adapters/AgentBridge.Sub2Api/AgentBridge.Sub2Api.csproj, adapters/AgentBridge.Persistence.EfCore/AgentBridge.Persistence.EfCore.csproj, adapters/AgentBridge.Integration/AgentBridge.Integration.csproj. Test projects: tests/AgentBridge.Tests/AgentBridge.Tests.csproj, tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj, tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj, tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj. Для последнего использованы три отдельных --filter-class: '*UnknownWindowSnapshotTests', '*DialogSettingsMappingTests', '*IntegrationRegistrationTests', каждый с --filter-not-trait 'Dependency=Database'.

Все восемь конкретных проектов собраны после финальных C# изменений: Exit0,0 warnings/errors. Финальные изолированные прогоны:

| Набор | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| AgentBridge.Tests, полный | 557 | 0 | 0 |
| AgentBridge.Sub2Api.Tests, полный | 111 | 0 | 0 |
| AgentBridge.CodexLb.Tests, полный | 414 | 0 | 0 |
| EF: UnknownWindowSnapshotTests | 6 | 0 | 0 |
| EF: DialogSettingsMappingTests | 7 | 0 | 0 |
| EF: IntegrationRegistrationTests | 56 | 0 | 0 |
| Всего | **1151** | **0** | **0** |

Успешные первоначальные прогоны этого продолжения: core554, Sub2Api109, CodexLb414, snapshot6, existing mapping7, facade56. После добавления core scenarios и gateway boundary cases финальные core557/Sub2Api111 повторены. Два подготовительных отказа: фильтр class с символом | не выбрал тесты (Exit5,0 tests); компиляция нового test scenario использовала несуществующее имя ContextContentInspector (CS0246). Фильтры разделены, inspector заменён actual ContextTokenCounter; последующие build/run green. Это не падения production тестов. После финальных прогонов изменялась только документация, повторная сборка не требовалась.

Сверены все46 прежних hashes sub2api; опись расширена двумя владеющими sources no-op text/IDs.28 sub2api entries evidence01 также совпали; остальные repositories из той смешанной описи не объявляются совпавшими. Проверка статическая, без Git/Go/live. Исторические01–02/main spec, baseline kit/frozen DLL не редактировались; AppData и внешняя поставка не требуются обычным tests. CLI OpenSpec, приложение/host, live HTTP/OAuth, БД, Docker и deployment не запускались. Статическая сверка delta/tasks не заменяет strict CLI validation05.01.

### Итоговая статическая сверка

Проверены19 актуальных документов:188 относительных ссылок без missing targets, корректные XML решения и JSON evidence, включение всех9 файлов03 в solution. Все48 SHA256/size записей sub2api совпали с локальными sources; это не доказательство deployment или live-совместимости. В50 затронутых документах/исходниках/инструкциях/solution строгий UTF-8 decoder не выявил ошибок, U+FFFD, четырёх question marks и проверенных mojibake-последовательностей; русский текст просмотрен. Сохранены LF исходников/Markdown/JSON и CRLF solution. Все19 изменённых C# файлов меньше20000 байт, максимум ContextCompactor13901.

Актуальные навигаторы,03.01–03.03, main/open-questions, локальные AGENTS и OpenSpec proposal/design/tasks/delta согласованы:03.01 verification,03 blocked T05/C01, T01–T05 открыты,04 not-started. Устаревшие утверждения об обязательной блокировке нового settings режима бюджетом удалены из актуальных описаний; предшествующие результаты ниже сохранены как история03. Ни успешная fixture, ни разрешённая budget policy не открывают generation/compact. Доработка sub2api и отвергнутое server proposal не являются следующими шагами. Работа в этом запуске завершена в границах03; изменение canonical гарантии C01 не согласовано.

## Предшествующие результаты03: отклонение серверного пути

Следующие записи сохраняют прежние проверки и состояние до разрешённой клиентской адаптации выше. Они не являются актуальным описанием budget/settings и не задают новых server зависимостей.

Пользователь исключил доработку sub2api: адаптируется только AgentBridge к существующему серверу. Подготовленное ниже серверное предложение **отклонено** и сохранено как запись отвергнутого варианта. Его согласование, новые headers/pool revision/metadata и single-attempt enforcement не являются следующими шагами или условиями плана. Требование отсутствия клиентских retries ошибочно расширялось до общего server single-attempt; это расширение устранено из активного OpenSpec delta и инструкций.

В [текущем пути адаптации](contract-blockers.md) сохранены факты о текущем API и отдельно указаны клиентские границы. Existing ServerValidation допускает неизвестную стоимость opaque, но `ModelSelectionValidator` всё ещё требует положительный InputContextWindow; `ContextBudgetGuard` и сохраняемый `TurnModelSettings` используют известное окно. Поэтому переключить policy или подставить локальный порог как model budget нельзя. Existing passthrough также не является доказательством полной canonical preservation: shared prelude и compatibility normalizers сохраняются. Эти вопросы относятся к клиентской адаптации и оценке конкретного existing сценария, а не к запросу переделать sub2api.

Синхронизированы активные main/open-questions/подэтапы, навигаторы, current integration guide, локальный AGENTS и OpenSpec change. Предыдущие результаты ниже описывают предшествующий разбор, а не действующий план серверных работ. Нового C# транспорта в этой корректировке нет; budget/gateway guards сохранены, T01/T05 открыты,04 не начат. Код, main spec и исторические01–02 не менялись; сборки/тесты/live/CLI/Git не запускались. От пользователя не требуется согласовывать серверную доработку: этот вариант исключён.

Проверка корректировки:19 документов/solution,188 относительных ссылок без missing targets,3 JSON-примера, XML/UTF-8/EOL корректны. SHA25617 historical01/02/04/main-spec файлов и size/SHA25646 sub2api sources не изменились. Эта проверка статическая; новых результатов выполнения адаптера нет.

## Предшествующий разбор03: отвергнутый путь серверного расширения

Повторно проверены actual source paths sub2api0.2.19 и доступные versioned публичные Model/routes/release материалы. Все28 sub2api hashes evidence01 совпали; [новая опись46 sources](contract-source-evidence.json) включает дополнительные владеющие файлы. Проверка публичных материалов ограничена описанными источниками, не подтверждает deployment и не доказывает отсутствие частной спецификации.

Достаточного существующего контракта не установлено. Уточнены причины: capabilities intersection не привязан публичной revision к фактическому generation pool; ordinary/passthrough применяют account mapping по-разному; compact способен заменить модель ещё до первого HTTP; encrypted items могут удаляться при invalid-lineage и cross-mode failover. MaxAccountSwitches0 оставляет default3, pool retry count0 не исключает OAuth deadline retry. Internal raw-model observer существует, но public Responses/compact не дают его как обязательную processing metadata; admin usage не является допустимым источником клиента.

Подготовлено [адресное серверное предложение](server-contract-proposal.md): точные владельцы и функции, versioned opt-in headers/response fields, полный pool snapshot и field-level provenance, семантика гарантированного input budget, общий single-attempt enforcement, requested/sent/reported model, явные отказы и regression tests ordinary/passthrough/non-stream/compact. Предложение не согласовано и не реализовано в sub2api. Безопасные guard/settings сохранены; новых C# transport/settings paths нет. T01/T05 не закрыты. Новый блок результатов относится только к этому продолжению; прежние фактические прогоны ниже сохранены.

В этом продолжении изменены только документы, локальная инструкция адаптера и solution items. Исправлено устаревшее текущее руководство26: доступ/ordinary ID catalog уже работают, Unsupported относится к settings с неизвестным бюджетом и generation/compact. Исторические отчёты01–02 не переписывались. Compile-check/.NET и Go tests повторно не запускались: исполняемый код/build chain не менялись; прежние568 passed не являются новым прогоном или приёмкой sub2api. CLI OpenSpec, Git, проектные скрипты, live gateway/OAuth, host, БД, Docker и deployment не выполнялись. Этап04 не начат; новая поставка/baseline/frozen DLL не затрагивались.

Статическая проверка документов этого продолжения и сохранности исходников описана в конце файла. Она не заменяет strict CLI validation, compile-check или выполнение тестов.

## Реализовано

Существующий Sub2Api bundle получил отдельные internal access resolver, ordinary ModelCatalog и ModelSettingsReader вместо четырёхпортового PendingConnector. Facade передаёт прежнюю app-owned HTTP factory в тот же composition path. Не добавлены параллельный способ подключения, public transport API, протокольный framework, скрипты или подготовка artifacts для обычных тестов.

Individual source scoped; индивидуальный ключ имеет приоритет, только null в Shared допускает общий. Ошибка/empty/непригодный ключ не запускают fallback; ReadWithAccessAsync использует pinned доступ без повторного source I/O. Primitive selection сохраняется до ожидания источника. Safe Options pipeline не публикует значения.

Каталог GET server-prefix + /v1/models через actual HttpClientLibrary использует per-request Bearer и GenerationTimeout на send/body. Принимаются root/prefix и один конечный /v1; неоднозначные endpoint/encoded route/двойной /v1 отклоняются. No retry/cache/смены модели/ключа. App HttpClient/handlers не освобождаются адаптером; response/content/streams освобождает actual library. 401/403 дают Unauthorized/Forbidden, прочие HTTP-отказы Rejected; caller/локальный Timeout/primary+cleanup различаются. Диагностика использует existing ILogger pipeline, без raw bodies/headers/секретов, error capture64 КиБ.

Ordinary каталог подтверждает только exact model IDs. Неизвестные metadata, включая похожий input_context_window, не подменяют согласованный контракт. Budget/output limit=null, efforts/modalities пусты, flags=false/HasMetadata=false. Malformed envelope/duplicate ID/properties, invalid JSON/UTF-8 отклоняются. JsonElement требует проверки UTF-8 также неизвестных полей. Settings сохраняет existing ModelSelectionValidator и отказывает Unsupported при недоказанном бюджете T01.

## Заблокировано

T01 не закрыт: в текущем публичном контракте нет доказанного входного бюджета и provenance возможностей для выбираемого generation pool. T05 не закрыт: server retries/opaque normalization, compact mapping/fallback и model echo не дают требуемых гарантий. Sub2ApiModelGateway явно Unsupported до HTTP/factory/callback; JSON/SSE/compact транспорт **не реализован**. Direct gateway/custom settings не обходят guard. Canonical input/legacy continuation не меняются; обычная генерация не заменяет compact.

[Конкретные источники и необходимое расширение](contract-blockers.md). T02 live/tool chain, T03 provenance04, T04 новая поставка/ABI также остаются открытыми. ContextBuilder/ContextCompactor/IDialogContextWriter, история, persistence, restart и codex-lb не изменялись. Этап04 не начинался.

## Проверки и команды

Из `D:\Media\User\source\repos\agent-bridge` перед сборкой прочитаны три конкретных test csproj и все транзитивные ProjectReference20 проектов, ancestor Directory.Build.props/targets/NuGet/lock inputs, global.json. Custom Exec/hooks не обнаружены; HTTP Directory.Build.props влияет только на его собственные tests. Assets/пакеты уже доступны: restore не выполнялся, сеть не использовалась. GeneratePackageOnBuild=false исключает упаковку EFCoreLibrary.

Для каждого конкретного проекта:

```text
tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj
tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj
tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj
```

```powershell
dotnet build <project> -c Debug --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false -v:q
```

Все финальные compile-check: Exit0,0 warnings/errors. Сборка Sub2Api test project также компилирует затронутые Sub2Api/Integration проекты. Первая сборка имела18 xUnit1051 warnings: пропущенные test cancellation tokens исправлены, suppression нет.

Дополнительно той же build-командой отдельно собраны `adapters/AgentBridge.Sub2Api/AgentBridge.Sub2Api.csproj` и `adapters/AgentBridge.Integration/AgentBridge.Integration.csproj`:0 warnings/errors. После финальной проверки стиля Sub2Api test project пересобран, полный98-case набор повторён без падений/пропусков.

```powershell
dotnet test --project tests/AgentBridge.Sub2Api.Tests/AgentBridge.Sub2Api.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --no-progress
dotnet test --project tests/AgentBridge.CodexLb.Tests/AgentBridge.CodexLb.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --no-progress
dotnet test --project tests/AgentBridge.Persistence.EfCore.Tests/AgentBridge.Persistence.EfCore.Tests.csproj --no-build --no-restore -p:BaseOutputPath=.\artifacts\compile-check\ -p:GeneratePackageOnBuild=false --filter-class '*IntegrationRegistrationTests' --filter-not-trait 'Dependency=Database' --no-progress
```

| Финальный набор | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Sub2Api: existing27 + новые71 | 98 | 0 | 0 |
| CodexLb: полный isolated suite | 414 | 0 | 0 |
| Прежний facade: IntegrationRegistrationTests | 56 | 0 | 0 |
| Всего | **568** | **0** | **0** |

Новые cases проверяют доступ, scope/pinning/selection, неизвестные capabilities/budget, exact alias/case, equality/overflow на synthetic catalog, последовательные запросы без cache, root/prefix URI, 401/403/429/5xx/оба error envelopes/oversize/malformed, invalid UTF-8 в известном и неизвестном поле, caller до headers и при success/error body, deadline на тех же стадиях, cleanup primary/secondary, resource ownership и safe logger channels. Gateway tests подтверждают только безопасный отказ, неизменность canonical input и отсутствие HTTP/callback. Shared tests не стали transport acceptance.

CodexLb regression сохраняет JSON/SSE/compact/tools, canonical/legacy continuation, output ordering/terminal/partial/EOF, awaited callbacks/failures, UTF-8/JSON, caller/deadline и cleanup/ownership. Это доказательство прежнего коннектора, не реализации sub2api.

### Неуспешные проверки до исправлений

Первый test run:83 cases,80 passed,3 failed,0 skipped. Ownership probe без Bearer выявил null dereference только в fake handler. Два success-body timing cases не достигали read gate: пустой MemoryStream объявлял Content-Length=0; один gate ждал10 секунд и завершился timeout. Исправлены fixture handler и non-seekable gated stream; pending task всегда отменяется и await в finally. После исправления83/83 passed.

Дополнительная регрессия `--filter-method '*InvalidUtf8OrJsonIsRejected'`:3 cases,2 passed,1 failed. Невалидный UTF-8 неизвестного поля принимался production reader из-за ленивого JsonElement decoding. Исправлена проверка полного JSON text перед проекцией. Адресный повтор3/3 passed; затем расширенный финальный набор98/98. Указанные неудачные прогоны не скрыты итоговым green. Зависаний вне описанного10-секундного fixture gate и других нестабильных прогонов не наблюдалось.

## Совместимость и границы доказательств

- Source: прежний source-linked transport probe и facade consumers компилируются без изменения их исходников; это не rebuild внешнего consumer против fresh kit.
- PE/API: existing tests сравнивают текущую поверхность с embedded текстовым эталоном. Старые CodexLb public types/assembly ownership сохранены. Исходники CodexLb/shared protocol не изменены.
- Old→new ABI: resolution frozen неперекомпилированных DLL против новой поставки не проверялся. Baseline kit/frozen consumers не использовались, не пересобирались и не перезаписывались; их сохранность в этом запуске отдельно не заявляется.
- Runtime: только Windows isolated DI/actual HttpClientLibrary с fake handlers/local streams. Нет live HTTP/OAuth, provider DB, приложения/host, Docker, deployment, новых DLL kits или других RID. Ordinary tests не зависят от AppData/kit/готового artifacts-комплекта.

Документы/AGENTS/навигаторы/solution items/OpenSpec tasks обновлены, исторические01–02 и main spec сохранены. Статически проверены137 относительных ссылок без broken targets, наличие всех6 stage03 Markdown solution items, XML решения и41 UTF-8 файлов без U+FFFD/четырёх question marks. Сохранены LF исходников/Markdown и CRLF решения; русский текст просмотрен без mojibake. Изменённый ConnectorSelectionTests18114 байт, Integration7118; все новые C# файлы меньше20000 байт (максимум CatalogTests12442). CLI OpenSpec/strict validation и Git не выполнялись. Stage03 остаётся blocked; переход к04 не выполнен.

## Статическая проверка продолжения03

Проверены19 новых/изменённых документов и solution:187 относительных ссылок без отсутствующих targets, XML решения и включение всех8 stage03 файлов. JSON evidence и3 синтетических JSON-примера предложения разбираются; это проверка синтаксиса, не server fixtures или transport acceptance. UTF-8 прочитан строгим decoder без U+FFFD/четырёх question marks; русский текст просмотрен без mojibake. Сохранены LF Markdown/JSON и CRLF solution, без добавления BOM.

Повторная SHA256/size сверка46 файлов sub2api совпала с новой описью. SHA25617 файлов исторических01–02, незапущенного04 и main spec до/после продолжения совпадают. Это проверка конкретных файлов, не Git revision, deployment, runtime или полная опись соседнего репозитория. Сохранность frozen kit/consumer DLL в этом продолжении отдельно не проверялась; записи/пересборки этих файлов не выполнялись. Статусы03.01–03.03/этап03 согласованы как blocked,04 остаётся not-started; все T01–T05 открыты. Новое предложение требует отдельного согласования серверных работ либо эквивалентных versioned доказательств, после чего остаются обязательные adapter/live проверки.
