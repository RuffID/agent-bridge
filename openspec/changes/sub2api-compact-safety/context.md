# Контекст и пределы проверки

## Решение и границы

Выбран безопасный режим полного контекста. Sub2api доступен только для чтения. Наличие route, новый native protocol, model echo и сохранение локального provenance не разрешают активацию opaque-замены prefix. Новый transport/reader до устранения T05 не подключается и не создаётся как обход блокировки.

## Wire и T05

В текущих исходниках sub2api `backend/internal/server/routes/gateway.go` маршрутизирует `/v1/responses` и compact subpath. `normalizeOpenAIResponsesCompactRequest` в `handler/openai_gateway_handler.go` сохраняет native v2 на `/responses` с `stream:true` и compaction_trigger; `service/openai_compact_body_signal.go` нормализует trigger в единственную последнюю позицию. Legacy `/responses/compact` остаётся отдельным unary путём; stream bridge и keepalive могут вернуть HTTP 200 с terminal response.failed. Автоматического выбора legacy нет.

`service/openai_gateway_forward.go` после invalid_encrypted_content вызывает trimOpenAIEncryptedReasoningItems и повторяет запрос. `sanitizeEncryptedReasoningInputItem` в openai_gateway_request_body.go удаляет encrypted compaction/compaction_summary целиком. TestOpenAIGatewayService_Forward_HTTPRetryRecoveryDropsCompaction и TestTrimOpenAIEncryptedReasoningItems_Compaction подтверждают это намерение исходников; Go tests здесь не исполняются. Mapping/fallback и requested echo не подтверждают чтение окна.

Пример: turns1–10 заменены opaque item, turn11 остаётся хвостом. Recovery удаляет opaque item и получает успех только с turn11. Полная история в AgentBridge не возвращает turns1–10 в уже изменённый upstream input. Поэтому даже родные compact items не отправляются; неизвестный новый opaque input также проходит прежний provenance guard.

## Подтверждённые соседние этапы

Usage и images имеют собственные active changes: [usage](../sub2api-usage/tasks.md), [images](../sub2api-images/tasks.md). Их isolated transport результаты и ранее записанная generation/restart приёмка не доказывают compact compatibility. Завершённый sub2api-connector план остаётся историческим; его legacy-only проектирование не переносится как актуальный выбор протокола.

## Проверки

Actual Sub2Api gateway/ContextCompactor/AgentRunner проверяются с fake HTTP/local streams и in-memory writer. Дополнительные persistence cases используют actual reader/UoW над base repository doubles. Serialized restart означает новый DI над сериализованными committed записями; это не БД/restart сервера. Запрещённый dispatch исключает попадание compact-окна в разрушающий recovery; fixture не доказывает безопасность deployed pool. Compact terminal/parser/save conflict/success integration sub2api остаются за пределами выбранного режима, поскольку compact HTTP и writer не вызываются. Existing core/CodexLb и отдельные actual writer tests проверяют прежние инварианты отдельно. Фактические результаты — [tasks](tasks.md).

