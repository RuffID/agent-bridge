# Подключение sub2api к AgentBridge

Дата создания: 2026-11-06. Статус плана: **in-progress**. Этапы01–02 завершены в своих границах; этап03 — verification в объёме первой версии без compact. Отложенный compact остаётся blocked T05.

## Цель и основание

Добавить рядом с codex-lb отдельный коннектор sub2api. Приложение выбирает технологию в DI; его существующие вызовы AgentBridge для диалогов, агента, инструментов и настроек сохраняются. AgentBridge авторизуется API-ключом, выпущенным в sub2api; отдельный OpenAI API key приложению не требуется.

Выполнены контракты/baseline01 и композиция/shared Responses02. На03 existing Sub2Api bundle получил доступ/ordinary каталог и разрешённый ServerValidationWithUnknownWindow: manifest объявляет ID/effort/API-доступность, settings и snapshot сохраняют неизвестное окно без guessed defaults. Прежние политики остаются строгими к окну. JSON/SSE generation реализована с canonical гарантией на входе sub2api и в истории AgentBridge; existing server transformations допустимы без обещания upstream сохранности. Compact блокируется до HTTP из-за неподтверждённой совместимости T05. Новая DLL-поставка не подготовлена; isolated tests не подтверждают развёрнутый sub2api.

Предлагаемые проверяемые требования находятся в [OpenSpec change](../../../openspec/changes/add-sub2api-connector/proposal.md), пояснения — в [design](../../../openspec/changes/add-sub2api-connector/design.md). [Действующий контракт](../../../openspec/specs/agent-runtime/spec.md) пока не изменяется. [Вопросы](open-questions.md) содержат оставшиеся технические проверки; открытых клиентских решений нет.

Первая версия согласована без обязательного compact. Её рабочий режим — полный контекст через отдельный generation budget guard; при превышении бюджета возвращается явная ошибка без обрезания истории или повторной отправки. Продолжение сколь угодно длинного диалога не гарантируется. Compact вынесен в отдельную отложенную работу03.03.02–03.03.06 с открытым T05 и не блокирует приёмку первой версии. Это изменение объёма, а не подтверждение совместимости окна или завершение остальных проверок.

## Границы первой версии

- Основная работа принадлежит agent-bridge. Репозиторий sub2api используется для чтения и проверки wire-контрактов; его изменение не входит в план. HttpClientLibrary остаётся единственной HTTP-границей.
- Доработка sub2api исключена решением пользователя. Подстраивается AgentBridge к существующим API/поведению; отвергнутое серверное предложение не является зависимостью или следующим шагом. Запрет client retry не расширяется автоматически на все внутренние server retries.
- Адаптер: отдельная физическая папка `adapters/AgentBridge.Sub2Api/`, собственный csproj net10.0, DLL и локальный AGENTS.md. Тесты — в `tests/AgentBridge.Sub2Api.Tests/`; фактическая частичная реализация03 и условия продолжения — в [результатах03](03-sub2api/results.md).
- Первый транспорт — HTTP JSON/SSE Responses с полным рабочим контекстом без исходящего previous_response_id. Рабочий контекст включает принятое compact-окно, непокрытую историю и текущий input; это не обязательно вся история БД.
- Проверяется OpenAI-направление sub2api, включая аккаунты подписки через OAuth. Подключение Anthropic/Gemini/Grok, WebSocket, административных API, автоматический failover между коннекторами и одновременный выбор коннектора на каждый запрос не входят.
- Existing optional auxiliary API usage/files/images сохраняется у codex-lb. Его реализация для sub2api отдельно не обещается; неподдержанные операции не направляются на другой шлюз.
- Перенос opaque-состояния старых диалогов между шлюзами не считается автоматической возможностью. История остаётся доступной для чтения; неподтверждённое продолжение должно явно отклоняться до отправки.
- На02 реализован AddAgentBridgeSub2ApiConnector перед существующим AddAgentBridge(configuration, httpClientFactory); явный codex-lb — AddAgentBridgeCodexLbConnector. Старый вызов без выбора сохраняет codex-lb, прежнюю конфигурацию и overrides.
- Сохранение публичного API не означает отсутствие дополнительных DLL. Состав поставки проверяется отдельно; удаление legacy DLL/типов ради минимизации комплекта не планируется.

## Этапы

| Этап | Статус | Зависимости | Результат |
| --- | --- | --- | --- |
| [01. Контракты и совместимость](01-contracts/main.md) | completed | Нет | Матрица с источниками/образцами и сохранённый source/PE baseline; два compile-check green |
| [02. DI и общая обработка Responses](02-composition/main.md) | completed | 01 | Выбор bundle, собственные Sub2Api options, shared source и проверенная legacy поверхность |
| [03. Коннектор sub2api](03-sub2api/main.md) | verification | 02 | Первая версия без compact; доступ/settings/generation и полный контекст проверены изолированно, T01/T02 открыты; отложенный compact blocked T05 |
| [04. История и продолжение](04-dialog-state/main.md) | not-started | 03 | Безопасные границы контекста, restart и смены шлюза |
| [05. Проверки и поставка](05-acceptance/main.md) | not-started | 04 | Регрессия, DLL consumer и отдельная live-приёмка |

Зависимости в таблицах этапов обязательны. Независимые подэтапы не требуют делегирования. Запуск каждого этапа — только после отдельного поручения реализации.

### Детализация оставшейся работы03

В [навигаторе03](03-sub2api/main.md) сохранены03.01/03.02. Обязательный объём03.03 первой версии ограничен [03.03.01 — безопасным полным контекстом](03-sub2api/03.03.01-full-context-mode.md), completed в изолированных границах. [03.03.02 — пригодность compact-окна](03-sub2api/03.03.02-window-compatibility.md) остаётся blocked T05: статический анализ завершён без основания допуска. Запрос, canonical ответ, сохранение/активация и регрессия03.03.03–03.03.06 — not-started и вынесены вместе с T05 за обязательный объём первой версии. Без нового достаточного основания и отдельного поручения transport не начинается. T01 не блокирует unknown-window режим; live T02 остаётся05.03. Этап04 и отложенный compact не начаты; согласование объёма не разрешает их реализацию.

## Итоговая приёмка

Прежнее приложение собирается с обновлёнными DLL без изменения бизнес-кода и сохраняет codex-lb-поведение. Новый consumer выбирает sub2api через DI и собственные настройки, использует ключ sub2api, получает каталог, запускает несколько turns и tools, stream и возобновление после пересоздания DI без нового compact. Полный контекст проходит отдельный guard; при превышении бюджета возвращается явная ошибка, ручной/force compact — Unsupported до factory/HTTP. Отказы/отмена не вызывают повтор бизнес-инструментов, смену ключа/шлюза или потерю сохранённой истории. Успешный compact и закрытие T05 не входят в приёмку первой версии; прежний compact codex-lb сохраняется.

Isolated green, бинарная компиляция, Windows runtime и live OAuth-проверка учитываются раздельно. Без обязательной live-проверки этап05 и весь план остаются verification, а не completed. Для WebSocket или изменения соседних библиотек требуется отдельное расширение объёма.

## Порядок документирования и разрешения

На каждом подэтапе фиксировать изменённые части, точные разрешённые команды, фактический результат и существенные пропуски; синхронизировать статусы файла, таблицы этапа и этого документа. Во время реализации обновлять только соответствующие актуальные документы и локальные AGENTS.md; исторические отчёты не переписывать.

Команды в плане не являются разрешением на исполнение. Сборки/тесты регулируются текущими правилами; новые скрипты, live HTTP, креденшелы, БД, Docker и OpenSpec mutation-команды автоматически не разрешены. Уроки и глобальные инструкции/память не обновляются.

## Фактический результат подготовки

Подготовлены 19 документов плана и 4 документа OpenSpec change. Проверены 70 относительных ссылок, наличие всех новых документов в solution, XML решения, статусы и UTF-8 без повреждённых символов. Навигаторы документации обновлены.

При первоначальной подготовке плана код коннекторов, csproj, настройки runtime и БД не изменялись; сборки, тесты, live HTTP и CLI OpenSpec не запускались. Проверки, выполненные позднее на этапе01, описаны ниже; строгая CLI-валидация нового change ещё не подтверждена.

## Результат этапа01

На 2026-11-06 подготовлены [wire-матрица и образцы](01-contracts/wire-matrix.md), [API baseline](01-contracts/legacy-api-baseline.md), [методика проверок](01-contracts/compatibility-checks.md) и SHA256 evidence. Текущая поставка сверена с исходниками и сохранена отдельно от репозиториев. Исходный consumer и расширенный probe собраны против её DLL без ProjectReference: оба 0 warnings/errors, методы не выполнялись.

Не найден подтверждённый публичный input budget; OAuth удаляет output-limit control; выявлены server-side retry/opaque normalization и compact model fallback. Технические T01/T02/T05 остаются обязательными, T03 относится к provenance04, T04 — к новой поставке05. Main spec не изменён, strict CLI нового change, old→new ABI, runtime и live OAuth не подтверждены.

Этап01 завершён до реализации02; его frozen baseline не перезаписывается последующими проверками.

## Результат этапа02

Добавлены AddAgentBridgeSub2ApiConnector/AddAgentBridgeCodexLbConnector перед прежним facade, собственные options и единые internal JSON/SSE исходники обоих адаптеров. Default codex-lb/direct registrations/overrides и legacy continuation сохранены. Sub2Api пока явно Unsupported; каталог/access/generation/compact не подменяются заглушками успеха.

[Результаты02](02-composition/results.md): сохранность baseline127/112 и frozen hashes, compile-check0 warnings/errors, 497 isolated cases passed (27 новый набор + 414 codex-lb + 56 facade), semantic PE/API comparison и source-linked legacy probe. External consumer/new kit, полный old→new ABI, другие RID, БД и live OAuth не подтверждены; T01–T05 открыты. Strict CLI не выполнялся, main spec не синхронизировалась. Работа остановлена после02; этап03 не начат.

## Текущий результат этапа03

Пользователь разрешил отдельный ServerValidationWithUnknownWindow и budget snapshot v1/v2 со чтением прежнего формата. Реализованы declared manifest settings, validator/guard/status/compactor без числовой подстановки и без ослабления старых политик. T01 открыт как вопрос доказанного бюджета, но не блокирует явный новый settings режим.

[Клиентская адаптация](03-sub2api/client-adaptation.md) использует принятую canonical гарантию на входе sub2api и в истории AgentBridge. Existing forwarding способен удалить reasoning внутри сервера; это допустимо без обещания неизменной upstream передачи. Generation выполняет один POST через HttpClientLibrary, передаёт полный prepared context, читает JSON/SSE без anchors, сохраняет awaited callbacks/partial/terminal/caller/deadline/cleanup. Automatic compact явно отключён: threshold не препятствует полному generation request после separate budget guard; ручной/force compact Unsupported до HTTP. Mapping сам по себе не доказывает несовместимость. Ограничение T05 — возможность удаления opaque compact при recovery, когда покрытая история уже отсутствует в рабочем input. sub2api не дорабатывается.

[Результаты03](03-sub2api/results.md): последние проверки —557 Core +161 Sub2Api +414 CodexLb =1132 passed, failed/skipped0; семь конкретных проектов compile-check без warnings/errors. Прежние1151 budget и565 generation относятся к пересекающимся прогонам и не складываются как уникальные tests. Текущий source sub2api0.2.20 отличается от исторической описи в10 из48 files; current compactReview сохранён отдельно. Actual library с fake handlers/in-memory storage не подтверждает live OAuth/compact или новый delivery closure. T01–T05 открыты;03 и03.01/03.02 verification в объёме первой версии, отложенный compact blocked T05,04 не начат. Согласование первой версии без compact изменило только актуальный объём и зависимости; новых сборок/тестов не было. Исторические01–02/main spec и frozen kit не переписывались; CLI OpenSpec/host/HTTP сеть/БД/Docker/deployment не запускались.
