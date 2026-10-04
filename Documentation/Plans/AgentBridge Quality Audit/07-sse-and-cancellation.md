# 07 — SSE, отмена и частичные ответы

Статус: **не начат**. Предпосылки: 06; известны JSON lifecycle и resource ownership.

## Цель и вопросы

Проверить сборку потока, сохранение частичных данных и завершение всех ресурсов/обработчиков на отказах.

## Компоненты и зависимости

[SseEventReader](../../../adapters/AgentBridge.CodexLb/Responses/SseEventReader.cs), ResponseSseState, CodexLbModelGateway.GenerateStreamAsync, HttpClientLibrary/Models/HttpStreamResponseResult.cs. [ResponsesSseTests](../../../tests/AgentBridge.CodexLb.Tests/ResponsesSseTests.cs), HTTP logging tests после чтения их локальных инструкций.

## Способ проверки и границы

Фрагментация по байтам UTF-8, BOM, LF/CRLF/CR, multiline data, comments, пустой/незакрытый frame, EOF и DONE без terminal. Проверить порядок индексов, partial arguments/text/reasoning, конфликт собранных items и авторитетного непустого terminal output. Callback должен ожидаться последовательно; ошибка callback не становится ошибкой сервера. Рассмотреть caller/deadline до данных, после данных и при disposal, typed failure до поздней отмены, I/O exception, cancel callback и ошибку освобождения. Проверить ABQA-002 через существующее покрытие; не создавать тест в рамках аудита. Не обещать принудительную остановку чужого callback.

## Разрешения

A; локальные streams/handlers существующих тестов — B. Реальный disconnect сервера — D; сценарий, требующий нового harness, остаётся пробелом. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Последовательности событий, partial/terminal отчёты, число и порядок callbacks, пути dispose и сохранённые исключения. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Framing, lifecycle, cancellation priority и cleanup paths рассмотрены с отдельным статусом ABQA-002. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Поведение реальной сети/прокси и отсутствие resource leak на основании обычного успешного disposal.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.
