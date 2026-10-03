# Анализ наработок TelegramCodexRelayBot

## Статус проверки

Источник: `D:\Media\User\source\repos\TelegramCodexRelayBot`. Изучены клиент codex-lb, wire DTO, построитель контекста, сервис памяти и настройки диалога. Сравнение с текущими исходниками codex-lb выполнено 2026-10-03 без запуска приложений и интеграционных тестов.

Этап 00 повторно подтвердил маршруты бота `/v1/responses`, `/v1/responses/compact` и `/v1/models` по константам `CodexLbClient` и актуальному роутеру шлюза. Это подтверждает адреса, но не полную совместимость старой памяти и всех DTO. Прежняя дата сверки 2026-09-07 не используется как доказательство актуального контракта.

Код и DLL на этом этапе не копировались. Переиспользование означает выборочную адаптацию проверенных решений под независимую библиотеку.

## Что пригодно для адаптации

| Наработка | Польза |
| --- | --- |
| Работа через `IHttpApiClient` | Используется обязательная HttpClientLibrary |
| Linked cancellation и конечные deadline | Отличаются отмена вызывающего кода и локальный таймаут |
| Структурированные HTTP/terminal ошибки | Образец переноса status/type/code/param и безопасного correlation ID; доступность полей зависит от транспорта и полноты error JSON |
| `ContentFactory` | Корректное владение multipart/byte содержимым при необходимости таких операций |
| Запрет слепого повторения Responses | Учитывается неоднозначность dispatch и возможных побочных действий |
| Обработка fragmented UTF-8 в WebSocket | Полезный образец для возможного будущего WS-транспорта |

WebSocket-клиент бота использует отдельный transport и соединение на один turn. Его наличие не обязывает AgentBridge вводить WS: публичные Responses/SSE доступны через HttpClientLibrary.

Проверенные участки [CodexLbClient](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Infrastructure/Clients/CodexLbClient.cs): `SendAsync` (строка 444) делает один `SendWithResponseAsync`, использует конечный deadline и сохраняет исходный caller cancellation; `CreateOperationCancellation` (1114) связан также с `IHostApplicationLifetime`, эту host-зависимость ядро AgentBridge не получает. `ExtractSafeCorrelationId` (1097) принимает до 128 ASCII-букв/цифр и `-_.`; это образец проверки, а не гарантия серверного ID. Для HTTP-отказов клиент использует локально созданный correlation ID и парсит ограниченный snippet; заголовки ошибки библиотека не предоставляет. `ReadResponseStreamAsync` (654) и `BuildCreateResponseResult` (755) требуют переработки, а не буквального переноса.

## Что требует переработки

| Место | Наблюдение | Требование к AgentBridge |
| --- | --- | --- |
| `TelegramDialogMemoryService.TryExtractCompactSummary` | Ищет `summary`, `output_text` или текст внутри `output`; непрозрачный compaction не восстанавливается этим способом | Сохранять каноническое новое окно контекста, включая opaque output items |
| `TelegramDialogMemoryService.CompactDialogAsync` | После текстовой сводки удаляет исходные сообщения | Разделить сжатие и настроенную политику хранения |
| `CodexCreateResponseRequest.Input` | Список только `CodexInputMessageRequest` | Поддерживать также function calls/outputs, reasoning и compaction |
| `CodexCreateResponseRequest` | Нет полей reasoning, include и previous_response_id | Контракт выбора effort и продолжения AgentBridge нельзя реализовать буквальным копированием этого DTO |
| `CodexResponseToolRequest` | Содержит type и параметры image generation; нет name/description/parameters для обычной function tool | Определить DTO инструментов по текущему Responses-контракту |
| `DialogContextBuilder.ToCodexRole` | Роль System преобразуется в User | Сохранять смысл инструкций и ролей |
| `DialogTokenEstimator` | Приближение `text.Length / 4` | Подсчитывать или явно оценивать весь подготовленный контекст, не выдавать эту эвристику за точный tokenizer |
| `ReadResponseStreamAsync` / `BuildCreateResponseResult` | При EOF accumulated text может стать результатом без `response.completed` | Не принимать частичный SSE-ответ за успешное завершение |
| `ProcessResponseEventData` / `TryExtractStreamError` | `response.incomplete` не выделяется, SSE-чтение не прекращается по возвращённому признаку completed | Явно различать terminal-состояния и завершать чтение по контракту; delta не подменяет полный output |
| `CodexLbClient.SendAsync` | Использует response wrapper, но наружу возвращает только Body | Учитывать необходимые заголовки и continuity-состояние |

Монолитный `CodexLbClient` не переносится целиком: в нём смешаны Responses, WebSocket, файлы, изображения, модели и usage, а также зависимости Telegram-приложения и host lifetime. Выделяются обязанности, необходимые текущему объёму AgentBridge.

Старая схема БД, миграции, Telegram-команды и outbox доставки не становятся требованиями универсальной библиотеки только потому, что существуют в боте.

Проверка совместимости ограничена исходниками. [Infrastructure.csproj бота](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Infrastructure/TelegramCodexRelayBot.Infrastructure.csproj) использует `net10.0`, EF SqlServer `10.0.8`, Telegram.Bot `22.10.0.1` и локальные DLL обеих библиотек из `libs`. Эти DLL не проверялись и не считаются эквивалентом текущих исходных контрактов EFCoreLibrary/HttpClientLibrary. Анализ не подтверждает работоспособность старой сборки с новыми DLL.

## Источники

- [CodexLbClient](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Infrastructure/Clients/CodexLbClient.cs)
- [Сервис памяти](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/UseCases/ProcessTelegramUpdate/Memory/TelegramDialogMemoryService.cs)
- [Построитель контекста](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/Services/DialogContextBuilder.cs)
- [DTO запроса](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/Clients/CodexLb/CodexCreateResponseRequest.cs)
- [DTO инструмента](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/Clients/CodexLb/CodexResponseToolRequest.cs)
- [Оценка токенов](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/UseCases/ProcessTelegramUpdate/Common/DialogTokenEstimator.cs)
