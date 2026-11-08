# Контролируемая live-приёмка

- Отдельный executable для 05.03; не включать в обычный test run или сборку solution. Использует публичную текущую PostgreSQL DLL/NuGet поставку; без ProjectReference и internals производителя.
- App-owned configuration, identity, projector и probe подключаются исходниками существующей консоли. Консоль и её vendor не изменяются. Секреты читаются из явно заданного каталога, не копируются в проект/output/evidence.
- Фазы `json` и `sse` запускаются разными процессами над одним новым диалогом. Никаких maintenance, init-db, migrations, fence, recovery, удаления или повторного send.
- До исполнения нужны разрешение точных команд и подтверждение тестовой PostgreSQL и OAuth-группы ключа. Общий лимит — 10 generation и 2 manifest sends; reservation сохраняется до отправки. Redirect и повтор одинакового запроса запрещены; первая неожиданная ошибка останавливает приёмку.
- Единственный probe резервирует выполнение до handler; во второй фазе выполнение запрещено. История читается только через catalog access/scope/profile, сравнивается между процессами и перед отправкой полного контекста.
- Evidence содержит только ID, хеши, счётчики, коды/типы и результаты проверок. Не выводить exception message, HTTP headers/body, ключ, connection string, reasoning или видимый ответ модели.
