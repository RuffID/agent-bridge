# Контекст этапа 19

Реализуется только dispatch tools, без цикла модель→инструмент→модель, EF или сети. Приложение программно регистрирует immutable definition, scoped handler и обязательный validator прав/полной схемы. Registry не объявляет авторизацию.

Сессия фиксирует owner/dialog/incarnation/turn/agent, разрешённый выбор имён, expiry и бюджет. StepId и исходная позиция output различают повторные call_id; завершённые пары не исполняются заново. Прерывание после начала handler имеет неизвестный исход и не получает выдуманного function_call_output. Срок — cooperative cancellation; executor ждёт и освобождает все начатые tasks/scopes, поэтому handler, игнорирующий cancellation, может превысить время.

Пользователь 2026-10-04 прямо согласовал: durable recovery в этапе 20. AgentRunner должен записать «начато» до внешнего handler и блокировать восстановленную незавершённую попытку. Создание новой session в 19 не является restart protection. Ни scoped DI, ни isolated tests не доказывают атомарность реального бизнес-хранилища.

CreateSession принимает optional IToolExecutionCheckpoint. BeforeExecuteAsync получает identity после app validator; handler начинается только после подтверждённого callback и повторной проверки времени/отмены. Порт не имеет default persistence implementation. В этапе20 callback должен создавать свой scope/UoW, сериализовать короткие записи одного диалога и передачу successful-write token, не делить DbContext, не держать transaction во время handler. При потере подтверждения записи — блокировать recovery, а не переписывать старый token свежим и повторять действие. Finish/report journaling и persistence canonical outputs также принадлежат20.

Пример: GetOrderStatus получает owner из ApplicationCallContext, validator проверяет аргументы и доступ к заказу, handler возвращает JSON. Canonical function_call_output с исходным call_id добавляется приложением через existing IDialogTurnWriter вместе с сохранённым call; actual ContextBuilder использует пару при следующем вопросе. Envelope/continuation остаются отдельно.
