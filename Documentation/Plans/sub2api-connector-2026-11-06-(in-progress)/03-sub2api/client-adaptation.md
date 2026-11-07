# Клиентская адаптация03: бюджет и граница canonical

Дата: 2026-11-07. [Результаты](results.md) · [Текущие ограничения](contract-blockers.md) · [OpenSpec delta](../../../../openspec/changes/add-sub2api-connector/specs/agent-runtime/spec.md).

## Разрешённый режим неизвестного окна

Пользователь разрешил отдельный режим `Compaction.BudgetPolicy=ServerValidationWithUnknownWindow`. Это клиентское решение, не новый контракт sub2api. Прежние `RequireLocalEstimate` и `ServerValidation` продолжают требовать известный InputContextWindow; default и codex-lb settings reader не меняются.

В новом режиме existing Sub2ApiModelCatalog читает `/v1/models?client_version=<assembly version AgentBridge>` через тот же HttpClientLibrary, Bearer и deadline/error pipeline. Непустой client_version выбирает существующий Codex manifest route; передаётся версия самого AgentBridge, без выдачи его за Codex CLI. Запрос не использует admin API, новые headers, pool revision, ETag/cache или fallback после ошибки. Default ordinary каталог ID сохранён.

Проецируются только объявленные `models[].slug`, `supported_in_api` и `supported_reasoning_levels[].effort`. Эти сведения могут происходить из локальных server descriptors; они не становятся independently verified metadata upstream pool. Отсутствующий effort/API-флаг, exact ID/effort mismatch и malformed manifest продолжают отклоняться. `context_window`, 95%, max context, input_context_window и output limit не используются как подтверждённое входное окно. Остальные capabilities остаются неизвестными.

ModelSelectionValidator получил дополнительный overload с явной policy; прежняя сигнатура остаётся строгой. Snapshot фиксирует policy до source/catalog I/O. ContextBudgetGuard требует разрешение unknown window одновременно в snapshot и собственной policy. При null окне он сохраняет null и RequiresServerValidation=true даже для точной текстовой оценки. Порог сравнивается с оценкой, при opaque — с известной частью; резерв хранится, но его соблюдение на сервере не обещается. Counter failure, caller cancellation, exact model/effort и невалидные числовые лимиты не обходятся.

Например, manifest объявляет `public-model/medium`, context_window=272000 и95%, но не доказывает input budget. При пороге100 и резерве10 новый snapshot имеет InputContextWindow=null, а не100/272000/258400. Оценка120 достигает локального порога, но не доказывает переполнение серверного окна. Сервер вправе отклонить большой запрос; автоматического retry нет.

## Snapshot и существующие сценарии

Старые публичные constructors/methods и int getters сохранены. Новое nullable свойство `DeclaredInputContextWindow` позволяет безопасно читать unknown. Старый int getter при unknown явно бросает InvalidOperationException; возвращать0/порог вместо лимита запрещено. System.Text.Json выводит прежнее имя InputContextWindow с null при unknown, не вызывая legacy getter.

TurnSettingsMapping сохраняет прежний v1 для strict known window и читает literal старого формата. Явные server policies сохраняются как v2 с обязательным BudgetPolicy и nullable InputContextWindow. Неизвестные/повреждённые форматы отклоняются. Это согласованное изменение JSON бюджета в существующем SettingsJson; таблиц, миграций и connector provenance нет. Старый reader v1 не сможет прочитать v2 — потребителю этого режима нужны обновлённые DLL. Frozen consumer/kit не обновлялись.

Existing status и compactor принимают новый режим без выдуманного окна. Сценарий compactor сохраняет Completed-only, terminal prefix, непокрытый хвост, save-before-activation и исходную историю. Unsupported compact не превращается в generation. Эти проверки сделаны на core ports с doubles, не на Sub2Api compact transport. Работа04 — connector provenance, legacy/cross-connector handling и restart — не начиналась.

## Что действительно меняет existing forwarding

| Путь/преобразование | Вывод для клиента |
| --- | --- |
| OAuth upstream stream=true/store=false, terminal SSE→JSON для внешнего stream=false | Транспортная нормализация сама по себе не означает потерю canonical. При отсутствии terminal сервер может вернуть SSE; будущему клиенту нужен честный partial/terminal lifecycle, а не ложный Completed. |
| Tool name/namespace aliases и восстановление response, согласованное преобразование call/output IDs | Нельзя объявить потерю лишь по факту преобразования. Нужно проверять оба направления и связи на конкретной форме; orphan cleanup не удаляет корректную полную пару. |
| `truncateOpenAIResponsesInputText` и `openAIResponsesInputMayNeedTruncation` | В текущем source оба no-op/false. Прежнее предположение об обрезании текста уточнено; это не блокер. |
| Shared `normalizeOpenAIResponsesReasoningContentReplay` до ordinary/passthrough | Непустой массив reasoning.content удаляется. Это конкретное несовпадение с требованием сохранять canonical reasoning, независимо от internal retry. |
| Ordinary HTTP400 invalid_encrypted_content recovery; passthrough→ordinary failover | Сервер может удалить encrypted reasoning items и затем вернуть успех. Client выполняет один HTTP, но сохранить opaque upstream при этом гарантировать не может. Passthrough не исключает shared normalizers или последующий cross-mode failover. |
| Effort minimal→none, schema normalization, instructions defaults | Не все запросы преобразуются, но отсутствие потери должно проверяться на конкретном входе; нельзя молча считать замену усилия/инструкций допустимой. |
| Response.model переписывается в requested echo | Это только объявленное имя. Его нельзя использовать как доказательство actual upstream identity; обычной generation новое vendor metadata не требуется. |
| Compact-only/global mapping до первого send, затем возможный fallback | Endpoint/body существуют, но совместимость compact с выбранной моделью не доказана; CompactAsync остаётся Unsupported. |

Владеющие sources: [Forward](../../../../../sub2api/backend/internal/service/openai_gateway_forward.go), [passthrough/non-stream](../../../../../sub2api/backend/internal/service/openai_gateway_passthrough.go), [shared body normalizers](../../../../../sub2api/backend/internal/service/openai_gateway_request_body.go), [orphan/no-op text](../../../../../sub2api/backend/internal/service/openai_responses_input_compat.go), [item IDs](../../../../../sub2api/backend/internal/service/openai_responses_item_id.go), [cross-mode](../../../../../sub2api/backend/internal/handler/openai_gateway_reasoning_failover.go), [compact mapping](../../../../../sub2api/backend/internal/service/openai_compact_fallback.go). [Hashes](contract-source-evidence.json) относятся к локальным sources, не deployment; Go/live проверки запрещены.

## Принятая граница canonical

Сценарий: следующий tool turn содержит полную пару call/output и encrypted reasoning предыдущего ответа. При upstream invalid_encrypted_content ordinary sub2api удаляет reasoning и повторяет внутри сервера; ответ может быть successful. Аналогичная потеря возможна при cross-mode failover. AgentBridge не повторяет HTTP или business tool и сохраняет локальный canonical input, но не может доказать передачу opaque модели по успешному публичному ответу. Даже без этой ошибки непустой reasoning.content удаляется shared prelude.

Для существующего сервера принята гарантия полной canonical сохранности на входе sub2api и в истории AgentBridge с допуском перечисленных server transformations при обработке. AgentBridge сохраняет также полученный canonical output/envelope, но успешный ответ не объявляет доказательством неизменной upstream передачи reasoning или independently verified model identity. Это отдельное принятое клиентское поведение, не следствие разрешения unknown budget и не требование переделать sub2api.

## Реализованная generation03.02

Existing Sub2ApiModelGateway выполняет один POST /v1/responses через scoped HttpClientLibrary. Полный prepared ModelRequest передаётся shared writer без сокращения: принятое compact-окно, хвост/current input, encrypted reasoning/content, instructions, effort, tools/controls и call/output пары сохраняются в исходном порядке. Gateway не строит историю заново и не выполняет бизнес-инструменты. Incoming continuation, включая codex-lb/legacy turn-state, отклоняется до factory; response.id и x-codex-turn-state не создают returned continuation.

Callback выбирает исходящий stream=true; без callback stream=false. Ответ читается по Content-Type как JSON либо SSE, поэтому existing non-stream SSE fallback не вызывает второй send. Shared ResponseSseAccumulator выделен из прежнего CodexLb ResponseSseState; обёртка codex-lb сохраняет его continuation/error mapping, sub2api передаёт null continuation. Непустой terminal output авторитетен, partial item events сохраняются при EOF/ошибке; [DONE]/HTTP200 не подтверждают Completed. JSON status/output/error проверяются явно. JSON, полученный вместо SSE, передаёт полные items в последовательные awaited callbacks.

GenerationTimeout покрывает headers/body/callbacks одним deadline. Caller имеет приоритет; после canonical данных сохраняется Canceled/Failed с output/envelope. Callback exceptions и read/cleanup primary identity сохраняются, secondary cleanup остаются наблюдаемыми. App HttpClient/handlers не освобождаются адаптером. Safe HTTP diagnostics дополнены итогом Sub2ApiGenerationFinished с TurnId, lifecycle и закрытым ServiceErrorType, без raw payload/ключей/ошибок сервера. Completed логируется только после terminal и cleanup; Incomplete/Failed/Exception не становятся успешной генерацией.

151 Sub2Api isolated cases и414 CodexLb regression прошли. Они проверяют actual library с fake handlers/local streams, а не deployed pool, OAuth или upstream сохранность. T01 остаётся открытым как вопрос доказанного input budget. T02 live chain и T05 compact не закрыты; compact Unsupported без generation fallback. Guessed enable flag, новый server API, provenance/persistence/restart04 не добавлены.
