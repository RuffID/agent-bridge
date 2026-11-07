# Модели прикладного слоя

- Immutable контракты application ports без HTTP/EF. ModelConnectorProvenance — отдельные локальные metadata, не wire DTO и не upstream identity. Группа однородная; новые transport DTO сюда не добавлять.
- ModelSettingsSnapshot фиксирует явную бюджетную политику. Неизвестное окно допустимо только при ServerValidationWithUnknownWindow; это не capability сервера.
- TurnModelSettings и ContextBudgetAssessment сохраняют nullable DeclaredInputContextWindow. Прежний int InputContextWindow доступен только для известного окна и fail-fast при null; ноль/порог вместо неизвестного лимита запрещены.
- Snapshot не содержит ключи, canonical payload или connector provenance. Изменения persistence/provenance этапа04 не включать в бюджетный контракт03.
- ContextCompactionStatus.Disabled добавлен после прежних значений enum: это отсутствие автоматического dispatch/save по решению gateway, а не успешный compact или разрешение generation без отдельного guard.
- ModelResponse/ModelRequest хранят nullable ConnectorProvenance отдельно от canonical envelope/output/input и continuation; WithConnectorProvenance создаёт новый snapshot, прежние сигнатуры сохранены. Binding включает connector/dialog/owner/agent/endpoint/key без TurnId; ключ не сохраняется, metadata не логируются и не отправляются серверу.
