## ADDED Requirements

### Requirement: Сквозное подтверждение завершённых сценариев

Проверка AgentRunner MUST проходить actual CodexLb adapter и HttpClientLibrary с fake handler/local streams совместно с actual SQLite/PostgreSQL через базовые CRUD и сценарные UoW EFCoreLibrary. Evidence MUST отличать реальные persistence проверки от isolated doubles и fake HTTP от live compatibility. Проверка MUST сохранять действующие canonical, fixed expiry, pinned access/settings и durable no-replay инварианты без автоматических retries.

#### Scenario: Повторяющиеся вызовы и перезапуск

- **WHEN** два завершённых шага модели используют один call_id через actual JSON/SSE transport
- **THEN** сохраняются отдельные attempts и полные canonical пары
- **AND** новый root не повторяет существующее обращение после подтверждённого либо неизвестного commit.

#### Scenario: Неполное состояние или конкурентная очистка

- **WHEN** сохранён partial function call, opaque compact без полной оценки либо диалог удалён во время действия
- **THEN** следующий неподдержанный запрос/запись отклоняется без fake output, повторного эффекта или восстановления удалённых данных.
