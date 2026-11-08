# Usage sub2api через независимый auxiliary-порт

## Why

Потребителю выбранного коннектора нужен снимок usage sub2api по gateway API-ключу через существующий IModelAuxiliaryGateway.

## What Changes

- Явное optional AddAgentBridgeSub2ApiAuxiliary после AddAgentBridge, без регистрации CodexLb и без новых обязательных настроек.
- GET /v1/usage через HttpClientLibrary с выбранным ModelAccess и существующим GenerationTimeout.
- Сохранение непосредственного JSON quota_limited/unrestricted, включая отсутствие optional-полей.
- Безопасные отказы, отмена, deadline, освобождение ресурсов и Unsupported остальных auxiliary-методов до HTTP.

## Impact

Только auxiliary sub2api, изолированные тесты и актуальная документация. Generation, история, tools, streaming, схема БД, Telegram-бот и завершённые планы не изменяются. CLI validation, упаковка DLL и live-приёмка требуют отдельных разрешений.
