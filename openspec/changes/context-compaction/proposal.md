# Сжатие контекста — этап18

## Зачем

Порт CompactAsync пока возвращает Unsupported. Нужен отдельный JSON-контракт compact и ограниченный сценарий принятия сохранённого окна.

## Изменения

- Реализовать POST /v1/responses/compact через actual HttpClientLibrary.
- Сохранять canonical output/envelope, не создавать generation continuation из compact id.
- Проверить отдельный discriminator/status контракт, ошибки, отмену и освобождение ресурсов.
- Реализовать согласованный persistable-only сценарий: terminal history, version-aware save, UnknownBudget и ограниченные проходы.

## Границы

Только этап18. Без tools19, AgentRunner20, settings21, удаления архива, нового server estimator и изменений соседних библиотек.
