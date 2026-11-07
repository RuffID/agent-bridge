# Отдельный коннектор sub2api

Статус: in-progress; этапы01–02 выполнены, транспорт sub2api03–05 не реализован. Создано 2026-11-06.

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

Изменяются Integration и транспортные адаптеры; общие application ports и business tools не получают обязательного нового аргумента. Схема БД и соседние библиотеки заранее не меняются. Если для безопасной provenance потребуется schema/public contract change, это отдельное адресное согласование.

OpenSpec main пока не синхронизируется: полная возможность delta ещё не реализована. [План](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/main.md) содержит этапы и приёмку; [design](design.md) — решения и ограничения. Этап02 подготовил выбор bundle и общий protocol source; sub2api операции явно Unsupported, без имитации успешного подключения.

## Out of Scope

WebSocket, другие платформы sub2api, admin API, автоматический fallback между шлюзами, переключение на каждый запрос, перенос всех старых opaque-диалогов и sub2api auxiliary usage/files/images. Действующие auxiliary API codex-lb сохраняются.
