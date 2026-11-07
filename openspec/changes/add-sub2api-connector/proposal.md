# Отдельный коннектор sub2api

Статус: in-progress; этапы01–02 выполнены,03.01/03.02 verification,03 blocked compact T05; T01 открыт,04–05 не начаты. Создано 2026-11-06.

## Why

AgentBridge уже отделяет application ports от codex-lb, но стандартный AddAgentBridge регистрирует именно этот транспорт. Требуется выбирать sub2api или codex-lb при подключении в DI, сохраняя прикладной API существующих приложений.

AgentBridge использует ключ, выпущенный в sub2api. Тип upstream аккаунта внутри sub2api остаётся обязанностью шлюза, но влияет на доступность HTTP continuation и compact.

## What Changes

- Добавить отдельный net10.0 проект AgentBridge.Sub2Api и isolated test project.
- Расширить composition root выбором одного согласованного connector bundle; прежний вызов и CodexLb-конфигурация сохраняют прежнее поведение.
- Реализовать каталог/settings/access, JSON/SSE generation и compact через HttpClientLibrary; первую версию проверять на OpenAI-направлении sub2api с OAuth upstream.
- Передавать полный рабочий контекст без исходящего previous_response_id. Совместимость reasoning/tools/compact должна быть доказана, а не предположена по общему названию Responses.
- Сохранить API/ABI заявленной старой поверхности, canonical history, durable no-replay и безопасный отказ при неподтверждённом cross-connector opaque-состоянии.
- Актуализировать DLL-поставку, внешние consumers и документацию после реализации.

## Impact

Разрешённое клиентское продолжение03 добавляет явный ServerValidationWithUnknownWindow, nullable budget assessment/snapshot и совместимое чтение budget JSON v1/v2 без изменения DB schema. Прежние API signatures и strict/ServerValidation сохраняются. [Клиентская адаптация](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/client-adaptation.md) реализована и проверена вместе с actual JSON/SSE generation. Принята canonical гарантия на входе sub2api и в истории AgentBridge с допуском existing server transformations, без обещания upstream сохранности. Compact Unsupported,04 не начат.

Изменяются Integration и транспортные адаптеры; общие application ports и business tools не получают обязательного нового аргумента. Схема БД и соседние библиотеки заранее не меняются. Если для безопасной provenance потребуется schema/public contract change, это отдельное адресное согласование.

OpenSpec main пока не синхронизируется: полная возможность delta ещё не реализована. [План](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/main.md) содержит этапы и приёмку; [design](design.md) — решения и ограничения. На03 existing bundle получил доступ/ordinary каталог и declared manifest settings при явном неизвестном бюджете. T01 не закрыт доказательством input budget, но не блокирует этот settings mode. JSON/SSE generation реализована через existing bundle/HttpClientLibrary; compact Unsupported из-за ограничений T05. [Фактические результаты03](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/results.md).

Продолжение03 подготовило [серверное предложение](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/server-contract-proposal.md), которое пользователь **отклонил**. Доработка sub2api исключена; выполнена [адаптация бюджета/settings AgentBridge к существующему серверу](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-blockers.md). Новые server fields/enforcement не являются зависимостью. T01/T05 остаются открытыми, этап04 не начат.

## Out of Scope

WebSocket, другие платформы sub2api, admin API, автоматический fallback между шлюзами, переключение на каждый запрос, перенос всех старых opaque-диалогов и sub2api auxiliary usage/files/images. Действующие auxiliary API codex-lb сохраняются.
