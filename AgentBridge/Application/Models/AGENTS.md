# Модели прикладного слоя

- Immutable контракты application ports, без HTTP/EF и connector identity. Группа однородная; новые transport DTO сюда не добавлять.
- ModelSettingsSnapshot фиксирует явную бюджетную политику. Неизвестное окно допустимо только при ServerValidationWithUnknownWindow; это не capability сервера.
- TurnModelSettings и ContextBudgetAssessment сохраняют nullable DeclaredInputContextWindow. Прежний int InputContextWindow доступен только для известного окна и fail-fast при null; ноль/порог вместо неизвестного лимита запрещены.
- Snapshot не содержит ключи, canonical payload или connector provenance. Изменения persistence/provenance этапа04 не включать в бюджетный контракт03.
