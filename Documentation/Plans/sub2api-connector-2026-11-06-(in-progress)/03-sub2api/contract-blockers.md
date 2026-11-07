# Ограничения existing sub2api и клиентского контракта

Дата: 2026-11-07. Статус03: **verification в объёме первой версии без compact**; отложенный compact — blocked T05. [Этап](main.md) · [Результаты](results.md) · [Клиентская адаптация](client-adaptation.md).

## Действующее решение

После реализации03 отдельно выполнен [04.01](../04-dialog-state/04.01-connector-provenance.md): локальное происхождение и один новый формат отчётов без чтения старых диалогов. Статус verification, compile-check пройден, тесты не запускались; [результаты04](../04-dialog-state/results.md). Это не новое свидетельство совместимости compact. Указания ниже о не начатом04 описывают прежний проход03;04.02 и05 по-прежнему не начаты.

Compact вынесен из обязательного объёма первой версии в отдельную отложенную работу03.03.02–03.03.06. T05 открыт и блокирует её включение, но не приёмку первой версии. Рабочий режим — полный контекст через отдельный guard, с явной ошибкой при превышении бюджета и Unsupported ручного/force compact до factory/HTTP. История сохраняется; продолжение сколь угодно длинного диалога не гарантируется. Остальные обязательные проверки этим не закрыты.

sub2api не дорабатывается. [Серверное предложение](server-contract-proposal.md) отклонено и сохранено только как отвергнутый вариант. Новые headers/metadata/pool revision/single-attempt API не являются зависимостью. Запрет client retries не распространяется автоматически на server retries; существенны конкретные изменения данных, а не само число внутренних попыток.

Пользователь разрешил отдельный клиентский режим неизвестного входного окна. Он реализован как Compaction.BudgetPolicy=ServerValidationWithUnknownWindow: manifest объявляет exact ID/effort/API-доступность, InputContextWindow остаётся null, budget guard делегирует приём серверу, threshold остаётся локальным ориентиром. Прежние strict/ServerValidation и default codex-lb сохраняются. Изменение v1/v2 snapshot бюджета разрешено отдельно; provenance/restart04 не реализованы.

T01 остаётся открытым: честного публичного доказательства input budget нет. Это больше не препятствие settings при явном новом режиме. context_window/95%/локальный порог/похожее поле input_context_window не принимаются за доказанное окно. Manifest metadata не выдаётся за provenance generation pool.

## Граница canonical и реализованная generation

Shared Forward выполняет normalizeOpenAIResponsesReasoningContentReplay до выбора ordinary/passthrough. Непустой reasoning.content удаляется в обеих ветках. Ordinary HTTP400 invalid_encrypted_content recovery может удалить encrypted reasoning, выполнить внутреннюю повторную отправку и вернуть успех. Handler при passthrough→ordinary failover также удаляет encrypted reasoning items. Один исходящий HTTP AgentBridge не исключает этих потерь, а успешный response.model может быть requested echo.

Принята граница canonical на входе sub2api и в локальной истории AgentBridge; перечисленные server transformations допустимы без обещания upstream сохранности. Generation реализована через existing bundle/HttpClientLibrary: один POST /v1/responses, shared writer с полным request и null previousResponseId, JSON/SSE по фактической media type. Incoming continuation отклоняется до HTTP factory, returned response id/turn-state не используются как anchor. Полный output/envelope сохраняется независимо от requested echo. Согласование серверной доработки не требуется и не предлагается.

Transport normalization OAuth stream/store и SSE→JSON сама по себе не означает потери данных. Tool/ID/namespace преобразования с восстановлением оцениваются по конкретным парам; корректная полная call/output pair не является orphan. В текущем source truncateOpenAIResponsesInputText и openAIResponsesInputMayNeedTruncation — no-op/false: прежнее предположение об обрезании текста уточнено. Non-stream может вернуть SSE при отсутствии terminal; такой ответ нельзя считать Completed.

## Compact03.03

POST /v1/responses/compact и whitelist body существуют. Ordinary/passthrough могут применить compact-only/global model mapping уже до первого send, затем fallback. Это возможность разных путей, **не доказанная несовместимость разных моделей**. Response.model может быть requested echo; independently verified actual identity не является самостоятельным условием, если пригодность compact для generation подтверждается существующим контрактом.

Конкретное ограничение — existing HTTP invalid_encrypted_content recovery удаляет **весь** compaction/compaction_summary item с encrypted_content и может затем вернуть успех. После сохранённой замены terminal prefix следующий input уже не включает покрытые turns. При удалении единственного opaque окна модель получает только хвост, хотя полная история сохранна. [Сценарий, source links и условия включения](03.03-compaction.md) отделяют доказанное поведение recovery от неподтверждённой совместимости конкретного pool. Принятый план Б про reasoning не подтверждает пригодность такого compact.

Выбран безопасный режим без compact: Sub2Api AutomaticCompactionEnabled=false; ContextCompactor возвращает Disabled без dispatch/save, полный PreparedRequest проходит separate guard. Threshold не становится ранним Unsupported; known budget и ошибки counter/guard сохраняются. Ручной/force CompactAsync возвращает Unsupported до factory/HTTP. Нет text summary, hidden generation fallback или клиентского retry. Existing Completed-only/save-before-activation/history не ослабляются. Проверки actual runner/adapter с doubles подтверждают этот режим, не совместимость opaque upstream.

[03.03.02](03.03.02-window-compatibility.md) завершил статический анализ без основания допуска: compact capability относится только к отдельному запросу, HTTP ingress принудительно остаётся http_sse, sticky допускает смену аккаунта. Даже без смены и mapping ordinary recovery не имеет отдельного исключения для OAuth/compact-window input и может успешно завершиться после удаления окна. Passthrough не выполняет именно этот recovery, но нет свидетельства исключительного passthrough для рассматриваемого deployment/pool; возможен переход в ordinary. Cross-mode sanitizer удаляет только reasoning, а не compaction. Источники доказывают условный error path; несовместимость deployment не установлена. Минимальная клиентская мера уже выполнена в03.03.01.03.03.02 остаётся blocked T05 для отложенного compact;03.03.03–03.03.06 и04 не начаты.

## Доказательства и оставшиеся границы

[Разбор](client-adaptation.md) содержит владеющие source links, различие транспортных изменений и потерь. [Опись](contract-source-evidence.json) сохраняет исторические48 entries0.2.19 и отдельный compactReview для current local0.2.20:38 hashes совпали,10 отличаются; source01 также имеет8 отличий из28 sub2api entries. Current sanitizer/recovery/mapping и два regression sources перечитаны; удаление compact при invalid_encrypted_content сохранилось. Совпадение исторических hashes больше не заявляется как текущий факт. Это не версия deployment или live OAuth proof. Stage01/02 отчёты и main spec не переписываются.

T02 live Sub2Api OAuth/tools, T03 provenance04, T04 delivery/ABI и T05 compact не закрыты. Generation реализована по принятой границе canonical; fixtures не выдают upstream compatibility/identity за доказанный факт. Последний итог557 Core +161 Sub2Api +414 CodexLb =1132 isolated cases, failed/skipped0, не live-приёмка. Режим без автоматического compact проверен;03 — verification в объёме первой версии, T05 вынесен в отложенную работу.04 не начат; согласование объёма не разрешает реализацию следующих этапов.
