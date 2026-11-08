# Доступ и объявленный каталог sub2api

- Scoped resolver/settings используют existing composition и actual HttpClientLibrary. Только null individual допускает shared; pinned access не читается повторно.
- Default ordinary /v1/models подтверждает exact ID. Явный Compaction.BudgetPolicy=ServerValidationWithUnknownWindow читает прямой /backend-api/codex/models без client_version и без версии AgentBridge/Codex в заголовках приложения. Sub2api сам выбирает Codex-версию upstream; prefix и нормализация конечного /v1 сохраняются. Отказ manifest не разрешает fallback к ordinary или повтор HTTP.
- Manifest проецирует только slug, supported_in_api и точные supported_reasoning_levels.effort как объявления сервера. Это не provenance runtime pool. InputContextWindow и остальные возможности остаются неизвестными; никаких guessed budgets/flags и fallback каталога после ошибки.
- Settings фиксирует policy до I/O и вызывает общий validator. Пригодные settings не доказывают OAuth, upstream input budget/identity или compact compatibility. Generation имеет отдельную принятую canonical границу; её HTTP lifecycle описан в ../Responses/AGENTS.md.
