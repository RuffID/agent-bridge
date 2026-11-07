# Ограничения existing sub2api и клиентского контракта

Дата: 2026-11-07. Статус03: **blocked T05**. [Этап](main.md) · [Результаты](results.md) · [Клиентская адаптация](client-adaptation.md).

## Действующее решение

sub2api не дорабатывается. [Серверное предложение](server-contract-proposal.md) отклонено и сохранено только как отвергнутый вариант. Новые headers/metadata/pool revision/single-attempt API не являются зависимостью. Запрет client retries не распространяется автоматически на server retries; существенны конкретные изменения данных, а не само число внутренних попыток.

Пользователь разрешил отдельный клиентский режим неизвестного входного окна. Он реализован как Compaction.BudgetPolicy=ServerValidationWithUnknownWindow: manifest объявляет exact ID/effort/API-доступность, InputContextWindow остаётся null, budget guard делегирует приём серверу, threshold остаётся локальным ориентиром. Прежние strict/ServerValidation и default codex-lb сохраняются. Изменение v1/v2 snapshot бюджета разрешено отдельно; provenance/restart04 не реализованы.

T01 остаётся открытым: честного публичного доказательства input budget нет. Это больше не препятствие settings при явном новом режиме. context_window/95%/локальный порог/похожее поле input_context_window не принимаются за доказанное окно. Manifest metadata не выдаётся за provenance generation pool.

## Граница canonical и реализованная generation

Shared Forward выполняет normalizeOpenAIResponsesReasoningContentReplay до выбора ordinary/passthrough. Непустой reasoning.content удаляется в обеих ветках. Ordinary HTTP400 invalid_encrypted_content recovery может удалить encrypted reasoning, выполнить внутреннюю повторную отправку и вернуть успех. Handler при passthrough→ordinary failover также удаляет encrypted reasoning items. Один исходящий HTTP AgentBridge не исключает этих потерь, а успешный response.model может быть requested echo.

Принята граница canonical на входе sub2api и в локальной истории AgentBridge; перечисленные server transformations допустимы без обещания upstream сохранности. Generation реализована через existing bundle/HttpClientLibrary: один POST /v1/responses, shared writer с полным request и null previousResponseId, JSON/SSE по фактической media type. Incoming continuation отклоняется до HTTP factory, returned response id/turn-state не используются как anchor. Полный output/envelope сохраняется независимо от requested echo. Согласование серверной доработки не требуется и не предлагается.

Transport normalization OAuth stream/store и SSE→JSON сама по себе не означает потери данных. Tool/ID/namespace преобразования с восстановлением оцениваются по конкретным парам; корректная полная call/output pair не является orphan. В текущем source truncateOpenAIResponsesInputText и openAIResponsesInputMayNeedTruncation — no-op/false: прежнее предположение об обрезании текста уточнено. Non-stream может вернуть SSE при отсутствии terminal; такой ответ нельзя считать Completed.

## Compact03.03

POST /v1/responses/compact и whitelist body существуют. Ordinary/passthrough могут применить compact-only/global model mapping уже до первого send, затем fallback. Response.model может быть восстановлен в requested имя. Совместимость opaque compact с выбранной моделью не установлена; независимо проверенная actual identity не выдумывается.

CompactAsync сохраняет Unsupported и не заменяется generation. Existing ContextCompactor/IDialogContextWriter сохраняют Completed-only/save-before-activation/history. Новый budget режим проверен через existing core compactor со stub gateway; это не actual Sub2Api compact acceptance.

## Доказательства и оставшиеся границы

[Разбор и пример противоречия](client-adaptation.md) содержит владеющие source links, различие транспортных изменений и потерь. [Опись](contract-source-evidence.json) фиксирует46 прежних и2 дополнительных source files. Все46 прежних hashes совпали; это не версия deployment или live OAuth proof. Stage01/02 отчёты и main spec не переписываются.

T02 live Sub2Api OAuth/tools, T03 provenance04, T04 delivery/ABI и T05 compact не закрыты. Generation включена после принятия границы canonical и реализации actual lifecycle; fixtures не выдают upstream compatibility/identity за доказанный факт.151 Sub2Api и414 CodexLb isolated tests прошли; новые tests не являются live-приёмкой. Работа остановлена в03,04 не начат.
