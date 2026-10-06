# Координация исправлений AgentBridge

[План](README.md) · [Решения](Decisions.md). Начало: 2026-10-06, Asia/Novosibirsk.

## Полномочия и порядок

Текущее поручение пользователя разрешает реализацию этапов00–20, необходимые изменения и отдельные локальные коммиты после приёмки координатором. Формулировки плана о разрешении только подготовки описывают прежнее состояние и не отменяют это поручение. Исторические отчёты сохраняются.

Один пользовательский чат на этап, создаваемый последовательно в существующем local checkout. Одновременно только один исполнитель изменяет checkout. Запрошены model `gpt-6.1-sol`, thinking `medium`; фактическое подтверждение инструмента записывается отдельно. Проект `local-adeec14dc238b4f3a81a18e79e595a60`, проверенный путь `D:/Media/User/source/repos/agent-bridge`, host `local`.

Исполнитель передаёт manifest, before/after evidence, точные проверки и ограничения без коммита. Координатор проверяет исходники и evidence, затем поручает адресный add/commit. Чужие изменения исключаются; `git add .` и `git add -A` запрещены. Push/PR и прочие запрещённые Git-операции не выполняются.

Реальные БД/SQL, migrations tooling, Docker, приложения/hosting, deployment и live HTTP требуют отдельных разрешений с точными командами и ресурсами. Compile-check и изолированные тесты — по AGENTS и профильным скиллам. Этап00 — только статическая проверка A.

## Исходное состояние

Повторно прочитаны status/diff/HEAD/branch всех четырёх разрешённых репозиториев. Индекс AgentBridge пуст. Новый план, Decisions и связанные README/solution items уже входят в отдельный docs-коммит `3c316f046d028c955d7a1ea057b42847f98899f5`; повторный коммит этих документов не требуется.

| Репозиторий | Ветка | HEAD | Чужие изменения |
| --- | --- | --- | --- |
| agent-bridge | master | `3c316f046d028c955d7a1ea057b42847f98899f5` | Нет |
| EFCoreLibrary | master | `3a8a53187af3c5df049770dfd6727b5065159d1f` | Нет |
| HttpClientLibrary | master | `6d0528d940d1d8494c722c22464051dd961d6bf7` | Нет |
| codex-lb | main | `f8ffbac2099a113fba54dfd8d77774f5bca80ffa` | Неотслеживаемая `.vs/`; исключена из manifest |

## Этапы

| Этап | Чат / host | Статус | Проверки и приёмка | Локальные коммиты |
| --- | --- | --- | --- | --- |
| 00 | `01a10f3f-ed96-7031-9941-e7372b0e45be` / `local` | Принят в A-границе | Baseline/map/manifest01; исходное задание/UTF8/LF/5 ссылок/diff check независимо сверены | AB `bfd09fda45d5d36f97feec2ee0f3af3a18912acd` |
| 01 | `01a10f47-8332-7fa3-b0cd-b8cdd07dcaa5` / `local` | Принят в A/B | EF50/50, AB40/40; before EF4/10 fail, AB3/3 fail; C17 открыт | EF `be05cc94b5fd7426699e12ea29e8814b361c4e5d`; AB `5ced2104a996c066da62d17ab51f09fc84201885` |
| 02 | `01a10f4f-96c8-7080-b4ff-7b03aec66cbf` / `local` | Принят в A/B | EF108/108, AB51/51; before primary-loss12/30 и3/8; C17 открыт | EF `bdb0e36bc1d52fb4b5a028559cd65373b4bad704`; AB `edd168a5360c2bda81066bd8317811c94d081316` |
| 03 | — | Не начат | Зависимость00 | — |
| 04 | — | Не начат | Зависимость00 | — |
| 05 | — | Не начат | Зависимость00 | — |
| 06 | — | Не начат | Зависимость00 | — |
| 07 | — | Не начат | Зависимость00 | — |
| 08 | — | Не начат | Зависимости по плану | — |
| 09 | — | Не начат | Q-003 принят; CLI permission отдельно | — |
| 10 | — | Не начат | Q-004 принят | — |
| 11 | — | Не начат | Q-005 принят; pairing codex-lb разрешён | — |
| 12 | — | Не начат | MSSQL; tooling permission отдельно | — |
| 13 | — | Не начат | Строгая конфигурация | — |
| 14 | — | Не начат | Короткая регистрация | — |
| 15 | — | Не начат | Три RID | — |
| 16 | — | Не начат | Свежая изолированная регрессия | — |
| 17 | — | Не начат | Реальные ресурсы/разрешения не заданы | — |
| 18 | — | Не начат | Runtime environments/разрешения не заданы | — |
| 19 | — | Не начат | Endpoint/бюджет/разрешения не заданы | — |
| 20 | — | Не начат | Отчёт или блокер каждого00–19 | — |

## Вопросы и ограничения

Q-003–005 не открываются повторно без обнаруженного противоречия. Q-002: версии/редакция SQL Server, Ubuntu/runtime machines, permissions/TLS/server backup path, deployed endpoint/commit/exact models, бюджет и cleanup остаются ресурсами будущих17–19. Windows cross-build не доказывает Linux ARM64 runtime. Подозрение ABQA-002 не объявляется дефектом без различающего воспроизведения.

## Журнал действий

- Прочитаны корневые и документационные AGENTS, README/Decisions/00 нового плана, действующая спецификация и итоговые источники аудита. Проверены точный project path и наличие отдельного docs-коммита подготовки. Производственный код и проверки B/C/D не запускались.
- Журнал и solution item созданы отдельным коммитом AgentBridge `686a0dbb42e7d6baba24b71277b636f182550c3c`. Проверены117 solution paths (нет missing/duplicates), strict UTF-8/маркеры русского текста и staged diff/check. Первая XML-проверка неверно обработала пустые Folder.File; исправлена команда проверки, файлы не менялись из-за этой ошибки.
- Чат00 создан через create_thread в проверенном local проекте. Вызов явно передал `model=gpt-6.1-sol`, `thinking=medium`; ответ подтвердил только threadId/hostId, actual model/effort не вернул. Полное задание ограничено A/одним файлом00, коммит ожидает приёмку.
- Review00: исправлено описание ABQA-001 в карте — сохраняется actual stale status AgentSettingsService (architecture52/66), навигация лишь частично обновлена. Остальная карта/проекты/traits/manifest01 и trace006 приняты. Пользовательские решения не открывались повторно. Отправлено явное поручение на адресный docs-коммит одного файла00; Coordination исключён. Соседи EF/HTTP чисты, codex-lb сохраняет `.vs/`.
- Commit00 `bfd09fda45d5d36f97feec2ee0f3af3a18912acd` фактически проверен через git show: только отчёт00, Conventional Commit. Index пуст, Coordinator diff сохранён отдельно. Зависимость00 для01 выполнена; B/C/D не заявлены.
- Coordination после приёмки00 сохранён коммитом `d68055727a80b0460d016a623616ee32e0d04a18`. Создан чат01 для EF maintenance/tests и адресной регистрации AgentBridge с manifest00, обязательными различающими B-контролями и запретом commit до review. create_thread подтвердил thread/host; model/effort переданы явно, actual не возвращены.
- Review01 в работе: независимо прочитан production diff (InitializationStarted перед CREATE; poison в catch до finally lease release) и test diffs. TRX actual before-v2 EF10:6 pass/4 fail, AB3:0 pass/3 fail; final EF50/50 и AB40/40, skipped0, XML rows соответствуют counters. Точные build/test вызовы через read_thread сверены, свежие successful build перед no-build; конкретные csproj/GeneratePackageOnBuild=false/Dependency!=Database. Эти адресные и повторные наборы не суммируются; приёмка ещё ожидает полного отчёта.
- Этап01 принят после чтения полного отчёта, source/test diff, spec733, независимой UTF8/LF проверки7 manifest files, сохранности исходного задания и diff check обеих repo. Отправлено поручение на отдельные EF fix/AB test commits с explicit manifest4+3. Partial cleanup existing gated tests допустим, но ABQA-010/07 ещё требует оба исходных места и различающий early-exit case. C17 остаётся открытым.
- Commit01 подтверждены через actual git show: EF `be05cc94b5fd7426699e12ea29e8814b361c4e5d` (4 файла), AB `5ced2104a996c066da62d17ab51f09fc84201885` (3 файла). EF чист, AB только отдельный Coordination diff, indexes пусты; HTTP/LB не менялись. Следующий02 получает эти hashes и сохраняет gate regression01.
- Coordination по01 закоммичен `36ca57ca3514d681b27ba7c650701e9665b6cf2d`. Чат02 создан последовательно для всех3 проявлений ABQA-007 с fake boundaries, before/after controls, явной приёмкой перед commit. Настройки model/effort переданы явно; инструмент подтвердил thread/host, actual не вернул.
- Review02: проверены полный source/test diff, точные final commands, пять concrete builds без warnings/errors и свежие TRX EF108/108, AB51/51 (skipped0; rows/counters совпали). Pin ownership под gate, process recovery handle и native Finish различают primary/cleanup; исходные successful/caller/late-cancel controls сохранены. Первые before runs содержали fixture shortcomings, повторный before-v2 EF30 дал12 primary-loss failures; AB8 дал3 primary-loss и1 cold-start deadline, последний не объявлен продуктовым дефектом. UTF8/LF17 файлов и diff checks проверены независимо. Accepted A/B; поручены отдельные explicit-manifest EF13/AB4 commits. C17 остаётся открытым; перекрывающиеся TRX не суммируются.
- Commit02 actual git show независимо подтвердил EF `bdb0e36bc1d52fb4b5a028559cd65373b4bad704` (13 файлов) и AB `edd168a5360c2bda81066bd8317811c94d081316` (4 файла). EF чист, AB только Coordination diff, indexes пусты; чужая LB .vs/ сохранена. Executor02 idle. Этап03 получает принятые00–02 и не затрагивает maintenance.
