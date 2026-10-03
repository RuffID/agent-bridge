# Анализ наработок TelegramCodexRelayBot

## Статус проверки

Источник: `D:\Media\User\source\repos\TelegramCodexRelayBot`. Изучены клиент codex-lb, wire DTO, построитель контекста, сервис памяти и настройки диалога. Сравнение с текущими исходниками codex-lb выполнено 2026-10-03 без запуска приложений и интеграционных тестов.

Основные маршруты бота `/v1/responses` и `/v1/responses/compact` продолжают существовать. Это подтверждает адреса, но не полную совместимость старой памяти и всех DTO. Документация бота отмечает предыдущую сверку 2026-09-07.

Код и DLL на этом этапе не копировались. Переиспользование означает выборочную адаптацию проверенных решений под независимую библиотеку.

## Что пригодно для адаптации

| Наработка | Польза |
| --- | --- |
| Работа через `IHttpApiClient` | Используется обязательная HttpClientLibrary |
| Linked cancellation и конечные deadline | Отличаются отмена вызывающего кода и локальный таймаут |
| Структурированные HTTP/terminal ошибки | Сохраняются status/type/code/param и correlation ID |
| `ContentFactory` | Корректное владение multipart/byte содержимым при необходимости таких операций |
| Запрет слепого повторения Responses | Учитывается неоднозначность dispatch и возможных побочных действий |
| Обработка fragmented UTF-8 в WebSocket | Полезный образец для возможного будущего WS-транспорта |

WebSocket-клиент бота использует отдельный transport и соединение на один turn. Его наличие не обязывает AgentBridge вводить WS: публичные Responses/SSE доступны через HttpClientLibrary.

## Что требует переработки

| Место | Наблюдение | Требование к AgentBridge |
| --- | --- | --- |
| `TelegramDialogMemoryService.TryExtractCompactSummary` | Ищет `summary`, `output_text` или текст внутри `output`; непрозрачный compaction не восстанавливается этим способом | Сохранять каноническое новое окно контекста, включая opaque output items |
| `TelegramDialogMemoryService.CompactDialogAsync` | После текстовой сводки удаляет исходные сообщения | Разделить сжатие и настроенную политику хранения |
| `CodexCreateResponseRequest.Input` | Список только `CodexInputMessageRequest` | Поддерживать также function calls/outputs, reasoning и compaction |
| `CodexResponseToolRequest` | Содержит type и параметры image generation; нет name/description/parameters для обычной function tool | Определить DTO инструментов по текущему Responses-контракту |
| `DialogContextBuilder.ToCodexRole` | Роль System преобразуется в User | Сохранять смысл инструкций и ролей |
| `DialogTokenEstimator` | Приближение `text.Length / 4` | Подсчитывать или явно оценивать весь подготовленный контекст, не выдавать эту эвристику за точный tokenizer |
| `ReadResponseStreamAsync` / `BuildCreateResponseResult` | При EOF accumulated text может стать результатом без `response.completed` | Не принимать частичный SSE-ответ за успешное завершение |
| `CodexLbClient.SendAsync` | Использует response wrapper, но наружу возвращает только Body | Учитывать необходимые заголовки и continuity-состояние |

Монолитный `CodexLbClient` не переносится целиком: в нём смешаны Responses, WebSocket, файлы, изображения, модели и usage, а также зависимости Telegram-приложения и host lifetime. Выделяются обязанности, необходимые текущему объёму AgentBridge.

Старая схема БД, миграции, Telegram-команды и outbox доставки не становятся требованиями универсальной библиотеки только потому, что существуют в боте.

## Источники

- [CodexLbClient](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Infrastructure/Clients/CodexLbClient.cs)
- [Сервис памяти](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/UseCases/ProcessTelegramUpdate/Memory/TelegramDialogMemoryService.cs)
- [Построитель контекста](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/Services/DialogContextBuilder.cs)
- [DTO запроса](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/Clients/CodexLb/CodexCreateResponseRequest.cs)
- [DTO инструмента](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/Clients/CodexLb/CodexResponseToolRequest.cs)
- [Оценка токенов](../../../TelegramCodexRelayBot/TelegramCodexRelayBot.Application/UseCases/ProcessTelegramUpdate/Common/DialogTokenEstimator.cs)
