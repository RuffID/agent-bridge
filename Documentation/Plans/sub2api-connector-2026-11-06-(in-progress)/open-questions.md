# Открытые вопросы

## Вопросы клиенту

Открытых вопросов нет.

## Технические вопросы разработчику — внутренний раздел

T01–T05 открыты. [Результаты03](03-sub2api/results.md), [разбор границ](03-sub2api/contract-blockers.md) и [клиентская адаптация](03-sub2api/client-adaptation.md) различают реализованные budget/settings/JSON/SSE generation и неподтверждённый compact. Canonical гарантия относится к границе AgentBridge и его истории; existing server transformations допустимы без обещания upstream сохранности. Это не разрешает provenance/restart04.

### T01. Доказанный входной бюджет и происхождение metadata

Достаточного публичного источника InputContextWindow не найдено. context_window, max_context_window,95%, token threshold и похожие поля не подставляются. Ordinary каталог сохраняет только ID; явный новый режим читает existing manifest и проецирует exact slug/effort/API-доступность как объявления, не как independently verified pool metadata.

При новом режиме validator/guard/snapshot допускают null окно, локальный порог и обязательную серверную проверку; это больше не блокирует пригодный settings path. Прежние strict и ServerValidation требуют известного окна. T01 остаётся открытым как проверка источника бюджета/metadata, а не требование нового server API. Изолированная fixture проверяет клиентскую проекцию, не серверную истинность budget или применимость всего pool.

### T02. Полный контекст с OAuth и инструментами

Actual Sub2Api JSON/SSE generation реализована и проверена через existing composition/HttpClientLibrary с fake handlers/local streams. Передаются полное принятое compact-окно, непокрытый хвост и текущий input без previous_response_id/legacy turn-state; call/output пары и ordering сохраняются на клиентской границе. Incoming continuation отклоняется до factory, returned response id не создаёт continuation. Client retries, смена ключа/модели/коннектора и business tool replay запрещены. Isolated проверки не подтверждают развёрнутый OAuth/tool chain; эта часть T02 остаётся открытой для05.

OAuth stream/store и SSE→JSON не объявляются потерями сами по себе. При отсутствии terminal возможен SSE fallback: будущий клиент обязан сохранять partial output и не подтверждать Completed по HTTP200/EOF. Live/OAuth, caller server drain и биллинг в текущем запуске не проверяются.

### T03. Provenance и legacy opaque-состояние

Этап04 не начат. Совпадение model ID не подтверждает совместимость opaque разных коннекторов. На04 отдельно проверить versioned identity/legacy handling, restart, pinned settings и no-replay. Разрешённый v2 бюджета не добавляет provenance, новые таблицы либо restart/recovery.

### T04. Новая поставка и прежняя бинарная поверхность

Изолированный PE/API comparator и source-linked legacy probe сохраняют прежние сигнатуры; это не проверка resolution frozen binaries против fresh closure. Baseline kit и frozen DLL не перезаписываются. На05.02 остаются source rebuild прежнего consumer, semantic API и TypeRef/MemberRef/MethodSpec resolution новой поставки, новые consumers и RID. [Методика01](01-contracts/compatibility-checks.md) и исторические [результаты02](02-composition/results.md) сохраняются.

Новый unknown-window режим использует nullable DeclaredInputContextWindow; legacy int getter fail-fast при null. Snapshot v2 читает обновлённый AgentBridge; старый reader v1 его не принимает. Это ограничение согласованного бюджетного режима, не доказательство полной old→new ABI.

### T05. Совместимость canonical generation и compact

Shared reasoning.content normalization, ordinary invalid-encrypted recovery и cross-mode encrypted stripping описаны в [клиентской адаптации](03-sub2api/client-adaptation.md). Они допустимы внутри sub2api по принятой границе canonical: полный клиентский request/ответ и локальная история сохраняются; неизменная передача upstream не обещается. Это больше не блокер generation. Internal server retries сами по себе не запрещены.

Compact может изменить модель уже до первого send по compact/global mapping. Возможность mapping не доказывает несовместимость разных моделей; response.model может быть requested echo. Independently verified actual identity не является самостоятельным условием включения при доказанной совместимости existing контракта.

[Конкретный сценарий03.03](03-sub2api/03.03-compaction.md): after accepted compact через turn10 generation отправляет opaque окно + turn11; invalid_encrypted_content recovery может удалить compaction item целиком и вернуть успех по одному хвосту. Покрытые turns сохранны в AgentBridge, но отсутствуют у модели. В source есть HTTP regression этого удаления; Go tests не запускались, несовместимость конкретного deployment/pool не установлена.

Автоматический compact sub2api явно отключён; local threshold не останавливает generation, полный контекст проходит отдельный budget guard. При серверном отказе по размеру возвращается ошибка без обрезания и retry. Ручной/force compact — Unsupported до HTTP. Этот безопасный режим реализован; best-effort с возможной потерей покрытого prefix не включён. Открыта последующая проверка совместимости existing endpoint/body/output и generation на целевом маршруте/pool с контролем encrypted-error recovery. Новые server API/headers/metadata/pool revision/single-attempt не требуются. Успех одной fixture и requested echo не закрывают T05. Этап04 не начат.

В уточнённом плане безопасный режим выделен как [03.03.01](03-sub2api/03.03.01-full-context-mode.md), completed только в изолированных границах. T05 принадлежит [03.03.02](03-sub2api/03.03.02-window-compatibility.md), blocked; дальнейшие request/response/активация/регрессия03.03.03–03.03.06 зависят от положительного основания. Эта декомпозиция не добавляет клиентского решения и не требует server API или запуска live05 ради завершения03.
