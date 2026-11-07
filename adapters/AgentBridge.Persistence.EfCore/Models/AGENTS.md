# Записи EF-хранилища

- Изменяемые storage DTO отделены от domain/application моделей; запись и транзакции принадлежат сценарным UoW.
- ModelResponseRecord сохраняет единственный текущий JSON wrapper с canonical envelope и отдельным локальным происхождением. Прежний raw EnvelopeJson не читается и автоматически не переписывается.
- Существующий ResponseFormatVersion=1 сохраняется как физическое ограничение схемы, не переключает JSON readers и не вводит v1/v2 совместимость.
- OutputJson и ContinuationJson остаются canonical/transport данными. ConnectorId/Binding не добавляются в них, не логируются и не отправляются upstream.
