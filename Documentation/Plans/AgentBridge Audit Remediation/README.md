# Исправление проблем аудита AgentBridge

Статус на 2026-10-06, Asia/Novosibirsk: **00 принят в A;01–07 приняты в локальной A/B-границе;08 принят в A;09 принят в A workflow/B CLI;10 принят в A/B;11–20 не начаты**. MSSQL/мультиплатформенное подключение и строгая конфигурация согласованы, но ещё не реализованы.

План основан на [реестре ABQA-001–010](<../AgentBridge Quality Audit/Findings.md>), [итоге аудита15](<../AgentBridge Quality Audit/15-final-reconciliation.md>) и [согласованных решениях](Decisions.md). Исторический аудит подтвердил пять дефектов кода статически;01–05 исправили их и получили локальное B evidence. ABQA-002 воспроизведено и исправлено в B ownership/error границе06; S2 остаётся предварительной, deployment connection leak не доказана. ABQA-010 закрыт в обоих исходных местах локальным evidence07. Общая регрессия16, C/D17–19 и implementation Q-005 ещё предстоят;09 принят в A workflow/B CLI,10 принят в A/B по Q-004, live Q-004–005 остаётся19. Исторические [вопросы аудита](<../AgentBridge Quality Audit/OpenQuestions.md>) не переписываются. Нормативные изменения по согласованным решениям синхронизируются с [OpenSpec](../../../openspec/specs/agent-runtime/spec.md) в соответствующих этапах.

## Границы исполнения

- Создание плана само по себе не разрешало реализацию. На 2026-10-06 действует отдельное прямое поручение пользователя на этапы00–20 и необходимые адресные code/tests/docs/project изменения: AgentBridge; EFCoreLibrary только maintenance/tests; HttpClientLibrary только streaming lifecycle; codex-lb только pairing compact с OpenSpec-first. Координатор выдаёт этапные задания, принимает результат и поручает отдельные локальные коммиты; один активный writer работает в existing checkout. Область конкретного этапа не расширяется автоматически на соседние репозитории.
- Основной новый сценарий — MSSQL; .NET10; библиотека для win-x64/linux-x64/linux-arm64, сейчас тестирование на Windows11, затем ASP.NET Core на Ubuntu. Existing SQLite/PostgreSQL сохраняются. Live codex-lb отложен до предоставления endpoint и конкретного небольшого бюджета.
- Исправления делать в компоненте, который владеет причиной. Не обходить библиотеки своим HTTP, прямым EF/SQL или альтернативным maintenance coordinator в AgentBridge.
- Сначала создать минимальный различающий регрессионный сценарий, затем точечно исправить причину и проверить положительные и отрицательные контроли. Если воспроизведение не подтверждает вывод, переоценить запись вместо обязательной правки кода.
- Сохранять архитектуру .NET10/DLL, публичные контракты, fixed expiry, CAS/короткие UoW, safe errors, unknown outcomes и запрет автоматического повтора side effects. Изменение контракта требует отдельного согласования.
- Исторические отчёты первоначальной реализации и аудита не переписывать. Фактические результаты исправлений записывать в новые этапы и текущую навигацию со ссылками, сохраняя прежнее evidence. После приёмки в этапе20 добавить в реестр аудита явно отделённые датированные remediation notes со ссылками на Results; исходные записи и степень прежнего evidence сохраняются.
- Сам план не разрешает команды. Текущее поручение допускает read-only Git status/diff/log/show/rev-parse/diff --check и адресные add/commit после явной приёмки и поручения координатора. Push/PR/GitHub mutations, fetch/pull/clone/reset/clean/rebase/merge/amend/force, новые ветки/worktree/checkout запрещены. Проектные скрипты/исполняемые файлы/CLI/tooling требуют отдельного точного разрешения. Приложение, hosting, Docker, реальные БД/SQL, migrations generation/apply и внешние бизнес-операции не разрешены. Compile-check и изолированные тесты регулируются применимыми skills/AGENTS после preflight; историческое разрешение аудита их не заменяет.

## Порядок и зависимости

Этапы имеют небольшую собственную область приёмки. Нумерация задаёт рекомендуемый порядок; незавершённая работа в соседней библиотеке не блокирует независимое исправление ядра. Контракты Q-003–005 приняты в Decisions; их исполнения ждать отдельно. Оставшиеся параметры Q-002 требуются перед реальными проверками17–19. Новые12–15 выполняются до общей регрессии; прежние проверочные12–16 перенумерованы в16–20.

| Этап | Результат | Обязательные зависимости | Область |
| --- | --- | --- | --- |
| [00 — Исходное состояние и границы](00-baseline-and-scope.md) | Актуальная карта проблем, областей и проверок | — | Документация |
| [01 — Gate после начавшейся установки](01-initialization-gate.md) | ABQA-006: защита независимо от текущей стадии | 00 | EFCoreLibrary; адресные тесты AgentBridge |
| [02 — Primary error при maintenance cleanup](02-maintenance-primary-errors.md) | ABQA-007: причина сохраняется во всех трёх путях | 00;01 для общей регрессии coordinator | EFCoreLibrary; адаптерные тесты |
| [03 — Отмена и cleanup scope агента](03-runner-cleanup-cancellation.md) | ABQA-009: cleanup OCE не маскируется | 00 | Ядро/Application |
| [04 — Отмена пустого SSE](04-empty-sse-cancellation.md) | ABQA-008: исходный caller token до данных | 00 | CodexLb adapter |
| [05 — Хронология Restore](05-restore-chronology.md) | ABQA-005: недопустимое состояние отклоняется | 00 | Domain |
| [06 — Проверка и исправление HTTP disposal](06-http-disposal.md) | ABQA-002: воспроизведение и условная правка | 00;04 для итоговой SSE-регрессии | HttpClientLibrary; CodexLb adapter |
| [07 — Завершение gated test work](07-gated-test-cleanup.md) | ABQA-010: cleanup на раннем выходе | 00 | Тесты AgentBridge и EFCoreLibrary |
| [08 — Актуальные описания](08-documentation-alignment.md) | ABQA-001: текущие документы соответствуют коду | 00; принятые затрагиваемые исправления | Документация |
| [09 — OpenSpec workflow и validation](09-openspec-reconciliation.md) | ABQA-003/004 по принятому Q-003 | 00; решение принято | OpenSpec/документация |
| [10 — Вложенные controls](10-nested-controls-contract.md) | Known shape/duplicates отклоняются до HTTP | 00; Q-004 принят | CodexLb adapter/контракт |
| [11 — Повторные call_id при compact](11-repeated-call-pairing.md) | Единый FIFO, явный отказ при неопределимой связи | 00; Q-005 принят | AgentBridge и отдельно согласованный codex-lb |
| [12 — Microsoft SQL Server](12-sql-server-provider.md) | Provider/migrations/maintenance основной БД | 00,01,02,05,07 | Persistence/MSSQL tooling |
| [13 — Строгая конфигурация](13-strict-configuration.md) | IConfiguration приложения, исключение при missing required settings | 00;12 для MSSQL | Options/adapter contracts |
| [14 — Короткая регистрация](14-simplified-registration.md) | Одна facade registration, app-owned logging/schedule | 12,13 | Отдельный integration adapter |
| [15 — Три платформы](15-multiplatform-delivery.md) | MSSQL kits win-x64/linux-x64/linux-arm64 | 12–14 | Delivery/consumer |
| [16 — Изолированная регрессия и compile-only DLL](16-isolated-regression-and-delivery.md) | Свежие сборки, тесты и binary consumer каждого kits/RID | Принятые01–15 в выбранном объёме | Конкретные проекты и tests/Delivery |
| [17 — Provider maintenance и сохранение](17-provider-verification.md) | Основной MSSQL и адресные existing provider проверки | 01,02,05,07,12–16; Q-002 и разрешение C | Собственные тестовые БД |
| [18 — Runtime комплектов DLL](18-runtime-delivery.md) | Actual win-x64/linux-x64/linux-arm64 загрузка/DI/native | 12–16; Q-002 и разрешение запуска | Выделенные потребители |
| [19 — Внешние контракты и recovery](19-live-contracts-and-recovery.md) | Live transport/compact и crash evidence | 03,04,06,10–18 в применимой части; Q-002 и разрешение C/D | Выделенные app/endpoint/данные |
| [20 — Итоговая приёмка](20-final-acceptance.md) | Состояние каждого ID, нового подключения и остаточных рисков | Отчёт или явный блокер каждого00–19 | Документация |

Этапы17–19 — отдельная очередь реальных проверок. Их пропуск не препятствует приёмке доказанной локальной правки, но не закрывает integration/runtime риск. MSSQL и три RID входят в согласованный объём; MySQL/AOT/trimming/single-file не добавляются автоматически. Поддержка Linux ARM64 клиента не является поддержкой локального SQL Server Engine на ARM64.

## Покрытие находок и вопросов

| Источник | Этапы | Условие закрытия |
| --- | --- | --- |
| ABQA-001 | 08,20 | Текущие описания сверены; исторические checkpoints сохранены |
| ABQA-002 | 06,16,19 | Подозрение воспроизведено и исправлено либо опровергнуто в явно указанной границе; неизвестное не объявлено отсутствующим |
| ABQA-003 | 09,20 | Есть актуальное CLI evidence для main и всех18 исходных changes либо документированный незакрытый остаток |
| ABQA-004 / Q-003 | 09 | Владелец согласовал workflow; результаты validation не подменяют это решение |
| ABQA-005 | 05,16,17 | Контрпример отклоняется; допустимые append round-trip сохранены |
| ABQA-006 | 01,16,17 | Gate блокируется при отказе после начала установки до освобождения ожидающего |
| ABQA-007 | 02,16,17 | Safe primary сохраняется при pin/process/native secondary failure |
| ABQA-008 | 04,16,19 | Отмена до данных даёт OCE с исходным token; partial/terminal контроли сохранены |
| ABQA-009 | 03,16,19 | Cleanup exception наблюдаем, подтверждённые записи сохранены, replay отсутствует |
| ABQA-010 | 07,16 | Начатые задачи завершены и await при успешном и раннем выходе |
| Q-002 | 00,12–19 | MSSQL/ОС/RID приняты; версии и реальные ресурсы уточняются до запуска |
| Q-004 | 10,19 | Решение принято: known validation до HTTP; implementation/live evidence ещё требуется |
| Q-005 | 11,19 | Решение принято: FIFO/явный отказ; final subset evidence ещё требуется |

Q-001 уже закрыт организационным evidence аудита; новый этап создания чатов не требуется. План не создаёт отдельные чаты и не разрешает сообщения в них.

## Принятые локальные результаты

Это состояние на 2026-10-06, а не новый прогон тестов. Точные команды, before/after, counts/TRX и actual/doubles находятся в результатах каждого этапа; пересекающиеся suites не суммируются.

| Этап / находка | Принятый результат | Сохраняемая граница |
| --- | --- | --- |
| [00](00-baseline-and-scope.md#результаты) | Baseline, карта причин/областей и blockers | A; код не менялся |
| [01 / ABQA-006](01-initialization-gate.md#результаты) | Начало CREATE фиксируется независимо от Stage; poison до release | Actual coordinator/gate, fake provider; real CREATE —17 |
| [02 / ABQA-007](02-maintenance-primary-errors.md#результаты) | Безопасный PrimaryError сохраняется при pin/process/native cleanup failure | Actual алгоритмы и EF pin с doubles; реальные ресурсы —17 |
| [03 / ABQA-009](03-runner-cleanup-cancellation.md#результаты) | Cleanup-only OCE не маскируется caller cancellation; accepted state/no replay сохранены | Actual runner/scopes/executor, fake storage/model; C/D отдельно |
| [04 / ABQA-008](04-empty-sse-cancellation.md#результаты) | Empty EOF + successful disposal cancellation до canonical data даёт original-token OCE | Actual transport, fake handler/local stream; live —19 |
| [05 / ABQA-005](05-restore-chronology.md#результаты) | Restore отклоняет несовместимые revision/LastChanged и сохраняет допустимые append | Actual Domain/loader/UoW с doubles; actual persistence —17 |
| [06 / ABQA-002](06-http-disposal.md#результаты) | Remaining HTTP cleanup attempted; original primary/stack + immutable secondary; cleanup-only identity | B ownership/error; попытка не гарантирует release, deployment leak не доказана |
| [07 / ABQA-010](07-gated-test-cleanup.md#результаты) | Оба исходных gated tests завершают/await tasks на normal и early exit | B test lifecycle; production hang/leak не заявлены |
| [09 / ABQA-003/004](09-openspec-reconciliation.md#результаты) | Q-003 закреплён в [workflow](../../../openspec/README.md); normative clauses/scenarios сохранены, actual CLI1.14.1 final main + original18 strict19 passed/0 failed | A workflow/B CLI приняты; ABQA-003/004 закрыты в этих границах; INFO2 archive collision отдельно; sync/archive/runtime не исполнялись |
| [10 / Q-004](10-nested-controls-contract.md#результаты) | Known nested shapes/duplicates → Validation до HTTP; unknown/schema JSON сохранены | Принят в локальных A/B границах; actual gateway/HTTP library с fake handler; live —19; исходный audit не повышен |

## Проверки и запись результатов

Использовать разделение A/B/C/D из [методики аудита](<../AgentBridge Quality Audit/Methodology.md>) как обозначение силы evidence. Разрешения текущей работы определяются поручением и действующими инструкциями. A — чтение; B — локальная сборка/изолированные tests/CLI; C — реальные provider/native/process ресурсы; D — внешний endpoint/приложение. B не включает БД, даже SQLite in-memory, hosting или дочерний бизнес-процесс.

Перед исполнением проверять точные `.csproj`, ancestor build/import-файлы, hooks, источники пакетов и traits. Собирать конкретные проекты, без автоматической сборки solution/Rebuild. Для цепочки с EFCoreLibrary задавать `GeneratePackageOnBuild=false`; для изолированных persistence-тестов — `Dependency!=Database`. Не добавлять restore/network незаметно. `--no-build` допустим лишь после успешной свежей сборки того же проекта и конфигурации; failed build не оставляет права считать старую DLL текущей.

Каждый этап содержит раздел «Результаты». При исполнении записывать дату, baseline файлов/версий, разрешённый manifest, контрпример до правки и результат после, команды/exit codes/TRX, actual и double компоненты, пропуски и влияние на следующие этапы. Git HEAD/status фиксировать лишь при разрешении соответствующей операции. Не суммировать пересекающиеся suites и не выдавать исторические TRX за свежий run.

Статусы: «не начат», «в работе», «реализован, проверка ожидается», «проверен с ограничениями», «принят», «заблокирован». Для подозрения06 допустимо «закрыт решением без изменения кода» только с явным решением и evidence. Согласование Q-003–005 не меняет статус реализации09–11 на «принят». «Исправлено локально» и «проверено в целевом окружении» — разные состояния.

## Завершение

Итог20 показывает результат для всех десяти находок, реализацию согласованных Q-003–005 и MSSQL/строгую configuration/facade/три RID. Неопределённые реальные ресурсы Q-002 и незапущенная проверка остаются явным остатком; такой итог является частичным. Полная приёмка выбранного объёма требует всех обязательных проверок, а не только наличия тестов или документов.
