# Задачи

- [x] Адаптировать базовые read API с родительскими фильтрами и стабильной сортировкой.
- [x] Делегировать одиночные и пакетные create/update/delete как staging без сохранения.
- [x] Реализовать IDialogReader и IExpiredDialogReader без изменяющего UoW.
- [x] Сохранить полную каноническую историю и отчёты; выбрать максимальную Version compact.
- [x] Зарегистрировать scoped adapters и только read-only Application ports.
- [x] Проверить заглушками делегирование, отказ, порядок, mapping и отмену; обновить документацию.

Статус: **Реализован и принят; запрещённые проверки пропущены**. 71 passed / 0 failed / 0 skipped, 37 новых тестов; production/test builds — 0 warnings/errors. Повторная primitive root-проверка отклоняет удаление/смену владельца/жизни/версии, orphan children не теряются. Saving/atomic write ports/Domain rehydration остаются этапу 10. OpenSpec CLI validation не выполнена: CLI отсутствует в PATH; изменение не архивировано. [Фактические команды, файлы и ограничения](<../../../Documentation/Plans/AgentBridge Initial Implementation/09-base-repository-adapters.md>).
