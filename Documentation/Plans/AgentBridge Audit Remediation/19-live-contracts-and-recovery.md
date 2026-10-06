# 19 — Внешние контракты и recovery после аварии

[Навигатор](README.md) · [Решения](Decisions.md). Статус: **доступная подготовка принята в A; C/D заблокированы**. Зависимости: 03,04,06,10–18 в применимой части; оставшиеся ресурсы **Q-002**, отдельные разрешения C/D. Контракты Q-004/005 уже согласованы; implementation evidence ещё требуется.

## Цель и область

Получить evidence там, где fake HTTP/new DI root/synthetic acknowledgement не отвечают на вопрос о deployment. Источник — [итог15 аудита](<../AgentBridge Quality Audit/15-final-reconciliation.md>) и [открытые вопросы](<../AgentBridge Quality Audit/OpenQuestions.md>).

Работа относится только к выделенным app/process/endpoint и test данным. Не запускать production, Telegram, реальные бизнес-действия или чужие ресурсы. Если собственное средство проверки отсутствует, его создание требует отдельного поручения.

## Подготовка

Endpoint пользователь предоставит позже; сейчас внешнее подключение отложено. Перед запуском зафиксировать deployed codex-lb commit/exact model IDs, аккаунт/ключевую политику, synthetic данные, небольшой конкретный предел расходов, разрешённые запросы, crash/disconnect точки, test processes/MSSQL storage и cleanup. Secrets/raw user payload не писать в отчёт. Для каждого исполняемого файла/команды получить требуемое разрешение; не придумывать готовый запуск до определения ресурсов.

## Работы: внешний transport и compact

1. Проверить live catalog/JSON/SSE/compact в согласованной версии, canonical controls/continuation и safe error metadata. Invalid individual key не должен незаметно переключаться на shared key.
2. Проверить отмену до данных, partial/terminal response и explicit Failed priority после04; throwing cleanup из06 — только если имеется согласованное наблюдаемое средство. Фактическую сетевую утечку не выводить из local counters без релевантного evidence.
3. Проверить pre-HTTP отказ malformed/duplicate known nested controls10 и FIFO repeated-ID association11 на конечном compact payload, включая protected/fitting и distinct args/results. Unknown nested fields сохраняются; неопределимая связь отклоняет сжатие без потери последнего окна. Не заменять deployed результат static helper trace.
4. Проверить full-request guard после compact/tools и UnknownBudget при opaque. Local KnownTokens не объявлять server billing count или полной оценкой скрытого состояния.

## Работы: recovery и no-replay

1. С synthetic handler/счётчиком согласовать остановки до/после durable Started, после test action до outcomes и после terminal save. Не использовать внешний необратимый бизнес-эффект ради доказательства.
2. Новый процесс должен читать actual journal через текущий EF adapter и не запускать неизвестную/незавершённую попытку повторно. Повторяемость показаний счётчика и persisted identity фиксируются явно.
3. Согласованный реальный disconnect/потерю acknowledgement отличать от test exception после настоящего commit. Проверить token/step persistence, blocked session и honest incomplete/unknown/canceled outcome после03.
4. Проверить owner/app authorization boundary только на выделенных users/data. Сохранить fixed expiry и независимый dialog selection snapshot.

## Проверки и критерии завершения

Есть C/D evidence для каждого включённого сценария, current versions, команды/exit codes и подтверждённая очистка. Fixtures, synthetic действия и actual process/network fault обозначены отдельно. No-replay не объявляется external exactly-once.

Недоступная fault injection, endpoint или unresolved environment Q-002 блокируют только соответствующий вывод. Незапущенный случай остаётся открытым; успешный JSON запрос не закрывает SSE/compact/crash матрицу целиком.

## Результаты

### Доступная подготовка и граница

2026-10-06, Asia/Novosibirsk. Выполнено отдельное поручение этапа19: [матрица готовности](19-live-recovery-readiness.md), [синтетические fixtures/карточка ресурсов/evidence template](19-live-recovery-fixtures.json). Исходное задание до Results сохранено, кроме статуса. Endpoint пользователь предоставит позже; real provider17 отложен прямым решением пользователя. Повторных вопросов ресурсов/бюджета/очистки не задавалось. **C/D заблокированы**; полное исполнение19 и приёмка20 не заявляются.

Before: задание19 и historical B/Windows runtime evidence предшественников, без case-by-case readiness/data/resource form. After:19 transport/compact и12 recovery строк с current source references, ожидаемыми наблюдениями, fault distinctions и отсутствующими средствами;8 raw parameter fixtures с сохранёнными duplicate fields,2 ordered pair fixtures/8 candidates/2 alternating variants. Все31 случая остаются `not_run`, JSON `runAllowed=false`, реальные ресурсы/расходы/команды не придуманы. Это A preparation, не fresh B tests/C/D pass.

Executable live/crash consumer, actual network/process fault controller и durable synthetic counter **не созданы**: их конкретная реализация зависит от ещё не заданных ресурсов/границ. Prepared MSSQL basic persistence17 и runtime loader probe18 не покрывают recovery19. Matrix/data — завершённая доступная подготовка; средства исполнения и все реальные случаи остаются явным остатком.

### Baseline, источники и exact manifest

AgentBridge HEAD на входе `2d5bd40f60996d11b75cd7de896ee3654d795307`. Единственное исходное modified — чужой Coordination.md, принадлежит координатору и исключён из manifest. Прочитаны текущие root/Documentation/Plans/tests/Application/CodexLb/Responses/Integration AGENTS, README/Decisions, полные Results03/04/06/10–18, итог15 исторического аудита, current main spec и адресные source assertions. Использован `csharp-project-rules`/build-validation для определения необходимости проверки: C#/project executable/harness не менялись, build/повтор неизменных suites16/17 не требуется по прямому уточнению координатора. Новый slnx File item не меняет csproj/build graph. Q-003–005 не переоткрывались; исторические отчёты не переписывались.

Точный writable manifest только AgentBridge,5 файлов:

```text
Documentation/Plans/AgentBridge Audit Remediation/19-live-contracts-and-recovery.md
Documentation/Plans/AgentBridge Audit Remediation/19-live-recovery-readiness.md
Documentation/Plans/AgentBridge Audit Remediation/19-live-recovery-fixtures.json
Documentation/Plans/AgentBridge Audit Remediation/README.md
agent-bridge.slnx
```

Соседи read-only, manifests пусты: EFCoreLibrary `5962deceb6ea01306cbbda400040db88ea8e5df9`, HttpClientLibrary `ba961c6dbbaeb4e265ab9a03b810fb980de3fec4`, codex-lb `f2b8e042c4ce012ae703bc939413bf9961b00032`. Чужая untracked codex-lb `.vs/` сохранена. Local LB commit не deployed version. Production/tests/C#/csproj/AGENTS/OpenSpec/packages/migrations/generated не изменены. Manual edits через apply_patch; source contract fixes не потребовались.

### Проверка подготовки

В ignored `artifacts/stage19/` через apply_patch создан только статический verifier данных/документов, без product execution/HTTP/DB/process. До запуска прочитан его source: только PowerShell/.NET JSON/XML/file parsing, хеширование и разрешённый `git show` исходного задания; imports/Exec/restore/packages отсутствуют. Это не live/crash harness. PowerShell actual7.6.5; новый inventory SDK/runtime/packages не выполнялся, их версии16–18 остаются historical receipts в соответствующих Results.

Точные команды из cwd `D:/Media/User/source/repos/agent-bridge`:

```powershell
& './artifacts/stage19/Verify-Preparation.ps1'
git diff --check -- 'Documentation/Plans/AgentBridge Audit Remediation/19-live-contracts-and-recovery.md' 'Documentation/Plans/AgentBridge Audit Remediation/19-live-recovery-readiness.md' 'Documentation/Plans/AgentBridge Audit Remediation/19-live-recovery-fixtures.json' 'Documentation/Plans/AgentBridge Audit Remediation/README.md' agent-bridge.slnx
```

Первый verifier Exit1: PowerShell precedence в массиве manifest склеил пять путей в один, чтение остановилось до изменения данных/receipt. Исправлены только скобки выражений helper; повтор Exit0: **5 manifest files,65 существующих local link paths,132 slnx paths без missing/duplicate,31 уникальный case/строка,35 существующих public method definitions,8 valid raw JSON fixtures,8 pair candidates,2 alternating variants**. Duplicate known/unknown/schema occurrences сохранены как2 каждого; nested raw strings не прошли object normalization. Независимый FIFO/subsequence oracle для данных различает unique/ambiguous/wrong/duplicate/incomplete candidates; это не вызов production inspector, gateway или теста продукта. Все resource fields null, outcomes not_run, runAllowed=false. Исходное задание совпало с HEAD кроме статуса.

[Preparation verification receipt](../../../artifacts/stage19/preparation-verification.json) содержит exact per-file SHA/size/UTF-8/no-BOM/LF, source method references+SHA, результаты raw JSON/pair checks и карту статической границы. U+FFFD, четыре question marks и проверенные mojibake markers отсутствуют. Scoped diff --check Exit0; git warnings о будущем LF→CRLF не означают изменение actual EOL. Окончательный повтор после Results: Exit0,66 local link paths, остальные counts прежние; его повтор не product test evidence. Artifact receipt/hash передаются координатору отдельно, чтобы не создавать цикл self-hash документа/receipt.

| Стабильный input | SHA256 |
| --- | --- |
| 19-live-recovery-fixtures.json | `0BB8A7157FEA456B31FA3E294B38C8D79C36EAB526D75400B6A5792C4EC72720` |
| 19-live-recovery-readiness.md | `46BAAAD6ADC40028A6039AB95FF88A838C9935EF708286CFB1435C3C280BF984` |
| artifacts/stage19/Verify-Preparation.ps1 | `C754AA37A05B14911DBA5E0D58DC1E623672699643D24D581BD74169F87BAB7D` |
| main openspec/specs/agent-runtime/spec.md (read-only source) | `DA118D9C62C74390DF53FE5FE910ACCAAEE5EBB8A8D26F180611DD1360952D67` |

Build/product tests/TRX/OpenSpec CLI/restore/network/runtime/live process/DB/native/hosting не запускались; исторические passing counts не повторены и не суммированы. Exact Git status/diff/rev-parse/show/diff --check использованы только read-only; index пуст, HEAD прежний, EF/HTTP status clean. Собственные untracked2 учтены явно; чужой Coordination/.vs вне manifest сохранён.

### Передача

Review должен принять только A preparation. C/D строки заблокированы ресурсами и отсутствующим actual fault harness; B doubles, synthetic postcommit throw, новый DI root, Windows cross-build и runtime Load/Free не закрывают эти случаи. No-replay не является external exactly-once. После финального контроля передать exact manifest/verification/limits координатору и остановить writes, **без add/commit**. Actual адресный commit возможен только после явной приёмки/поручения; поздние endpoint/resources не разрешают самостоятельно возобновить writes.

**Приёмка доступной части19, 2026-10-06:** координатор от имени пользователя принял только A preparation после независимой проверки31 not_run строк, raw duplicate/FIFO fixtures,35 source method references, полного manifest5/Results/current navigation/slnx, receipt hashes/UTF-8/diff check. C/D, executable harness, fault controllers и durable counter остаются открытыми; полный19 не принят. Поручен фактический локальный English Conventional docs commit только exact5 manifest выше, без Coordination/соседей/outputs. Перед commit изменены только статус и эта запись приёмки; fixtures/readiness/source references сохранены. Production/tests и неизменные suites не менялись/не запускались. Передача20 — по частичным результатам; прежний checkpoint передачи сохранён.
