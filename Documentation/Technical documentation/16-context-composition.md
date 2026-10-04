# Композиция контекста

Этап16 принят и закоммичен085779a15c4126ac567a6d1307f497dcae8dc9c5. Нормативный источник: [agent-runtime](../../openspec/specs/agent-runtime/spec.md), [context](../../openspec/specs/agent-runtime/context.md). [Offline tokenizer и budget guard17](07-tokenizer-and-settings.md#public-guard-и-подключение) приняты и закоммичены af72252c5d5638137738957c200492923a425da8. [ContextCompactor18](18-context-compaction.md) фиксирует один результат builder и отдельно сжимает terminal history; tools/orchestration остаются последующим этапам. Проверки ниже — исторический отчёт16.

## Публичный API

`AgentBridge.Application.ContextBuilder(IEnumerable<IContextProvider> providers)` фиксирует выбранные приложением источники в заданном порядке. `BuildAsync(ApplicationCallContext call, DialogSnapshot dialog, ModelRequest newRequest, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)` возвращает `ServiceResult<ModelRequest>`.

Приложение заранее получает актуальный snapshot через `IDialogReader` и выбирает только разрешённые провайдеры для actual владельца/агента. Оно управляет их DI scope и авторизацией; builder не владеет ими, не выбирает источники по имени и не делает fan-out. Его можно создать из явно выбранной коллекции в composition root приложения; новых options, registry или DI-регистрации всей коллекции по умолчанию нет.

`newRequest.Input` — **только ещё не сохранённые items** очередного шага. Уже сохранённое новое сообщение берётся из `StoredDialogTurn.Items`, повторно передавать его в newRequest не нужно. Сборщик не угадывает дубликаты по JSON, тексту или call_id. Инструкции, exact model/effort, tools, parameters и явно переданный continuation фиксируются этим входным ModelRequest. Builder не выбирает модель, не получает ключ и не проверяет транспортную поддержку контролей.

## Порядок и протокол

Полные инструкции остаются в отдельном системном поле `Instructions`. Порядок `Input`:

1. Items провайдеров в порядке их последовательного вызова и исходном порядке каждого вклада.
2. Items активного StoredDialogContext, если окно принято.
3. Все Items обращений с Sequence > ThroughTurnSequence в сохранённом порядке; без окна включаются все обращения.
4. Ещё не сохранённый newRequest.Input.

Пользователь разрешил любые canonical items провайдера с исходными ролями. Авторизация принадлежит провайдеру; builder не повышает бизнес-данные до системной роли и не переписывает уже заданную роль. Провайдер получает actual ApplicationCallContext, включая AgentId/TurnId/owner/dialog, и только новый input, без автоматического раскрытия ему истории.

Items — единственный источник элементов сохранённой истории. `StoredModelStep.Response.Output` повторно не добавляется, даже если содержит те же items. Envelope/continuation compact и шагов остаются metadata; они не становятся input и не заменяют явно переданный newRequest.Continuation. Транспорт отдельно проверяет привязку continuation к owner/dialog/agent/key/endpoint. Snapshot не хранит AgentId: builder не вводит persisted agent ownership или новую схему.

Unknown/opaque поля и исходные canonical объекты не нормализуются и не сворачиваются в текст. Known function_call/function_call_output проверяются по call_id после полного объединения: output закрывает один предшествующий незакрытый call с тем же точным ID. Повторное использование ID в разных парах допустимо. Пара может проходить через границы провайдера, окна, хвоста и нового input. Скрытые вызовы внутри opaque/unknown state не проверяются, arguments/output не разбираются и не исправляются.

Если известный call не имеет результата, включая partial arguments после SSE обрыва, builder возвращает безопасный `Conflict` **без ModelRequest**. Некорректный call_id или output без предшествующего незакрытого call — `Validation`. История и lifecycle отчёт сохраняются целиком, фиктивного output и fallback нет.

## Доступ, prefix и отмена

До провайдеров проверяются owner (`Forbidden`), dialog ID из token (`Conflict`), фиксированный срок (`Expired` при nowUtc >= ExpiresAtUtc), непрерывный порядок/идентичности turns и корректность принятого окна (`Conflict`). ThroughTurnSequence может быть 0 и не превышает всю историю; покрытые turns должны иметь конечный статус. InProgress не покрывается и остаётся целиком в хвосте. Failed/Incomplete/Canceled являются terminal statuses prefix, но не успешным ходом модели. Compact в ActiveContext должен быть Completed. Повреждённый порядок не сортируется и не исправляется.

Время передаётся явно в UTC; builder не читает часы и не продлевает срок. Snapshot/token и успешная подготовка **не являются write authorization или транзакционным снимком**. После внешнего ожидания write port отдельно проверяет исходные incarnation/revision, владельца, срок и существование; получить свежий token ради принятия устаревшего результата нельзя.

Каждый provider Task ожидается до следующего, с исходным caller token. Отмена проверяется перед вызовами, после успешного вклада и при обходе истории/items. Typed provider failure передаётся тем же ServiceError без запроса, даже при поздней отмене; неожиданные exceptions распространяются неизменными. Builder не отсоединяет tasks, не запускает таймер, внешний I/O или автоматические retries. Провайдер обязан соблюдать token; отдельный timeout/оркестратор здесь не вводится. Общий EF scope внутри providers нельзя использовать параллельно.

## Пример

```csharp
using AgentBridge.Application;
using AgentBridge.Application.Models;
using AgentBridge.Application.Ports;
using AgentBridge.Application.Results;

// call, nowUtc, selectedProviders, instructions и newInput задаёт приложение.
ServiceResult<DialogSnapshot> read = await dialogReader.ReadAsync(
    new DialogAccess(call.DialogId, call.OwnerId, nowUtc), cancellationToken);
if (!read.Success)
{
    return ServiceResult<ModelRequest>.Fail(read.Error
        ?? throw new InvalidOperationException("Отказ чтения должен содержать ошибку."));
}

ContextBuilder builder = new(selectedProviders);
ModelRequest newRequest = new(model, effort, instructions, newInput, allowedTools,
    continuation: continuation, parameters: parameters);
ServiceResult<ModelRequest> prepared = await builder.BuildAsync(call,
    read.Data ?? throw new InvalidOperationException("Успех чтения должен содержать snapshot."),
    newRequest, nowUtc, cancellationToken);
// Только prepared.Success передаёт полный запрос будущему count/gateway.
```

Окно покрывает turns1–2, turn3 содержит call и output. Подготовка включает разрешённые вклады, opaque окно, все items turn3 и новое сообщение. При prefix0 все turns сохраняются в хвосте. Call в окне и output в newInput образуют допустимую пару; незакрытый call текущего turn даёт Conflict, сохраняя partial state для приложения.

## Проверки и ограничения

59 новых composition cases плюс 27 затронутых Application contract cases: **86 passed / 0 failed / 0 skipped**. Core/test compile-check — **0 warnings/errors**. Public ContextBuilder проверен с настоящими canonical DTO и fake IContextProvider; нет модели/tokenizer/HTTP/DB/сервера/hosting. Гарантия persistence/restart/transaction snapshot этими тестами не доказывается. Точные команды и полный manifest: [этап16](<../Plans/AgentBridge Initial Implementation/16-context-composition.md>).

HTTP/DB integration и остальные suites не повторены: их код/схемы/библиотеки не изменены. OpenSpec CLI отсутствует, CLI validation не выполнена, [change](../../openspec/changes/context-composition/proposal.md) не архивирован. На checkpoint16 следующие этапы не начинались; текущая реализация17–25 завершена в документированных границах. [Карта evidence](<../Plans/AgentBridge Initial Implementation/25-usage-guide-and-closure.md>).
