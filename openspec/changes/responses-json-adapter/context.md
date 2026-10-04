# Контекст JSON адаптера

## Назначение и границы

Этап 14 реализует только JSON генерацию существующего IModelGateway. SSE 15, composition 16, tokenizer 17, compact 18 и tools 19 отсутствуют. HTTP выполняется actual HttpClientLibrary 0.0.0.5; app owns HttpClient, handlers/logging pipeline. Base prefix сохраняется, endpoint canonical /v1/responses.

## Сохранение протокола

Например, input содержит message, function_call_output и reasoning с encrypted_content; output может состоять только из function_call и opaque reasoning. Они копируются полностью и в исходном порядке. Envelope с id/usage/model/unknown fields хранится отдельно от следующего input. 2xx без completed не доказывает completion.

## Согласованное расширение

Пользователь принял independent parameter snapshot и bound continuation 2026-10-04. Обязательные model/instructions/input/tools/stream/store не допускают override параметрами. Continuation хранит adapter version, hash binding и canonical anchor/header; неизвестные metadata сохраняются. Binding связывает локальный контекст и ключ/endpoint; это защита от случайного смешивания, а не доказательство серверного account ownership или защита от подделки доверенным приложением.

## Ошибки и ограничения

Late caller cancellation после получения полного JSON сохраняет report как Canceled; до отчёта распространяется OCE с исходным token. Explicit typed failure не подменяется поздней отменой. Детерминированный fake stream отменяет caller на disposal после deserialization, доказывая сохранение данных на public pipeline. Новый absent/непригодный id удаляет прежний anchor; raw новый id сохраняется в envelope. Continued metadata/header могут оставаться без previous_response_id, что явно означает отсутствие нового response anchor.

Публичные ошибки содержат semantic ServiceError и безопасные транспортные status/type/code/param адаптера. Только закрытые известные значения проходят из complete error JSON; неизвестное поле опускается. Не разбирать snippet/truncated prefix и не выводить raw exception. GenerationTimeout действует на отправку и чтение; caller cancellation приоритетнее локального deadline. Никаких blind retries либо key/model/account fallback.

## Источники и проверка

Сверены актуальные app/core/openai/v1_requests.py, requests.py, models.py и app/modules/proxy/api.py/affinity.py соседнего codex-lb; инструкции соседей прочитаны, исходники только read-only. Проверки проходят public DI/gateway через HttpClientLibrary к fake handler, без real HTTP/upstream. Нормы: [delta spec](specs/agent-runtime/spec.md). Change не архивировать без CLI validation.

Финально136 CodexLb tests (75 новых) и121 core tests прошли, 0 failed/skipped; builds0warnings/errors. Первая сборка выявила reference-type constraint ServiceResult<T>, первый test build — warning fake handler, усиленный I/O fixture сначала имел Content-Length=0; всё исправлено, первоначальные неуспешные попытки записаны в плане. 33 files strict UTF-8/LF,206 local links и git diff --check проверены. CLI отсутствует, validation не выполнена; stage14 принят координатором по коду/документам/actual TRX, локальный коммит33 файлов разрешён. CLI limitation принята как невыполненная проверка, change не архивирован. Stage15–25 не начаты, checkpoint18 не достигнут.
