# Адаптер sub2api

- Отдельная net10.0 библиотека зависит только от ядра и HttpClientLibrary. CodexLb, EF и hosting dependencies запрещены. Общие внутренние механизмы Responses подключены source link из соседней AgentBridge.Responses.
- Этап02 содержит только собственные options/локальную валидацию и подготовку четырёх scoped портов. Sub2ApiPendingConnector возвращает Unsupported для access/catalog/settings/generation/compact; не заменять его фиктивным успехом. Реализация этих операций принадлежит этапу03 и требует отдельного поручения.
- Публичный выбор — Integration.AddAgentBridgeSub2ApiConnector до старого AddAgentBridge. Bind получает section Sub2Api, не CodexLb. IConfiguration не передаётся runtime-компонентам. Секреты/значения неисправных options не включаются в ошибки.
- Приложение владеет ключевым источником, HttpClient/handlers/logger; регистрация не вызывает factory и не строит ServiceProvider. Individual source регистрируется заранее; error/empty индивидуального ключа не разрешает shared fallback.
- Будущий HTTP путь использует полный рабочий контекст без previous_response_id и legacy turn-state. Нормализация URL, каталога и серверных особенностей не меняет codex-lb. context_window не доказывает input budget; T01/T02/T05 плана остаются открыты.
- tests/AgentBridge.Sub2Api.Tests проверяет public DI и явные ограничения без сети/host/БД. Наличие DLL/green DI не означает пригодность sub2api к генерации.
