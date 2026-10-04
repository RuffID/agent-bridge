# Прикладные контракты

Этап 13 расширяет Application независимыми портами доступа/каталога и безопасными model settings snapshots. [Фактический API и ограничения](13-model-catalog-and-keys.md). ModelAccess теперь выбирается resolver адаптера через индивидуальный источник приложения; generation/compact реализация IModelGateway ещё отсутствует.

Реализации существующих read/write ports, короткая transaction и правила внешнего ожидания: [этап 10](10-scenario-unit-of-work.md). Application не получил EF-зависимостей или публичного transaction callback.

## Фактический API этапа 07

Этап 07 реализован и принят; запрещённые проверки пропущены. Порты находятся в `AgentBridge.Application.Ports`, неизменяемые DTO — в `AgentBridge.Application.Models`, результаты — в `AgentBridge.Application.Results`. Production-зависимости не добавлены. Источник требований: [agent-runtime](../../openspec/specs/agent-runtime/spec.md); [контекст и ограничения](../../openspec/specs/agent-runtime/context.md).

| Порт | Обязанность |
| --- | --- |
| `IModelGateway` | Generate/Compact с фиксированными ModelRequest, ApplicationCallContext и ModelAccess |
| `IContextProvider` | Разрешённый приложением бизнес-контекст по ContextRequest |
| `IToolHandler` | Definition и выполнение ToolInvocation с проверкой прав/аргументов |
| `IContextTokenCounter` | Полный подготовленный запрос и раздельный известный/оценочный бюджет |
| `IDialogReader` | Защищённый снимок всей истории и активного окна, без read-only UoW |
| `IDialogCreator` | Короткое создание с фиксированным сроком и новой сохраняемой идентичностью жизни |
| `IDialogTurnWriter` | Begin/Append/Finish с token и атомарной проверкой актуальности |
| `IDialogContextWriter` | Принятие подтверждённого compact с сохранением истории |
| `IDialogDeletion` | Явное удаление владельцем вместе с зависимыми состояниями |
| `IExpiredDialogReader` | Ограниченная выборка кандидатов по сроку |
| `IExpiredDialogDeletion` | Удаление кандидата после повторной проверки incarnation/version/expiry |

JSON IModelGateway реализован этапом 14 через `AddCodexLbResponses`; SSE callback и CompactAsync явно Unsupported. Провайдеры контекста, handler registry, tokenizer и AgentRunner ещё не реализованы. Этап 09 добавил read ports; этап 10 реализовал write ports/UoW через `AddAgentBridgePersistence`. Это ещё не полный сценарий агента. [Фактический JSON transport](14-responses-json-adapter.md).

## Результаты и отмена

`ServiceResult` используется для команд без данных; `ServiceResult<T>` при Success имеет ненулевой Data и не содержит Error. Fail содержит семантический ServiceError без данных и HTTP-статуса. Неожиданные исключения не превращаются в ожидаемый отказ.

`ServiceResult<ModelResponse>.Ok` означает получение lifecycle-отчёта. Его Status отдельно различает Completed, Incomplete, Failed и Canceled. В отчёте сохраняется известный output даже при ошибке или отмене. Failed требует ServiceError; остальные состояния не содержат Error. Completed создаётся адаптером только после подтверждения завершения; сама фабрика не парсит terminal события. Непустой output или text delta не доказывают успешное завершение.

Переданный gateway callback возвращает ValueTask: шлюз ожидает его последовательно и не вызывает после возврата. Caller cancellation до получения отчёта распространяется OperationCanceledException с исходным токеном; при наличии отчёта возвращается Canceled. Deadline имеет отдельный смысл Timeout. Скрытого повторного вызова инструмента после неоднозначного сбоя контракт не разрешает.

## Снимки канонических данных

`CanonicalModelItem` хранит полный объект элемента; `CanonicalModelEnvelope` — весь объект результата, включая id/usage/discriminator/unknown fields. `ModelContinuation` сохраняет непрозрачные метаданные для того же диалога и upstream-владения. JsonElement клонируется, поэтому уничтожение исходного JsonDocument не повреждает снимок. Это чистый контейнер BCL, без wire DTO, HTTP serializer или SSE parser; mapping остаётся этапам 14–15.

`ModelRequest` копирует Input и Tools, содержит полные инструкции, выбранную модель/effort, continuation и optional независимый `ModelRequestParameters` snapshot этапа 14. Поддержку контролей и запрет override mandatory fields проверяет adapter; будущий tokenizer должен учитывать полный подготовленный запрос с Parameters. `ModelAccess` фиксирует уже выбранный ключ на конкретный вызов; resolver этапа 13 использует shared только при null. Секрет раскрывается только явным `RevealApiKey` для транспорта; ToString и обычная сериализация не раскрывают его. Сырые protocol/envelope/continuation/parameters чувствительны и не предназначены для логов.

`StoredDialogTurn.Items` содержит канонические элементы истории. `ModelSteps` отдельно связывает каждый StoredModelStep.StepId с полным ModelResponse. Envelope не помещается в список items для следующего input. `StoredDialogContext.Compaction` сохраняет полный compact-отчёт, а Items возвращает его каноническое окно. ThroughTurnSequence остаётся только terminal-prefix metadata; точная граница отдельных items и composition предстоят на этапах 14–18. Чтение не отбрасывает историю по этому числу.

## Короткие операции хранения

`DialogAccess` содержит проверяемого владельца, ID и явное UTC. `DialogWriteToken` содержит прочитанные сохраняемые DialogId/IncarnationId/Revision. Любая поздняя изменяющая операция проверяет существование, совпадение ID, владельца, фиксированный срок и incarnation/revision в одной атомарной границе с записью. Успех возвращает новую версию, отказ не меняет данные. Удаление инвалидирует старые tokens; запись не создаёт отсутствующий диалог заново. Системная очистка повторно проверяет срок и token кандидата.

Чтение до физического удаления позволяет владельцу получить дату и IsExpired по явному времени, но не разрешает продолжение. Явное удаление допускается и после expiry. Создание/изменение/удаление выполняются EF-адаптером через общий сценарный scope/UoW; сеть и инструменты завершаются вне транзакции. Порты не предоставляют глобальный UoW, EF query или делегат сетевой операции.

Пример использования write ports будущим оркестратором: оркестратор получает token после Begin, ждёт модель вне хранилища и вызывает Append/Finish с тем же incarnation/revision. Если диалог удалён, пересоздан или изменён, порт отказывает; замена старого token свежим ради записи устаревшего результата недопустима. Этап 08 добавляет DTO/колонки, полный payload и EF metadata; этап 09 — чтение и staging. Этап 10 реализует короткие write UoW и валидирующий Domain Restore; статус — реализован и принят; запрещённые проверки пропущены. Private lifetime доменного объекта не переносится в token и не выдаётся за persistence. [Формат принятого этапа 08](02-efcorelibrary.md#реализация-этапа-08).

`DialogReader` проверяет owner ordinal до загрузки истории и повторно проверяет root после неё, сравнивая зафиксированные primitive incarnation/revision/owner. NotFound/Forbidden/Conflict не содержат snapshot; скрытого retry нет. Все turns/items/steps читаются в сохранённом порядке, active compact выбирается по Version; prefix ничего не отбрасывает. Orphan children/повреждённый формат/незавершённый принятый compact отклоняются явно. Это защита read path, не транзакционный снимок. `ExpiredDialogReader` возвращает положительно ограниченные кандидаты по точному expiry/ID, включая равенство now. **71 persistence-тест, 37 новых**, без БД; [команды и ограничения этапа 09](<../Plans/AgentBridge Initial Implementation/09-base-repository-adapters.md>).

## Проверка

Изолированные проверки без инфраструктуры: [этап 07](<../Plans/AgentBridge Initial Implementation/07-application-ports.md>), 93 теста ядра, включая 27 новых. Round-trip и guards проверены однопоточными заглушками: это доказательство формы/заменяемости контрактов, не EF concurrency, restart или совместимость реального Responses транспорта.
