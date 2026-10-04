# SSE Responses, этап 15

Статус: **Реализован, адресно проверен и принят координатором; локальный коммит разрешён**. Нормы: [agent-runtime](../../openspec/specs/agent-runtime/spec.md), [change](../../openspec/changes/responses-sse-adapter/proposal.md). [Команды/результаты/manifest](<../Plans/AgentBridge Initial Implementation/15-responses-sse-adapter.md>). OpenSpec CLI отсутствует, validation не выполнена, change не архивирован.

## Подключение и вызов

Используется существующий `AddCodexLbResponses`/resolver/`IModelGateway` из [этапа14](14-responses-json-adapter.md). Отсутствие callback сохраняет JSON; наличие callback передаёт `stream=true`, `store=false` на canonical POST `/v1/responses` через actual HttpClientLibrary0.0.0.5. Instructions/exact model/effort/input/tools/Parameters и binding continuation общие с JSON, без public API additions. Ни catalog lookup, ни composition/tools execution gateway не выполняет.

```csharp
ServiceResult<ModelResponse> result = await gateway.GenerateAsync(
    call, preparedRequest, access,
    async (update, token) =>
    {
        // Приложение обрабатывает предварительную delta либо полный item последовательно.
        await applicationConsumer(update, token);
    }, callerToken);
// result.Success означает получение отчёта; успех модели требует result.Data.Status == Completed.
```

`ModelStreamUpdate.TextDelta` выдаётся для `response.output_text.delta`; `Item` — полный snapshot `response.output_item.done`, включая unknown/opaque поля. Partial function arguments/reasoning/refusal не выдаются за готовые tools: сохраняются в итоговом output. Callback вызывается последовательно, awaited, без background task и после возврата запрещён. Его исключения, включая JsonException/HttpRequestFailedException/OCE, распространяются тем же объектом; consumer обязан соблюдать переданный cancellation token, gateway не отрывает его task ради timeout.

## Framing и состояние

SseEventReader использует строгий UTF-8, игнорирует только начальный BOM, поддерживает fragmentation включая отдельные байты Unicode, LF/CRLF/CR, comments и multiline `data:` с объединением через LF. Поля event учитываются, если JSON type отсутствует; id/retry не создают reconnect. Пустая строка завершает frame. Незакрытый frame на EOF отбрасывается, `[DONE]` сам по себе не terminal completion. Некорректные JSON/UTF-8/known event shape дают safe Rejected; после данных это Failed report с сохранённым output.

ResponseSseState сохраняет indexed added/done items и updates content/summary/function arguments. Unknown fields полных canonical объектов остаются целиком. Дельта с неизвестным index/другим item_id отклоняется без выдумывания item. Done value заменяет накопленную строку, полный done item заменяет partial snapshot. Output упорядочен по protocol output_index, не по очередности done arrival.

Непустой terminal `response.output` авторитетен и заменяет collected items целиком. При empty/absent output используются collected items, как в актуальном codex-lb collector. Raw canonical response envelope сохраняется отдельно без локального backfill/подмены. Например, он может содержать `output=[]`, тогда report.Output содержит items из item events. Полные usage/model/error/unknown fields остаются в envelope; envelope/continuation чувствительны и не являются UI/log DTO.

## Lifecycle, отмена и владение

`response.completed` подтверждает Completed только с response.status=completed, отсутствием error и output array либо collected items. Completed без response/без output и items остаётся Incomplete. `response.failed`/`error` и explicit response error дают Failed с safe typed error и исходным sensitive envelope. `response.incomplete`, unknown lifecycle и EOF без terminal дают Incomplete. Server cancelled сам по себе не является caller Canceled. Tools-only/reasoning/compaction/unknown output не требует видимого текста для completion.

Caller cancellation до данных распространяется OCE с исходным caller token; после canonical response/item/delta сохраняет данные в Canceled. Deadline до данных — ServiceResult.Fail(Timeout), после данных — ModelResponse.Failed с Timeout. Explicit typed failure имеет приоритет над поздней caller cancellation; caller приоритетнее deadline. Late cancellation на disposal после terminal сохраняет report, включая envelope/continuation. Unexpected I/O и неподтверждённая app-owned timeout cancellation распространяются без synthetic failure/retry.

HttpClientLibrary owns request/response; gateway освобождает `HttpStreamResponseResult` через await using на completion/error/EOF/cancellation/callback exception. StreamReader оставляет underlying stream wrapper-владельцу. Deadline/linked CTS принадлежат gateway, HttpClient/handlers — приложению. Нового reconnect/retry/key/model/account fallback нет. Continuation использует принятый `codex-lb-json-v1` binding: JSON/SSE взаимно совместимы; новый отсутствующий/непригодный id не возвращает старый anchor. Binding — trusted application data guard, не upstream account proof.

## Проверка и границы

ResponsesSseTests проходит public DI/resolver/gateway/actual HttpClientLibrary с fake handler и local fragmented streams. Финальный transport suite **191 passed / 0 failed / 0 skipped**, **55 новых SSE cases**; после точечного test task cleanup адресно **7/0/0**. Все builds0warnings/errors. JSON/core API, DB/providers/migrations и соседние библиотеки не менялись; core/DB suites не повторялись. Live HTTP/upstream/аккаунты/hosting — **Пропущено по указанию пользователя**. Fake pipeline не доказывает live compatibility. Composition16/tokenizer17/compact18/orchestration20 не реализованы; следующие этапы не начинались.
