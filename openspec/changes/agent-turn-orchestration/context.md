# Контекст

Пользователь согласовал durable recovery в20 и две команды AddDurableToolAttempts. Session-memory19 недостаточна после restart: действие могло произойти без получения результата.

Журнал размещается в отдельной nullable text-колонке ModelSteps.ToolAttemptsJson с явной версией формата. Parent-aware step key и root owner/incarnation вместе с AgentId/output position задают identity. call_id не является уникальным ключом. Внешние действия не входят в transaction; start commit заканчивается до handler. Подтверждённые outputs и outcomes принимаются одной транзакцией. Unknown остаётся без output.

Например, call(x) в stepA/position2 после Started и crash нельзя повторить. Новая completed пара call(x) в stepB/position1 допустима. Восстановленный turn не запускается повторно автоматически; сохранённые calls продолжают блокировать context без выдуманных outputs.

Scope создаётся на каждую запись, checkpoint callbacks сериализованы в рамках run. Конкурирующий run получает storage conflict, без refresh/retry. HTTP проверяется только doubles; SQLite/PostgreSQL не подтверждают SQL Server/MySQL. OpenSpec CLI недоступен.
