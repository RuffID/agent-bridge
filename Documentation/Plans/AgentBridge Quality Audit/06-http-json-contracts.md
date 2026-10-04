# 06 — HTTP и canonical JSON

Статус: **не начат**. Предпосылки: 01; exact settings и per-call доступ.

## Цель и вопросы

Проверить транспортный контракт без потери canonical данных и без ложного успешного завершения.

## Компоненты и зависимости

[CodexLbModelGateway](../../../adapters/AgentBridge.CodexLb/Responses/CodexLbModelGateway.cs), ResponseRequestWriter/ResponseJsonReader/ResponseErrorReader/ResponseContinuationMapper, Models/ModelCatalogJsonReader. [HttpClientLibrary](../../../../work/HttpClientLibrary/Abstractions/IHttpApiClient.cs); codex-lb app/modules/proxy/api.py, schemas.py. Тесты ModelCatalogTests и ResponsesJsonTests.

## Способ проверки и границы

Сверить canonical маршруты и base-prefix, exact model/effort, store/stream, поддержанные controls, default include и duplicate/unknown параметры. Проверить 2xx с missing output/unknown status/explicit error, completed без текста, malformed/truncated/не-UTF-8 ошибки и 64 КиБ границу. Сохранить unknown поля/output/envelope раздельно. Continuation: другой owner/dialog/agent/endpoint/key, unknown format, новый ответ без id, отсутствие старого anchor fallback. Отдельно проверить отсутствие retry/смены ключа в adapter и app-owned handlers. Сопоставить локальный codex-lb source contract, не объявляя его live поведением.

## Разрешения

A; actual HttpClientLibrary с fake handler — B; живой endpoint, ключ и HTTP — D после отдельного разрешения. Compact подробно в 09. Общие правила — [методика](Methodology.md). Исправления и изменение тестов запрещены.

## Доказательства и запись результатов

Матрица request/response полей и error mapping, отрицательные fixtures, число отправок и границы владения ресурсами. Заполнить [шаблон отчёта](Methodology.md#шаблон-результатов-этапа) ниже; проблемы заносить в [Findings](Findings.md), указывая ID, категорию, достоверность, серьёзность и ограничения. Наличие проблемы не препятствует завершению исследования.

## Критерий завершения

Каталог, JSON generation, ошибки и continuation сопоставлены с требованиями и выбранными сценариями. Пропуск записать с причиной и влиянием; не считать его успешной проверкой.

## Что этап не подтверждает

Upstream account ownership, доступность сервера, сохранность opaque у иной модели и live совместимость.

## Результаты

Проверки ещё не выполнялись. Команды, результаты и Findings этого этапа отсутствуют; подготовительные записи реестра не означают его запуск.
