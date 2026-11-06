# Устройство планируемого подключения

Реализация отсутствует. Документ описывает направление работ, не подтверждённый runtime API. [План](../../../Documentation/Plans/sub2api-connector-2026-11-06-(not-started)/main.md), [технические вопросы](../../../Documentation/Plans/sub2api-connector-2026-11-06-(not-started)/open-questions.md).

## Подтверждённая текущая граница

[IModelGateway](../../../AgentBridge/Application/Ports/IModelGateway.cs) отделяет генерацию/compact от транспорта; IModelCatalog, IModelAccessResolver и IModelSettingsReader также принадлежат ядру. [Facade](../../../adapters/AgentBridge.Integration/AgentBridgeIntegrationExtensions.cs) сейчас непосредственно подключает CodexLb options и AddCodexLbResponses. [Settings validator](../../../AgentBridge/Application/ModelSelectionValidator.cs) требует подтверждённого input budget и exact effort.

[CodexLb continuation](../../../adapters/AgentBridge.CodexLb/Responses/ResponseContinuationMapper.cs) содержит adapter marker и binding диалога/owner/agent/endpoint/key. [ContextModelGuard](../../../AgentBridge/Application/ContextModelGuard.cs) сравнивает provenance модели, но совпадение model ID само по себе не доказывает совместимость двух шлюзов.

Исходники sub2api: [routes](../../../../sub2api/backend/internal/server/routes/gateway.go), [Responses handler](../../../../sub2api/backend/internal/handler/openai_gateway_handler.go), [обычный каталог](../../../../sub2api/backend/internal/handler/gateway_handler.go), [Codex manifest](../../../../sub2api/backend/internal/handler/openai_codex_models_handler.go) и [его структура](../../../../sub2api/backend/internal/service/openai_codex_models_service.go). Это evidence локального исходного кода, а не версии развёрнутого сервера.

## Проекты и зависимости

AgentBridge.Sub2Api размещается отдельным csproj в adapters, зависит от ядра и HttpClientLibrary. CodexLb остаётся самостоятельным адаптером. Общие Responses readers/writers/SSE выделяются только по подтверждённому совпадению протокола; возможный AgentBridge.Responses не содержит конфигурацию конкретного шлюза. Старые public CodexLb types/constructors и assembly identity сохраняются через прежние точки входа, при необходимости wrappers.

Integration собирает один connector bundle. Существующий AddAgentBridge(configuration, httpClientFactory) и отсутствие нового выбора сохраняют codex-lb. Для sub2api предлагается отдельная регистрация до facade; точное имя ещё не реализовано. Новый раздел Sub2Api не требует фиктивного CodexLb. Изменение выбора после facade и неоднозначность отклоняются; documented custom service overrides не теряются.

Legacy facade может сохранять статическую ссылку на CodexLb. Поэтому отдельные csproj не объявляются доказательством DLL-поставки только одного коннектора. Сначала проверяется совместимость старого приложения, затем фиксируется реальный closure.

## Ключи и ресурсы

Для AgentBridge ключ sub2api — Bearer credential gateway. Он отличается от OpenAI upstream API key и OAuth подписки. Индивидуальный/общий источник, scoped lifetime и pinning на run следуют existing контрактам; ошибка ключа не разрешает fallback.

IConfiguration используется для Options binding в регистрации. HttpClient/handlers/logger принадлежат приложению. Регистрация не выполняет HTTP/SQL, не запускает host и не строит временный ServiceProvider. Diagnostics использует existing safe pipeline: код/статус/operation IDs без ключей, raw bodies/headers, reasoning и произвольного server text.

## Протокол и конкретный сценарий

План первой версии: HTTP JSON/SSE с полным рабочим контекстом, без исходящего previous_response_id. Пример: turn1 получает function_call, приложение сохраняет durable checkpoint, выполняет tool один раз, отправляет полный input с парой function_call/function_call_output, получает terminal response. На turn2 передаётся новое рабочее окно с принятым compact и непокрытым хвостом, а не только новое сообщение.

В проверенном sub2api HTTP previous_response_id исключает OpenAI OAuth upstream accounts. Полная история без anchor — выбранное направление, которое ещё надо подтвердить для reasoning/encrypted_content, tools и compact. Если матрица или live-проверка обнаружит несовместимость, реализация не подменяет стратегию скрытым retry или WebSocket.

Обычный /v1/models может не подтверждать нужные metadata. Codex manifest имеет другой envelope/поля; преобразование бюджета и effort должно опираться на проверенный контракт. context_window и input_context_window не приравниваются автоматически. Неподтверждённые возможности дают Unsupported.

## Сохранённые диалоги

Сохранность истории и успешное продолжение через другой шлюз — разные свойства. Не удалять историю, не сбрасывать opaque items, не переписывать настройки/выбор модели ради прохождения запроса. Connector identity учитывается при проверке compact и непокрытых outputs, даже когда model ID совпадает.

Предпочтительно использовать versioned metadata существующих persistence JSON-контрактов. Применимость и legacy handling проверяются на04.01; новая схема БД не считается уже согласованной. Чужое/неизвестное opaque-состояние отклоняется до HTTP. Родной legacy codex-lb продолжает работать через существующий путь.

## Ввод и доказательства

Сначала фиксируются baseline/API и wire fixtures, затем DI/common protocol, адаптер, guards/restart и acceptance. До live05.03 новый путь не объявляется полностью принятым. Поставляются fresh DLL/manifests с source provenance; старый source consumer и baseline binary проверяются отдельно от нового consumer.

Strict OpenSpec validation, build, isolated tests, external compile, Windows runtime, real persistence и live OAuth имеют отдельные результаты. При отсутствии обязательной проверки сохраняется незавершённый статус. Main requirements и current usage docs обновляются только по реализованному результату; archive не выполняется автоматически.
