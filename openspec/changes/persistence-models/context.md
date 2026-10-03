# Границы этапа 08

Статус: **Реализован и принят; запрещённые проверки пропущены**. Изменение не архивировано: OpenSpec CLI validation и реальные интеграции не подтверждены.

Источник: [agent-runtime](../../specs/agent-runtime/spec.md). DTO хранения не предоставляют способ обойти методы Domain. Восстановление агрегата и atomic guards принадлежат следующим этапам.

Время хранится UTC ticks в целочисленных колонках: одинаковые точность и порядок сравнения в обоих providers. JSON хранится text, чтобы provider не нормализовал opaque/unknown поля. Результат инструмента — канонический item, например function_call_output с call_id; собственный второй формат результатов инструментов не вводится.

Активный compact определяется максимальной принятой Version диалога; прежние версии и история сохраняются. ThroughTurnSequence — terminal-prefix metadata, не cutoff items. Проверку prefix и монотонности выполняет будущий сценарий, не FK.

Пример: два экземпляра DTO с одинаковыми Id/IncarnationId/Revision выражают одинаковое persistent условие; private lifetime нового Domain-объекта к нему отношения не имеет. Metadata/serialization tests без БД не доказывают реальный restart, relational concurrency или enforcement constraints. Ошибка/несовместимая версия payload при восстановлении отклоняется явно.
