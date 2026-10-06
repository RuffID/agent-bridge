# 08 — Сверка текущих описаний с реализацией

[Навигатор](README.md). Статус: **принят в A-границе**. Зависимости: 00 и принятые исправления, которые затрагивают описываемые контракты. Находка: **ABQA-001, подтверждённое расхождение документации, S3**.

## Цель и область

Обновить актуальные документы, сохранив исторические отчёты. Источник — [Findings](<../AgentBridge Quality Audit/Findings.md>) и [итог15](<../AgentBridge Quality Audit/15-final-reconciliation.md>). Первые три расхождения на baseline уже исправлялись до аудита; повторно «исправлять» исторические версии не нужно.

## Работы

1. Проверить [архитектуру](<../../Technical documentation/01-architecture.md>): AgentSettingsService существует; правило таблицы о будущих типах и checkpoint формулировки должны отличать actual API от исторической точки.
2. Сверить текущие Documentation/README, Plans/README и Business logic/README с завершённым статическим аудитом00–15 и частичными границами A/B/C/D. Подготовка нового плана уже обновляет его навигацию, но не закрывает остальные устаревания автоматически.
3. После приёмки01–07 обновить затронутые technical errors/cancellation/maintenance описания. Бизнес-описания менять лишь при действительном расхождении существующего правила; не добавлять новую политику, чтобы оправдать код.
4. Сверить public API/examples/XML comments с текущими исходниками. При изменении API передать compile-only consumer checks16; не выдавать app factories/business ports за API AgentBridge. Новые MSSQL/config/facade/RID12–15 описывать как реализованные лишь после соответствующих работ.
5. Обновить ближайший AGENTS только при изменении устойчивых обязанностей/инвариантов. Сохранить даты/ограничения старых отчётов, audit findings и первоначального плана.

## Проверки

A: существование описанных типов/методов, статусы, пути/anchors, английские имена файлов, UTF-8 и исходные EOL. Проверить U+FFFD, четыре вопросительных знака и характерные mojibake markers. Документальные правки не требуют нового runtime теста.

При реальном изменении C# примера его сборка относится к16; Markdown-чтение не заменяет compilation.

## Критерии завершения

Все актуальные места ABQA-001 сверены; существующие типы не названы будущими, ограничения evidence честны. Навигаторы согласованы. Исторические checkpoints не переписаны, новые открытые решения не выданы за требования.

## Результаты

### Область, baseline и результат

2026-10-06, Asia/Novosibirsk. Выполнено отдельное поручение08: актуальные документы и этот отчёт. Прочитаны весь08, принятые результаты00–07, root/Documentation/Plans AGENTS, README/Decisions, Findings/итог15 аудита и действующая main spec. Применены csharp-project-rules для статической сверки API и backend-uow-repositories с reference для maintenance/UoW границ. Новых архитектурных обязанностей нет, поэтому AGENTS не менялись. Исходное задание08 выше сохранено кроме статуса; исторические audit/Initial Implementation reports и результаты00–07 не переписаны.

Входной AB HEAD `9b46d3479c514828b1910251b0f1d8690c102d9f`; единственная чужая tracked правка — Coordination.md координатора. Read-only соседние HEAD: EF `5962deceb6ea01306cbbda400040db88ea8e5df9`, HTTP `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`, LB `f8ffbac2099a113fba54dfd8d77774f5bca80ffa`. EF/HTTP чистые; LB сохраняет чужую untracked .vs/. Эти файлы исключены, не изменялись и не откатывались. Новых чатов/субагентов/веток/worktrees нет.

### Exact manifest

Только `D:/Media/User/source/repos/agent-bridge`,12 существующих Markdown:

| Файл от корня | Что и зачем изменено |
| --- | --- |
| `README.md` | Текущий checkpoint:00 A/01–07 A/B,08 сверка; новое подключение и16–19 ещё предстоят |
| `Documentation/README.md` | Та же граница принятой реализации и планируемых09–15 |
| `Documentation/Plans/README.md` | Согласованные статусы и действующее отдельное поручение вместо «только план» |
| `Documentation/Business logic/README.md` | Завершённый статический аудит вместо «пока не начат»; историческое/текущее evidence разделено |
| `Documentation/Technical documentation/README.md` | Архитектурная таблица описывает существующие типы/контракты |
| `Documentation/Technical documentation/01-architecture.md` | AgentSettingsService21 actual; все строки таблицы сверены; старое рабочее имя DialogRetentionService заменено фактическим ExpiredDialogCleanup22 с явным пояснением |
| `Documentation/Technical documentation/06-database-maintenance.md` | Принятые gate01, primary02 и test lifecycle07 с точными границами и ссылками на Results |
| `Documentation/Technical documentation/08-dialog-domain-state.md` | Restore05, реализованные Responses/composition/compact/settings; пример Domain оставлен историческим checkpoint06 без изменения C# |
| `Documentation/Technical documentation/20-agent-turn-orchestration.md` | Cleanup origin03, accepted state/no replay, ToolExecutionSession.LastResult отдельно от runner |
| `Documentation/Plans/AgentBridge Audit Remediation/README.md` | Current statuses/authorization и карта принятых00–07, ABQA-002 B-only/ABQA-010, остаточные09–20 |
| `Documentation/Plans/AgentBridge Audit Remediation/Decisions.md` | Датированное пояснение последующего поручения; исходные Q-002–005 и запрет real operations сохранены |
| `Documentation/Plans/AgentBridge Audit Remediation/08-documentation-alignment.md` | Статус и Results08 |

EFCoreLibrary/HttpClientLibrary/codex-lb: собственного writable manifest нет. Coordination, production/tests/projects/examples, slnx, OpenSpec и generated/build outputs не менялись. JSON/SSE docs и Responses/AGENTS уже актуализированы06 и оставлены без повторной правки.

### Before/after для ABQA-001

| До на входном HEAD | После |
| --- | --- |
| Architecture правило «без явного статуса — будущие типы» относит AgentSettingsService к плану | Все12 строк существующих типов/контрактов сверены с исходниками; ReadAsync/SelectAsync/GetStatusAsync actual21; DialogRetentionService прямо назван прежним нереализованным именем |
| Business logic/README: аудит «пока не начат» | Статический00–15 завершён, общий A/B/C/D частичный, результаты исправлений отдельно |
| Root/Documentation/Plans/Remediation README: исправления не начаты, implementation evidence отсутствует, разрешён только план | 00 принят A,01–07 приняты A/B,08 принят A; действует прямое поручение00–20 в адресных областях |
| Maintenance/runner/Restore общие описания не отражают принятые уточнения01–05/07 | Gate независимо Stage, safe primary всех трёх cleanup путей, cleanup OCE origin, chronology bounds и оба early-exit test lifecycle со ссылками на точные Results |

Три original baseline места001 уже были исправлены до аудита: Business logic/01-purpose-and-scope.md и02-dialogs-and-tools.md остаются без правки; Plans/README обновлён только в актуальном статусе remediation. Исторические counts, checkpoints/STOP и степень доказанности аудита не повышены задним числом. ABQA-002 теперь подтверждён/исправлен локально в B ownership/error границе06, S2 предварительно; deployment leak не заявлена. ABQA-010 закрыт07 в обоих исходных местах. Q-003–005 implementation/CLI/live evidence,09–15,16 и C/D17–19 остаются открытыми; MSSQL/config/facade/три RID не названы actual API.

### Статические проверки и ограничения

Все успешные read-only checks Exit0; builds/tests/CLI/tooling/scripts не запускались. Команды чтения: `Get-Content -Raw -Encoding UTF8 <exact path>` и `Get-Content -Encoding UTF8 <exact path> | Select-Object -Skip N -First M`; поиска: `rg --files <root>` / `rg -n <pattern> <exact source paths>`. Source сверка включает Application public типы/порты, AgentSettingsService XML/public methods, Domain/Dialog Restore XML и body, Tokenization.ContextTokenCounter, EF maintenance interface/coordinator/process/native algorithms, CodexLb gateway cleanup/HasData и consumer UsageRegistration/UsageFlow/IAccountSummarySource. Business factories/authorization ports остаются приложению; нового library API не описано.

Inline read-only PowerShell checks использовали strict `[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes(...))`, Regex Markdown links/headings, `[IO.Path]::GetFullPath`, `Test-Path`, `[xml](Get-Content -Raw -Encoding UTF8 agent-bridge.slnx)`, File Path nodes и Get-FileHash. Итог:12 Markdown,243 локальные ссылки/21 fragment, issues0; UTF-8 без BOM, LF/CR0, U+FFFD/четыре вопросительных знака/проверенные mojibake markers отсутствуют.11 затронутых Documentation paths уже присутствуют в slnx ровно по одному; root README существовал вне solution items и это не менялось. Новых файлов/ссылок решения не требуется.

Raw baseline12 файлов сохранён в памяти текущей tool-сессии до правок. Проверены сохранность исходного задания08 до Results (кроме статуса), неизменность всех12 C# fenced blocks и LF исходных файлов. Примеры/XML source не изменены: нового compile evidence нет, исторические consumer builds не выдаются за проверку на текущем HEAD. Compile-only regression остаётся16. Documentary runtime suite не требуется. Замечания промежуточного review координатора учтены: обязательные датированные registry notes сохранены как задача20; restart ограничение отнесено к checkpoint10 со ссылкой на позднюю DB evidence map; counts66/164 явно помечены checkpoint06/12 и не изменены.

Read-only Git: `git status --short`, `git rev-parse HEAD`, `git diff --stat`, `git diff --name-only`, `git diff --check`; для соседей `git -C ../work/EFCoreLibrary status --short`/`rev-parse HEAD`, аналогично HTTP и `../codex-lb`. Git предупреждает будущую LF→CRLF conversion, actual12 файлов остаются LF. До записи результатов проверка нового fragment с en dash дала1 ошибку; heading/link упрощены, повтор issues0. Первая проверка slnx ошибочно требовала already-unlisted root README; уточнена existing граница11 Documentation paths, slnx не изменён. Discovery двух предполагаемых source paths был неуспешен; actual Dialog.cs и consumer files найдены через rg --files. Первая попытка передать весь raw baseline одним JSON была усечена output limit; повтор по одному файлу успешно сохранил все12. Это ошибки проверочных команд, не дефекты продукта и не скрытые runtime failures.

Новых TRX/DLL/build logs/артефактов нет; артефакт08 — только Markdown этого этапа. B01–07 описывается по принятым Results/source, повторно не исполнялся; actual алгоритмы не приравнены к live HTTP, SQL atomicity, native/provider/process effects или OS crash. Исторические отчёты и чужие изменения сохранены. Локальных вопросов/блокеров A нет. На первичной передаче ABQA-001 текущие места сверены, этап готов к приёмке в A; add/commit не выполнялись.

**Приёмка08, 2026-10-06:** координатор от имени пользователя принял A после независимой проверки полного docs diff, actual методов/remediation contracts, двух исправленных review замечаний, UTF-8/LF всех12 файлов,243 локальных ссылок и12 неизменных C# blocks. ABQA-001 текущие документы исправлены в A; CLI/runtime не закрыты. Поручен фактический отдельный local docs-коммит ровно12 файлов manifest; Coordination исключён. Статус и навигаторы обновлены после приёмки, ссылки проверены повторно без build/test. После коммита остановка;09 не начинается.
