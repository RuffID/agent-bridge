# 09 — OpenSpec workflow и отсутствующее CLI evidence

[Навигатор](README.md) · [Принятое решение](Decisions.md). Статус: **принят в A workflow/B CLI**. Зависимость: 00. Q-003 согласован: main spec актуальна, старые задания — история. Находки: **ABQA-003 закрыт в CLI boundary; ABQA-004 принят по Q-003 workflow**.

## Цель и область

Разобраться с промежуточным запретом compact в старых JSON/SSE deltas и получить фактическую validation main/all18 changes. Источники — [Findings](<../AgentBridge Quality Audit/Findings.md>), [Q-003](<../AgentBridge Quality Audit/OpenQuestions.md#abqa-q-003>), [main spec](../../../openspec/specs/agent-runtime/spec.md) и [changes](../../../openspec/changes).

Этап не разрешает удалять/архивировать changes, устанавливать CLI или менять действующий compact contract. Textual mismatch не является доказанным runtime-дефектом CompactAsync.

## Работы

1. Составить точный список18 исходных change-папок и отличить их от новых работ, если такие появятся до исполнения. Зафиксировать actual CLI availability/version по отдельно разрешённой команде, а не по старой записи об отсутствии.
2. Закрепить согласованный Q-003: действующие правила в main, старые задания отражают историю и не возвращают промежуточный запрет compact. Проверить actual semantics sync/rename у доступного CLI, чтобы техническое завершение старых changes сохранило этот контракт.
3. Подготовить минимальный manifest документальных правок по принятому решению. Сохранить действующий main compact contract. Если нужны sync/rename операции CLI, согласовать точные команды отдельно от validation; решение о продукте не означает разрешение этих операций.
4. По доступной согласованной версии CLI сформировать точные strict validation команды для main и каждой из18 папок. Запуск разрешается отдельно; unknown command syntax не заменять выдуманным примером.
5. Сохранить version, command, exit code и sanitized output по каждому объекту. Исправить только доказанные и согласованные ошибки; task checkboxes менять лишь после соответствующего evidence.
6. Отделить validation от archive readiness. Archive/sync не выполнять без отдельного поручения, даже если validation прошла.

## Проверки

A: карта требований main/deltas, compact ambiguity и проверка локальных ссылок. B: реальные CLI результаты на текущем наборе файлов после разрешения запуска. Если CLI отсутствует, записать блокер и потребность в отдельном решении об установке; Markdown lint не закрывает ABQA-003.

## Критерии завершения

Согласованный Q-003 отражён в actual workflow без возврата старого запрета compact; ABQA-004 получает проверенный результат. ABQA-003 закрывается только при evidence main + всех18 исходных changes; новые changes перечисляются отдельно. Недоступная CLI или неразрешённая операция остаются открытыми частями, без automatic archive.

## Результаты

Промежуточный A/evidence checkpoint ниже принят отдельно; итог после разрешённого продолжения находится в разделе «Продолжение после промежуточного review». Статус полного CLI набора определяется final19/0, а не сохранённой прежней таблицей5/14.

### Область и baseline

2026-10-06, Asia/Novosibirsk. Выполнена документальная часть09 и реальные CLI проверки в отдельном пользовательском чате. Прочитаны задание09, принятые Results00/06/08, root/Documentation/Plans AGENTS, README/Decisions, Findings003/004 и итог15 аудита, main и original18. Csharp-project-rules прочитан по поручению; C#/persistence код, проекты и compile-check не затронуты. Отдельные профильные build references не применялись: сборки не требовались.

Входной HEAD `8525601e07be5ead657fe5794674ed8c7c91f658`. Единственная чужая tracked правка на входе — Coordination.md. EFCoreLibrary/HttpClientLibrary read-only, status чистые; codex-lb read-only, чужая untracked .vs/ сохранена. Coordination, чужие файлы и исторические Initial Implementation/Quality Audit отчёты не изменены. Original proposals/contexts/tasks, все task checkboxes и16 остальных delta specs не изменены. Дополнительных changes нет: original18 сверены с исторической картой00 аудита, текущие18 совпадают с ней.

### Exact manifest

Только `D:/Media/User/source/repos/agent-bridge`,11 файлов:

- `openspec/specs/agent-runtime/spec.md`: исправлены только служебные Purpose/Requirements/Requirement/Scenario заголовки; русский текст, names, scenarios, MUST положения и compact contract сохранены.
- `openspec/changes/base-repository-adapters/specs/agent-runtime/spec.md`: Requirement/Scenario вместо нераспознаваемых русских служебных заголовков; содержание сохранено.
- `openspec/changes/expired-dialog-cleanup/specs/agent-runtime/spec.md`: та же грамматическая правка MODIFIED, без изменения нормативного текста.
- `openspec/README.md`: действующий workflow Q-003, compact/name map, version-bound static semantics и запрет повторного применения старого запрета compact.
- `Documentation/Plans/AgentBridge Audit Remediation/09-openspec-cli-evidence.json`: actual JSON before/after каждого из19 объектов, полные commands/exit codes и final source SHA256; локальный путь root не является секретом, raw значения runtime не использованы.
- `Documentation/Plans/AgentBridge Audit Remediation/09-openspec-reconciliation.md`: статус и эти Results; исходное задание выше сохранено кроме статуса.
- `Documentation/Plans/AgentBridge Audit Remediation/README.md`: текущее состояние09 и ссылка на workflow/evidence; приёмка не заявлена.
- `Documentation/Plans/AgentBridge Audit Remediation/Decisions.md`: датированное исполнение Q-003; само принятое решение сохранено.
- `Documentation/README.md`: актуальная доступность CLI/strict остаток и workflow navigation вместо «CLI отсутствует».
- `README.md`: текущая workflow09 navigation и честный strict остаток; C# примеры не изменены.
- `agent-bridge.slnx`: два новых артефакта видны как Solution Items; projects/references не менялись.

Другие repo: writable manifest пуст. Production/tests/generated/build outputs не изменены. Новых TRX, DLL, runtime или doubles evidence нет. Add/commit не выполнялись.

### A workflow и compact map

[Workflow](../../../openspec/README.md) закрепляет main как действующую норму по Q-003. JSON14 «Безопасные ошибки и ограниченный вызов JSON» → current «Безопасные ошибки и ограниченный JSON вызов»; «Продолжение только своего вызова» → current «Продолжение только своего JSON вызова». JSON14 сохраняет Unsupported streaming/compact; SSE15 MODIFIED сохраняет Unsupported compact; compact18 содержит только четыре ADDED. В main все четыре действуют, safe-error JSON ссылается на отдельный compact. Более поздний remediation06 HTTP-disposal requirement действует для всех трёх transport путей и не является новой change-папкой. Исторические запреты не стёрты и не перенесены в main.

Семантика установленного CLI1.14.1 проверена статически в `D:/Media/User/AppData/npm/node_modules/@fission-ai/openspec/dist/core/specs-apply.js:300–421`, `dist/core/archive.js:1340–1370`: RENAMED→REMOVED→MODIFIED→ADDED; rename сохраняет body, identical additions/modifications — no-op, differing ADDED — collision, MODIFIED проверяет потерю scenarios, но заменяет полный body. Archive default готовит применение deltas, skipSpecs пропускает его. Actual help не предлагает top-level sync/rename. Ни одна mutation-команда не запущена: это static semantics, не runtime archive evidence.

SSE MODIFIED имеет matching name/scenarios и может вернуть старый compact Unsupported даже без INFO о collision. Поэтому successful validation и rename сами по себе не обеспечивают Q-003. Безопасное будущее завершение требует отдельного review main-preserving manifest и exact commands; archive/sync/rename/installation/deletion не выполнены.

**ABQA-004: проверен в A workflow, приёмка ожидается; Q-003 не переоткрыт.** Actual archive readiness не доказана. Textual mismatch не объявлен CompactAsync runtime defect.

### Реальные команды и результаты B CLI

Discovery `Get-Command openspec -All -ErrorAction SilentlyContinue` без исполнения обнаружил openspec.ps1/openspec.cmd/extensionless wrapper. Package.json статически1.14.1, отдельно от actual version. Пользователь в этом чате явно разрешил три команды, затем19 strict commands, затем точный повтор тех же19 после grammar fixes. Разрешения не распространялись на mutations или дополнительные runs.

Cwd всех CLI commands: `D:/Media/User/source/repos/agent-bridge`. Executable wrapper: `D:/Media/User/AppData/npm/openspec.cmd`.

```powershell
& 'D:\Media\User\AppData\npm\openspec.cmd' --version
& 'D:\Media\User\AppData\npm\openspec.cmd' --help
& 'D:\Media\User\AppData\npm\openspec.cmd' validate --help
```

Каждая Exit0. Version output `1.14.1`. Help выводит `Usage: openspec [options] [command]`, `validate [options] [item-name]`; validate help: `--type <type>` (change|spec), `--strict`, `--json`, `--no-interactive`. Exact строгие команды, каждая выполнена один раз before и один раз after по отдельным разрешениям:

```powershell
& 'D:\Media\User\AppData\npm\openspec.cmd' validate agent-runtime --type spec --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate agent-turn-orchestration --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate application-tools --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate base-repository-adapters --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate context-compaction --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate context-composition --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate cross-component-verification --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate database-startup-and-backup --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate dialog-settings-and-status --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate dll-delivery --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate expired-dialog-cleanup --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate initial-provider-migrations --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate model-catalog-and-keys --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate model-tokenizer --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate persistence-models --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate responses-json-adapter --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate responses-sse-adapter --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate scenario-unit-of-work --type change --strict --json --no-interactive
& 'D:\Media\User\AppData\npm\openspec.cmd' validate usage-guide-and-closure --type change --strict --json --no-interactive
```

Полный sanitized CLI JSON сохранён в [evidence09](09-openspec-cli-evidence.json); без сокращения diagnostic сообщений. JSON `version: "1.0"` — версия report schema, не версия CLI. JSON root source nearest соответствует нашему checkout. Итоги before/after — отдельные проверки разных source состояний, не суммарный successful count.

| Main/original change | Before exit | After exit | After ERROR/WARNING/INFO |
| --- | --- | --- | --- |
| agent-runtime | 1 | 1 | 0/36/0 |
| agent-turn-orchestration | 1 | 1 | 0/2/0 |
| application-tools | 1 | 1 | 0/3/0 |
| base-repository-adapters | 1 | 1 | 0/1/0 |
| context-compaction | 1 | 1 | 0/2/0 |
| context-composition | 1 | 1 | 0/3/0 |
| cross-component-verification | 0 | 0 | 0/0/0 |
| database-startup-and-backup | 1 | 1 | 0/2/0 |
| dialog-settings-and-status | 1 | 1 | 0/3/0 |
| dll-delivery | 0 | 0 | 0/0/0 |
| expired-dialog-cleanup | 1 | 0 | 0/0/0 |
| initial-provider-migrations | 1 | 1 | 0/1/1 |
| model-catalog-and-keys | 0 | 0 | 0/0/0 |
| model-tokenizer | 1 | 1 | 0/2/0 |
| persistence-models | 1 | 1 | 0/1/0 |
| responses-json-adapter | 1 | 1 | 0/3/1 |
| responses-sse-adapter | 1 | 1 | 0/2/0 |
| scenario-unit-of-work | 0 | 0 | 0/0/0 |
| usage-guide-and-closure | 1 | 1 | 0/1/0 |

Before: main missing Purpose/Requirements, два changes без parsed deltas,12 других changes strict failures по длине;4 passed/15 failed. INFO о structurally invalid main у16 changes — archive advisory, не standalone strict error. After:5 passed/14 failed (main failed, changes5/13); ERROR0, main36 WARNING +26 ADDED WARNING в13 deltas; INFO2 о collision у initial-provider-migrations/responses-json-adapter. Нет runtime/code дефекта, установленного этими diagnostics.

Header fix дал положительный контроль: expired-dialog-cleanup Exit1→0, parser видит MODIFIED; base-repository-adapters Exit1→1, но no-delta ERROR устранены и появилась реальная length WARNING. Main missing-section ERROR устранена, parser теперь проверяет63 requirement blocks. Четыре прежних successful changes остаются Exit0; final names/tasks/body сохранены. After INFO2 отделён от strict failures: сам INFO не делает report invalid.

**ABQA-003 остаётся открытым:** CLI теперь есть и actual main/all18 evidence получено, но весь набор strict не проходит. Нельзя назвать09 полностью завершённым или подменить strict run Markdown lint/non-strict pass.

### Оценка length warnings и точная область возможного продолжения

`dist/core/validation/constants.js:9` задаёт500 characters; `validator.js:256–265` проверяет ADDED, MODIFIED намеренно не проверяется этим length правилом; main проверяется целиком. `validator.js:873–875`: strict valid только при ERROR0/WARNING0, INFO не влияет. Поэтому expired-dialog-cleanup after passes не доказывает короткий текст или возможность повторного безопасного sync.

Предыдущая промежуточная формулировка исполнителя о запрете любых структурных правок уточнена по review: **реорганизация с сохранением каждого normative положения сама по себе разрешена текущим документальным remediation поручением**. Она не является автоматически запрещённым historical rewrite. Запрещено стирать промежуточный no-compact, менять history evidence/checklists или прятать требования ради pass.

Оставшиеся warnings не просто ошибка заголовка: для pass потребуется разделить36 main blocks и26 ADDED blocks в13 old deltas на самостоятельные требования до500 characters, с отдельными scenarios. Просто разбить абзацы недостаточно: validator считает текст требования до сценариев. Минимальная предлагаемая область дальнейшего review — эти36/26 blocks в main и original deltas, плюс matching MODIFIED/name map, workflow/Results/evidence. Никаких proposals/tasks/history report изменений.

Предлагаемый метод: сохранить исходное имя за первым выделенным правилом; новые русские names отражают одну обязанность; каждое исходное MUST/MUST NOT и оговорка получает явную карту old clause→new requirement/scenario без сокращения; проверять все исходные sentences/порядок условий и сохранить исторические Unsupported в своих old deltas. Main сохраняет current compact/HTTP06 authority. Для требований, на которые ссылаются MODIFIED, нужно синхронно подготовить mapping всех затронутых blocks; RENAMED/новые имена меняют future merge identity, поэтому одна length-правка main без этой карты недостаточна. Не переносить normative text в context, не отключать strict и не сокращать смысл до общего обещания.

Таким образом доступный объём **проверен**, а не оставлен «непроверенным»: фактический failure и причины получены. Дальнейшая реорганизация ещё не сделана; передаётся review с partial manifest и методом продолжения по требованию координатора. После handoff исполнитель прекращает запись в shared checkout до нового writer допуска. Вопрос продукта compact не требуется повторно согласовывать; повтор CLI потребует отдельного exact command разрешения.

### Статические проверки и ограничения

Ручные правки через apply_patch. Strict UTF-8/LF, русский текст/отсутствие U+FFFD/четырёх вопросительных знаков/mojibake, локальные ссылки, XML slnx и JSON evidence проверены отдельно. Для трёх specs после обратного преобразования только служебных заголовков весь текст равен baseline: требования/scenarios/MUST положения не потеряны. Final source hashes19 находятся в evidence; CLI after source не менялся после run. Original09 до Results сохранён кроме статуса.

Фактический итог inline read-only PowerShell checks:11 файлов,125 локальных ссылок,13 fragments, issues0, Exit0; JSON19 before/19 after, SHA25619 совпадают, XML slnx parsed. Использованы strict UTF8Encoding(false,true), ReadAllBytes, regex Markdown links/headings, GetFullPath/Test-Path, Get-FileHash, ConvertFrom-Json и сравнение baseline через разрешённый git show. После расширения manifest на root README первая позиционная проверка ошибочно сравнивала его как grammar-only spec; исправлена на три exact paths, повтор issues0. Source specs после CLI after не менялись.

Read-only Git status/diff/stat/name-only/diff --check разрешены переданным поручением, используются для manifest. EF/HTTP чистые; LB .vs и AB Coordination исключены. Git предупреждения LF→CRLF касаются будущей Git conversion, actual файлы LF. Build/test/restore/app/hosting/DB/SQL/HTTP/Docker/scripts/install/archive/sync/rename не запускались. CLI — actual local1.14.1, не doubles; runtime compact, deployment и archive execution этим не проверены.

Initial read commands с предположенным openspec/AGENTS.md и неверным именем00 вернули missing-path, реальные инструкции/00 прочитаны. Несколько слишком больших read outputs были усечены; baseline чтение повторено небольшими chunks. Первая составная apply_patch попытка с out-of-order slnx context отклонена целиком; повтор с точечными двумя вставками успешен. Это ошибки служебных команд, не validation pass или product failures.

**Статус передачи: проверен с ограничениями, приёмка ожидается. Commit не выполнен.** A workflow готов к review; strict failures62 warnings и archive INFO2 остаются явным остатком. Исторические отчёты и чужие изменения сохранены.

### Продолжение после промежуточного review

Координатор принял промежуточные A/evidence09, но не окончательный этап и не commit. Затем явно допустил единственного writer продолжить обычную документальную remediation:36 main blocks,26 ADDED/13 deltas и связанные MODIFIED/name maps. Новый продуктовый контракт не вводился;10 ещё не запущен этим исполнителем. Предыдущие разделы Results сохраняют промежуточное состояние5/14,11-file manifest и тогдашние ограничения; **текущее final состояние описано здесь**.

#### Структурная правка и эквивалентность

Изменены66 длинных blocks в15 specs:36 main,26 ADDED и4 matching MODIFIED (settings selection/migrations в dialog-settings-and-status, cleanup и SSE safe JSON). Они разделены на192 requirements по связанным обязанностям; исходное имя сохраняется за первым блоком каждого, остальные получают русские предметные имена и отдельные проверяемые WHEN/THEN scenarios. Main содержит134 requirements,18 deltas —99 requirements. Исходный main после grammar fixes содержал64, не63: предыдущая запись63 ошибочно использовала последний диагностический index как count, static count уточнён.

[Clause map](09-openspec-clause-map.json) фиксирует для каждого original name/операции и каждого из460 исходных нормативных предложений точный текст, индекс, target requirement, диапазон clause indices, body length и новый scenario. Текст всех460 предложений сохранён дословно; нормализован только whitespace между предложениями при сборке отдельного body. Условия/оговорки/отрицания и порядок предложений сохранены. Положения не перенесены в context или synthetic scenario. Новые scenarios показывают конкретный trigger/outcome выделенной обязанности; они не заменяют normative body.

Все126 прежних scenarios внутри66 изменённых blocks сохранены полным текстом на первом block; у extracted blocks добавлены свои scenarios. Нет изменения прежних scenario conditions или checkboxes. Inline baseline check читает HEAD через разрешённый git show, нормализует только служебные Requirement/Scenario заголовки и сравнивает каждое исходное предложение с clause map; issues0. Actual source check собирает body каждого target из trace clauses:460/460 совпадают, clauses не потеряны и не добавлены в body. Имена уникальны в каждом spec; максимум body492 characters. Full original scenario strings сохранены, не только заголовки.

Остальные28 main blocks и13 delta blocks вне split остались текстово неизменны после grammar normalization. Четыре полностью незатронутых delta specs (cross-component-verification, dll-delivery, model-catalog-and-keys, scenario-unit-of-work) имеют прежние SHA256. Actual final hashes19 сохранены отдельно от grammar-after hashes в evidence.

Matching MODIFIED разделены той же картой: имена extracted rules совпадают с соответствующими main blocks, delta operation остаётся MODIFIED. Исторические differences не устранены косметически: old migrations сохраняет пять таблиц/прежние scenarios, JSON14 сохраняет Unsupported callback/compact, SSE15 сохраняет Unsupported compact в выделенном «Cleanup и граница compact». В current main этот block сохраняет ссылку на отдельный действующий compact contract. Прежние renamed continuation/safe-error names также сохранены в original changes. Future MODIFIED может перезаписать main current body даже при pass; Q-003 authority и запрет автоматического sync/rename/archive остаются обязательными.

#### Final manifest

Только AgentBridge,24 файла, включая3 новых артефакта workflow/evidence/trace:

- `openspec/specs/agent-runtime/spec.md`
- `openspec/changes/base-repository-adapters/specs/agent-runtime/spec.md`
- `openspec/changes/expired-dialog-cleanup/specs/agent-runtime/spec.md`
- `openspec/README.md`
- `Documentation/Plans/AgentBridge Audit Remediation/09-openspec-cli-evidence.json`
- `Documentation/Plans/AgentBridge Audit Remediation/09-openspec-reconciliation.md`
- `Documentation/Plans/AgentBridge Audit Remediation/README.md`
- `Documentation/Plans/AgentBridge Audit Remediation/Decisions.md`
- `Documentation/README.md`
- `agent-bridge.slnx`
- `README.md`
- `openspec/changes/agent-turn-orchestration/specs/agent-runtime/spec.md`
- `openspec/changes/application-tools/specs/agent-runtime/spec.md`
- `openspec/changes/context-compaction/specs/agent-runtime/spec.md`
- `openspec/changes/context-composition/specs/agent-runtime/spec.md`
- `openspec/changes/database-startup-and-backup/specs/agent-runtime/spec.md`
- `openspec/changes/dialog-settings-and-status/specs/agent-runtime/spec.md`
- `openspec/changes/initial-provider-migrations/specs/agent-runtime/spec.md`
- `openspec/changes/model-tokenizer/specs/agent-runtime/spec.md`
- `openspec/changes/persistence-models/specs/agent-runtime/spec.md`
- `openspec/changes/responses-json-adapter/specs/agent-runtime/spec.md`
- `openspec/changes/responses-sse-adapter/specs/agent-runtime/spec.md`
- `openspec/changes/usage-guide-and-closure/specs/agent-runtime/spec.md`
- `Documentation/Plans/AgentBridge Audit Remediation/09-openspec-clause-map.json`

Этот final manifest заменяет промежуточный11-file manifest для будущей приёмки/адресного staging. Proposals/tasks/contexts всех18, historical Initial Implementation/Quality Audit, Coordination, production/tests/generated/build outputs не менялись. В EFCoreLibrary/HttpClientLibrary/codex-lb собственного manifest нет. Чужой Coordination и LB .vs сохранены.

#### Final actual CLI

Пользователь в этом чате на exact request19 commands ответил: **«разрешаю всегда прогон этих команд»**. Это разрешение на повторение тех же main/all18 strict commands указанным openspec.cmd из указанного cwd, не на mutation CLI. После структурных правок выполнен ровно один новый прогон19, все команды совпадают с полным списком в предыдущем разделе. CLI1.14.1; дополнительные version/help/install/archive/sync/rename не исполнялись.

[CLI evidence](09-openspec-cli-evidence.json) содержит отдельные before, after (grammar checkpoint) и final arrays с полным sanitized JSON/command/exit каждого объекта. Final SHA25619 зафиксированы после CLI; specs после final run не изменялись. Before4/15, grammar-after5/14 и final19/0 — три разных source состояния; counts не суммируются как успешные независимые runs.

| Объект | Final exit | ERROR/WARNING/INFO |
| --- | --- | --- |
| agent-runtime | 0 | 0/0/0 |
| agent-turn-orchestration | 0 | 0/0/0 |
| application-tools | 0 | 0/0/0 |
| base-repository-adapters | 0 | 0/0/0 |
| context-compaction | 0 | 0/0/0 |
| context-composition | 0 | 0/0/0 |
| cross-component-verification | 0 | 0/0/0 |
| database-startup-and-backup | 0 | 0/0/0 |
| dialog-settings-and-status | 0 | 0/0/0 |
| dll-delivery | 0 | 0/0/0 |
| expired-dialog-cleanup | 0 | 0/0/0 |
| initial-provider-migrations | 0 | 0/0/1 |
| model-catalog-and-keys | 0 | 0/0/0 |
| model-tokenizer | 0 | 0/0/0 |
| persistence-models | 0 | 0/0/0 |
| responses-json-adapter | 0 | 0/0/1 |
| responses-sse-adapter | 0 | 0/0/0 |
| scenario-unit-of-work | 0 | 0/0/0 |
| usage-guide-and-closure | 0 | 0/0/0 |

**Main + original18 strict:19 passed/0 failed, все Exit0, ERROR0/WARNING0.** Все62 length warnings устранены структурно. Два INFO сохранены: initial-provider-migrations — ADDED existing «Раздельные миграции выбранного провайдера»; responses-json-adapter — ADDED existing «Каноническая JSON генерация Responses». Они сообщают archive collision, не strict failure и не runtime дефект. Сам archive/sync/rename не запускался; отсутствие INFO у SSE не отменяет риск повторного исторического MODIFIED.

**ABQA-003:** обязательное actual strict main/all18 evidence теперь получено полностью; готово к закрытию в B CLI после окончательной приёмки. **ABQA-004:** Q-003 закреплён в A workflow с сохранением historical Unsupported и current main authority; готово к окончательной приёмке. Archive readiness/runtime compact остаются отдельными непроверенными границами, не остатком CLI отсутствия.

#### Final проверки и передача

Inline read-only PowerShell: strict UTF8/LF без BOM, U+FFFD, четырёх вопросительных знаков и проверенных mojibake markers; JSON/trace/slnx parsed; Markdown links/fragments и Solution Items проверены. Clause/scenario comparison Exit0,66 source blocks460 clauses126 original scenarios, max492, issues0; target bodies соответствуют trace. Root9 C# examples unchanged. Финальные counts links/encoding и manifest/Git evidence переданы координатору после записи Results.

Фактический final static итог:24 файла,129 локальных ссылок/13 fragments, issues0, Exit0. В15 изменённых specs224 parsed targets, из них192 относятся к trace и32 nonsplit blocks сохранены целиком;4 untouched delta SHA256 совпадают. New Solution Items3 присутствуют ровно по одному. JSON final19/19, final SHA25619 совпадают, original09 до Results сохранён кроме статуса. Git diff --check Exit0; HEAD неизменен8525601e07be5ead657fe5794674ed8c7c91f658, собственных tracked21/untracked3; чужой Coordination исключён. EF/HTTP clean, LB .vs сохранён. CLI final sources после run неизменны; documentary Results/navigation обновлены после него.

Несколько служебных patch попыток с out-of-order контекстом отклонены без частичных правок; корректный порядок hunk применён повторно. Первый orchestration вызов использовал недоступный structuredClone, заменён на JSON copy до записи specs. Эти ошибки не CLI runs/результаты продукта. Build/test/runtime/DB/HTTP/Docker/restore/hosting не запускались; новые TRX/DLL отсутствуют.

**Final status: проверен с ограничениями, окончательная приёмка ожидается; commit не выполнен.** Результат готов к повторному review. После handoff shared checkout не изменять до допуска координатора.

#### Окончательная приёмка09

2026-10-06 координатор от имени пользователя принял09 в A workflow/B CLI. Независимо проверены actual final19 Exit0/valid/ERROR0/WARNING0, SHA25619, HEAD→clause map460 положений66 blocks15 files, все167 original scenarios (126 mapped и41 остальных), delta operations, UTF-8/EOL/diff. ABQA-003 закрыт в CLI boundary; ABQA-004 принят по Q-003 workflow. INFO2/архивирование и runtime не заявлены проверенными. Промежуточные Results/evidence сохранены как история.

Поручен локальный English Conventional Commit ровно final24-file manifest, без Coordination и соседних репозиториев, push/PR/amend. Перед commit актуализированы только статусы/navigation, «четыре исходных ADDED» и постоянное exact19 разрешение пользователя в workflow. Source specs после final CLI не изменялись. Read-only checks и actual hash/status после коммита передаются координатору отдельным сообщением.
