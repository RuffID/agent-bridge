# HttpClientLibrary и контракты codex-lb

## HTTP-основа

Все исходящие HTTP-запросы адаптера выполняются через HttpClientLibrary из `D:\Media\User\source\repos\work\HttpClientLibrary`.

На этапе 00 исходный [проект библиотеки](../../../work/HttpClientLibrary/HttpClientLibrary.csproj) имел FileVersion 0.0.0.4. На согласованном этапе 04 реализована версия 0.0.0.5 с прежними `net8.0;net10.0` и Logging Abstractions `10.0.2`. Сборки и изолированные тесты обоих TFM проверены, FileVersion/TFM/MVID/hash build DLL сверены с тестовыми копиями. Этап 13 подключает локальный ProjectReference и actual HttpApiClient для `/v1/models`; Responses/SSE/compact ещё не реализованы. Точные результаты библиотеки: [этап 04](<../Plans/AgentBridge Initial Implementation/04-httpclientlibrary-logging.md>), [API каталога](13-model-catalog-and-keys.md).

| Реальный тип | Использование |
| --- | --- |
| `IHttpApiClient` / `HttpApiClient` | Общий HTTP pipeline |
| `HttpRequestOptions` | Метод, URL, заголовки и тело запроса |
| `SendWithResponseAsync<T>` / `HttpResponseResult<T>` | JSON вместе со статусом и заголовками |
| `SendStreamAsync` / `HttpStreamResponseResult` | Поток SSE вместе со статусом и заголовками |
| `HttpRequestFailedException` / `HttpErrorResponseDetails` | HTTP-статус, безопасный Message, raw снимок error headers и ограниченного тела с явной полнотой |

SSE-парсер относится к адаптеру codex-lb: общий HTTP-клиент предоставляет поток, а адаптер понимает события Responses. Потоковый результат освобождается через `Dispose`/`DisposeAsync`. Если используется `ContentFactory`, каждый её вызов создаёт новый HttpContent; владение передаётся библиотеке.

### Фактическое поведение HTTP-обёрток

| Источник | Подтверждённый контракт |
| --- | --- |
| [HttpJsonResponseClient.SendWithResponseAsync / ReadBodyAsync](../../../work/HttpClientLibrary/Clients/HttpJsonResponseClient.cs) | `ResponseHeadersRead`, затем чтение JSON с cancellation; 204/205 или `Content-Length: 0` дают пустое тело. Для `T=string` возвращается текст. Request/response освобождаются внутри вызова. |
| [HttpStreamingResponseClient.SendStreamAsync](../../../work/HttpClientLibrary/Clients/HttpStreamingResponseClient.cs) | `ResponseHeadersRead`, проверка статуса до выдачи потока; при ошибке освобождает request/response, при успехе response принадлежит возвращённой обёртке. Cancellation дальнейшего чтения передаёт потребитель потока. |
| [HttpResponseMetadataReader.ExtractHeaders](../../../work/HttpClientLibrary/Clients/HttpResponseMetadataReader.cs) | Успешные JSON/stream-обёртки содержат response и content headers в словаре без учёта регистра. |
| [HttpRequestMessageFactory.Create](../../../work/HttpClientLibrary/Clients/HttpRequestMessageFactory.cs) | Пустой URL, Body вместе с ContentFactory, BodyContentType вместе с ContentFactory, HttpContent в Body и тело GET/HEAD отклоняются. Строковый Body по умолчанию `text/plain`, объект — JSON. |
| [Serializer](../../../work/HttpClientLibrary/Clients/Serializer.cs) | По умолчанию camelCase и пропуск null. Для snake_case Responses нужны явные имена JSON-полей либо переданные настройки; автоматически snake_case не возникает. |

Повторов и собственного deadline в этих реализациях нет: они используют переданный HttpClient и cancellation. Настройки и дополнительные handlers подключающего приложения этой статической проверкой не проверялись.

## Проверка текущего codex-lb

Контракты сверены с локальными исходниками 2026-10-03. Проверка статическая; работа живого сервера и конкретного upstream не проверялась.

| Публичный маршрут | Контракт |
| --- | --- |
| `POST /v1/responses` | Генерация; JSON при `stream=false` или SSE при `stream=true` |
| `POST /v1/responses/compact` | Сжатие; итоговый JSON с каноническим новым окном контекста |
| `GET /v1/models` | Актуальный каталог доступных моделей |

Точки входа: [v1_responses](../../../codex-lb/app/modules/proxy/api.py#L1350), [v1_responses_compact](../../../codex-lb/app/modules/proxy/api.py#L6981), [v1_models](../../../codex-lb/app/modules/proxy/api.py#L1712). `/v1/responses/` зарегистрирован отдельно как эквивалент без redirect; для models и compact в этом роутере отдельной регистрации завершающего `/` нет. Адаптеру достаточно канонических адресов таблицы; поведение redirect живого сервера не проверялось.

Для авторизации используется Bearer API-ключ codex-lb, предоставленный приложением. Ключ не является upstream-ключом OpenAI и не помещается в payload модели.

Responses принимает `model`, `input`, `instructions`, `tools`, `tool_choice`, настройки reasoning и другие поддержанные поля. `input` содержит сообщения и элементы протокола, включая результаты инструментов и сохраняемое состояние. Список только текстовых сообщений недостаточен для общего агента.

Текущий код codex-lb нормализует `store` в `false`. Диалоги AgentBridge сохраняются в собственной БД. `conversation` и `previous_response_id` нельзя указывать одновременно; upstream-продолжение не заменяет локальную историю.

Каталог моделей следует читать через `/v1/models`, а не переносить фиксированный список из старого бота.

### Карта wire-контрактов

| Область | Подтверждение в текущем исходном коде |
| --- | --- |
| Запрос Responses | [V1ResponsesRequest](../../../codex-lb/app/core/openai/v1_requests.py#L37): непустой `model`; `input` — строка или массив либо альтернативный `messages`. Оба не допускаются, кроме `messages` с пустым `input=[]`. Дополнительные JSON-поля разрешены; это не обещает их поддержку каждым upstream. `stream=true` выбирает SSE, false/отсутствие — JSON. |
| Элементы и функции | [ResponsesRequest](../../../codex-lb/app/core/openai/requests.py#L643) принимает структурированный input и tools; [enforce_strict_function_tools_format](../../../codex-lb/app/modules/proxy/request_policy.py#L966) проверяет плоский Responses tool `{type:"function", name, parameters, strict}`. Для `strict=true` проверяется schema; неверная schema даёт `invalid_function_parameters`. Вызовы/результаты функций и их `call_id` нельзя заменить текстовыми сообщениями. |
| Контроли | [ResponsesReasoning](../../../codex-lb/app/core/openai/requests.py#L614): `effort`, `summary` и дополнительные поля. `include` проверяется по allowlist; `reasoning.encrypted_content` входит в него. `store` принудительно false. `truncation=auto/disabled` проходит валидацию и удаляется из ChatGPT-bound payload; другие значения отклоняются. |
| JSON-результат | [OpenAIResponsePayload / ResponseUsage](../../../codex-lb/app/core/openai/models.py): id, status, error, usage и дополнительные поля, включая output. usage содержит input/output/total tokens и details. [Коллектор](../../../codex-lb/app/modules/proxy/api.py#L8895) различает completed/incomplete/failed/error; HTTP 2xx не заменяет проверку status. |
| Каталог | [ModelListResponse / ModelListItem / ModelMetadata](../../../codex-lb/app/modules/proxy/schemas.py#L202): `{object:"list", data:[...]}`, элементы id/object/created/owned_by, metadata с context_window, input_context_window, max_output_tokens, supported_reasoning_levels (`effort`, `description`), default_reasoning_level и флагами возможностей. [Сборка каталога](../../../codex-lb/app/modules/proxy/api.py#L4013) объединяет registry/fallback и enabled model sources, фильтрует по аутентифицированному ключу. При client_version маршрут возвращает другую Codex-native форму; AgentBridge query не добавляет. Пустой data допустим; поле created формируется при запросе и не является версией модели. |

В [context.md codex-lb](../../../codex-lb/openspec/specs/responses-api-compat/context.md) ещё есть утверждения об отклонении `store=true` и любого `truncation`. Они расходятся с текущими валидаторами; для truncation актуальное поведение дополнительно подтверждено [нормативной спецификацией](../../../codex-lb/openspec/specs/responses-api-compat/spec.md#L3233). Это наблюдение сверки, не изменение требований или файлов шлюза.

## Compact

`/v1/responses/compact` сохраняет raw upstream-форму нового окна контекста. Выход может содержать сообщения и непрозрачные compaction-элементы, включая `encrypted_content`. Результат сохраняется как состояние протокола, не только как извлечённый текст.

codex-lb самостоятельно выбирает поддержанный upstream-механизм compact. AgentBridge использует публичный endpoint и не дублирует внутреннюю маршрутизацию proxy. Недоступность upstream-compact возвращается как явная ошибка; подмена обычным запросом генерации с инструкцией «сожми» не выполняется незаметно.

Точный запрос описан [V1ResponsesCompactRequest](../../../codex-lb/app/core/openai/v1_requests.py#L131): model, input/messages, instructions, reasoning и дополнительные поля. [CompactResponsePayload](../../../codex-lb/app/core/openai/models.py#L132) требует непустой discriminator `object`, начинающийся с `response.compact`, и сохраняет дополнительные поля. В [upstream-нормализаторе](../../../codex-lb/app/core/clients/proxy.py#L1655) уже compact-shaped JSON сохраняется; обычный Responses-результат может преобразовываться в `response.compaction` с compaction item. Поэтому «raw» здесь означает сохранение полученного канонического JSON адаптером AgentBridge, а не побайтовое отсутствие преобразований внутри шлюза.

[Текущий compact transport](../../../codex-lb/app/core/clients/proxy.py#L4874) добавляет terminal `compaction_trigger`, устанавливает `stream=true/store=false`; [SSE-коллектор](../../../codex-lb/app/core/clients/proxy.py#L1579) требует completed, собирает output items и отклоняет failed/incomplete/error/EOF. Публичный `/v1/responses/compact` возвращает JSON и не выполняет дополнительную Codex-affinity нормализацию до одного item, применяемую к backend-маршруту.

Ограничение каталога: `_build_models_response_body` включает model sources, а `_compact_responses` идёт по subscription-only пути. Наличие модели в `/v1/models` само по себе не подтверждает её compact-возможность; отдельного compact-флага в ModelMetadata нет. Работа конкретной модели с compact требует последующей проверки, без скрытого выбора другой модели.

## Streaming, завершение и ошибки

HTTP 2xx или полученный text delta сами по себе не доказывают завершение обращения. Парсер различает terminal lifecycle, `error`, `response.failed` и незавершённый результат. EOF без подтверждённого завершения не сохраняется как успешный ответ.

Следует сохранять полный необходимый `output`: сообщения, вызовы инструментов, их идентичность и непрозрачные элементы reasoning/compaction. Неизвестные поддержанные поля не должны теряться из-за DTO, рассчитанного только на текст.

Статус, type/code ошибки и безопасный correlation ID преобразуются в контракт адаптера. Caller cancellation передаётся без подмены локальным таймаутом. Возвращаемые continuity-заголовки и `previous_response_id` рассматриваются как состояние, связанное с диалогом и upstream-владением, а не как глобальные настройки клиента.

Не выполняется автоматический повтор операции после неоднозначного disconnect или полученного результата инструмента. Безопасность повторной отправки должна подтверждаться машинным контрактом, а не текстом ошибки.

## Принятое логирование

Пользователь согласовал default-safe и только opt-in `JsonStructure`. HttpClientLoggingOptions.ErrorContentMode по умолчанию None. Структурная сводка не является текстом содержимого; исходные имена полей и любые значения исключены. SanitizedText и sanitizer не реализованы.

Прежний `HttpApiClient(HttpClient, ILogger<HttpApiClient>, JsonSerializerOptions? = null)` сохранён. Новые перегрузки добавляют обязательные четвёртый HttpClientLoggingOptions и пятый HttpErrorResponseOptions; третий null однозначен. HttpRequestOptions.CorrelationId принимает необязательный непустой GUID, не отправляемый серверу.

Событие 5100/HttpResponseReceived содержит только Method из закрытого набора (либо Other), числовой StatusCode, сгенерированный RequestId, CorrelationId, ResponseKind и ElapsedToHeadersMs. 2xx — Information, остальные статусы — Warning. Это получение заголовков, не успех JSON/SSE. URL/userinfo/path/query, reason, headers, body, snippet и Exception не передаются logger. HttpRequestFailedException.Message теперь только `HTTP <status>.`; raw Url/Reason/ResponseSnippet сохранены для совместимости и не предназначены для логирования.

При JsonStructure дополнительное событие 5101/HttpErrorContentSummary содержит RequestId, фиксированный ContentState, RootKind и счётчики объектов/массивов/свойств/скаляров. JSON разбирается лишь при Complete; невалидный/неполный ответ даёт фиксированный признак пропуска. Успешный SSE не читается ради диагностики. Приложение отвечает за свои scopes/enrichers, handlers и логирование raw результатов.

## Транспортный контракт HTTP-ошибки этапа 04

Отдельно согласован и реализован ErrorResponse в HttpRequestFailedException: независимый case-insensitive снимок response/content headers с неизменяемыми значениями, BodyText и BodyState. Старый четырёхпараметрический конструктор исключения даёт ErrorResponse=null. Совпадающие имена заголовков сохраняют обе группы значений.

HttpErrorResponseOptions.MaxBodyBytes по умолчанию 65536. Чтение ограничено лимитом плюс один проверочный байт, без доверия к Content-Length, с cancellation. Complete содержит полный текст, Truncated — декодируемый prefix, остальные состояния Empty/UnsupportedContent/InvalidEncoding — null. Complete не гарантирует валидность JSON. Поддерживается строгий UTF-8 (BOM допустим) для text/*, application/json, *+json и отсутствующего Content-Type; иная объявленная кодировка/тип — UnsupportedContent. Незавершённый UTF-8 символ на лимите исключается из prefix, реальные неверные байты дают InvalidEncoding без replacement chars. Непрочитанный остаток не проверяется.

ResponseSnippet остаётся ограниченным 2000 UTF-16 символами preview с заменой CR/LF. Для type/code/param будущий Responses/SSE адаптер разбирает BodyText только при Complete и валидном envelope. Он же интерпретирует server correlation и Retry-After, проверяет безопасность возвращаемых полей. Raw headers/body могут содержать секреты и не логируются даже при JsonStructure. Неполнота или отсутствие полей не доказывают безопасность retry. Каталог этапа 13 реализован с безопасными ошибками только по HTTP-статусу; полноценная нормализация Responses/SSE error envelope и Retry-After этапов 14–15 ещё не реализована.

`AddCodexLbModelCatalog` принимает необязательный `HttpClientLoggingOptions`; например, `new HttpClientLoggingOptions { ErrorContentMode = HttpErrorContentLogMode.JsonStructure }`. Actual HttpApiClient получает ILogger приложения и явный error limit 65536. Проверки без сети и ограничения кодировок описаны также в [README HttpClientLibrary](../../../work/HttpClientLibrary/README.md).

## Источники

- [IHttpApiClient](../../../work/HttpClientLibrary/Abstractions/IHttpApiClient.cs)
- [HttpStreamResponseResult](../../../work/HttpClientLibrary/Models/HttpStreamResponseResult.cs)
- [codex-lb: публичные маршруты](../../../codex-lb/app/modules/proxy/api.py)
- [codex-lb: V1 requests](../../../codex-lb/app/core/openai/v1_requests.py)
- [codex-lb: модели Responses](../../../codex-lb/app/core/openai/models.py)
- [codex-lb: контекст совместимости](../../../codex-lb/openspec/specs/responses-api-compat/context.md)

Бизнес-сценарий: [контекст и сжатие](<../Business logic/03-context-and-compaction.md>).
