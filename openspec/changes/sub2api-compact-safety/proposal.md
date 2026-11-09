# Безопасный режим sub2api без compact

## Why

Текущий sub2api поддерживает legacy compact и native remote compaction v2, но encrypted-content recovery может удалить compaction/compaction_summary и повторить generation. Успешный ответ не подтверждает сохранность покрытого prefix. T05 остаётся открытым. Принят режим полного контекста без изменения sub2api и без ослабления гарантии.

## What Changes

- Закрепить запрет automatic/manual/force compact и запрет передачи compact через обычную generation.
- Отклонять compaction_trigger и compact-окна до HTTP, в том числе с родным provenance. Сохранять исходные данные без удаления items или автоматического восстановления окна из истории.
- Проверить actual gateway/compactor/runner с локальным transport, неизменность истории и срока, budget и no-retry границы.

## Impact

Изменяются только AgentBridge.Sub2Api, адресные isolated tests и актуальные требования/документация. CodexLb compact, persistence schema, библиотеки-соседи, Telegram и исторические планы не меняются. Для запроса с compact items теперь возвращается Unsupported до HTTP вместо небезопасной отправки. Compact transport/reader/активация не реализуются.
