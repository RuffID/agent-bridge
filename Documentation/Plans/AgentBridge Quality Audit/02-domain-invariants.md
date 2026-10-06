# 02 — Доменные состояния, владельцы, сроки и версии

Статус: **проверен с ограничениями**. Предпосылки: 00–01; согласованная политика fixed expiry.

## Цель и вопросы

Проверить защиту состояния в памяти и не смешать её с авторизацией приложения и сохраняемой версией БД.

## Компоненты и зависимости

[Dialog](../../../Domain/Dialogs/Dialog.cs), DialogStateVersion, DialogTurn/Context snapshots; [DialogWriteToken](../../../Application/Models/DialogWriteToken.cs), DialogAccess. Тесты [DialogTests](../../../tests/AgentBridge.Tests/DialogTests.cs), DialogRestorationTests, ApplicationPortsTests.

## Способ проверки и границы

Рассмотреть чужого owner, одинаковый ID новой жизни, stale snapshot, UTC и now до/равно/после expiry. Проверить отсутствие частичной мутации при отказе; неизменяемость коллекций и дочерних объектов. Restore: повреждённые даты, revision, порядок, terminal status и повторное завершение. Prefix 0, пропуск обращения, InProgress внутри префикса, завершение позже даты compact, повторное покрытие того же префикса. Разделить domain lifetime, persisted incarnation/history revision и независимую settings version. Убедиться, что активность и изменение выбора не продлевают срок.

## Разрешения

A; существующие тесты публичного доменного API — B. Никакого подключения БД. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Таблица переходов и отказов, точные граничные значения, источник каждого инварианта и существующие проверки. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Все переходы, восстановление и границы времени/версий рассмотрены; ограничения concurrency записаны. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Атомарность БД, блокировки между процессами или права на бизнес-объект приложения.

## Результаты

### Состояние и границы

Дата: **2026-10-05, Asia/Novosibirsk**. Исполнитель: чат02 `01a10cbc-c20c-7760-af45-4142313a4580`; cwd `D:/Media/User/source/repos/agent-bridge`. Выполнен только **A**: чтение исходников, требований, существующих tests/TRX, локальных ссылок и read-only Git. Фабрики и методы, включая описанный ниже контрпример, не исполнялись.

| Репозиторий | Повторно проверенный HEAD | Состояние на входе |
| --- | --- | --- |
| AgentBridge | `41072fc37bb718dd9f4bb0473912802260bb9164` | Чужие tracked Markdown00/01/Findings/Methodology/README: 354 добавленных/12 удалённых строк; untracked Coordination/OpenQuestions. Свой02 без исходного diff; недокументальных изменений нет |
| EFCoreLibrary | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Чисто; Version0.0.5; только чтение |
| HttpClientLibrary | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Чисто; FileVersion0.0.0.5; только чтение |

Приняты результаты [00](00-contract-baseline.md) и [01](01-configuration-and-access.md); прочитаны README/Baseline/Methodology/Findings/OpenQuestions/Coordination. [Coordination](Coordination.md) фиксирует запуск на41072fc; более ранний Baseline не подменяет его. Предварительного отчёта02 не было. Прочитаны applicable root/Documentation/Plans/Domain/Application/tests AGENTS и root обеих обязательных библиотек; применён `csharp-project-rules` с `references/domain-modeling.md`. Прямой запрет B/C/D выше обычных разрешений инструкций/скилла.

Полностью рассмотрены все10 C#-файлов `Domain/Dialogs`, `DialogTests`, `DialogRestorationTests`; адресно — DialogAccess/WriteToken/Snapshot/ModelSelection/ContractSnapshot, write-port contracts и относящиеся к владельцу/версиям tests. Domain не выполняет HTTP/DB; полный обзор адаптеров и библиотечных I/O здесь не требуется и не выполнен. Дополнительные соседние проекты не потребовались. Исходное задание выше сохранено.

### Все группы вопросов и переходов

Нормативный источник — [main spec](../../../openspec/specs/agent-runtime/spec.md):380–387 (Restore),537–558 (fixed expiry),638–646 (independent settings version),807–845 (состояния/terminal prefix). Пути таблицы — от корня AgentBridge, номера проверены по текущим файлам; **D** = `Domain/Dialogs`, **M** = `Application/Models`, **T** = `tests/AgentBridge.Tests`. Все результаты — статические, чтение assertions не означает их выполнение.

| Вопрос | Исследовано | Результат A | Evidence: файл/символ/строки | Ограничение |
| --- | --- | --- | --- | --- |
| Идентичности и чужой owner | Фабрики ID, ordinal equality, все входы агрегата | Empty GUID/null/whitespace отклоняются, допустимый owner не нормализуется. Чужой owner получает OwnerMismatch до записи; совпадение строки не аутентифицирует пользователя | D/DialogId.cs:14–31; DialogOwnerId.cs:14–27; Dialog.cs:273–277,309–323; T/DialogTests.cs:61–77,304–314 | Trusted owner и права на бизнес-объект устанавливает приложение |
| UTC, создание и неизменный срок | Create, getters, переданное время; все mutation paths | Offset должен быть0, expiry строго позже creation. Даты/owner только get; begin/append/finish/compact не меняют их. Новые options действуют на будущий Create | D/Dialog.cs:17–25,29–41,50–61,345–350; T/DialogTests.cs:17–33,318–345; T/DialogRestorationTests.cs:78–92 | Options/DI повторно не аудировались; доверие к actual now — приложение |
| До/равно/после expiry; чтение | IsExpired/Availability, capture, все late writes | До expiry−1tick доступность есть; equality и +1tick дают Expired для capture/begin/append/finish/context. DTO metadata может читаться после expiry; оно не разрешает запись | D/Dialog.cs:132–138,153–173,294–323; M/DialogSnapshot.cs:45–48; T/DialogTests.cs:39–57; DialogRestorationTests.cs:85; ApplicationPortsTests.cs:251–262 | IsAvailable/capture проверяют owner/deleted/expiry, не монотонность now; хронология записи проверяется отдельно |
| Begin и порядок | Empty/duplicate IDs, sequence, одинаковые даты, обратный finish | Begin создаёт InProgress, следующий Sequence и revision+1; duplicate не меняет state. Несколько InProgress допустимы; порядок определяется началом, а не завершением | D/Dialog.cs:177–202; DialogTurnStatus.cs:4–15; T/DialogTests.cs:81–93,135–146 | Domain не выбирает политику конкурентных обращений |
| Append и terminal transitions | Missing/finished turn, stale version, все4 конечных статуса | Append повышает revision и LastChanged агрегата только для активного turn. Finish заменяет ребёнка; Completed/Failed/Canceled/Incomplete допустимы, InProgress/99 запрещены. Со свежим snapshot повторный finish даёт TurnAlreadyFinished; со старым — StaleOperation | D/Dialog.cs:132–149,207–235; T/DialogTests.cs:101–131; DialogRestorationTests.cs:78–92 | Наличие текста и реальное terminal completion определяет транспорт, не Domain |
| Prefix0/дыра/running/выход/возврат/повтор | TryApplyContext и все конечные статусы | 0 допустим даже с running tail. Любой InProgress внутри объявленного префикса даёт UnfinishedContextRange; отрицательный/слишком большой/обратный prefix — исключение. Повтор того же prefix повышает Context.Version и revision, история остаётся | D/Dialog.cs:238–267; T/DialogTests.cs:101–116,150–190,367–384 | Prefix — метаданные turns, не cutoff items; фактическая composition08/compact09 здесь не подтверждена |
| Неизменяемость и отсутствие обходов | Private root constructor/setters, read-only wrappers, дочерние getters, input records | Коллекции нельзя изменить через IList; старый DialogTurn не меняется после finish. Restore создаёт новые children из входных DTO. Internal constructors вызываются корнем; поиск ordinary production constructors/friend assemblies обхода не выявил | D/Dialog.cs:9–25,99,117,233; DialogTurn.cs:7–25; DialogContextState.cs:8–20; DialogTurnSnapshot.cs:9–10; DialogContextSnapshot.cs:7; T/DialogTests.cs:105–112,285–300; M/ContractSnapshot.cs:9–17 | Read-only view коллекции живёт вместе с агрегатом, не обещает неизменный список после его операций; reflection не проверялась |
| Локальный lifetime, stale и удаление | CheckVersion, новое Create того же ID, Restore, TryDelete | Snapshot проверяет ID/lifetime/revision. Новый объект того же ID/revision и Restore имеют новый lifetime. Delete доступен после expiry, очищает children, повышает revision; поздние операции получают Deleted, повтор delete не меняет state | D/Dialog.cs:9,163–173,271–305; DialogStateVersion.cs:13–26; T/DialogTests.cs:194–264; DialogRestorationTests.cs:14–38 | Нет process lock, persisted incarnation или физического удаления |
| Restore: корневые даты и revision | Create reuse, UTC/LastChanged bounds, negative/minimum revision | Фиксированные даты валидируются; `created <= lastChanged < expiry`; revision>=0. При rev0 LastChanged=creation. Restore не читает часы: просроченный живой root можно материализовать, поздняя мутация всё равно запрещена | D/Dialog.cs:67–79,121–128; T/DialogRestorationTests.cs:25–36,54–74 | Полная согласованность LastChanged с событиями не обеспечена: [ABQA-005](Findings.md#abqa-005) ниже |
| Restore: turns/статусы/времена/порядок | Полный foreach и минимальная revision | Empty/duplicate ID, Sequence не1..N, undefined status, start назад/после LastChanged, terminal без finish, InProgress с finish, finish до start/после LastChanged и non-UTC отклоняются. Terminal turn после Restore нельзя повторно завершить | D/Dialog.cs:81–102,207–235; T/DialogRestorationTests.cs:54–73; T/DialogTests.cs:101–131 | Не все сочетания повреждений имеют отдельный test case; дата каждого finish не требует порядка завершений |
| Restore: все context versions | Version1..N, неубывающие date/prefix, каждый historical terminal prefix | Проверяется каждый context, а не только active: prefix0 допустим, <0/rollback/>count/version gap/date назад/после LastChanged отклоняются; running или finish позже compact внутри prefix запрещены. Restore не чинит/не сортирует/не обрезает данные | D/Dialog.cs:103–120; T/DialogRestorationTests.cs:49–53,67–73 | Отказ внутри фабрики меняет только ещё не выданный локальный объект; aggregate не возвращается |
| Revision/LastChanged и отказы без частичной мутации | Все commit sites, guards/NextRevision/overflow, Restore lower bound | Каждая успешная mutation повышает revision ровно1 и фиксирует now. Обратная хронология и checked overflow выявляются до changes; typed refusals сохраняют state. Restore учитывает begin=1, terminal=2, context=1, допускает extra append revisions; найден неверный LastChanged при exact minimum | D/Dialog.cs:83,101,119–128,148,196–201,232–234,262–266,285–289,327–342; T/DialogTests.cs:135–145,175–189,349–363; DialogRestorationTests.cs:78–92 | Переполнение рассмотрено по checked-ветвям, не исполнялось. Времена append не представлены в child snapshots; их нельзя требовать восстановить как отдельную историю |
| Domain version / persisted token / settings version | Значения и public contracts, storage fake, отдельный выбор | DialogStateVersion локален экземпляру; DialogWriteToken явно содержит persisted incarnation/revision и не конвертируется из него. DialogAccess — DTO owner/ID/UTC. DialogModelSelection.Version независима; смена выбора не должна менять history token/expiry или active run | M/DialogWriteToken.cs:5–28; DialogAccess.cs:8–24; DialogModelSelection.cs:7–21; Application/Ports/IDialogTurnWriter.cs:7–11; IDialogSettingsWriter.cs:9–13; T/ApplicationPortsTests.cs:211–262,522–544; AgentSettingsTests.cs:118–132 | Fake store лишь выражает guards. Actual EFCoreLibrary transaction/CAS, независимая запись settings и pinned run проверяются04/11–12 |
| Чистый Domain и concurrency | Imports всех10 типов, AGENTS, write-port boundary | Только BCL/Domain; нет чтения часов/options/HTTP/EF. Одновременная мутация одного Dialog не поддерживается. Проверка snapshot не блокирует гонку; атомарность и fresh UTC после I/O — внешний write port | D/Dialog.cs:1–9,294–342; Domain/AGENTS.md:5–13; Application/AGENTS.md:39–45; spec:359–387 | Thread safety/DB isolation/права приложения не доказаны и Domain их не обещает |

### [ABQA-005](Findings.md#abqa-005) — Restore принимает LastChangedAtUtc без соответствующего изменения

- **Категория / достоверность:** дефект реализации, **подтверждено статическим разбором ветвей**; исполнение контрпримера не выполнялось. **Серьёзность S3:** нарушается локальная хронология валидирующей фабрики; возможен ограниченный отказ следующей записи из-за принятого неверного барьера времени. Потеря данных/реальная повреждённая строка БД не установлены.
- **Контракт:** main spec:380–387 требует отклонять нарушения локальных инвариантов восстановления; `Dialog.LastChangedAtUtc` (Dialog.cs:38–39) — время последнего принятого изменения. Каждый mutation commit фиксирует переданный now (338–342), NextRevision запрещает движение назад (327–334).
- **Минимальный вход для будущей проверки:** корректные ID/owner; `created=t0` в UTC, `expiry=t0+1day`, `revision=1`, `lastChanged=t0+2min`, turns пусты, contexts содержит ровно `DialogContextSnapshot(1,0,t0+1min)`. Ожидание — фабрика отклоняет невозможную хронологию.
- **Факт A:** Create и root bounds проходят (Dialog.cs:73–79); turn loop пуст, minimumRevision=0 (81–102); единственный prefix0/context проходит, minimumRevision становится1 (103–119). Все условия отказа121–124 ложны; фабрика возвращает revision1/LastChanged=t0+2min (126–128). Между временем контекста и LastChanged нет изменения, которым это можно объяснить: в живом root без turns единственная mutation с revision1 — TryApplyContext, одновременно создающая context и фиксирующая то же now (244–267). Append требует существующий InProgress turn (132–148), settings не меняет root, а delete не восстанавливается как живой root.
- **Условное влияние:** следующий TryBeginTurn при `now=t0+1min+1tick` получает исключение NextRevision, хотя время позже единственного actual context. Это вывод по коду, не наблюдённый runtime-сбой. Сценарий требует некорректного restore input; обычное создание/compact не порождает его.
- **Ограничения/существующие проверки:** `DialogRestorationTests.InvalidSnapshotIsRejected`:54–74 проверяет12 иных corruptions, но не этот LastChanged case. `RestorePreservesHistoryAndRejectsOtherInstanceSnapshot`:14–38 проверяет корректный round-trip с extra append revision. Отсутствие case не является доказательством дефекта; доказательство — конкретные условия фабрики и commit sites. Не предлагается требовать равенство LastChanged последнему child date для всех snapshots: допустимые append-операции не имеют своего child timestamp.
- **Связи/дедупликация:** действующие ABQA-001/002/003/004 не описывают эту первопричину. Устойчивый ID назначает координатор. Передать03–04 для рассмотрения достижения фабрики по сохранённым данным; это не выполненный аудит persistence и не доказательство обхода EFCoreLibrary.

### Tests и историческое evidence

Исходники [DialogTests](../../../tests/AgentBridge.Tests/DialogTests.cs), [DialogRestorationTests](../../../tests/AgentBridge.Tests/DialogRestorationTests.cs) и owner/token часть [ApplicationPortsTests](../../../tests/AgentBridge.Tests/ApplicationPortsTests.cs) рассмотрены как описание сценариев. Ни один тест не запускался. В ApplicationPortsTests storage — однопоточный ContractStore:443–545, не actual persistence.

Статически прочитаны TestRun/Times и относящиеся к этим трём классам UnitTestResult в [stage13-core.trx](../../../artifacts/compile-check/stage13/results/stage13-core.trx) (2026-10-04 00:05 UTC+07) и [core.trx](../../../artifacts/test-results/integration-20261004/core.trx) (2026-10-04 09:10 UTC+07). В каждом71 записей Passed этих классов; это пересекающиеся наборы, **не142 проверки**, не pass текущего HEAD и не test на [ABQA-005](Findings.md#abqa-005). Соответствие каждой записи source/build hash не установлено, полная provenance —14. Исторические [06](<../AgentBridge Initial Implementation/06-dialog-domain-state.md>):39–51 и [10](<../AgentBridge Initial Implementation/10-scenario-unit-of-work.md>):35–51,69–76 описывают прежние команды и ограничения; старые разрешения не переносились.

**Новых S02-QNN вопросов нет.** [ABQA-005](Findings.md#abqa-005) не требует выбрать новую бизнес-политику; [ABQA-Q-002](OpenQuestions.md#abqa-q-002) остаётся общей границей будущего окружения. ABQA-002 не повышен до доказанной runtime-утечки; ABQA-003/004 и Q-003 не переоценивались за пределами02. Общие реестры не изменялись.

### Пропущенные проверки

| Что / уровень | Причина и влияние | Необходимое окружение/разрешение и будущий сценарий |
| --- | --- | --- |
| Build и существующие Domain/ports tests, B | Прямой запрет; current compile/исполнение assertions не подтверждены | .NET10/локально восстановленные packages, отдельные точные команды для `tests/AgentBridge.Tests/AgentBridge.Tests.csproj` после актуальной проверки build/import/output chain. Фильтр трёх названных классов; выполнить expiry−1tick/equality/+1tick, stale/new lifetime, terminal prefix и restore corruptions. Новая готовая команда не установлена: исторический --no-build не гарантирует текущую бинарную сборку |
| Динамический counterexample [ABQA-005](Findings.md#abqa-005), B | Фабрика не исполнялась; подтверждён статический путь, не runtime reproduction | Отдельное поручение вне аудита на средство проверки: context-only revision1/несогласованный LastChanged; также законный InProgress append с LastChanged позже start. Существующего точного теста нет; harness/test не создан, команда сейчас не установима |
| Реальное сохранение/rehydration/CAS/late delete, C | DB/SQL/processes запрещены; domain lifetime и ContractStore не доказывают persisted guards | Собственные согласованные SQLite/PostgreSQL resources, fresh UTC/original token, отдельные точные команды существующих integration tests после03–04. Проверить owner/recreate/revision/equality и atomic refusal. Другие providers/OS crash не охватываются |

App/hosting/CLI/HTTP/Docker/DB/SQL/migrations/backup/restore/native, сборки, тесты, project/user scripts/exe не запускались; новые tests/harness/scripts не создавались. Production/dependencies/build/config/AGENTS/spec/исторический план не менялись.

### Итог и передача

**Проверен с ограничениями.** Все группы02 рассмотрены A. Один новый кандидат **[ABQA-005](Findings.md#abqa-005)**; прочие рассмотренные переходы и граничные отказы согласуются с main. Runtime pass, thread safety, actual persistence atomicity/права приложения и отсутствие других дефектов не доказаны.

Передать03 валидирующий вход Restore и [ABQA-005](Findings.md#abqa-005);04 — различие нового локального lifetime и исходного persisted token, монотонную chronology и отказ без refresh;08–09 — prefix0/terminal gap/повторное покрытие не item cutoff;11–12 — separate selection version не меняет expiry/history snapshot. Детальный аудит этих этапов здесь не выполнялся. Координатор самостоятельно читает этот файл и обновляет реестры/статусы.

Финальный контроль A, 2026-10-05 22:54 UTC+07:00: собственный diff прочитан; diff --check успешен, все18 локальных path-ссылок доступны. Strict UTF-8 без BOM/LF сохранены; U+FFFD/mojibake/четырёх вопросительных знаков нет. Задание совпадает с HEAD, кроме статуса; исправлена опечатка hash EFCoreLibrary по повторному rev-parse. Только свой02 изменён исполнителем; остальные Markdown сохранены, index пуст, недокументальных изменений нет. Обе обязательные зависимости чисты, HEAD неизменны; предупреждение autocrlf не меняло config или файл.
