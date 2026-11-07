# Отдельный коннектор sub2api

Статус: in-progress; этапы01–02 выполнены,03 и03.01/03.02 verification в объёме первой версии без compact; отложенный compact blocked T05; T01 открыт. 04.01 completed изолированно,04.02 verification до реального persistence restart;05 не начат. Создано 2026-11-06.

## Why

AgentBridge уже отделяет application ports от codex-lb, но стандартный AddAgentBridge регистрирует именно этот транспорт. Требуется выбирать sub2api или codex-lb при подключении в DI, сохраняя прикладной API существующих приложений.

AgentBridge использует ключ, выпущенный в sub2api. Тип upstream аккаунта внутри sub2api остаётся обязанностью шлюза, но влияет на доступность HTTP continuation и compact.

## What Changes

- Добавить отдельный net10.0 проект AgentBridge.Sub2Api и isolated test project.
- Расширить composition root выбором одного согласованного connector bundle; прежний вызов и CodexLb-конфигурация сохраняют прежнее поведение.
- Реализовать каталог/settings/access и JSON/SSE generation через HttpClientLibrary; первую версию без compact проверять на OpenAI-направлении sub2api с OAuth upstream.
- Передавать полный рабочий контекст без исходящего previous_response_id. Сохранять принятую клиентскую canonical границу reasoning/tools; compact не включать без отдельного доказательства пригодности окна.
- Сохранить API/ABI заявленной публичной поверхности и durable no-replay. Использовать один новый формат сохранённых отчётов с отдельным connector provenance; совместимость с ранее сохранёнными диалогами обоих коннекторов не требуется. Неизвестное/чужое opaque-состояние отклонять до HTTP, canonical данные новых отчётов сохранять отдельно от provenance.
- Актуализировать DLL-поставку, внешние consumers и документацию после реализации.

## Impact

Обязательный объём первой версии согласован без compact. Полный контекст проходит отдельный budget guard; превышение бюджета возвращает ошибку без обрезания истории и retry. Неограниченная длина диалога не гарантируется, ручной/force compact остаётся Unsupported до factory/HTTP. Compact03.03.02–03.03.06 — отдельная отложенная работа с T05; приёмку первой версии не блокирует. 04.01 проверен изолированно;04.02.01–03 прошли16 новых cases public runner/restart/recovery, реальный persistence restart04.02.04 остаётся непроверенным;05 не начат.

Клиентское продолжение03 добавило ServerValidationWithUnknownWindow, nullable budget assessment/snapshot и чтение budget JSON v1/v2 без изменения DB schema. Этот независимый бюджетный контракт сохраняется. [Клиентская адаптация](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/client-adaptation.md) проверена вместе с JSON/SSE generation в границах03. Canonical гарантия относится к границе AgentBridge, server transformations допустимы без обещания upstream сохранности. Новое локальное происхождение04.01 и его проверки описаны отдельно в [результатах04](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/04-dialog-state/results.md).

Изменяются Integration и транспортные адаптеры; общие application ports и business tools не получают обязательного нового аргумента. Схема БД и соседние библиотеки заранее не меняются. Если для безопасной provenance потребуется schema/public contract change, это отдельное адресное согласование.

OpenSpec main пока не синхронизируется: полная возможность delta ещё не реализована. [План](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/main.md) содержит этапы и приёмку; [design](design.md) — решения и ограничения. На03 existing bundle получил доступ/ordinary каталог и declared manifest settings при явном неизвестном бюджете. T01 не закрыт доказательством input budget, но не блокирует этот settings mode. JSON/SSE generation реализована через existing bundle/HttpClientLibrary; compact Unsupported из-за ограничений T05. [Фактические результаты03](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/results.md).

Подготовленное на03 [серверное предложение](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/server-contract-proposal.md) отклонено. Доработка sub2api исключена; выполнена [адаптация AgentBridge к существующему серверу](../../../Documentation/Plans/sub2api-connector-2026-11-06-(in-progress)/03-sub2api/contract-blockers.md). Новые server fields/enforcement не являются зависимостью; T01/T05 остаются открытыми.

Текущий безопасный режим03.03 явно отключает automatic compact sub2api, сохраняя полный generation input выше локального threshold через отдельный budget guard. Ручной/force compact остаётся Unsupported. Аддитивный default interface member сохраняет автоматическое сжатие прежних IModelGateway; Disabled добавлен после прежних значений enum. Это не включение compact, новая поставка/ABI приёмка или restart04. Совместимость compact-окна с generation и recovery остаётся T05; mapping сам по себе не объявляется доказанной несовместимостью.

## Out of Scope

WebSocket, другие платформы sub2api, admin API, автоматический fallback между шлюзами, переключение на каждый запрос, перенос всех старых opaque-диалогов и sub2api auxiliary usage/files/images. Действующие auxiliary API codex-lb сохраняются.
